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
        voz.RegistrarFerramenta(DeclFechar, args => { Fechar(); return new JObject { ["result"] = "central fechada" }; });
    }

    void OnDestroy() { if (Instancia == this) Instancia = null; }

    public void Alternar() { if (aberta) Fechar(); else Abrir(); }

    public void Abrir()
    {
        if (aberta || Camera.main == null) return;
        if (raiz == null) Montar();
        aberta = true;
        ultimaAtividade = Time.time;
        BotaoHolo.UltimaInteracao = Time.time;
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0f, distancia));
        raiz.position = pos;
        raiz.rotation = HoloUtil.OlharParaUsuario(pos);
        raiz.gameObject.SetActive(true);
        AtualizarTextos();
        StopAllCoroutines();
        StartCoroutine(Entrada());
    }

    public void Fechar()
    {
        if (!aberta) return;
        aberta = false;
        StopAllCoroutines();
        StartCoroutine(Saida());
    }

    // ---------- montagem (uma vez; depois só liga/desliga) ----------
    void Montar()
    {
        pass = FindAnyObjectByType<PassthroughMR>();
        gerador3d = FindAnyObjectByType<GeradorModelo3D>();
        desenho = FindAnyObjectByType<DesenhoNoAr>();

        raiz = new GameObject("CentralZetta").transform;
        raiz.SetParent(transform, false);

        var cabecalho = Painel("Cabecalho", new Vector2(0.72f, 0.1f), new Vector3(0f, 0.3f, 0f));
        var apps = Painel("Aplicativos", new Vector2(0.3f, 0.4f), new Vector3(-0.43f, 0f, 0f));
        var barra = Painel("Comandos", new Vector2(0.58f, 0.12f), new Vector3(0f, -0.26f, 0f));
        var status = Painel("Sistema", new Vector2(0.3f, 0.07f), new Vector3(-0.43f, -0.265f, 0f));
        partes = new[] { cabecalho, apps, barra, status };

        MontarCabecalho(cabecalho);
        MontarApps(apps);
        MontarBarra(barra);
        MontarStatus(status);
        raiz.gameObject.SetActive(false);
    }

    Transform Painel(string nome, Vector2 tam, Vector3 pos)
    {
        var p = HoloUtil.CriarPainel(raiz, nome, tam);
        p.localPosition = pos;
        return p;
    }

    TextMeshPro Texto(Transform pai, string nome, string conteudo, Vector3 pos, Vector2 caixa, float max, Color cor, TextAlignmentOptions al)
        => HoloUtil.CriarTexto(pai, nome, conteudo, new Vector3(pos.x, pos.y, TemaZetta.ProfundidadeTexto), caixa, max, cor, al);

    void MontarCabecalho(Transform p)
    {
        Texto(p, "Marca", "ZETTA", new Vector3(-0.2f, 0.014f, 0), new Vector2(0.24f, 0.06f), 0.4f, TemaZetta.Branco, TextAlignmentOptions.Left)
            .fontStyle = FontStyles.Bold;
        Texto(p, "Sub", "INTELIGÊNCIA ARTIFICIAL ESPACIAL", new Vector3(-0.15f, -0.026f, 0), new Vector2(0.34f, 0.024f), 0.1f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        txtHora = Texto(p, "Hora", "", new Vector3(0.055f, 0f, 0), new Vector2(0.14f, 0.07f), 0.42f, TemaZetta.Branco, TextAlignmentOptions.Right);
        txtData = Texto(p, "Data", "", new Vector3(0.2f, 0.004f, 0), new Vector2(0.12f, 0.07f), 0.13f, TemaZetta.Ciano, TextAlignmentOptions.Left);
        var ponto = HoloUtil.CriarPainel(p, "PontoConexao", new Vector2(0.012f, 0.012f));
        ponto.localPosition = new Vector3(0.292f, 0.012f, -0.001f);
        vidroPontoConexao = ponto.GetComponent<PainelVidro>();
        txtConexao = Texto(p, "Conexao", "", new Vector3(0.335f, 0.012f, 0), new Vector2(0.07f, 0.03f), 0.15f, TemaZetta.Ok, TextAlignmentOptions.Left);
    }

    void MontarApps(Transform p)
    {
        Texto(p, "Titulo", "Aplicativos", new Vector3(0.03f, 0.165f, 0), new Vector2(0.24f, 0.04f), 0.2f, TemaZetta.AzulTec, TextAlignmentOptions.Left);
        IconesZetta.Criar(p, "cubo", new Vector3(-0.118f, 0.165f, TemaZetta.ProfundidadeTexto), 0.026f, TemaZetta.Dourado);
        var tam = new Vector2(0.125f, 0.095f);
        float[] xs = { -0.0685f, 0.0685f };
        float[] ys = { 0.068f, -0.037f, -0.142f };
        // Sites, Modelos 3D, Notas, Timers, Desenhar, Informações (só funções existentes)
        Botao(p, "Sites", "internet", xs[0], ys[0], tam, () => PedirPorVoz(
            "o usuário tocou em Sites na central. Pergunte em uma frase curta que site ele quer criar, ou se quer listar os sites dele."));
        Botao(p, "Modelos 3D", "cubo", xs[1], ys[0], tam, () => PedirPorVoz(
            "o usuário tocou em Modelos 3D na central. Pergunte em uma frase curta se quer ver os modelos salvos, criar um modelo realista ou um objeto holográfico."));
        Botao(p, "Notas", "nota", xs[0], ys[1], tam, () => PedirPorVoz(
            "o usuário tocou em Notas na central. Pergunte em uma frase curta o que ele quer anotar."));
        Botao(p, "Timers", "timer", xs[1], ys[1], tam, () => PedirPorVoz(
            "o usuário tocou em Timers na central. Pergunte em uma frase curta quanto tempo e para quê."));
        var bDesenhar = Botao(p, "Desenhar", "lapis", xs[0], ys[2], tam, AlternarDesenho);
        vidroDesenhar = bDesenhar.transform.Find("Visual").GetComponent<PainelVidro>();
        Botao(p, "Informações", "info", xs[1], ys[2], tam, () => PedirPorVoz(
            "o usuário tocou em Informações na central. Mostre agora o clima e as manchetes do dia no painel e resuma em uma frase."));
    }

    BotaoHolo Botao(Transform p, string rotulo, string icone, float x, float y, Vector2 tam, Action acao)
        => BotaoHolo.Criar(p, rotulo, new Vector3(x, y, -0.004f), tam, acao, icone);

    void MontarBarra(Transform p)
    {
        BotaoHolo.Criar(p, "PARAR", new Vector3(-0.215f, 0.005f, -0.004f), new Vector2(0.12f, 0.055f), () => { if (voz != null) voz.Interromper(); });
        BotaoHolo.Criar(p, "FECHAR", new Vector3(0.215f, 0.005f, -0.004f), new Vector2(0.12f, 0.055f), () => Fechar());

        // microfone ao centro: círculo de vidro + ícone + medidor de nível
        var mic = HoloUtil.CriarPainel(p, "Mic", new Vector2(0.075f, 0.075f));
        mic.localPosition = new Vector3(0f, 0.012f, -0.004f);
        vidroMic = mic.GetComponent<PainelVidro>();
        vidroMic.material.SetFloat("_Raio", 0.0375f);   // raio = metade do lado: círculo
        IconesZetta.Criar(mic, "mic", new Vector3(0, 0, -0.002f), 0.04f, TemaZetta.Ciano);

        txtMic = Texto(p, "Estado", "Em espera", new Vector3(0f, -0.047f, 0), new Vector2(0.2f, 0.03f), 0.17f, TemaZetta.Branco, TextAlignmentOptions.Center);
        const int n = 10;
        medidor = new Transform[n];
        alturaMedidor = new float[n];
        var mat = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.8f) };
        for (int i = 0; i < n; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Nivel" + i;
            Destroy(q.GetComponent<Collider>());
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.transform.SetParent(p, false);
            float lado = i < n / 2 ? -1f : 1f;
            int k = i % (n / 2);
            q.transform.localPosition = new Vector3(lado * (0.055f + k * 0.0075f), 0.012f, -0.003f);
            q.transform.localScale = new Vector3(0.0035f, 0.004f, 1f);
            medidor[i] = q.transform;
        }
    }

    void MontarStatus(Transform p)
    {
        var ponto = HoloUtil.CriarPainel(p, "Ponto", new Vector2(0.014f, 0.014f));
        ponto.localPosition = new Vector3(-0.135f, 0.012f, -0.002f);
        vidroPontoStatus = ponto.GetComponent<PainelVidro>();
        txtStatus = Texto(p, "Status", "", new Vector3(0.01f, 0.012f, 0), new Vector2(0.26f, 0.034f), 0.17f, TemaZetta.Branco, TextAlignmentOptions.Left);
        txtDetalhe = Texto(p, "Detalhe", "", new Vector3(0.01f, -0.018f, 0), new Vector2(0.27f, 0.026f), 0.09f, TemaZetta.ComAlfa(TemaZetta.Ciano, 0.75f), TextAlignmentOptions.Left);
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

    IEnumerator FecharEm(float s) { yield return new WaitForSeconds(s); Fechar(); }

    // ---------- atualização ----------
    void Update()
    {
        if (!aberta || raiz == null) return;
        if (voz != null && voz.Falando) ultimaAtividade = Time.time;
        ultimaAtividade = Mathf.Max(ultimaAtividade, BotaoHolo.UltimaInteracao);
        if (Time.time - ultimaAtividade > segundosParaRecolher) { Fechar(); return; }

        AtualizarMedidor();
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
        if (voz != null)
        {
            if (voz.Falando) estado = "Falando...";
            else if (voz.nivelMic > 0.04f && voz.estadoMic == "ouvindo") estado = "Ouvindo...";
            else if (voz.acordada) estado = "Pronta";
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
    IEnumerator Entrada()
    {
        foreach (var p in partes) p.localScale = Vector3.zero;
        float dur = TemaZetta.TempoEntrada, passo = 0.08f;
        float total = dur + passo * (partes.Length - 1);
        for (float t = 0; t < total; t += Time.deltaTime)
        {
            for (int i = 0; i < partes.Length; i++)
            {
                float x = Mathf.Clamp01((t - i * passo) / dur);
                float e = TemaZetta.EaseOutBack(x);
                partes[i].localScale = x <= 0f ? Vector3.zero : new Vector3(e, Mathf.Clamp01(x * 2f) * e, 1f);
            }
            yield return null;
        }
        foreach (var p in partes) p.localScale = Vector3.one;
    }

    IEnumerator Saida()
    {
        float dur = TemaZetta.TempoSaida;
        var inicio = new Vector3[partes.Length];
        for (int i = 0; i < partes.Length; i++) inicio[i] = partes[i].localScale;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = 1f - t / dur;
            for (int i = 0; i < partes.Length; i++) partes[i].localScale = new Vector3(inicio[i].x, Mathf.Max(0.02f, k * k), 1f);
            yield return null;
        }
        raiz.gameObject.SetActive(false);
    }
}
