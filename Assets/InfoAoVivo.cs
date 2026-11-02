using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Informação ao vivo sem depender da busca do Google (que não é grátis na Live API):
// clima pelo Open-Meteo e manchetes pelo Google News (RSS). Ambos gratuitos e sem chave.
public class InfoAoVivo : MonoBehaviour
{
    const string DeclClima = @"{
  ""name"": ""consultar_clima"",
  ""description"": ""Consulta o clima agora e a previsão de hoje de uma cidade e mostra num painel holográfico."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""cidade"": { ""type"": ""STRING"", ""description"": ""Nome da cidade, ex.: São Paulo."" } },
    ""required"": [""cidade""]
  }
}";

    const string DeclNoticias = @"{
  ""name"": ""buscar_noticias"",
  ""description"": ""Busca as manchetes mais recentes sobre um assunto (ou as principais do Brasil) e mostra num painel holográfico."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""assunto"": { ""type"": ""STRING"", ""description"": ""Assunto, ex.: futebol, tecnologia. Vazio para as principais."" } }
  }
}";

    const string Instrucao =
        "Para clima chame consultar_clima e para notícias chame buscar_noticias. Elas devolvem os dados alguns segundos depois " +
        "como aviso do sistema: nesse momento resuma por voz em até duas frases. Ao chamar, diga só 'um instante'.";

    AssistenteVoz voz;
    PainelInfo painel;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        painel = FindAnyObjectByType<PainelInfo>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclClima, Clima, Instrucao);
        voz.RegistrarFerramenta(DeclNoticias, Noticias, null);
    }

    JObject Clima(JObject a)
    {
        string cidade = (a.Value<string>("cidade") ?? "").Trim();
        if (cidade.Length == 0) return new JObject { ["erro"] = "cidade não informada" };
        StartCoroutine(ClimaRotina(cidade));
        return new JObject { ["result"] = "consultando; os dados chegam em instantes" };
    }

    JObject Noticias(JObject a)
    {
        StartCoroutine(NoticiasRotina((a.Value<string>("assunto") ?? "").Trim()));
        return new JObject { ["result"] = "buscando; as manchetes chegam em instantes" };
    }

    IEnumerator ClimaRotina(string cidade)
    {
        string urlGeo = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=pt&name=" + UnityWebRequest.EscapeURL(cidade);
        string geo = null;
        yield return Baixar(urlGeo, t => geo = t);
        if (geo == null) { Falha("o serviço de clima está sem resposta"); yield break; }

        JToken local = null;
        try { local = JObject.Parse(geo)["results"]?[0]; } catch { }
        if (local == null) { Falha("não encontrei a cidade " + cidade); yield break; }
        string nome = local.Value<string>("name") ?? cidade;
        double lat = local.Value<double>("latitude"), lon = local.Value<double>("longitude");

        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "&longitude=" + lon.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m"
            + "&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=1";
        string dados = null;
        yield return Baixar(url, t => dados = t);
        if (dados == null) { Falha("o serviço de clima está sem resposta"); yield break; }

        string c1, c2, c3, c4, resumo;
        try
        {
            var j = JObject.Parse(dados);
            var cur = j["current"];
            var dia = j["daily"];
            int temp = Mathf.RoundToInt(cur.Value<float>("temperature_2m"));
            int sens = Mathf.RoundToInt(cur.Value<float>("apparent_temperature"));
            int umid = Mathf.RoundToInt(cur.Value<float>("relative_humidity_2m"));
            int vento = Mathf.RoundToInt(cur.Value<float>("wind_speed_10m"));
            string ceu = DescreverCodigo(cur.Value<int>("weather_code"));
            int max = Mathf.RoundToInt(dia["temperature_2m_max"][0].Value<float>());
            int min = Mathf.RoundToInt(dia["temperature_2m_min"][0].Value<float>());
            int chuva = Mathf.RoundToInt(dia["precipitation_probability_max"][0].Value<float>());
            c1 = "Agora: " + temp + "°C (sensação " + sens + "°C)";
            c2 = ceu;
            c3 = "Mín " + min + "°C • Máx " + max + "°C";
            c4 = "Chuva " + chuva + "% • Umidade " + umid + "% • Vento " + vento + " km/h";
            resumo = "Clima em " + nome + ": " + temp + " graus, sensação de " + sens + ", " + ceu.ToLowerInvariant() + ". Hoje mínima de "
                + min + " e máxima de " + max + " graus, chance de chuva de " + chuva + " por cento.";
        }
        catch (Exception) { Falha("não consegui ler a previsão"); yield break; }

        Mostrar("Clima em " + nome, new[] { c1, c2, c3, c4 });
        if (voz != null) voz.Falar(resumo + " Diga isso ao usuário em até duas frases curtas.");
    }

    IEnumerator NoticiasRotina(string assunto)
    {
        string url = assunto.Length == 0
            ? "https://news.google.com/rss?hl=pt-BR&gl=BR&ceid=BR:pt-419"
            : "https://news.google.com/rss/search?q=" + UnityWebRequest.EscapeURL(assunto) + "&hl=pt-BR&gl=BR&ceid=BR:pt-419";
        string xml = null;
        yield return Baixar(url, t => xml = t);
        if (xml == null) { Falha("o serviço de notícias está sem resposta"); yield break; }

        var titulos = new List<string>();
        var fontes = new List<string>();
        try
        {
            foreach (var item in XDocument.Parse(xml).Descendants("item").Take(4))
            {
                string t = (string)item.Element("title") ?? "";
                int i = t.LastIndexOf(" - ", StringComparison.Ordinal);
                string fonte = i > 0 ? t.Substring(i + 3) : "";
                if (i > 0) t = t.Substring(0, i);
                if (t.Length > 0) { titulos.Add(t); fontes.Add(fonte); }
            }
        }
        catch (Exception) { Falha("não consegui ler as notícias"); yield break; }
        if (titulos.Count == 0) { Falha("não achei notícias sobre " + (assunto.Length == 0 ? "isso" : assunto)); yield break; }

        var linhas = titulos.Select(t => t.Length > 62 ? t.Substring(0, 60) + "…" : t).ToArray();
        Mostrar(assunto.Length == 0 ? "Principais notícias" : "Notícias: " + assunto, linhas);
        var falado = string.Join(" ", titulos.Select((t, i) => (i + 1) + ") " + t + (fontes[i].Length > 0 ? " (" + fontes[i] + ")" : "") + "."));
        if (voz != null) voz.Falar("manchetes: " + falado + " Resuma por voz em até duas frases curtas.");
    }

    IEnumerator Baixar(string url, Action<string> aoTerminar)
    {
        using (var r = UnityWebRequest.Get(url))
        {
            r.timeout = 15;
            r.SetRequestHeader("User-Agent", "ZettaMR/1.0");
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success) aoTerminar(r.downloadHandler.text);
            else Debug.LogWarning("InfoAoVivo: falha em " + new Uri(url).Host + ": " + r.error);
        }
    }

    void Mostrar(string titulo, string[] linhas)
    {
        if (painel == null) painel = FindAnyObjectByType<PainelInfo>();
        if (painel == null) return;
        painel.Mostrar(new JObject { ["titulo"] = titulo, ["linhas"] = new JArray(linhas), ["duracao"] = 25 });
    }

    void Falha(string motivo)
    {
        Debug.LogWarning("InfoAoVivo: " + motivo);
        if (voz != null) voz.Falar("falha ao buscar a informação: " + motivo + ". Avise o usuário com educação, em uma frase, e sugira tentar de novo.");
    }

    static string DescreverCodigo(int c)
    {
        if (c == 0) return "Céu limpo";
        if (c == 1) return "Predominantemente limpo";
        if (c == 2) return "Parcialmente nublado";
        if (c == 3) return "Nublado";
        if (c == 45 || c == 48) return "Neblina";
        if (c >= 51 && c <= 57) return "Garoa";
        if (c >= 61 && c <= 67) return "Chuva";
        if (c >= 71 && c <= 77) return "Neve";
        if (c >= 80 && c <= 82) return "Pancadas de chuva";
        if (c >= 85 && c <= 86) return "Pancadas de neve";
        if (c >= 95) return "Trovoadas";
        return "Tempo instável";
    }
}
