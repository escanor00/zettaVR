using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// Ajuste ao vivo do visual: velocidade, brilho, tamanho e cor da esfera; opacidade e brilho da borda dos painéis.
// Os valores ficam em PlayerPrefs e são lidos por EsferaZetta (velocidade, brilho, tamanho) e pelo shader VidroHolo (globais).
public static class AjustesZetta
{
    public struct Item { public string chave, nome; public float min, max, padrao; }

    public static readonly Item[] Itens =
    {
        new Item { chave = "velocidade", nome = "Velocidade da esfera", min = 0.3f, max = 2.5f, padrao = 1f },
        new Item { chave = "brilho",     nome = "Brilho da esfera",     min = 0.4f, max = 2f,   padrao = 1f },
        new Item { chave = "tamanho",    nome = "Tamanho da esfera",    min = 0.6f, max = 1.6f, padrao = 1f },
        new Item { chave = "opacidade",  nome = "Opacidade dos painéis", min = 0.5f, max = 1.4f, padrao = 1f },
        new Item { chave = "borda",      nome = "Brilho da borda",      min = 0.4f, max = 2.2f, padrao = 1f },
    };
    public static readonly string[] Cores = { "dourado", "ciano", "azul", "roxo", "rosa", "vermelho", "laranja", "verde", "branco" };

    static readonly Dictionary<string, float> valores = new Dictionary<string, float>();
    static bool carregado;

    // lidos todo quadro pela esfera
    public static float Velocidade => Obter("velocidade");
    public static float BrilhoEsfera => Obter("brilho");
    public static float TamanhoEsfera => Obter("tamanho");

    static string Pref(string chave) { return "zetta_aj_" + chave; }

    static void Carregar()
    {
        if (carregado) return;
        carregado = true;
        foreach (var i in Itens) valores[i.chave] = Mathf.Clamp(PlayerPrefs.GetFloat(Pref(i.chave), i.padrao), i.min, i.max);
    }

    public static float Obter(string chave) { Carregar(); return valores.TryGetValue(chave, out var v) ? v : 1f; }

    public static void Definir(string chave, float v)
    {
        Carregar();
        foreach (var i in Itens)
            if (i.chave == chave)
            {
                valores[chave] = Mathf.Clamp(v, i.min, i.max);
                PlayerPrefs.SetFloat(Pref(chave), valores[chave]);
                AplicarGlobais();
                return;
            }
    }

    public static void Restaurar()
    {
        Carregar();
        foreach (var i in Itens) { valores[i.chave] = i.padrao; PlayerPrefs.SetFloat(Pref(i.chave), i.padrao); }
        AplicarGlobais();
    }

    public static void Salvar() { PlayerPrefs.Save(); }

    public static void AplicarGlobais()
    {
        Shader.SetGlobalFloat("_ZettaOpac", Obter("opacidade"));
        Shader.SetGlobalFloat("_ZettaBorda", Obter("borda"));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Iniciar() { carregado = false; AplicarGlobais(); }
}

// Barra deslizante holográfica: arraste a ponta do indicador sobre o trilho.
public class SliderHolo : MonoBehaviour
{
    public Action<float> aoMudar;
    public float comprimento = 0.3f;
    float min, max, valor;
    int passos;   // >0: valores discretos (0..passos)
    Transform alca;
    PainelVidro vidroAlca;
    TextMeshPro texto;
    Func<float, string> formato;
    XROrigin origem;
    float brilho;

    public float Valor => valor;

    public static SliderHolo Criar(Transform pai, string nome, Vector3 pos, float comprimento, float min, float max, float inicial,
        Func<float, string> formato, Action<float> aoMudar, int passos = 0)
    {
        var go = new GameObject("Slider_" + nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        var s = go.AddComponent<SliderHolo>();
        s.comprimento = comprimento; s.min = min; s.max = max; s.formato = formato; s.aoMudar = aoMudar; s.passos = passos;
        s.Montar();
        s.Definir(inicial, false);
        return s;
    }

    void Montar()
    {
        var trilho = HoloUtil.CriarPainel(transform, "Trilho", new Vector2(comprimento, 0.012f), TemaZetta.OrdemBotao);
        trilho.localPosition = new Vector3(0, 0, -0.004f);
        trilho.GetComponent<PainelVidro>().DefinirBorda(TemaZetta.AzulTec, 0.8f);
        var a = HoloUtil.CriarPainel(transform, "Alca", new Vector2(0.026f, 0.05f), TemaZetta.OrdemBotao + 1);
        alca = a;
        vidroAlca = a.GetComponent<PainelVidro>();
        vidroAlca.DefinirBorda(TemaZetta.Ciano, 1.4f);
        texto = HoloUtil.CriarTexto(transform, "Valor", "", new Vector3(comprimento * 0.5f + 0.08f, 0, TemaZetta.ProfundidadeTexto),
            new Vector2(0.13f, 0.04f), 0.25f, TemaZetta.Branco, TextAlignmentOptions.Center);
        texto.fontSizeMin = 0.18f;
    }

    public void Definir(float v, bool notificar = true)
    {
        v = Mathf.Clamp(v, min, max);
        if (passos > 0) v = Mathf.Round(Mathf.InverseLerp(min, max, v) * passos) / passos * (max - min) + min;
        bool mudou = !Mathf.Approximately(v, valor);
        valor = v;
        float k = Mathf.InverseLerp(min, max, valor);
        alca.localPosition = new Vector3((k - 0.5f) * comprimento, 0, -0.012f);
        texto.text = formato(valor);
        if (mudou && notificar) aoMudar?.Invoke(valor);
    }

    void Update()
    {
        var maos = BotaoHolo.MaosAtivas();
        bool tocando = false;
        if (maos != null)
        {
            if (origem == null) origem = FindAnyObjectByType<XROrigin>();
            if (origem != null)
                foreach (var mao in new[] { maos.rightHand, maos.leftHand })
                {
                    if (!mao.isTracked || !mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose)) continue;
                    var l = transform.InverseTransformPoint(origem.Origin.transform.TransformPoint(pose.position));
                    if (Mathf.Abs(l.y) > 0.035f || Mathf.Abs(l.x) > comprimento * 0.5f + 0.03f || l.z < -0.05f || l.z > 0.025f) continue;
                    tocando = true;
                    Definir(Mathf.Lerp(min, max, Mathf.InverseLerp(-comprimento * 0.5f, comprimento * 0.5f, l.x)));
                    break;
                }
        }
        brilho = Mathf.Lerp(brilho, tocando ? 1f : 0f, Time.deltaTime * 12f);
        vidroAlca.DefinirRealce(brilho);
    }
}

// Painel "Ajustes" + ferramentas de voz (abrir_ajustes, ajustar_visual).
public class AjustesHolo : MonoBehaviour
{
    const string DeclAbrir = @"{
  ""name"": ""abrir_ajustes"",
  ""description"": ""Abre ou fecha o painel de ajustes visuais com barras deslizantes (velocidade, brilho, tamanho e cor da esfera; opacidade e borda dos painéis)."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] } } }
}";
    const string DeclAjustar = @"{
  ""name"": ""ajustar_visual"",
  ""description"": ""Ajusta um item visual. 'item': velocidade, brilho, tamanho (da esfera), opacidade ou borda (dos painéis), restaurar. 'valor' é um multiplicador (1 = normal; ex.: 1.5 = 50% mais)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""item"": { ""type"": ""STRING"", ""enum"": [""velocidade"", ""brilho"", ""tamanho"", ""opacidade"", ""borda"", ""restaurar""] },
      ""valor"": { ""type"": ""NUMBER"", ""description"": ""Multiplicador; 1 = padrão."" }
    },
    ""required"": [""item""]
  }
}";
    const string Instrucao = "'abra os ajustes' → abrir_ajustes. 'esfera mais rápida/mais brilhante/maior', 'painéis mais transparentes' → ajustar_visual com um multiplicador (ex.: velocidade 1.5). 'restaurar o visual' → item=restaurar. Para trocar a cor da esfera use mudar_cor_esfera.";

    AssistenteVoz voz;
    EsferaZetta esfera;
    Transform painel;
    readonly Dictionary<string, SliderHolo> sliders = new Dictionary<string, SliderHolo>();
    SliderHolo sliderCor;
    public bool Aberto => painel != null;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        esfera = FindAnyObjectByType<EsferaZetta>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclAbrir, Abrir, Instrucao);
        voz.RegistrarFerramenta(DeclAjustar, Ajustar, "");
    }

    void OnApplicationPause(bool pausado) { if (pausado) AjustesZetta.Salvar(); }

    JObject Abrir(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "ajustes fechados" }; }
        Montar();
        return new JObject { ["result"] = "ajustes abertos; toque e arraste nas barras" };
    }

    JObject Ajustar(JObject a)
    {
        string item = (a.Value<string>("item") ?? "").ToLowerInvariant();
        if (item.StartsWith("rest")) { AjustesZetta.Restaurar(); AtualizarSliders(); AjustesZetta.Salvar(); return new JObject { ["result"] = "visual restaurado ao padrão" }; }
        float? v = a.Value<float?>("valor");
        if (v == null) return new JObject { ["erro"] = "faltou o valor" };
        foreach (var i in AjustesZetta.Itens)
            if (i.chave == item)
            {
                AjustesZetta.Definir(item, v.Value);
                AjustesZetta.Salvar();
                AtualizarSliders();
                return new JObject { ["result"] = i.nome + " agora em " + Mathf.RoundToInt(AjustesZetta.Obter(item) * 100f) + "%" };
            }
        return new JObject { ["erro"] = "item inválido; use velocidade, brilho, tamanho, opacidade, borda ou restaurar" };
    }

    void AtualizarSliders()
    {
        foreach (var kv in sliders) if (kv.Value != null) kv.Value.Definir(AjustesZetta.Obter(kv.Key), false);
    }

    public void Montar()
    {
        if (painel != null) return;
        if (esfera == null) esfera = FindAnyObjectByType<EsferaZetta>();
        var tam = new Vector2(0.72f, 0.66f);
        painel = HoloUtil.CriarPainel(transform, "Ajustes", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0.1f, 0.02f, 0.95f));
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        var t = HoloUtil.CriarTexto(painel, "Titulo", "AJUSTES DO VISUAL", new Vector3(0, tam.y * 0.5f - 0.045f, TemaZetta.ProfundidadeTexto), new Vector2(0.5f, 0.05f), 0.3f, TemaZetta.AzulTec);
        t.fontStyle = FontStyles.Bold;

        sliders.Clear();
        float y = 0.2f;
        foreach (var item in AjustesZetta.Itens)
        {
            string chave = item.chave;
            HoloUtil.CriarTexto(painel, "R_" + chave, item.nome, new Vector3(-0.33f + 0.11f, y, TemaZetta.ProfundidadeTexto), new Vector2(0.22f, 0.05f), 0.22f, TemaZetta.Branco, TextAlignmentOptions.Left).fontSizeMin = 0.18f;
            sliders[chave] = SliderHolo.Criar(painel, chave, new Vector3(0.06f, y, 0), 0.3f, item.min, item.max, AjustesZetta.Obter(chave),
                v => Mathf.RoundToInt(v * 100f) + "%", v => AjustesZetta.Definir(chave, v));
            y -= 0.075f;
        }
        // cor da esfera: valores discretos (usa a mesma troca de cor da ferramenta de voz)
        HoloUtil.CriarTexto(painel, "R_cor", "Cor da esfera", new Vector3(-0.33f + 0.11f, y, TemaZetta.ProfundidadeTexto), new Vector2(0.22f, 0.05f), 0.22f, TemaZetta.Branco, TextAlignmentOptions.Left).fontSizeMin = 0.18f;
        int corAtual = Mathf.Max(0, Array.IndexOf(AjustesZetta.Cores, PlayerPrefs.GetString("zetta_cor_esfera", "dourado")));
        sliderCor = SliderHolo.Criar(painel, "cor", new Vector3(0.06f, y, 0), 0.3f, 0f, AjustesZetta.Cores.Length - 1, corAtual,
            v => AjustesZetta.Cores[Mathf.Clamp(Mathf.RoundToInt(v), 0, AjustesZetta.Cores.Length - 1)],
            v => { if (esfera != null) esfera.MudarCor(new JObject { ["cor"] = AjustesZetta.Cores[Mathf.Clamp(Mathf.RoundToInt(v), 0, AjustesZetta.Cores.Length - 1)] }); },
            AjustesZetta.Cores.Length - 1);

        BotaoHolo.Criar(painel, "RESTAURAR", new Vector3(-0.12f, -tam.y * 0.5f + 0.05f, -0.002f), new Vector2(0.2f, 0.065f), () => { AjustesZetta.Restaurar(); AtualizarSliders(); });
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(0.12f, -tam.y * 0.5f + 0.05f, -0.002f), new Vector2(0.2f, 0.065f), Fechar);
        StartCoroutine(HoloUtil.Surgir(painel));
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    public void Fechar()
    {
        if (painel == null) return;
        AjustesZetta.Salvar();
        Destroy(painel.gameObject);
        painel = null;
        sliders.Clear();
        SonsZetta.Tocar(SonsZetta.Fechar);
    }
}
