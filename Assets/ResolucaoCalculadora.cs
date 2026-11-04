using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// "Zetta, resolva ...": a Gemini resolve e manda as etapas; o painel mostra cada uma enquanto ela explica.
// O painel avança sozinho (tempo de leitura) e a Zetta pode sincronizar chamando acao=proxima a cada etapa; há botões ANTERIOR/PRÓXIMA.
public class ResolucaoHolo : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""resolver_passo_a_passo"",
  ""description"": ""Mostra num painel holográfico a resolução de um problema de matemática, etapa por etapa. Resolva o problema com cuidado ANTES de chamar (use a ferramenta calcular para contas numéricas) e envie todas as etapas. Depois explique cada etapa em voz alta, em ordem, uma frase curta por etapa."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""mostrar"", ""proxima"", ""anterior"", ""fechar""], ""description"": ""mostrar (padrão) abre o painel; proxima/anterior mudam a etapa em destaque; fechar fecha."" },
      ""problema"": { ""type"": ""STRING"", ""description"": ""Enunciado curto, ex.: Resolva 2x + 6 = 14."" },
      ""etapas"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""STRING"" }, ""description"": ""Cada etapa em uma linha curta com a conta, ex.: 2x = 14 - 6 = 8."" },
      ""resultado"": { ""type"": ""STRING"", ""description"": ""Resposta final curta, ex.: x = 4."" }
    }
  }
}";
    const string Instrucao =
        "Para 'resolva...', 'como se resolve...', 'passo a passo' use resolver_passo_a_passo: resolva primeiro (contas numéricas com a ferramenta calcular), envie problema, etapas e resultado, " +
        "e em seguida explique cada etapa em voz alta, em ordem; ao começar cada etapa chame resolver_passo_a_passo com acao=proxima para o painel acompanhar.";

    Transform painel;
    TextMeshPro txtProblema, txtEtapas, txtResultado, txtContador;
    readonly List<string> etapas = new List<string>();
    string resultado = "";
    int atual;                 // etapas visíveis (1..n)
    float proximaEtapaEm;
    bool manual;               // depois de um toque nos botões ou de "proxima" da Zetta, o tempo não avança mais sozinho
    float atividade;

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    public JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "mostrar").ToLowerInvariant();
        if (acao == "fechar") { Fechar(); return new JObject { ["result"] = "painel fechado" }; }
        if (acao == "proxima" || acao == "anterior")
        {
            if (painel == null) return new JObject { ["erro"] = "não há resolução aberta" };
            manual = true;
            Mover(acao == "proxima" ? 1 : -1);
            return new JObject { ["result"] = "etapa " + atual + " de " + etapas.Count };
        }
        var lista = new List<string>();
        if (a["etapas"] is JArray arr) foreach (var t in arr) if (!string.IsNullOrWhiteSpace(t.ToString())) lista.Add(t.ToString().Trim());
        if (lista.Count == 0) return new JObject { ["erro"] = "faltam as etapas" };
        if (lista.Count > 9) lista.RemoveRange(9, lista.Count - 9);
        Fechar();
        etapas.AddRange(lista);
        resultado = (a.Value<string>("resultado") ?? "").Trim();
        Montar((a.Value<string>("problema") ?? "").Trim());
        atual = 1; manual = false;
        proximaEtapaEm = Time.time + TempoDeLeitura(etapas[0]);
        Atualizar();
        return new JObject { ["result"] = "painel aberto com " + etapas.Count + " etapas; explique cada uma em voz alta, em ordem, chamando acao=proxima a cada etapa" };
    }

    static float TempoDeLeitura(string s) => Mathf.Clamp(2.5f + s.Length / 12f, 3f, 9f);

    void Montar(string problema)
    {
        var tam = new Vector2(0.64f, 0.5f);
        painel = HoloUtil.CriarPainel(transform, "Resolucao", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0.02f, 0.85f));
        painel.position = pos;
        painel.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        float zT = TemaZetta.ProfundidadeTexto;
        var cab = HoloUtil.CriarTexto(painel, "Titulo", "RESOLUÇÃO PASSO A PASSO", new Vector3(-0.02f, 0.215f, zT), new Vector2(0.5f, 0.04f), 0.3f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        cab.fontStyle = FontStyles.Bold;
        txtContador = HoloUtil.CriarTexto(painel, "Contador", "", new Vector3(0.25f, 0.215f, zT), new Vector2(0.14f, 0.04f), 0.25f, TemaZetta.Ciano, TextAlignmentOptions.Right);
        txtProblema = HoloUtil.CriarTexto(painel, "Problema", problema, new Vector3(0f, 0.16f, zT), new Vector2(0.58f, 0.06f), 0.3f, TemaZetta.Branco, TextAlignmentOptions.Left);
        txtEtapas = HoloUtil.CriarTexto(painel, "Etapas", "", new Vector3(0f, 0.015f, zT), new Vector2(0.58f, 0.23f), 0.3f, TemaZetta.Branco, TextAlignmentOptions.TopLeft);
        txtEtapas.fontSizeMin = 0.12f; txtEtapas.fontSizeMax = 0.34f;
        txtEtapas.overflowMode = TextOverflowModes.Ellipsis;
        txtResultado = HoloUtil.CriarTexto(painel, "Resultado", "", new Vector3(0f, -0.125f, zT), new Vector2(0.58f, 0.05f), 0.4f, TemaZetta.Dourado, TextAlignmentOptions.Left);
        txtResultado.fontStyle = FontStyles.Bold;
        float zB = TemaZetta.ProfundidadeBotao;
        BotaoHolo.Criar(painel, "ANTERIOR", new Vector3(-0.2f, -0.205f, zB), new Vector2(0.17f, 0.06f), () => { manual = true; Mover(-1); });
        BotaoHolo.Criar(painel, "PRÓXIMA", new Vector3(0f, -0.205f, zB), new Vector2(0.17f, 0.06f), () => { manual = true; Mover(1); });
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(0.2f, -0.205f, zB), new Vector2(0.17f, 0.06f), Fechar);
        StartCoroutine(HoloUtil.Surgir(painel));
        atividade = Time.time;
    }

    void Mover(int d)
    {
        atual = Mathf.Clamp(atual + d, 1, etapas.Count);
        // a última etapa mostra também a resposta
        Atualizar();
        atividade = Time.time;
    }

    void Atualizar()
    {
        if (painel == null) return;
        var sb = new StringBuilder();
        for (int i = 0; i < atual; i++)
        {
            bool corrente = i == atual - 1;
            string cor = corrente ? "#" + ColorUtility.ToHtmlStringRGB(TemaZetta.Branco) : "#" + ColorUtility.ToHtmlStringRGB(TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.8f));
            sb.Append("<color=").Append(cor).Append(">").Append(corrente ? "<b>" : "").Append(i + 1).Append(".  ").Append(etapas[i]).Append(corrente ? "</b>" : "").Append("</color>\n");
        }
        txtEtapas.text = sb.ToString().TrimEnd('\n');
        txtContador.text = "etapa " + atual + "/" + etapas.Count;
        txtResultado.text = atual >= etapas.Count && resultado.Length > 0 ? "Resposta: " + resultado : "";
    }

    void Update()
    {
        if (painel == null) return;
        if (!manual && atual < etapas.Count && Time.time >= proximaEtapaEm)
        {
            atual++;
            Atualizar();
            proximaEtapaEm = Time.time + TempoDeLeitura(etapas[atual - 1]);
        }
        // some sozinho 90 s depois da última mudança
        if (Time.time - atividade > 90f && atual >= etapas.Count) Fechar();
        if (!manual) atividade = Mathf.Max(atividade, Time.time - 85f * 0f);
    }

    public void Fechar()
    {
        if (painel != null) Destroy(painel.gameObject);
        painel = null;
        etapas.Clear();
    }
}

// Ferramenta "calcular" (avalia com o parser seguro, para o modelo não errar contas) e calculadora holográfica com botões poke.
public class CalculadoraHolo : MonoBehaviour
{
    const string DeclCalc = @"{
  ""name"": ""calcular"",
  ""description"": ""Calcula uma expressão matemática com precisão (use SEMPRE para contas numéricas antes de responder). Variáveis opcionais x, y. Funções: sen, cos, tan, exp, ln, log (base 10), raiz, abs, ^ para potência, pi, e."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""expressao"": { ""type"": ""STRING"" },
      ""x"": { ""type"": ""NUMBER"" }, ""y"": { ""type"": ""NUMBER"" }
    },
    ""required"": [""expressao""]
  }
}";
    const string DeclCalculadora = @"{
  ""name"": ""calculadora_holografica"",
  ""description"": ""Abre ou fecha a calculadora holográfica com botões que se tocam com o dedo."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] } },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Use a ferramenta calcular para qualquer conta numérica em vez de calcular de cabeça. 'Abra a calculadora' → calculadora_holografica acao=abrir.";

    Transform painel;
    TextMeshPro txtExpr, txtRes;
    string expr = "";
    double ultimo;
    bool temUltimo, mostrouResultado;

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclCalc, Calcular, Instrucao);
        voz.RegistrarFerramenta(DeclCalculadora, a =>
        {
            if ((a.Value<string>("acao") ?? "abrir").ToLowerInvariant().StartsWith("f")) { Fechar(); return new JObject { ["result"] = "calculadora fechada" }; }
            Abrir();
            return new JObject { ["result"] = "calculadora aberta" };
        });
    }

    public JObject Calcular(JObject a)
    {
        string txt = a.Value<string>("expressao") ?? "";
        if (!ExpressaoMatematica.Tentar(txt, out var e, out string erro)) return new JObject { ["erro"] = erro };
        double r = e.Avaliar(a.Value<double?>("x") ?? 0, a.Value<double?>("y") ?? 0);
        if (painel != null) { expr = txt; ultimo = r; temUltimo = true; MostrarResultado(r); }
        return new JObject { ["resultado"] = ExpressaoMatematica.Formatar(r), ["valor"] = double.IsNaN(r) || double.IsInfinity(r) ? (JToken)r.ToString() : r };
    }

    static readonly string[][] Teclas =
    {
        new[] { "C", "⌫", "(", ")", "÷" },
        new[] { "sen", "cos", "tan", "√", "^" },
        new[] { "7", "8", "9", "×", "π" },
        new[] { "4", "5", "6", "−", "e" },
        new[] { "1", "2", "3", "+", "ln" },
        new[] { "0", ",", "x", "ANS", "=" },
    };

    public void Abrir()
    {
        if (painel != null) return;
        var tam = new Vector2(0.44f, 0.56f);
        painel = HoloUtil.CriarPainel(transform, "Calculadora", tam);
        painel.position = HoloUtil.PontoNaVista(new Vector3(0.12f, -0.1f, 0.6f));
        painel.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        float zT = TemaZetta.ProfundidadeTexto;
        var cab = HoloUtil.CriarTexto(painel, "Titulo", "CALCULADORA", new Vector3(-0.1f, 0.255f, zT), new Vector2(0.22f, 0.03f), 0.2f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        cab.fontStyle = FontStyles.Bold;
        txtExpr = HoloUtil.CriarTexto(painel, "Expressao", "", new Vector3(0f, 0.2f, zT), new Vector2(0.4f, 0.05f), 0.3f, TemaZetta.Branco, TextAlignmentOptions.Right);
        txtExpr.fontSizeMin = 0.1f; txtExpr.fontSizeMax = 0.3f; txtExpr.textWrappingMode = TextWrappingModes.NoWrap; txtExpr.overflowMode = TextOverflowModes.Ellipsis;
        txtRes = HoloUtil.CriarTexto(painel, "Resultado", "0", new Vector3(0f, 0.15f, zT), new Vector2(0.4f, 0.06f), 0.5f, TemaZetta.Dourado, TextAlignmentOptions.Right);
        txtRes.fontSizeMin = 0.15f; txtRes.fontSizeMax = 0.45f; txtRes.fontStyle = FontStyles.Bold; txtRes.textWrappingMode = TextWrappingModes.NoWrap;
        var bt = new Vector2(0.074f, 0.056f);
        for (int r = 0; r < Teclas.Length; r++)
            for (int c = 0; c < 5; c++)
            {
                string tecla = Teclas[r][c];
                BotaoHolo.Criar(painel, tecla, new Vector3(-0.16f + c * 0.08f, 0.08f - r * 0.065f, TemaZetta.ProfundidadeBotao), bt, () => Tecla(tecla));
            }
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(0.14f, 0.255f, TemaZetta.ProfundidadeBotao), new Vector2(0.12f, 0.04f), Fechar);
        StartCoroutine(HoloUtil.Surgir(painel));
        expr = ""; mostrouResultado = false;
    }

    void Tecla(string t)
    {
        if (mostrouResultado && t != "=" && t != "C" && t != "⌫")
        {
            // depois de um resultado, operador continua a conta; número ou função começa outra
            if (t == "+" || t == "−" || t == "×" || t == "÷" || t == "^") expr = ExpressaoMatematica.Formatar(ultimo).Replace(',', '.');
            else expr = "";
            mostrouResultado = false;
        }
        switch (t)
        {
            case "C": expr = ""; txtRes.text = "0"; mostrouResultado = false; break;
            case "⌫": if (expr.Length > 0) expr = expr.Substring(0, expr.Length - 1); mostrouResultado = false; break;
            case "=": Resolver(); return;
            case "sen": case "cos": case "tan": case "ln": expr += t + "("; break;
            case "√": expr += "raiz("; break;
            case "ANS": if (temUltimo) expr += ExpressaoMatematica.Formatar(ultimo).Replace(',', '.'); break;
            case ",": expr += "."; break;
            case "π": expr += "pi"; break;
            default: expr += t; break;
        }
        txtExpr.text = expr.Replace("*", "×").Replace("/", "÷").Replace("pi", "π").Replace("raiz", "√").Replace(".", ",");
    }

    void Resolver()
    {
        if (expr.Length == 0) return;
        // x é calculado com o último resultado (se existir) para dar um uso a tecla "x"
        if (!ExpressaoMatematica.Tentar(expr, out var e, out string erro)) { txtRes.text = "erro"; txtRes.color = TemaZetta.Erro; mostrouResultado = false; return; }
        double r = e.Avaliar(temUltimo ? ultimo : 0);
        ultimo = r; temUltimo = true;
        MostrarResultado(r);
    }

    void MostrarResultado(double r)
    {
        txtRes.color = TemaZetta.Dourado;
        txtRes.text = ExpressaoMatematica.Formatar(r);
        txtExpr.text = expr.Replace("*", "×").Replace("/", "÷").Replace("pi", "π").Replace("raiz", "√").Replace(".", ",") + " =";
        mostrouResultado = true;
    }

    public void Fechar()
    {
        if (painel != null) Destroy(painel.gameObject);
        painel = null;
    }
}
