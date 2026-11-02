using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

// Cliente mínimo do protocolo HTTP/SSE do Gradio (queue/join + queue/data). Com token gratuito do Hugging Face a cota
// do ZeroGPU é maior; o token só é enviado para hf.space / huggingface.co e nunca é registrado em log.
public class GradioClient
{
    public readonly string baseUrl;
    readonly string sessao = Guid.NewGuid().ToString("N").Substring(0, 12);
    static readonly HttpClient http = CriarHttp();
    Dictionary<string, int> indices;

    // texto curto do andamento ("na fila: posição 3", "processando...")
    public string estado = "";

    static HttpClient CriarHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ZettaMR/1.0");
        return c;
    }

    readonly string token;

    public GradioClient(string baseUrl, string token = null)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        this.token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    public bool UsaToken => token != null;

    HttpRequestMessage Msg(HttpMethod metodo, string url, HttpContent conteudo = null)
    {
        var r = new HttpRequestMessage(metodo, url) { Content = conteudo };
        if (token != null)
        {
            var host = new Uri(url).Host;
            if (host.EndsWith(".hf.space") || host == "huggingface.co" || host.EndsWith(".huggingface.co"))
                r.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        return r;
    }

    // resolve api_name → fn_index a partir de /config (cacheado)
    async Task<int> Indice(string apiName)
    {
        if (indices == null)
        {
            using var rc = await http.SendAsync(Msg(HttpMethod.Get, baseUrl + "/config"));
            var txt = await rc.Content.ReadAsStringAsync();
            if (!rc.IsSuccessStatusCode) throw new Exception("config HTTP " + (int)rc.StatusCode + ": " + Resumo(txt));
            var cfg = JObject.Parse(txt);
            indices = new Dictionary<string, int>();
            foreach (var d in (JArray)cfg["dependencies"])
            {
                var n = d.Value<string>("api_name");
                if (!string.IsNullOrEmpty(n)) indices[n] = d.Value<int>("id");
            }
        }
        if (!indices.TryGetValue(apiName, out var id)) throw new Exception("endpoint não existe: " + apiName);
        return id;
    }

    public async Task<JArray> Chamar(string apiName, JArray data)
    {
        int fn = await Indice(apiName);
        var corpo = new JObject
        {
            ["data"] = data,
            ["event_data"] = null,
            ["fn_index"] = fn,
            ["trigger_id"] = null,
            ["session_hash"] = sessao
        };
        estado = "enviando pedido";
        using var resp = await http.SendAsync(Msg(HttpMethod.Post, baseUrl + "/gradio_api/queue/join",
            new StringContent(corpo.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")));
        var txt = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception("join HTTP " + (int)resp.StatusCode + ": " + Resumo(txt));
        string eventId = JObject.Parse(txt).Value<string>("event_id");

        using var req = Msg(HttpMethod.Get, baseUrl + "/gradio_api/queue/data?session_hash=" + sessao);
        using var r = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        r.EnsureSuccessStatusCode();
        using var stream = await r.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string linha;
        while ((linha = await reader.ReadLineAsync()) != null)
        {
            if (!linha.StartsWith("data:")) continue;
            JObject msg;
            try { msg = JObject.Parse(linha.Substring(5)); } catch { continue; }
            var idMsg = msg.Value<string>("event_id");
            if (idMsg != null && idMsg != eventId) continue;

            switch (msg.Value<string>("msg"))
            {
                case "estimation":
                    int? rank = msg.Value<int?>("rank");
                    estado = rank.HasValue ? "na fila: posição " + (rank.Value + 1) : "na fila";
                    break;
                case "process_starts":
                    estado = "processando";
                    break;
                case "progress":
                    var pd = msg["progress_data"] as JArray;
                    if (pd != null && pd.Count > 0)
                    {
                        var p = pd[0];
                        var desc = p.Value<string>("desc");
                        estado = "processando" + (string.IsNullOrEmpty(desc) ? "" : " (" + desc + ")");
                    }
                    break;
                case "process_completed":
                    bool ok = msg.Value<bool?>("success") ?? false;
                    var saida = msg["output"] as JObject;
                    if (!ok)
                    {
                        string erro = saida?.Value<string>("error") ?? saida?.Value<string>("title") ?? "erro desconhecido";
                        throw new Exception(erro);
                    }
                    return (saida?["data"] as JArray) ?? new JArray();
                case "close_stream":
                    throw new Exception("conexão encerrada pelo servidor antes do resultado");
            }
        }
        throw new Exception("stream encerrado sem resultado");
    }

    public async Task<JObject> Enviar(byte[] bytes, string nome, string mime)
    {
        using var form = new MultipartFormDataContent();
        var parte = new ByteArrayContent(bytes);
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
        form.Add(parte, "files", nome);
        using var resp = await http.SendAsync(Msg(HttpMethod.Post, baseUrl + "/gradio_api/upload", form));
        var txt = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) throw new Exception("upload HTTP " + (int)resp.StatusCode + ": " + Resumo(txt));
        var caminho = ((JArray)JToken.Parse(txt))[0].ToString();
        return new JObject
        {
            ["path"] = caminho,
            ["orig_name"] = nome,
            ["mime_type"] = mime,
            ["meta"] = new JObject { ["_type"] = "gradio.FileData" }
        };
    }

    public async Task<byte[]> Baixar(JToken fileData)
    {
        string url = fileData.Value<string>("url");
        if (string.IsNullOrEmpty(url)) url = baseUrl + "/gradio_api/file=" + fileData.Value<string>("path");
        using var rd = await http.SendAsync(Msg(HttpMethod.Get, url));
        if (!rd.IsSuccessStatusCode) throw new Exception("download HTTP " + (int)rd.StatusCode);
        return await rd.Content.ReadAsByteArrayAsync();
    }

    static string Resumo(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 200 ? s.Substring(0, 200) : s);
}
