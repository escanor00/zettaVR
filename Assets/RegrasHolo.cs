using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Programação por blocos por voz: "quando eu disser <frase>, faça <ações>". Cada ação é uma ferramenta que a Zetta já tem
// (nada novo é executado: só se encadeiam as ferramentas existentes). As regras ficam em PlayerPrefs e aparecem como blocos
// encaixados num painel ("mostre as regras"): bloco dourado QUANDO + blocos ciano FAÇA, com botões testar e apagar.
public class RegrasHolo : MonoBehaviour
{
    const string DeclCriar = @"{
  ""name"": ""criar_regra"",
  ""description"": ""Cria (ou substitui) uma regra: quando o usuário disser a frase, o app executa em sequência as ferramentas indicadas. Só ferramentas que já existem."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""quando"": { ""type"": ""STRING"", ""description"": ""Frase gatilho curta, ex.: boa noite."" },
      ""acoes"": {
        ""type"": ""ARRAY"",
        ""description"": ""De 1 a 6 ações executadas em ordem."",
        ""items"": {
          ""type"": ""OBJECT"",
          ""properties"": {
            ""ferramenta"": { ""type"": ""STRING"", ""description"": ""Nome exato da ferramenta, ex.: modo_foco."" },
            ""args_json"": { ""type"": ""STRING"", ""description"": ""Argumentos da ferramenta como texto JSON, ex.: {\""acao\"":\""entrar\"",\""minutos\"":25}. Use {} se não houver."" }
          },
          ""required"": [""ferramenta""]
        }
      }
    },
    ""required"": [""quando"", ""acoes""]
  }
}";
    const string DeclMostrar = @"{
  ""name"": ""mostrar_regras"",
  ""description"": ""Mostra (ou fecha) o painel de regras em blocos e devolve a lista de regras criadas."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] } } }
}";
    const string DeclApagar = @"{
  ""name"": ""apagar_regra"",
  ""description"": ""Apaga a regra cuja frase gatilho é 'quando' (ou todas, com quando=todas)."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""quando"": { ""type"": ""STRING"" } }, ""required"": [""quando""] }
}";
    const string Instrucao =
        "Regras: 'quando eu disser X, faça Y' → criar_regra (quando=X, acoes=[{ferramenta, args_json}]) usando SÓ ferramentas que você já tem, com os mesmos argumentos que usaria normalmente. " +
        "'mostre as regras' → mostrar_regras; 'apague a regra X' → apagar_regra. Se o usuário disser uma frase de regra já criada, o app executa as ações sozinho: " +
        "responda só 'feito' em poucas palavras e NÃO chame as ferramentas dela de novo.";

    // ferramentas que uma regra não pode chamar (recursão, custo alto ou publicação)
    static readonly HashSet<string> Proibidas = new HashSet<string> { "criar_regra", "apagar_regra", "mostrar_regras", "gerar_modelo_3d" };
    const int MaxRegras = 20, MaxPassos = 6;
    const string Chave = "zetta_regras";

    public class Passo { [JsonProperty("f")] public string ferramenta = ""; [JsonProperty("a")] public string args = "{}"; }
    public class Regra { [JsonProperty("q")] public string quando = ""; [JsonProperty("p")] public List<Passo> passos = new List<Passo>(); }

    static List<Regra> regras;
    public static List<Regra> Regras { get { Carregar(); return regras; } }

    AssistenteVoz voz;
    Transform painel;
    float desde;   // quando o painel foi aberto (evita refazer no mesmo quadro)

    // ---------------------------------------------------------------- dados
    static void Carregar()
    {
        if (regras != null) return;
        try { regras = JsonConvert.DeserializeObject<List<Regra>>(PlayerPrefs.GetString(Chave, "[]")) ?? new List<Regra>(); }
        catch { regras = new List<Regra>(); }
    }

    public static void Salvar()
    {
        Carregar();
        PlayerPrefs.SetString(Chave, JsonConvert.SerializeObject(regras));
        PlayerPrefs.Save();
    }

    // minúsculas, sem acento nem pontuação, espaços colapsados
    public static string Normalizar(string s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        bool esp = false;
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); esp = false; }
            else if (!esp && sb.Length > 0) { sb.Append(' '); esp = true; }
        }
        return sb.ToString().Trim();
    }

    // a frase falada contém o gatilho como palavras inteiras ("zetta, boa noite!" casa com "boa noite")
    public static bool Casa(string frase, string gatilho)
    {
        string f = " " + Normalizar(frase) + " ", g = Normalizar(gatilho);
        return g.Length >= 3 && f.Contains(" " + g + " ");
    }

    // devolve null se estiver tudo certo; senão o motivo
    public static string Validar(string quando, List<Passo> passos, Func<string, bool> existe)
    {
        if (Normalizar(quando).Length < 3) return "a frase gatilho é curta demais";
        if (passos == null || passos.Count == 0) return "faltaram as ações";
        if (passos.Count > MaxPassos) return "no máximo " + MaxPassos + " ações por regra";
        foreach (var p in passos)
        {
            if (Proibidas.Contains(p.ferramenta)) return "a ferramenta " + p.ferramenta + " não pode ser usada em regras";
            if (!existe(p.ferramenta)) return "ferramenta desconhecida: " + p.ferramenta;
            try { JObject.Parse(string.IsNullOrWhiteSpace(p.args) ? "{}" : p.args); }
            catch { return "argumentos inválidos em " + p.ferramenta; }
        }
        return null;
    }

    public static void Adicionar(Regra r)
    {
        Carregar();
        string q = Normalizar(r.quando);
        regras.RemoveAll(x => Normalizar(x.quando) == q);
        regras.Add(r);
        while (regras.Count > MaxRegras) regras.RemoveAt(0);
        Salvar();
    }

    // ---------------------------------------------------------------- ferramentas
    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclCriar, Criar, Instrucao);
        voz.RegistrarFerramenta(DeclMostrar, Mostrar, "");
        voz.RegistrarFerramenta(DeclApagar, Apagar, "");
        voz.aoFraseCompleta += Disparar;
    }

    void OnDestroy() { if (voz != null) voz.aoFraseCompleta -= Disparar; }

    JObject Criar(JObject a)
    {
        string quando = (a.Value<string>("quando") ?? "").Trim();
        var passos = new List<Passo>();
        if (a["acoes"] is JArray arr)
            foreach (var x in arr)
                passos.Add(new Passo { ferramenta = x.Value<string>("ferramenta") ?? "", args = x.Value<string>("args_json") ?? "{}" });
        string erro = Validar(quando, passos, voz.TemFerramenta);
        if (erro != null) return new JObject { ["erro"] = erro };
        Adicionar(new Regra { quando = quando, passos = passos });
        if (painel != null) Montar();
        return new JObject { ["result"] = "regra criada: quando disser \"" + quando + "\", faço " + passos.Count + " ação(ões)" };
    }

    JObject Mostrar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "painel de regras fechado" }; }
        Montar();
        var lista = new JArray();
        foreach (var r in Regras) lista.Add(r.quando + " → " + string.Join(", ", r.passos.ConvertAll(p => p.ferramenta)));
        return new JObject { ["result"] = Regras.Count == 0 ? "ainda não há regras; o painel está aberto" : "painel aberto", ["regras"] = lista };
    }

    JObject Apagar(JObject a)
    {
        string q = (a.Value<string>("quando") ?? "").Trim();
        Carregar();
        int antes = regras.Count;
        if (Normalizar(q) == "todas") regras.Clear();
        else regras.RemoveAll(r => Normalizar(r.quando) == Normalizar(q) || Casa(q, r.quando));
        Salvar();
        if (painel != null) Montar();
        return regras.Count < antes ? new JObject { ["result"] = "regra apagada" } : new JObject { ["erro"] = "não achei essa regra" };
    }

    // ---------------------------------------------------------------- execução
    void Disparar(string frase)
    {
        foreach (var r in Regras.ToArray())
            if (Casa(frase, r.quando)) { StartCoroutine(Executar(r)); break; }
    }

    public void Testar(Regra r) { StartCoroutine(Executar(r)); }

    IEnumerator Executar(Regra r)
    {
        DiarioZetta.Registrar("regra", r.quando);
        foreach (var p in r.passos)
        {
            JObject args;
            try { args = JObject.Parse(string.IsNullOrWhiteSpace(p.args) ? "{}" : p.args); } catch { args = new JObject(); }
            var res = voz.ChamarFerramenta(p.ferramenta, args);
            if (res["erro"] != null) Debug.LogWarning("RegrasHolo: " + p.ferramenta + " → " + res["erro"]);
            yield return new WaitForSeconds(0.6f);
        }
    }

    // ---------------------------------------------------------------- painel de blocos
    static string Humano(Passo p)
    {
        string nome = p.ferramenta.Replace('_', ' ');
        try
        {
            var o = JObject.Parse(string.IsNullOrWhiteSpace(p.args) ? "{}" : p.args);
            var partes = new List<string>();
            foreach (var kv in o) partes.Add(kv.Value.ToString());
            if (partes.Count > 0)
            {
                string extra = string.Join(" ", partes);
                if (extra.Length > 16) extra = extra.Substring(0, 15) + "…";
                return nome + "\n<size=75%>" + extra + "</size>";
            }
        }
        catch { }
        return nome;
    }

    void Montar()
    {
        if (painel != null) Destroy(painel.gameObject);
        const int Mostradas = 5;
        var lista = Regras;
        int n = Mathf.Min(lista.Count, Mostradas);
        float linha = 0.105f;
        var tam = new Vector2(1.12f, 0.2f + Mathf.Max(1, n) * linha);
        painel = HoloUtil.CriarPainel(transform, "Regras", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0.02f, 1.1f));
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        var t = HoloUtil.CriarTexto(painel, "Titulo", "REGRAS  ·  quando eu disser… faça…", new Vector3(0, tam.y * 0.5f - 0.04f, TemaZetta.ProfundidadeTexto), new Vector2(0.9f, 0.05f), 0.3f, TemaZetta.AzulTec);
        t.fontStyle = FontStyles.Bold;

        if (lista.Count == 0)
        {
            var vazio = HoloUtil.CriarTexto(painel, "Vazio", "Nenhuma regra. Diga: \"Zetta, quando eu disser boa noite, ative o modo foco\".", new Vector3(0, -0.01f, TemaZetta.ProfundidadeTexto), new Vector2(0.95f, 0.1f), 0.25f, TemaZetta.Branco);
            vazio.fontSizeMin = 0.18f;
        }
        float y = tam.y * 0.5f - 0.115f;
        for (int i = 0; i < n; i++, y -= linha)
        {
            var r = lista[i];
            float x = -tam.x * 0.5f + 0.03f;
            x = Bloco(painel, "QUANDO\n<size=75%>" + (r.quando.Length > 22 ? r.quando.Substring(0, 21) + "…" : r.quando) + "</size>", x, y, 0.24f, TemaZetta.Dourado);
            foreach (var p in r.passos)
            {
                if (x + 0.17f > tam.x * 0.5f - 0.27f) break;   // sem espaço: o restante fica oculto
                x = Bloco(painel, Humano(p), x - 0.012f, y, 0.17f, TemaZetta.Ciano);   // encaixe: os blocos se sobrepõem um pouco
            }
            var regra = r;
            BotaoHolo.Criar(painel, "TESTAR", new Vector3(tam.x * 0.5f - 0.21f, y, -0.002f), new Vector2(0.17f, 0.065f), () => Testar(regra));
            BotaoHolo.Criar(painel, "APAGAR", new Vector3(tam.x * 0.5f - 0.075f, y, -0.002f), new Vector2(0.115f, 0.065f), () => { Regras.Remove(regra); Salvar(); Montar(); });
        }
        if (lista.Count > Mostradas)
        {
            var mais = HoloUtil.CriarTexto(painel, "Mais", "+" + (lista.Count - Mostradas) + " regra(s)", new Vector3(0.3f, -tam.y * 0.5f + 0.04f, TemaZetta.ProfundidadeTexto), new Vector2(0.3f, 0.04f), 0.2f, TemaZetta.AzulTec);
            mais.fontSizeMin = 0.18f;
        }
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(-tam.x * 0.5f + 0.1f, -tam.y * 0.5f + 0.04f, -0.002f), new Vector2(0.15f, 0.055f), Fechar);
        if (desde == 0f || Time.time - desde > 0.5f) { StartCoroutine(HoloUtil.Surgir(painel)); SonsZetta.Tocar(SonsZetta.Abrir); }
        desde = Time.time;
    }

    // um bloco de vidro com borda colorida; devolve o x do fim dele
    static float Bloco(Transform pai, string texto, float xInicio, float y, float largura, Color cor)
    {
        var b = HoloUtil.CriarPainel(pai, "Bloco", new Vector2(largura, 0.075f), TemaZetta.OrdemBotao);
        b.localPosition = new Vector3(xInicio + largura * 0.5f, y, -0.004f);
        b.GetComponent<PainelVidro>().DefinirBorda(cor, 1.2f);
        var t = HoloUtil.CriarTexto(b, "Texto", texto, new Vector3(0, 0, TemaZetta.ProfundidadeTexto), new Vector2(largura - 0.02f, 0.065f), 0.22f, TemaZetta.Branco);
        t.fontSizeMin = 0.16f;
        // encaixe (peça de quebra-cabeça): pequeno retângulo na borda direita
        var e = HoloUtil.CriarPainel(b, "Encaixe", new Vector2(0.014f, 0.026f), TemaZetta.OrdemBotao + 1);
        e.localPosition = new Vector3(largura * 0.5f, 0, -0.002f);
        e.GetComponent<PainelVidro>().DefinirBorda(cor, 1.6f);
        return xInicio + largura;
    }

    public void Fechar()
    {
        if (painel == null) return;
        Destroy(painel.gameObject);
        painel = null;
        SonsZetta.Tocar(SonsZetta.Fechar);
    }
}
