using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

// "Zetta, mostre desempenho": painel de vidro no canto inferior esquerdo com FPS, tempo de quadro (médio e pior dos últimos
// 2 s), memória e quantidade de hologramas. A borda muda de cor conforme a taxa (verde ≥ 66, dourado ≥ 45, vermelho abaixo).
public class DesempenhoHolo : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""mostrar_desempenho"",
  ""description"": ""Mostra ou esconde o painel de desempenho com FPS, tempo de quadro, memória e número de hologramas."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] } } }
}";
    const string Instrucao = "'mostre desempenho', 'quantos FPS', 'está lento?' → mostrar_desempenho acao=abrir; 'esconda o desempenho' → acao=fechar.";

    AssistenteVoz voz;
    Transform painel;
    PainelVidro vidro;
    TextMeshPro linhas;
    readonly float[] janela = new float[120];
    int pos, preenchidas;
    float proximaAtualizacao;
    public bool Aberto => painel != null;

    // para o relatório de desempenho: últimas leituras
    public static float UltimoFps, UltimoMs, UltimoPiorMs;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "painel de desempenho fechado" }; }
        Abrir();
        return new JObject { ["result"] = "painel de desempenho aberto no canto inferior esquerdo" };
    }

    public void Abrir()
    {
        if (painel != null) return;
        var tam = new Vector2(0.34f, 0.2f);
        painel = HoloUtil.CriarPainel(transform, "Desempenho", tam);
        vidro = painel.GetComponent<PainelVidro>();
        var titulo = HoloUtil.CriarTexto(painel, "Titulo", "DESEMPENHO", new Vector3(0, 0.075f, TemaZetta.ProfundidadeTexto), new Vector2(0.3f, 0.035f), 0.22f, TemaZetta.AzulTec);
        titulo.fontStyle = FontStyles.Bold;
        linhas = HoloUtil.CriarTexto(painel, "Linhas", "...", new Vector3(0, -0.012f, TemaZetta.ProfundidadeTexto), new Vector2(0.3f, 0.12f), 0.2f, TemaZetta.Branco);
        linhas.alignment = TextAlignmentOptions.TopLeft;
        pos = 0; preenchidas = 0;
        Posicionar(true);
        StartCoroutine(HoloUtil.Surgir(painel));
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    public void Fechar()
    {
        if (painel == null) return;
        Destroy(painel.gameObject);
        painel = null;
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    void Posicionar(bool imediato)
    {
        if (painel == null || Camera.main == null) return;
        var alvo = HoloUtil.PontoNaVista(new Vector3(-0.32f, -0.2f, 1.0f));
        painel.position = imediato ? alvo : Vector3.Lerp(painel.position, alvo, Time.deltaTime * 2f);
        painel.rotation = HoloUtil.OlharParaUsuario(painel.position);
    }

    void Update()
    {
        // amostra sempre (barato) para que o painel já abra com histórico
        janela[pos] = Time.unscaledDeltaTime;
        pos = (pos + 1) % janela.Length;
        if (preenchidas < janela.Length) preenchidas++;

        Medir();
        if (painel == null) return;
        Posicionar(false);
        if (Time.unscaledTime < proximaAtualizacao) return;
        proximaAtualizacao = Time.unscaledTime + 0.25f;

        float memMb = Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
        float gcMb = System.GC.GetTotalMemory(false) / 1048576f;
        linhas.text =
            $"FPS  <b>{UltimoFps:0}</b>\n" +
            $"Quadro  <b>{UltimoMs:0.0} ms</b>  (pior {UltimoPiorMs:0.0})\n" +
            $"Memória  <b>{memMb:0} MB</b>  (C# {gcMb:0})\n" +
            $"Hologramas  <b>{HoloRegistro.Itens.Count}</b>";
        var cor = UltimoFps >= 66f ? TemaZetta.Ok : UltimoFps >= 45f ? TemaZetta.Dourado : TemaZetta.Erro;
        vidro.DefinirBorda(cor, 1.3f);
    }

    static float ultimaMedida = -1f;

    void Medir()
    {
        if (preenchidas == 0 || Time.unscaledTime - ultimaMedida < 0.25f) return;
        ultimaMedida = Time.unscaledTime;
        float soma = 0f, pior = 0f;
        for (int i = 0; i < preenchidas; i++) { soma += janela[i]; if (janela[i] > pior) pior = janela[i]; }
        float media = soma / preenchidas;
        UltimoMs = media * 1000f;
        UltimoPiorMs = pior * 1000f;
        UltimoFps = media > 0f ? 1f / media : 0f;
    }
}
