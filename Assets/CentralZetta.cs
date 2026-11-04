using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Central da Zetta: cabeçalho, aplicativos e barra de comandos, SOB DEMANDA.
// Abre por voz ("abra a central"), por pinça na esfera ou pelo próprio script; se recolhe sozinha após ~15 s sem interação.
// Em repouso só a esfera (e o rótulo de status dela) ficam visíveis. Só usa funções que já existem.
public class CentralZetta : MonoBehaviour
{
    public static CentralZetta Instancia;

    public float distancia = 1.1f;
    public float segundosParaRecolher = TemaZetta.RecolherCentral;

    const string DeclAbrir = @"{
  ""name"": ""abrir_central"",
  ""description"": ""Abre a central da Zetta: cabeçalho com hora e conexão, painel de aplicativos e barra de comandos, à frente do usuário."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": {} }
}";
    const string DeclFechar = @"{
  ""name"": ""fechar_central"",
  ""description"": ""Fecha a central da Zetta (cabeçalho, aplicativos e barra de comandos)."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": {} }
}";
    const string Instrucao =
        "Quando o usuário pedir para abrir a central (ou o menu, ou os aplicativos), chame abrir_central; " +
        "quando pedir para fechar a central, chame fechar_central. Responda em poucas palavras.";

    AssistenteVoz voz;
    DesenhoNoAr desenho;
    PassthroughMR pass;
    GeradorModelo3D gerador3d;

    Transform raiz;
    Transform[] partes;
    Vector3[] basePartes;
    bool aberta;
    float ultimaAtividade, proximaAtualizacao;

    TextMeshPro txtHora, txtData, txtConexao, txtStatus, txtDetalhe, txtMic;
    PainelVidro vidroPontoConexao, vidroPontoStatus, vidroMic, vidroDesenhar;
    Transform[] medidor;
    float[] alturaMedidor;

    public bool Aberta => aberta;

    void Awake()
    {
        Instancia = this;
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclAbrir, args => { Abrir(); return new JObject { ["result"] = "central aberta" }; }, Instrucao);
        voz.RegistrarFerramenta(DeclFechar, args => { Fechar("voz"); return new JObject { ["result"] = "central fechada" }; });
    }

    void OnDestroy() { if (Instancia == this) Instancia = null; }

    float ultimaAbertura = -10f, ultimoAlternar = -10f;

    // pinça na esfera: ignora repiques da detecção de pinça (1,5 s) e nunca fecha logo depois de abrir (3 s)
    public void Alternar()
    {
        if (Time.time - ultimoAlternar < 1.5f) return;
        ultimoAlternar = Time.time;
        if (aberta) { if (Time.time - ultimaAbertura > 3f) Fechar("pinça"); }
        else Abrir();
    }

    public void Abrir()
    {
        if (aberta || Camera.main == null) return;
        if (raiz == null) Montar();
        aberta = true;
        ultimaAbertura = Time.time;
        ultimaAtividade = Time.time;
        BotaoHolo.UltimaInteracao = Time.time;
        // a raiz fica na posição da cabeça (só giro em y): o arco de painéis é ao redor do usuário
        var cam = Camera.main.transform;
        raiz.position = cam.position;
        raiz.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        raiz.gameObject.SetActive(true);
        SonsZetta.Tocar(SonsZetta.Abrir);
        AtualizarTextos();
        var esfera = FindAnyObjectByType<EsferaZetta>();
        if (esfera != null) esfera.Realcar();
        StopAllCoroutines();
        StartCoroutine(Entrada());
    }

    public void Fechar(string motivo = "")
    {
        if (!aberta) return;
        Debug.Log("CentralZetta: fechada (" + motivo + ")");
        SonsZetta.Tocar(SonsZetta.Fechar);
        aberta = false;
        StopAllCoroutines();
        StartCoroutine(Saida());
    }

    // ---------- montagem (uma vez; depois só liga/desliga) ----------
    // Painéis em arco ao redor do usuário (viram para ele), em duas camadas: ~0,9 m (aplicativos e comandos) e ~1,1 m (cabeçalho e sistema).
    Transform[] brilhos;
    Vector3[] brilhosBase;
    float[] brilhosFase;
    const int NBrilhos = 26;

    void Montar()
    {
        pass = FindAnyObjectByType<PassthroughMR>();
        gerador3d = FindAnyObjectByType<GeradorModelo3D>();
        desenho = FindAnyObjectByType<DesenhoNoAr>();

        raiz = new GameObject("CentralZetta").transform;
        raiz.SetParent(transform, false);

        var cabecalho = Painel("Cabecalho", new Vector2(0.9f, 0.14f), 0f, 1.1f, 0.36f);
        var apps = Painel("Aplicativos", new Vector2(0.44f, 0.58f), -36f, 0.9f, 0.02f);
        var barra = Painel("Comandos", new Vector2(0.56f, 0.15f), -12f, 0.9f, -0.36f);
        var status = Painel("Sistema", new Vector2(0.52f, 0.13f), -42f, 1.15f, 0.52f);
        partes = new[] { cabecalho, apps, barra, status };

        MontarCabecalho(cabecalho);
        MontarApps(apps);
        MontarBarra(barra);
        MontarStatus(status);
        MontarBrilhos();
        raiz.gameObject.SetActive(false);
    }

    Transform Painel(string nome, Vector2 tam, float graus, float dist, float alt)
    {
        var p = HoloUtil.CriarPainel(raiz, nome, tam);
        var rot = Quaternion.Euler(0f, graus, 0f);
        p.localPosition = rot * new Vector3(0f, alt, dist);
        p.localRotation = rot;   // +Z para fora: a face legível olha para o usuário
        return p;
    }

    TextMeshPro Texto(Transform pai, string nome, string conteudo, Vector3 pos, Vector2 caixa, float max, Color cor, TextAlignmentOptions al)
    {
        var t = HoloUtil.CriarTexto(pai, nome, conteudo, new Vector3(pos.x, pos.y, TemaZetta.ProfundidadeTexto), caixa, max, cor, al);
        t.fontSizeMin = Mathf.Min(max, TemaZetta.TextoMinimo);
        return t;
    }

    void MontarCabecalho(Transform p)
    {
        Texto(p, "Marca", "ZETTA", new Vector3(-0.27f, 0.028f, 0), new Vector2(0.3f, 0.07f), 0.5f, TemaZetta.Branco, TextAlignmentOptions.Left)
            .fontStyle = FontStyles.Bold;
        Texto(p, "Sub", "INTELIGÊNCIA ARTIFICIAL ESPACIAL", new Vector3(-0.19f, -0.04f, 0), new Vector2(0.46f, 0.03f), 0.2f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        txtHora = Texto(p, "Hora", "", new Vector3(0.02f, 0f, 0), new Vector2(0.2f, 0.09f), 0.5f, TemaZetta.Branco, TextAlignmentOptions.Right);
        txtData = Texto(p, "Data", "", new Vector3(0.195f, 0f, 0), new Vector2(0.14f, 0.08f), 0.2f, TemaZetta.Ciano, TextAlignmentOptions.Left);
        var ponto = HoloUtil.CriarPainel(p, "PontoConexao", new Vector2(0.016f, 0.016f), TemaZetta.OrdemBotao);
        ponto.localPosition = new Vector3(0.3f, 0.01f, TemaZetta.ProfundidadeTexto);
        vidroPontoConexao = ponto.GetComponent<PainelVidro>();
        txtConexao = Texto(p, "Conexao", "", new Vector3(0.365f, 0.01f, 0), new Vector2(0.11f, 0.035f), 0.2f, TemaZetta.Ok, TextAlignmentOptions.Left);
    }

    void MontarApps(Transform p)
    {
        Texto(p, "Titulo", "Aplicativos", new Vector3(0.03f, 0.245f, 0), new Vector2(0.3f, 0.05f), 0.28f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        IconesZetta.Criar(p, "cubo", new Vector3(-0.15f, 0.245f, TemaZetta.ProfundidadeTexto), 0.04f, TemaZetta.Dourado);
        var tam = new Vector2(0.19f, 0.13f);
        float[] xs = { -0.1f, 0.1f };
        float[] ys = { 0.09f, -0.06f, -0.21f };
        // Sites, Modelos 3D, Notas, Timers, Desenhar, Informações (só funções existentes)
        Botao(p, "Sites", "internet", xs[0], ys[0], tam, () => PedirPorVoz(
            "o usuário tocou em Sites na central. Chame listar_sites, diga em uma frase quais existem e pergunte se quer criar um novo."));
        Botao(p, "Modelos 3D", "cubo", xs[1], ys[0], tam, () => PedirPorVoz(
            "o usuário tocou em Modelos 3D na central. Chame modelos_salvos para mostrar a biblioteca e pergunte em uma frase se quer criar um modelo novo."));
        Botao(p, "Notas", "nota", xs[0], ys[1], tam, () => PedirPorVoz(
            "o usuário tocou em Notas na central. Chame gerenciar_notas com acao=mostrar e leia em voz alta, em poucas palavras, o que está anotado."));
        Botao(p, "Timers", "timer", xs[1], ys[1], tam, () => PedirPorVoz(
            "o usuário tocou em Timers na central. Timers ativos agora: " + ResumoTimers() + ". Diga isso em uma frase e pergunte se quer criar um timer."));
        var bDesenhar = Botao(p, "Desenhar", "lapis", xs[0], ys[2], tam, AlternarDesenho);
        vidroDesenhar = bDesenhar.transform.Find("Visual").GetComponent<PainelVidro>();
        Botao(p, "Informações", "info", xs[1], ys[2], tam, () => PedirPorVoz(
            "o usuário tocou em Informações na central. Mostre agora o clima e as manchetes do dia no painel e resuma em uma frase."));
    }

    string ResumoTimers()
    {
        var t = FindAnyObjectByType<TimersHolo>();
        return t != null ? t.Resumo() : "nenhum timer ativo";
    }

    BotaoHolo Botao(Transform p, string rotulo, string icone, float x, float y, Vector2 tam, Action acao)
        => BotaoHolo.Criar(p, rotulo, new Vector3(x, y, TemaZetta.ProfundidadeBotao), tam, acao, icone);

    void MontarBarra(Transform p)
    {
        BotaoHolo.Criar(p, "PARAR", new Vector3(-0.2f, 0.022f, TemaZetta.ProfundidadeBotao), new Vector2(0.15f, 0.075f), () => { if (voz != null) voz.Interromper(); });
        BotaoHolo.Criar(p, "FECHAR", new Vector3(0.2f, 0.022f, TemaZetta.ProfundidadeBotao), new Vector2(0.15f, 0.075f), () => Fechar("botão FECHAR"));

        // microfone ao centro: círculo de vidro + ícone + medidor de nível
        var mic = HoloUtil.CriarPainel(p, "Mic", new Vector2(0.085f, 0.085f), TemaZetta.OrdemBotao);
        mic.localPosition = new Vector3(0f, 0.022f, TemaZetta.ProfundidadeBotao);
        vidroMic = mic.GetComponent<PainelVidro>();
        vidroMic.material.SetFloat("_Raio", 0.0425f);   // raio = metade do lado: círculo
        IconesZetta.Criar(mic, "mic", new Vector3(0, 0, -0.008f), 0.05f, TemaZetta.Ciano);

        txtMic = Texto(p, "Estado", "Em espera", new Vector3(0f, -0.05f, 0), new Vector2(0.3f, 0.035f), 0.22f, TemaZetta.Branco, TextAlignmentOptions.Center);
        const int n = 10;
        medidor = new Transform[n];
        alturaMedidor = new float[n];
        var mat = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(TemaZetta.Ciano, 0.9f) };
        for (int i = 0; i < n; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Nivel" + i;
            Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemTexto;
            q.transform.SetParent(p, false);
            float lado = i < n / 2 ? -1f : 1f;
            int k = i % (n / 2);
            q.transform.localPosition = new Vector3(lado * (0.065f + k * 0.0075f), 0.022f, TemaZetta.ProfundidadeTexto);
            q.transform.localScale = new Vector3(0.0035f, 0.004f, 1f);
            medidor[i] = q.transform;
        }
    }

    void MontarStatus(Transform p)
    {
        var ponto = HoloUtil.CriarPainel(p, "Ponto", new Vector2(0.018f, 0.018f), TemaZetta.OrdemBotao);
        ponto.localPosition = new Vector3(-0.23f, 0.02f, TemaZetta.ProfundidadeTexto);
        vidroPontoStatus = ponto.GetComponent<PainelVidro>();
        txtStatus = Texto(p, "Status", "", new Vector3(0.02f, 0.02f, 0), new Vector2(0.44f, 0.04f), 0.2f, TemaZetta.Branco, TextAlignmentOptions.Left);
        txtDetalhe = Texto(p, "Detalhe", "", new Vector3(0.02f, -0.025f, 0), new Vector2(0.46f, 0.036f), 0.18f, TemaZetta.ComAlfa(TemaZetta.Ciano, 0.9f), TextAlignmentOptions.Left);
        txtDetalhe.textWrappingMode = TextWrappingModes.NoWrap;
        txtDetalhe.overflowMode = TextOverflowModes.Ellipsis;
    }

    // partículas finas de luz flutuando entre as duas camadas de painéis
    void MontarBrilhos()
    {
        brilhos = new Transform[NBrilhos];
        brilhosBase = new Vector3[NBrilhos];
        brilhosFase = new float[NBrilhos];
        var mat = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(TemaZetta.Ciano, 0.55f) };
        var rnd = new System.Random(11);
        for (int i = 0; i < NBrilhos; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Brilho" + i;
            Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemTexto;
            q.transform.SetParent(raiz, false);
            float ang = Mathf.Lerp(-50f, 25f, (float)rnd.NextDouble());
            float dist = Mathf.Lerp(0.88f, 1.12f, (float)rnd.NextDouble());
            float alt = Mathf.Lerp(-0.42f, 0.42f, (float)rnd.NextDouble());
            brilhosBase[i] = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, alt, dist);
            brilhosFase[i] = (float)rnd.NextDouble() * 6.28f;
            q.transform.localScale = Vector3.one * Mathf.Lerp(0.003f, 0.007f, (float)rnd.NextDouble());
            brilhos[i] = q.transform;
        }
    }

    void AtualizarBrilhos()
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        for (int i = 0; i < NBrilhos; i++)
        {
            float t = Time.time * 0.25f + brilhosFase[i];
            brilhos[i].localPosition = brilhosBase[i] + new Vector3(Mathf.Sin(t * 1.3f) * 0.02f, Mathf.Repeat(t * 0.05f, 0.08f) - 0.04f, Mathf.Cos(t) * 0.02f);
            if (cam != null) brilhos[i].rotation = cam.rotation;
        }
    }

    // ---------- ações ----------
    void PedirPorVoz(string pedido)
    {
        if (voz != null) voz.Falar(pedido);
        StartCoroutine(FecharEm(0.5f));
    }

    void AlternarDesenho()
    {
        if (desenho == null) desenho = FindAnyObjectByType<DesenhoNoAr>();
        if (desenho == null) return;
        desenho.Ativar(!desenho.ativo);
        if (voz != null) voz.MostrarAviso(desenho.ativo ? "modo desenho" : "desenho encerrado", 2.5f);
        StartCoroutine(FecharEm(0.5f));
    }

    IEnumerator FecharEm(float s) { yield return new WaitForSeconds(s); Fechar("após app"); }

    // ---------- atualização ----------
    void Update()
    {
        if (!aberta || raiz == null) return;
        if (voz != null && voz.Falando) ultimaAtividade = Time.time;
        ultimaAtividade = Mathf.Max(ultimaAtividade, BotaoHolo.UltimaInteracao);
        if (Time.time - ultimaAtividade > segundosParaRecolher) { Fechar("15 s sem interação"); return; }

        AtualizarMedidor();
        AtualizarBrilhos();
        if (Time.time < proximaAtualizacao) return;
        proximaAtualizacao = Time.time + 0.25f;
        AtualizarTextos();
    }

    void AtualizarTextos()
    {
        var agora = DateTime.Now;
        txtHora.text = agora.ToString("HH:mm");
        txtData.text = agora.ToString("dd/MM/yyyy") + "\n" + agora.ToString("dddd");

        bool semRede = Application.internetReachability == NetworkReachability.NotReachable;
        txtConexao.text = semRede ? "Sem rede" : "Online";
        txtConexao.color = semRede ? TemaZetta.Erro : TemaZetta.Ok;
        vidroPontoConexao.DefinirBorda(semRede ? TemaZetta.Erro : TemaZetta.Ok, 1.4f);
        vidroPontoConexao.DefinirRealce(1f);

        bool erro = voz != null && !string.IsNullOrEmpty(voz.ultimoErro);
        string resumo;
        if (voz == null) resumo = "assistente indisponível";
        else if (erro) resumo = voz.ultimoErro;
        else if (voz.estadoMic == "microfone negado") resumo = "microfone negado";
        else resumo = "pronto para ajudar";
        txtStatus.text = "Sistema: " + resumo;
        var corStatus = erro || semRede ? TemaZetta.Erro : TemaZetta.Ok;
        vidroPontoStatus.DefinirBorda(corStatus, 1.4f);
        vidroPontoStatus.DefinirRealce(1f);

        string det = voz != null ? "Gemini: " + voz.status + " · Mic: " + voz.estadoMic : "";
        if (pass != null && !string.IsNullOrEmpty(pass.estado)) det += " · " + pass.estado;
        if (gerador3d != null && !string.IsNullOrEmpty(gerador3d.ultimaFalha)) det += " · 3D: " + gerador3d.ultimaFalha;
        txtDetalhe.text = det;

        string estado = "Em espera";
        switch (EstadoZetta.Atual)
        {
            case EstadoVoz.Falando: estado = "Falando..."; break;
            case EstadoVoz.Ouvindo: estado = "Ouvindo..."; break;
            case EstadoVoz.Pensando: estado = "Pensando..."; break;
            case EstadoVoz.Erro: estado = "Erro"; break;
            default: if (voz != null && voz.acordada) estado = "Pronta"; break;
        }
        txtMic.text = estado;
        vidroMic.DefinirBorda(estado == "Ouvindo..." ? TemaZetta.Ciano : TemaZetta.AzulTec, estado == "Em espera" ? 0.8f : 1.3f);
        vidroDesenhar.DefinirRealce(desenho != null && desenho.ativo ? 0.7f : 0f);
    }

    void AtualizarMedidor()
    {
        float nivel = voz != null ? (voz.Falando ? Mathf.Max(voz.volumeSaida, 0.2f) : voz.nivelMic) : 0f;
        int n = medidor.Length / 2;
        for (int i = 0; i < medidor.Length; i++)
        {
            int k = i % n;
            float alvo = 0.004f + nivel * (0.05f - k * 0.007f) * (0.75f + 0.25f * Mathf.Sin(Time.time * 14f + i * 1.7f));
            alturaMedidor[i] = Mathf.Lerp(alturaMedidor[i], alvo, Time.deltaTime * 14f);
            var s = medidor[i].localScale; s.y = alturaMedidor[i]; medidor[i].localScale = s;
        }
    }

    // ---------- animação ----------
    // entrada de trás para frente: cada painel vem ~0,35 m de trás, crescendo, e assenta na posição do arco
    IEnumerator Entrada()
    {
        basePartes = new Vector3[partes.Length];
        for (int i = 0; i < partes.Length; i++) { basePartes[i] = partes[i].localPosition; partes[i].localScale = Vector3.zero; }
        float dur = TemaZetta.TempoEntrada, passo = 0.08f;
        float total = dur + passo * (partes.Length - 1);
        for (float t = 0; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < partes.Length; i++)
            {
                float x = Mathf.Clamp01((t - i * passo) / dur);
                float e = TemaZetta.EaseOutBack(x);
                partes[i].localPosition = basePartes[i] + basePartes[i].normalized * (0.35f * (1f - Mathf.Min(1f, e)));
                float k = Mathf.Lerp(0.7f, 1f, Mathf.Min(1f, e));
                partes[i].localScale = x <= 0f ? Vector3.zero : new Vector3(k, k * Mathf.Clamp01(x * 2f), 1f);
            }
            yield return null;
        }
        for (int i = 0; i < partes.Length; i++) { partes[i].localPosition = basePartes[i]; partes[i].localScale = Vector3.one; }
    }

    IEnumerator Saida()
    {
        float dur = TemaZetta.TempoSaida;
        var inicio = new Vector3[partes.Length];
        for (int i = 0; i < partes.Length; i++) inicio[i] = partes[i].localScale;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = 1f - t / dur;
            for (int i = 0; i < partes.Length; i++)
            {
                partes[i].localScale = new Vector3(inicio[i].x, Mathf.Max(0.02f, k * k), 1f);
                partes[i].localPosition = basePartes[i] + basePartes[i].normalized * (0.2f * (1f - k));
            }
            yield return null;
        }
        raiz.gameObject.SetActive(false);
    }
}
