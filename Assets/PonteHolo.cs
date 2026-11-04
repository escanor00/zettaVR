using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Ponte PC: manda pedidos de mudança do headset para a fila do PC (Docs/pedidos-zetta.md, servidor em Docs/ponte-pc/) e mostra
// o estado da fila. NADA é executado: o servidor só guarda os pedidos. Senha e endereço vêm do ZettaSegredos (fora do git).
// Usa HttpClient (como o GradioClient), sem mudar a regra de HTTP simples do projeto; só rede local.
public class PonteHolo : MonoBehaviour
{
    const string DeclPedir = @"{
  ""name"": ""pedir_ao_pc"",
  ""description"": ""Envia ao PC um pedido de mudança no app (texto livre). O pedido entra numa fila para o desenvolvedor atender; nada é executado automaticamente."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""texto"": { ""type"": ""STRING"", ""description"": ""O pedido, em uma ou duas frases."" } }, ""required"": [""texto""] }
}";
    const string DeclFila = @"{
  ""name"": ""mostrar_fila_pc"",
  ""description"": ""Mostra (ou fecha) o painel da ponte do PC: se está conectada e quantos pedidos estão pendentes."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] } } }
}";
    const string Instrucao = "'peça ao PC para…', 'anote para o computador…' → pedir_ao_pc com o texto do pedido; confirme em UMA frase quando o sistema avisar que entrou na fila. " +
        "'mostre a fila do PC' → mostrar_fila_pc. Deixe claro que o PC só guarda o pedido; ele não executa sozinho.";

    static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

    AssistenteVoz voz;
    Transform painel;
    TextMeshPro txtEstado, txtLista;
    PainelVidro vidro;
    string estado = "—";
    bool online;
    string lista = "";
    float proximaAtualizacao;
    bool atualizando;
    public bool Aberto => painel != null;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclPedir, Pedir, Instrucao);
        voz.RegistrarFerramenta(DeclFila, Fila, "");
    }

    bool Configurada(out string endereco, out string senha)
    {
        endereco = voz != null && voz.segredos != null ? (voz.segredos.enderecoPonte ?? "").Trim().TrimEnd('/') : "";
        senha = voz != null && voz.segredos != null ? (voz.segredos.senhaPonte ?? "").Trim() : "";
        return endereco.Length > 0 && senha.Length >= 16;
    }

    // Requisição à ponte (sem nunca registrar a senha). Devolve (código, json) ou lança.
    static async Task<(int, JObject)> Chamar(HttpMethod metodo, string endereco, string rota, string senha, JObject corpo)
    {
        using (var req = new HttpRequestMessage(metodo, endereco + rota))
        {
            req.Headers.Add("X-Zetta-Senha", senha);
            if (corpo != null) req.Content = new StringContent(corpo.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            using (var r = await http.SendAsync(req))
            {
                string txt = await r.Content.ReadAsStringAsync();
                JObject j;
                try { j = JObject.Parse(txt); } catch { j = new JObject(); }
                return ((int)r.StatusCode, j);
            }
        }
    }

    static string Motivo(Exception e, int codigo)
    {
        if (codigo == 401) return "senha incorreta";
        if (codigo == 403) return "o PC recusou (fora da rede local)";
        if (codigo == 429) return "bloqueada temporariamente";
        if (e is TaskCanceledException || e is HttpRequestException) return "PC desligado, servidor parado ou rede diferente";
        return "falhou";
    }

    // ---------------------------------------------------------------- pedir
    JObject Pedir(JObject a)
    {
        string texto = (a.Value<string>("texto") ?? "").Trim();
        if (texto.Length < 3) return new JObject { ["erro"] = "faltou o texto do pedido" };
        if (!Configurada(out var end, out var senha))
            return new JObject { ["erro"] = "a ponte do PC ainda não foi configurada neste app; avise o usuário para rodar 'Zetta > Ponte PC > Gerar senha' no Unity e instalar um novo APK" };
        StartCoroutine(EnviarPedido(texto, end, senha));
        return new JObject { ["result"] = "enviando o pedido ao PC; o sistema avisará quando entrar na fila" };
    }

    IEnumerator EnviarPedido(string texto, string end, string senha)
    {
        var t = Chamar(HttpMethod.Post, end, "/pedido", senha, new JObject { ["texto"] = texto, ["origem"] = "headset" });
        while (!t.IsCompleted) yield return null;
        if (!t.IsFaulted && t.Result.Item1 == 200)
        {
            int pend = t.Result.Item2.Value<int?>("pendentes") ?? 0;
            DiarioZetta.Registrar("pedido", texto);
            MostrarNoPainelInfo("Pedido enviado ao PC", new[] { texto, pend + " pendente(s) na fila" });
            voz?.Falar("o pedido entrou na fila do PC (" + pend + " pendente" + (pend == 1 ? "" : "s") + "). Avise o usuário em uma frase curta e lembre que o PC só guarda o pedido.");
            if (painel != null) proximaAtualizacao = 0f;
        }
        else
        {
            int cod = t.IsFaulted ? 0 : t.Result.Item1;
            string m = Motivo(t.IsFaulted ? t.Exception.GetBaseException() : null, cod);
            MostrarNoPainelInfo("Pedido não enviado", new[] { m });
            voz?.Falar("não consegui enviar o pedido ao PC: " + m + ". Explique em uma frase simples e sugira conferir se o servidor está rodando.");
        }
    }

    void MostrarNoPainelInfo(string titulo, string[] linhas)
    {
        var info = FindAnyObjectByType<PainelInfo>();
        if (info != null) info.Mostrar(new JObject { ["titulo"] = titulo, ["linhas"] = new JArray(linhas), ["duracao"] = 12 });
    }

    // ---------------------------------------------------------------- painel
    JObject Fila(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "painel da ponte fechado" }; }
        if (!Configurada(out _, out _)) { Montar(); estado = "não configurada"; Redesenhar(); return new JObject { ["erro"] = "a ponte do PC ainda não foi configurada neste app (Zetta > Ponte PC > Gerar senha e novo APK)" }; }
        Montar();
        proximaAtualizacao = 0f;
        return new JObject { ["result"] = "painel da ponte aberto; ele busca o estado da fila agora" };
    }

    void Montar()
    {
        if (painel != null) return;
        var tam = new Vector2(0.64f, 0.42f);
        painel = HoloUtil.CriarPainel(transform, "PontePC", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0.36f, 0.02f, 1.0f));   // à direita: o PainelInfo (avisos) fica à esquerda
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        vidro = painel.GetComponent<PainelVidro>();
        var t = HoloUtil.CriarTexto(painel, "Titulo", "PONTE PC", new Vector3(0, tam.y * 0.5f - 0.04f, TemaZetta.ProfundidadeTexto), new Vector2(0.5f, 0.05f), 0.3f, TemaZetta.AzulTec);
        t.fontStyle = FontStyles.Bold;
        txtEstado = HoloUtil.CriarTexto(painel, "Estado", estado, new Vector3(0, tam.y * 0.5f - 0.1f, TemaZetta.ProfundidadeTexto), new Vector2(0.58f, 0.045f), 0.25f, TemaZetta.Branco);
        txtEstado.fontSizeMin = 0.18f;
        txtLista = HoloUtil.CriarTexto(painel, "Lista", lista, new Vector3(0, -0.01f, TemaZetta.ProfundidadeTexto), new Vector2(0.58f, 0.2f), 0.2f, TemaZetta.Branco, TextAlignmentOptions.TopLeft);
        txtLista.fontSizeMin = 0.18f; txtLista.textWrappingMode = TextWrappingModes.Normal;
        BotaoHolo.Criar(painel, "ATUALIZAR", new Vector3(-0.14f, -tam.y * 0.5f + 0.045f, -0.002f), new Vector2(0.2f, 0.06f), () => proximaAtualizacao = 0f);
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(0.14f, -tam.y * 0.5f + 0.045f, -0.002f), new Vector2(0.2f, 0.06f), Fechar);
        Redesenhar();
        StartCoroutine(HoloUtil.Surgir(painel));
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    void Redesenhar()
    {
        if (painel == null) return;
        txtEstado.text = (online ? "● " : "○ ") + estado;
        txtEstado.color = online ? TemaZetta.Ok : TemaZetta.Erro;
        txtLista.text = lista;
        vidro.DefinirBorda(online ? TemaZetta.AzulTec : TemaZetta.Erro, 1.2f);
    }

    public void Fechar()
    {
        if (painel == null) return;
        Destroy(painel.gameObject);
        painel = null;
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    void Update()
    {
        if (painel == null || atualizando || Time.unscaledTime < proximaAtualizacao) return;
        proximaAtualizacao = Time.unscaledTime + 20f;
        if (Configurada(out var end, out var senha)) StartCoroutine(Atualizar(end, senha));
    }

    IEnumerator Atualizar(string end, string senha)
    {
        atualizando = true;
        estado = "conectando..."; online = false; Redesenhar();
        var t = Chamar(HttpMethod.Get, end, "/fila", senha, null);
        while (!t.IsCompleted) yield return null;
        atualizando = false;
        if (!t.IsFaulted && t.Result.Item1 == 200)
        {
            var j = t.Result.Item2;
            int total = j.Value<int?>("total_pendentes") ?? 0;
            online = true;
            estado = "conectada · " + total + " pendente" + (total == 1 ? "" : "s") + " · " + (j.Value<int?>("feitos") ?? 0) + " atendido(s)";
            var sb = new StringBuilder();
            int n = 0;
            if (j["pendentes"] is JArray arr)
                for (int i = arr.Count - 1; i >= 0 && n < 5; i--, n++)
                {
                    string tx = arr[i].Value<string>("texto") ?? "";
                    sb.Append("#").Append(arr[i].Value<int?>("id")).Append("  ").AppendLine(tx.Length > 60 ? tx.Substring(0, 59) + "…" : tx);
                }
            lista = total == 0 ? "Fila vazia." : sb.ToString();
        }
        else
        {
            online = false;
            estado = Motivo(t.IsFaulted ? t.Exception.GetBaseException() : null, t.IsFaulted ? 0 : t.Result.Item1);
            lista = "";
        }
        Redesenhar();
    }
}
