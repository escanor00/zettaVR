using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// "Zetta, mostre o código da central": painel flutuante SOMENTE LEITURA com o código C# do projeto (cópia empacotada em
// Resources/Codigo pelo ExportarCodigoZetta, já sem chaves/tokens), com cores de sintaxe e rolagem por botões, pela voz
// ("desça", "suba") ou arrastando o dedo sobre o texto.
public class CodigoHolo : MonoBehaviour
{
    public static CodigoHolo Instancia { get; private set; }

    const string Decl = @"{
  ""name"": ""mostrar_codigo"",
  ""description"": ""Mostra o código-fonte (somente leitura) de uma parte do app num painel flutuante com cores. 'parte' é o nome do arquivo/sistema (ex.: central, esfera, notas, timers, voz). Também rola o código."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""parte"": { ""type"": ""STRING"", ""description"": ""Sistema ou arquivo, ex.: central, esfera, assistente de voz, notas."" },
      ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""descer"", ""subir"", ""fechar""], ""description"": ""abrir (padrão), descer/subir para rolar, fechar."" }
    }
  }
}";
    const string Instrucao = "'mostre o código da <parte>' → mostrar_codigo parte=<parte>; 'desça/suba o código' → acao=descer/subir; 'feche o código' → acao=fechar. " +
        "Depois de abrir, diga em UMA frase o que esse arquivo faz. Nunca invente o conteúdo: ele aparece no painel.";

    const int Linhas = 16;
    const int Passo = 10;
    const int Colunas = 68;   // linhas mais compridas são cortadas com … (a rolagem é só vertical)
    readonly Vector2 tamanho = new Vector2(0.86f, 0.56f);

    AssistenteVoz voz;
    Transform painel;
    TextMeshPro corpo, titulo;
    string nomeAtual;
    string[] linhasTexto = new string[0];
    string[] coloridas;
    int topo;
    float acumulado, yAnterior = float.NaN;
    XROrigin origem;
    static Dictionary<string, TextAsset> arquivos;

    void Awake()
    {
        Instancia = this;
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    // ---------------------------------------------------------------- arquivos
    static Dictionary<string, TextAsset> Arquivos()
    {
        if (arquivos != null) return arquivos;
        arquivos = new Dictionary<string, TextAsset>();
        foreach (var t in Resources.LoadAll<TextAsset>("Codigo")) if (!t.name.StartsWith("_")) arquivos[t.name] = t;
        return arquivos;
    }

    public static string Normalizar(string s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // nome exato > começa com > contém > qualquer palavra (4+ letras) contida no nome do arquivo
    public static string Achar(string parte)
    {
        string alvo = Normalizar(parte);
        if (alvo.Length == 0) return null;
        var nomes = new List<string>(Arquivos().Keys);
        string melhor = null;
        foreach (var n in nomes) if (Normalizar(n) == alvo) return n;
        foreach (var n in nomes) if (Normalizar(n).StartsWith(alvo) && (melhor == null || n.Length < melhor.Length)) melhor = n;
        if (melhor != null) return melhor;
        foreach (var n in nomes) if (Normalizar(n).Contains(alvo) && (melhor == null || n.Length < melhor.Length)) melhor = n;
        if (melhor != null) return melhor;
        foreach (var palavra in Regex.Split(parte ?? "", @"\W+"))
        {
            string p = Normalizar(palavra);
            if (p.Length < 4 || p == "codigo" || p == "sistema") continue;
            foreach (var n in nomes) if (Normalizar(n).Contains(p) && (melhor == null || n.Length < melhor.Length)) melhor = n;
            if (melhor != null) return melhor;
        }
        return null;
    }

    // ---------------------------------------------------------------- ferramenta
    JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "painel de código fechado" }; }
        string parte = a.Value<string>("parte") ?? "";
        if (acao.StartsWith("d")) { Rolar(Passo); return new JObject { ["result"] = "rolei para baixo" }; }
        if (acao.StartsWith("s")) { Rolar(-Passo); return new JObject { ["result"] = "rolei para cima" }; }
        if (string.IsNullOrWhiteSpace(parte) && nomeAtual != null) parte = nomeAtual;
        if (!Abrir(parte, out var erro)) return new JObject { ["erro"] = erro };
        return new JObject { ["result"] = "mostrando o código de " + nomeAtual + " (" + linhasTexto.Length + " linhas)" };
    }

    public bool Abrir(string parte, out string erro)
    {
        erro = null;
        string nome = Achar(parte);
        if (nome == null)
        {
            erro = Arquivos().Count == 0 ? "o código não está empacotado neste build" : "não achei essa parte; arquivos como: " + string.Join(", ", Primeiros(8));
            return false;
        }
        nomeAtual = nome;
        linhasTexto = Arquivos()[nome].text.Replace("\r", "").Split('\n');
        coloridas = new string[linhasTexto.Length];
        topo = 0;
        if (painel == null) Montar();
        Atualizar();
        return true;
    }

    static IEnumerable<string> Primeiros(int n)
    {
        int i = 0;
        foreach (var k in Arquivos().Keys) { if (i++ >= n) yield break; yield return k; }
    }

    public void Fechar()
    {
        if (painel == null) return;
        Destroy(painel.gameObject);
        painel = null;
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    // ---------------------------------------------------------------- painel
    void Montar()
    {
        painel = HoloUtil.CriarPainel(transform, "Codigo", tamanho);
        var pos = HoloUtil.PontoNaVista(new Vector3(0.1f, 0.02f, 1.05f));
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        titulo = HoloUtil.CriarTexto(painel, "Titulo", "", new Vector3(-0.1f, tamanho.y * 0.5f - 0.04f, TemaZetta.ProfundidadeTexto),
            new Vector2(0.55f, 0.05f), 0.3f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        titulo.fontStyle = FontStyles.Bold;
        corpo = HoloUtil.CriarTexto(painel, "Corpo", "", new Vector3(0, -0.03f, TemaZetta.ProfundidadeTexto),
            new Vector2(tamanho.x - 0.08f, tamanho.y - 0.14f), 0.17f, TemaZetta.Branco, TextAlignmentOptions.TopLeft);
        corpo.enableAutoSizing = false;
        corpo.fontSize = 0.19f;
        corpo.textWrappingMode = TextWrappingModes.NoWrap;
        corpo.overflowMode = TextOverflowModes.Overflow;
        corpo.richText = true;
        float y = tamanho.y * 0.5f - 0.04f;
        BotaoHolo.Criar(painel, "SUBIR", new Vector3(0.2f, y, -0.002f), new Vector2(0.11f, 0.055f), () => Rolar(-Passo));
        BotaoHolo.Criar(painel, "DESCER", new Vector3(0.32f, y, -0.002f), new Vector2(0.11f, 0.055f), () => Rolar(Passo));
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(-tamanho.x * 0.5f + 0.08f, -tamanho.y * 0.5f + 0.04f, -0.002f), new Vector2(0.12f, 0.055f), Fechar);
        var rodape = HoloUtil.CriarTexto(painel, "Rodape", "SOMENTE LEITURA", new Vector3(0.18f, -tamanho.y * 0.5f + 0.04f, TemaZetta.ProfundidadeTexto),
            new Vector2(0.4f, 0.04f), 0.2f, TemaZetta.AzulTec, TextAlignmentOptions.Right);
        rodape.fontSizeMin = 0.18f;
        StartCoroutine(HoloUtil.Surgir(painel));
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    void Rolar(int linhas)
    {
        if (painel == null) return;
        int novo = Mathf.Clamp(topo + linhas, 0, Mathf.Max(0, linhasTexto.Length - Linhas));
        if (novo == topo) return;
        topo = novo;
        Atualizar();
    }

    void Atualizar()
    {
        int fim = Mathf.Min(linhasTexto.Length, topo + Linhas);
        var sb = new StringBuilder();
        for (int i = topo; i < fim; i++)
        {
            if (coloridas[i] == null) coloridas[i] = Colorir(linhasTexto[i]);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(TemaZetta.AzulTec)).Append("><mspace=0.55em>").Append((i + 1).ToString().PadLeft(4)).Append("</mspace></color>  <mspace=0.55em>")
              .Append(coloridas[i]).Append("</mspace>\n");
        }
        corpo.text = sb.ToString();
        titulo.text = nomeAtual + ".cs   " + (topo + 1) + "–" + fim + " / " + linhasTexto.Length;
    }

    // ---------------------------------------------------------------- cores de sintaxe
    static readonly Regex Token = new Regex(
        @"(?<com>//.*$)|(?<str>@?""(?:[^""\\]|\\.)*""?)|(?<num>\b\d+(?:\.\d+)?f?\b)|(?<pal>\b(?:public|private|protected|static|void|class|struct|enum|interface|new|return|if|else|for|foreach|while|switch|case|break|continue|using|namespace|null|true|false|this|var|int|float|bool|string|const|readonly|override|virtual|abstract|partial|async|await|yield|in|out|ref|is|as|try|catch|finally|throw|get|set|default|object|double|byte|long|char)\b)|(?<tipo>\b[A-Z][A-Za-z0-9]+\b)",
        RegexOptions.Compiled);
    static string hexCom, hexStr, hexNum, hexPal, hexTipo;

    static string Colorir(string linha)
    {
        if (hexCom == null)
        {
            hexCom = ColorUtility.ToHtmlStringRGB(new Color(0.45f, 0.58f, 0.68f));
            hexStr = ColorUtility.ToHtmlStringRGB(TemaZetta.Dourado);
            hexNum = ColorUtility.ToHtmlStringRGB(TemaZetta.Roxo);
            hexPal = ColorUtility.ToHtmlStringRGB(TemaZetta.AzulTec);
            hexTipo = ColorUtility.ToHtmlStringRGB(TemaZetta.Ciano);
        }
        linha = linha.Replace("\t", "    ");
        if (linha.Length > Colunas) linha = linha.Substring(0, Colunas - 1) + "…";
        var sb = new StringBuilder();
        int pos = 0;
        foreach (Match m in Token.Matches(linha))
        {
            sb.Append(Escapar(linha.Substring(pos, m.Index - pos)));
            string hex = m.Groups["com"].Success ? hexCom : m.Groups["str"].Success ? hexStr : m.Groups["num"].Success ? hexNum : m.Groups["pal"].Success ? hexPal : hexTipo;
            sb.Append("<color=#").Append(hex).Append('>').Append(Escapar(m.Value)).Append("</color>");
            pos = m.Index + m.Length;
        }
        sb.Append(Escapar(linha.Substring(pos)));
        return sb.ToString();
    }

    // '<' viraria tag do TMP: <noparse> o mostra literalmente
    static string Escapar(string s) { return s.Replace("<", "<noparse><</noparse>"); }

    // ---------------------------------------------------------------- rolagem com o dedo
    void Update()
    {
        if (painel == null) return;
        var maos = BotaoHolo.MaosAtivas();
        if (maos == null) { yAnterior = float.NaN; return; }
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        if (origem == null) return;
        bool tocando = false;
        foreach (var mao in new[] { maos.rightHand, maos.leftHand })
        {
            if (!mao.isTracked || !mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose)) continue;
            var l = painel.InverseTransformPoint(origem.Origin.transform.TransformPoint(pose.position));
            // sobre o texto (abaixo da barra de botões), encostando ou muito perto da superfície
            if (Mathf.Abs(l.x) > tamanho.x * 0.5f || l.y > tamanho.y * 0.5f - 0.09f || l.y < -tamanho.y * 0.5f + 0.09f) continue;
            if (l.z < -0.03f || l.z > 0.03f) continue;
            tocando = true;
            if (!float.IsNaN(yAnterior))
            {
                acumulado += (yAnterior - l.y) / ((tamanho.y - 0.14f) / Linhas);   // dedo para cima = texto sobe = desce no arquivo
                int n = (int)acumulado;
                if (n != 0) { acumulado -= n; Rolar(n); }
            }
            yAnterior = l.y;
            break;
        }
        if (!tocando) { yAnterior = float.NaN; acumulado = 0f; }
    }
}
