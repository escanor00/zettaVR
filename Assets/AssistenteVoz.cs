using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Assistente de voz em tempo real via Gemini Live API (WebSocket direto).
public partial class AssistenteVoz : MonoBehaviour
{
    public ZettaSegredos segredos;   // chave fica no asset Assets/Segredos (fora do git)
    string chaveApi => segredos != null ? segredos.chaveGemini : "";
    public string modelo = "gemini-3.8-live";
    const string BaseInstrucao =
        "Seu nome é Zetta (sempre com dois T's), uma assistente holográfica em realidade mista. " +
        "Personalidade: educada, espirituosa e prestativa, com humor leve e sutil. " +
        "REGRA DE LIMITES: quando o usuário pedir algo para o qual você não tem ferramenta, nunca responda só 'não posso': " +
        "diga em uma frase o que você consegue fazer de parecido com as ferramentas que tem e ofereça fazer isso. " +
        "Responda sempre em português do Brasil, em frases curtas e naturais (no máximo duas frases, salvo se pedirem mais). " +
        "REGRA DE ATIVAÇÃO: você se chama Zetta e só responde quando o usuário te chama pelo nome no começo da fala. " +
        "O nome costuma ser transcrito errado: pode soar como \"Zeta\", \"Zé\", \"Zê\", \"Z\" ou \"Seta\"; trate qualquer um desses como um chamado. " +
        "Se a fala não começar com um chamado assim, fique em completo silêncio: " +
        "não responda, não comente, não use ferramentas e não faça perguntas. Ruídos e conversas ao fundo devem ser ignorados. " +
        "Depois que você responde, a conversa continua por alguns segundos e o usuário pode seguir sem repetir o nome. ";
    [TextArea(2, 6)]
    public string instrucaoExtra = "";
    // true quando o usuário disse "Zetta" e ela está atendendo; false em espera
    public bool acordada;
    public float tempoAtivaSegundos = 8f;   // modo conversa: depois de responder, sem precisar do nome
    float ultimaAtividade;
    string entradaTurno = "";
    public string UltimaFraseUsuario => entradaTurno;
    bool novoTurnoEntrada = true;
    static readonly System.Text.RegularExpressions.Regex RegexAtivacao = new System.Text.RegularExpressions.Regex(
        @"^[\s\p{P}]*(z[eéê]+\s?t+[aáà]+|j[eéê]+\s?t+[aáà]+|zeda|seta|zeca|cheta|geta)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    // chamado pelo nome: primeira palavra da fala, sem pontuação/acentos/maiúsculas, inteira e isolada
    static readonly System.Collections.Generic.HashSet<string> Chamados = new System.Collections.Generic.HashSet<string>
        { "zetta", "zeta", "seta", "zera", "z", "ze", "zeda", "zeca", "jeta", "jetta", "cheta", "geta" };

    static bool EhChamado(string fala)
    {
        if (string.IsNullOrEmpty(fala)) return false;
        if (RegexAtivacao.IsMatch(fala)) return true;
        var sb = new System.Text.StringBuilder();
        bool comecou = false;
        foreach (char c in fala.Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetter(c)) { sb.Append(char.ToLowerInvariant(c)); comecou = true; }
            else if (comecou) break;
        }
        return Chamados.Contains(sb.ToString());
    }

    // dica curta no rótulo de status embaixo do anel
    string avisoTexto;
    [HideInInspector] public Color corFala = new Color(0.2f, 0.9f, 1f);   // a esfera define: texto da fala e rótulo na cor dela
    float avisoAte;
    public void MostrarAviso(string t, float segundos) { avisoTexto = t; avisoAte = Time.time + segundos; }

    // pinça sobre o anel: acorda sem precisar do nome
    public void Acordar()
    {
        acordada = true;
        ultimaAtividade = Time.time;
        MostrarAviso("pode falar", 2f);
    }

    bool audioNoTurno;
    public float distancia = 1.2f;
    public float offsetAbaixo = 0.4f;
    public int tentativasReconexao = 3;

    // 0..1, volume suavizado da voz do assistente (usado pelo núcleo)
    [Range(0f, 1f)] public float volumeSaida;
    public string status = "Desconectado";
    // "aguardando permissão", "microfone negado", "iniciando microfone" ou "ouvindo"
    // A busca do Google no Live foi recusada por cota no plano grátis (testado); ligue se tiver cota.
    public bool usarBuscaGoogle = false;
    public string estadoMic = "aguardando permissão";
    [Range(0f, 1f)] public float nivelMic;   // volume que o microfone está captando agora
    public string ultimoOuvido = "";         // última transcrição da sua fala devolvida pela Gemini
    public string ultimoErro = "";
    bool processando, setupOk;
    [Tooltip("Sem cancelamento de eco do Android, o mic fica mudo enquanto a Zetta fala (interrompe com pinça longa).")]
    public bool silenciarMicEnquantoFala = true;
    bool falando, descartarSaida;
    float tempoFala;
    // O Unity não tem cancelamento de eco: a voz da Zetta entra no microfone e a Gemini a toma por interrupção (frases cortadas).
    // Defesa dupla: (1) o servidor não aceita interrupção por voz (activityHandling=NO_INTERRUPTION; só a pinça/PARAR interrompe);
    // (2) o microfone fica fechado durante todo o turno de resposta, não só enquanto há áudio no buffer.
    [Tooltip("Envia realtimeInputConfig.activityHandling=NO_INTERRUPTION. Desligado sozinho se o servidor recusar a configuração 2 vezes seguidas.")]
    public bool semInterrupcaoServidor = true;
    int falhasSemSetup;
    bool turnoAberto;
    float ultimoAudioEm;
    // turno do servidor: abre no primeiro áudio/ferramenta, fecha em turnComplete/interrupted. Mantém o mic fechado em pausas longas (ferramenta lenta).
    bool turnoServidorAberto;
    float ultimaMsgModelo;
    string motivoFim = "terminou";
    string falaPendente;
    bool falavaAntes;
    float inicioFala;
    int underruns;
    TextMeshPro textoStatus;
    RectTransform alvoStatus;
    BotaoHolo botaoParar;
    string statusMostrado;
    public bool Falando => falando;
    public bool Processando => processando;

    // Para a fala atual: limpa o áudio já recebido e ignora o resto do turno.
    public void Interromper(string quem = "usuário")
    {
        if (!falando && !processando) return;
        motivoFim = "interrompido (" + quem + ")";
        LimparReproducao();
        turnoAberto = false;
        // só descarta o resto do turno se o servidor ainda está nele; senão a próxima resposta inteira seria jogada fora
        descartarSaida = turnoServidorAberto;
        processando = false;
        novoTurno = true;
    }
    int posVista = -1;
    float ultimoAvancoMic, inicioProcessando;
    bool microfonePermitido;

    // Chamado na thread principal; devolve o objeto "response" da ferramenta.
    public Func<string, JObject, JObject> aoChamarFuncao;
    // Declarações de ferramentas (functionDeclarations) registradas por outros componentes.
    public readonly JArray declaracoesFuncoes = new JArray();
    readonly System.Collections.Generic.Dictionary<string, Func<JObject, JObject>> ferramentas =
        new System.Collections.Generic.Dictionary<string, Func<JObject, JObject>>();

    // Instruções extras registradas por outros componentes (somadas à instrução base).
    public readonly System.Collections.Generic.List<string> instrucoesRuntime = new System.Collections.Generic.List<string>();

    const int TaxaEntrada = 16000;
    const int TaxaSaida = 24000;

    ClientWebSocket ws;
    CancellationTokenSource cts;
    readonly SemaphoreSlim travaEnvio = new SemaphoreSlim(1, 1);
    readonly ConcurrentQueue<string> recebidas = new ConcurrentQueue<string>();
    volatile bool pronto;
    volatile bool fechou;

    // microfone
    AudioClip clipMic;
    int posMic;
    readonly System.Collections.Generic.List<float> bufferMic = new System.Collections.Generic.List<float>();

    // reprodução (buffer circular lido pelo callback de áudio)
    AudioSource fonte;
    readonly float[] anel = new float[TaxaSaida * 30];
    int leitura, escrita, contagem;
    readonly object trava = new object();
    volatile float rmsAtual;

    // texto
    TextMeshPro texto;
    AnelHolografico anelRef;
    const float LarguraTexto = 0.5f;
    Transform alvoTexto;
    bool novoTurno = true;

    IEnumerator Start()
    {
        CriarTexto();
        if (GetComponent<InterrupcaoPinca>() == null) gameObject.AddComponent<InterrupcaoPinca>();
        fonte = gameObject.AddComponent<AudioSource>();
        fonte.clip = AudioClip.Create("voz", TaxaSaida, 1, TaxaSaida, true, LerPcm);
        fonte.loop = true;
        fonte.spatialBlend = 0f;
        fonte.Play();

        // 1) pede a permissão e espera a resposta; só depois inicia o microfone
        yield return PedirPermissaoMicrofone();
        PedirOutrasPermissoes();
        if (!microfonePermitido)
        {
            estadoMic = "microfone negado";
            status = estadoMic;
            texto.text = "Microfone negado.\nAtive em Configurações > Apps > Permissões.";
#if UNITY_ANDROID && !UNITY_EDITOR
            // se o usuário liberar nas configurações, segue sozinho
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                yield return new WaitForSeconds(1f);
            microfonePermitido = true;
#else
            yield break;
#endif
        }
        if (Microphone.devices.Length == 0)
        {
            estadoMic = "sem microfone";
            status = estadoMic;
            texto.text = "Nenhum microfone encontrado.";
            yield break;
        }
        estadoMic = "iniciando microfone";
        clipMic = Microphone.Start(null, true, 10, TaxaEntrada);
        ultimoAvancoMic = Time.realtimeSinceStartup;
        Debug.Log("AssistenteVoz: microfone iniciado (dispositivos=" + Microphone.devices.Length + ", taxa=" + (clipMic != null ? clipMic.frequency : 0) + ")");
        while (Microphone.GetPosition(null) <= 0) yield return null;

        // dá tempo para outros componentes registrarem ferramentas
        yield return null;
        yield return null;
        // nunca desiste: o app pode ter ficado em segundo plano (menu do sistema) e a rede cai nessa hora
        for (int i = 0; ; i++)
        {
            yield return ConectarEOuvir();
            if (setupOk) { i = 0; setupOk = false; falhasSemSetup = 0; }
            else if (semInterrupcaoServidor && ++falhasSemSetup >= 2 && Application.internetReachability != NetworkReachability.NotReachable
                     && (ultimoErro ?? "").ToLowerInvariant().Contains("activity")) // só desliga se o servidor reclamou DESSE campo (queda de rede não conta)
            {
                // duas sessões seguidas nem chegaram ao setupComplete: pode ser o campo novo; volta ao modo só com mic mudo
                semInterrupcaoServidor = false;
                Debug.LogWarning("AssistenteVoz: sessão não iniciou com NO_INTERRUPTION; desligando essa configuração.");
            }
            status = "Reconectando";
            texto.text = "Reconectando..." + (string.IsNullOrEmpty(ultimoErro) ? "" : "\n" + ultimoErro);
            yield return new WaitForSeconds(Mathf.Min(3f + i, 10f));
        }
    }

    void Update()
    {
        SeguirOlhar();
        while (recebidas.TryDequeue(out var msg)) TratarMensagem(msg);
        volumeSaida = Mathf.Lerp(volumeSaida, Mathf.Clamp01(rmsAtual * 4f), Time.deltaTime * 15f);
        if (volumeSaida > 0.02f) { ultimaAtividade = Time.time; acordada = true; }
        if (acordada && Time.time - ultimaAtividade > tempoAtivaSegundos) acordada = false;
        falando = contagem > 0 || (turnoAberto && Time.time - ultimoAudioEm < 3f);
        if (falando) tempoFala = Time.time;
        RegistrarFimDeFala();
        if (falaPendente != null && !falando && !turnoServidorAberto && pronto) { var f = falaPendente; falaPendente = null; Falar(f); }
        if (clipMic != null)
        {
            VigiarMicrofone();
            CapturarMicrofone();   // mede o nível sempre; só envia à Gemini quando conectado
            if (processando && Time.time - inicioProcessando > 20f) processando = false;
            if (!pronto) estadoMic = "conectando";
            else if (Time.realtimeSinceStartup - ultimoAvancoMic > 1f) estadoMic = "microfone parado";
            else estadoMic = falando ? "falando" : processando ? "processando" : "ouvindo";
        }
        AtualizarStatusVisivel();
    }

    float pausadoEm;

    // Um log por fala: duração e como acabou (terminou, interrompido pela pinça/PARAR/voz, servidor, erro/conexão).
    void RegistrarFimDeFala()
    {
        if (falando && !falavaAntes) { inicioFala = Time.time; underruns = 0; motivoFim = "terminou"; }
        if (!falando && falavaAntes)
        {
            Debug.Log("AssistenteVoz: fim de fala (" + motivoFim + "), " + (Time.time - inicioFala).ToString("0.0") + " s, falhas de buffer=" + underruns);
            motivoFim = "terminou";
        }
        falavaAntes = falando;
    }

    void OnDestroy()
    {
        try { cts?.Cancel(); } catch { }
        try { ws?.Dispose(); } catch { }
        if (Microphone.IsRecording(null)) Microphone.End(null);
    }
}
