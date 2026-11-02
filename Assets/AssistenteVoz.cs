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
public class AssistenteVoz : MonoBehaviour
{
    public ZettaSegredos segredos;   // chave fica no asset Assets/Segredos (fora do git)
    string chaveApi => segredos != null ? segredos.chaveGemini : "";
    public string modelo = "gemini-3.8-live";
    const string BaseInstrucao =
        "Seu nome é Zetta (sempre com dois T's), uma assistente holográfica em realidade mista. " +
        "Personalidade: educada, espirituosa e prestativa, com humor leve e sutil. " +
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
    TextMeshPro textoStatus;
    RectTransform alvoStatus;
    BotaoHolo botaoParar;
    string statusMostrado;
    public bool Falando => falando;

    // Para a fala atual: limpa o áudio já recebido e ignora o resto do turno.
    public void Interromper()
    {
        if (!falando && !processando) return;
        Debug.Log("AssistenteVoz: fala interrompida pelo usuário.");
        LimparReproducao();
        descartarSaida = true;
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

    // Registra uma ferramenta (function calling): declaração JSON + tratador chamado na thread principal.
    public void RegistrarFerramenta(string declaracaoJson, Func<JObject, JObject> tratador, string instrucao = null)
    {
        var decl = JObject.Parse(declaracaoJson);
        declaracoesFuncoes.Add(decl);
        ferramentas[decl.Value<string>("name")] = tratador;
        if (!string.IsNullOrEmpty(instrucao)) instrucoesRuntime.Add(instrucao);
    }

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
    const float LarguraTexto = 0.42f;
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
            if (setupOk) { i = 0; setupOk = false; }
            status = "Reconectando";
            texto.text = "Reconectando..." + (string.IsNullOrEmpty(ultimoErro) ? "" : "\n" + ultimoErro);
            yield return new WaitForSeconds(Mathf.Min(3f + i, 10f));
        }
    }

    // O Android corta o microfone quando o app perde o foco (menu do sistema, dois toques no óculos) e
    // o Unity continua com um clip parado: sem este reinício a Zetta nunca mais ouviria.
    void VigiarMicrofone()
    {
        if (clipMic == null) return;
        int p = Microphone.GetPosition(null);
        if (p != posVista)
        {
            posVista = p;
            ultimoAvancoMic = Time.realtimeSinceStartup;
            return;
        }
        if (Time.realtimeSinceStartup - ultimoAvancoMic < 2f) return;
        Debug.LogWarning("AssistenteVoz: microfone parado; reiniciando.");
        try { Microphone.End(null); } catch { }
        clipMic = Microphone.Start(null, true, 10, TaxaEntrada);
        posMic = 0;
        posVista = -1;
        bufferMic.Clear();
        ultimoAvancoMic = Time.realtimeSinceStartup;
        if (clipMic == null) { estadoMic = "erro no microfone"; ultimoErro = "microfone não reiniciou"; }
    }

    // Pede RECORD_AUDIO em tempo de execução e espera a resposta do usuário.
    IEnumerator PedirPermissaoMicrofone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            microfonePermitido = true;
            yield break;
        }

        estadoMic = "aguardando permissão";
        status = estadoMic;
        texto.text = "Aguardando permissão do microfone...";

        // Um pedido enquanto outro diálogo está aberto (ex.: PermissionsManager) é recusado na hora:
        // nesse caso espera um pouco e pede de novo.
        for (int tentativa = 0; tentativa < 4 && !microfonePermitido; tentativa++)
        {
            bool respondeu = false, naoPerguntarMais = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => { respondeu = true; microfonePermitido = true; };
            cb.PermissionDenied += _ => respondeu = true;
            cb.PermissionDeniedAndDontAskAgain += _ => { respondeu = true; naoPerguntarMais = true; };

            float inicio = Time.realtimeSinceStartup;
            Permission.RequestUserPermission(Permission.Microphone, cb);

            // o callback pode não disparar após o app perder o foco: confere também direto
            while (!respondeu && Time.realtimeSinceStartup - inicio < 120f)
            {
                if (Permission.HasUserAuthorizedPermission(Permission.Microphone)) { microfonePermitido = true; break; }
                yield return null;
            }
            if (microfonePermitido || Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                microfonePermitido = true;
                break;
            }
            if (naoPerguntarMais) break;
            bool imediata = Time.realtimeSinceStartup - inicio < 1f;
            if (!imediata) break; // o usuário respondeu "negar"
            yield return new WaitForSeconds(2f);
        }
#else
        microfonePermitido = true;
        yield break;
#endif
    }

    // Permissões do template (cena, etc.) pedidas só depois do microfone, para não competirem.
    void PedirOutrasPermissoes()
    {
        var pm = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.PermissionsManager>();
        if (pm != null) pm.ProcessPermissions();
    }

    IEnumerator ConectarEOuvir()
    {
        pronto = false;
        fechou = false;
        status = "Conectando";
        texto.text = "Conectando ao Gemini...";
        cts = new CancellationTokenSource();
        var tarefa = ExecutarSessao(cts.Token);
        while (!fechou) yield return null;
        yield return null;
        if (tarefa.IsFaulted && tarefa.Exception != null)
            Debug.LogWarning("AssistenteVoz: " + Sanitizar(tarefa.Exception.GetBaseException().Message));
    }

    async Task ExecutarSessao(CancellationToken ct)
    {
        try
        {
            ws = new ClientWebSocket();
            var uri = new Uri("wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" + chaveApi);
            await ws.ConnectAsync(uri, ct);
            await EnviarJson(MontarSetup(), ct);

            var buf = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                using (var ms = new MemoryStream())
                {
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                        if (r.MessageType == WebSocketMessageType.Close)
                        {
                            ultimoErro = "servidor fechou: " + r.CloseStatus;
                            Debug.LogWarning("AssistenteVoz: servidor fechou a conexão: " + r.CloseStatus + " - " + Sanitizar(r.CloseStatusDescription));
                            return;
                        }
                        ms.Write(buf, 0, r.Count);
                    } while (!r.EndOfMessage);
                    recebidas.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
        }
        catch (Exception e)
        {
            if (!ct.IsCancellationRequested)
            {
                var msg = Sanitizar(e.Message);
                ultimoErro = msg.Length > 70 ? msg.Substring(0, 70) : msg;
                Debug.LogWarning("AssistenteVoz: falha na sessão: " + msg);
            }
        }
        finally
        {
            pronto = false;
            fechou = true;
        }
    }

    string MontarSetup()
    {
        var setup = new JObject
        {
            ["model"] = "models/" + modelo,
            ["generationConfig"] = new JObject { ["responseModalities"] = new JArray("AUDIO") },
            ["systemInstruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = BaseInstrucao + instrucaoExtra + " " + string.Join(" ", instrucoesRuntime) }) },
            ["inputAudioTranscription"] = new JObject(),
            ["outputAudioTranscription"] = new JObject()
        };
        // busca do Google (grounding) sempre ativa + ferramentas do app
        var tools = new JArray();
        if (usarBuscaGoogle) tools.Add(new JObject { ["googleSearch"] = new JObject() });
        if (declaracoesFuncoes.Count > 0)
            tools.Add(new JObject { ["functionDeclarations"] = declaracoesFuncoes });
        setup["tools"] = tools;
        return new JObject { ["setup"] = setup }.ToString(Newtonsoft.Json.Formatting.None);
    }

    async Task EnviarJson(string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await travaEnvio.WaitAsync(ct);
        try
        {
            if (ws != null && ws.State == WebSocketState.Open)
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        finally { travaEnvio.Release(); }
    }

    async void EnviarSemEsperar(string json)
    {
        try { if (cts != null) await EnviarJson(json, cts.Token); }
        catch (Exception e) { if (cts != null && !cts.IsCancellationRequested) Debug.LogWarning("AssistenteVoz: erro ao enviar: " + Sanitizar(e.Message)); }
    }

    void Update()
    {
        SeguirOlhar();
        while (recebidas.TryDequeue(out var msg)) TratarMensagem(msg);
        volumeSaida = Mathf.Lerp(volumeSaida, Mathf.Clamp01(rmsAtual * 4f), Time.deltaTime * 15f);
        if (volumeSaida > 0.02f) { ultimaAtividade = Time.time; acordada = true; }
        if (acordada && Time.time - ultimaAtividade > tempoAtivaSegundos) acordada = false;
        falando = contagem > 0;
        if (falando) tempoFala = Time.time;
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

    // Rótulo pequeno e sempre visível embaixo do anel.
    void AtualizarStatusVisivel()
    {
        if (textoStatus == null) return;
        if (botaoParar != null && botaoParar.gameObject.activeSelf != falando) botaoParar.gameObject.SetActive(falando);
        string s;
        Color cor = new Color(0.2f, 0.9f, 1f, 0.8f);
        if (Application.internetReachability == NetworkReachability.NotReachable) { s = "sem internet"; cor = new Color(1f, 0.6f, 0.2f); }
        else if (estadoMic == "microfone negado" || estadoMic == "sem microfone" || estadoMic == "erro no microfone")
        { s = estadoMic; cor = new Color(1f, 0.4f, 0.3f); }
        else if (!pronto && !string.IsNullOrEmpty(ultimoErro))
        { s = "erro: " + (ultimoErro.Length > 34 ? ultimoErro.Substring(0, 34) : ultimoErro); cor = new Color(1f, 0.4f, 0.3f); }
        else if (Time.time < avisoAte) { s = avisoTexto; cor = new Color(1f, 0.9f, 0.3f, 0.95f); }
        else s = estadoMic;
        if (s == statusMostrado) return;
        statusMostrado = s;
        textoStatus.text = s;
        textoStatus.color = cor;
    }

    float pausadoEm;
    void OnApplicationPause(bool pausado)
    {
        Debug.Log("AssistenteVoz: app " + (pausado ? "pausado" : "retomado") + " (mic=" + estadoMic + ", sessão=" + status + ")");
        if (pausado) { pausadoEm = Time.realtimeSinceStartup; return; }
        // depois de uma pausa longa o WebSocket costuma ficar "aberto" porém morto: derruba para reconectar
        if (pausadoEm > 0f && Time.realtimeSinceStartup - pausadoEm > 15f && pronto)
        {
            Debug.LogWarning("AssistenteVoz: pausa longa; reconectando a sessão.");
            try { cts?.Cancel(); ws?.Abort(); } catch { }
        }
    }

    // Faz a Zetta falar por iniciativa do app (ex.: timer acabou). Começa com "Zetta" para passar na regra de ativação.
    public void Falar(string pedido)
    {
        if (!pronto) return;
        acordada = true;
        ultimaAtividade = Time.time;
        entradaTurno = "";
        novoTurnoEntrada = true;
        EnviarSemEsperar(new JObject { ["realtimeInput"] = new JObject { ["text"] = "Zetta, aviso do sistema: " + pedido } }
            .ToString(Newtonsoft.Json.Formatting.None));
    }

    // Envia um quadro (JPEG) para a Gemini Live; ela o usa como contexto da próxima resposta.
    public void EnviarImagem(byte[] jpeg)
    {
        if (!pronto || jpeg == null || jpeg.Length == 0) return;
        EnviarSemEsperar(new JObject
        {
            ["realtimeInput"] = new JObject { ["video"] = new JObject { ["mimeType"] = "image/jpeg", ["data"] = Convert.ToBase64String(jpeg) } }
        }.ToString(Newtonsoft.Json.Formatting.None));
    }

    void TratarMensagem(string json)
    {
        JObject m;
        try { m = JObject.Parse(json); }
        catch { return; }

        if (m["setupComplete"] != null)
        {
            pronto = true;
            setupOk = true;
            ultimoErro = "";
            status = "Conectado";
            texto.text = "Pode falar...";
            Debug.Log("AssistenteVoz: sessão Gemini Live pronta; enviando áudio do microfone.");
            return;
        }

        var sc = m["serverContent"] as JObject;
        if (sc != null)
        {
            if (sc["interrupted"] != null && (bool)sc["interrupted"])
            {
                descartarSaida = false;
                LimparReproducao();
                novoTurno = true;
            }
            var entrada = sc["inputTranscription"]?["text"]?.ToString();
            if (!string.IsNullOrEmpty(entrada))
            {
                if (novoTurnoEntrada) { entradaTurno = ""; novoTurnoEntrada = false; }
                entradaTurno += entrada;
                ultimoOuvido = entradaTurno.Length > 60 ? entradaTurno.Substring(entradaTurno.Length - 60) : entradaTurno;
                if (EhChamado(entradaTurno))
                {
                    acordada = true;
                    ultimaAtividade = Time.time;
                }
                processando = acordada;
                inicioProcessando = Time.time;
            }
            // sem a palavra de ativação, qualquer áudio/texto do modelo é descartado
            bool emEspera = descartarSaida;
            var partes = sc["modelTurn"]?["parts"] as JArray;
            if (partes != null && !emEspera)
                foreach (var p in partes)
                {
                    var dados = p["inlineData"]?["data"]?.ToString();
                    if (string.IsNullOrEmpty(dados)) continue;
                    // a Gemini só responde se ela mesma detectou o chamado: vale como ativação
                    audioNoTurno = true;
                    acordada = true;
                    ultimaAtividade = Time.time;
                    processando = false;
                    var taxa = TaxaSaida;
                    var mime = p["inlineData"]["mimeType"]?.ToString();
                    int i = mime == null ? -1 : mime.IndexOf("rate=", StringComparison.Ordinal);
                    if (i >= 0) int.TryParse(mime.Substring(i + 5), out taxa);
                    EnfileirarAudio(Convert.FromBase64String(dados), taxa);
                }
            var saida = sc["outputTranscription"]?["text"]?.ToString();
            if (!string.IsNullOrEmpty(saida) && !emEspera)
            {
                ultimaAtividade = Time.time;
                if (novoTurno) { texto.text = ""; novoTurno = false; }
                texto.text += saida;
            }
            if (sc["turnComplete"] != null && (bool)sc["turnComplete"])
            {
                if (entradaTurno.Trim().Length > 0)
                    Debug.Log("AssistenteVoz: ouvi [" + (acordada ? "Zetta reconhecida" : "sem a palavra Zetta") + "] \"" + Sanitizar(entradaTurno.Trim()) + "\"");
                if (!acordada && !audioNoTurno && entradaTurno.Trim().Length >= 4) MostrarAviso("diga 'Zetta' antes", 2f);
                audioNoTurno = false;
                processando = false;
                descartarSaida = false;
                novoTurno = true;
                novoTurnoEntrada = true;
                ultimaAtividade = Time.time;
            }
        }

        var tc = m["toolCall"]?["functionCalls"] as JArray;
        if (tc != null)
        {
            var respostas = new JArray();
            foreach (var f in tc)
            {
                var nome = f["name"]?.ToString();
                JObject resp;
                try
                {
                    resp = ferramentas.TryGetValue(nome, out var tratador) ? tratador(f["args"] as JObject ?? new JObject())
                        : aoChamarFuncao != null ? aoChamarFuncao(nome, f["args"] as JObject ?? new JObject()) : null;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("AssistenteVoz: ferramenta falhou: " + Sanitizar(e.Message));
                    resp = new JObject { ["erro"] = "deu um problema interno ao fazer isso; avise o usuário em português, em uma frase simples, sem detalhes técnicos, e sugira tentar de novo" };
                }
                respostas.Add(new JObject
                {
                    ["id"] = f["id"],
                    ["name"] = nome,
                    ["response"] = resp ?? new JObject { ["result"] = "ok" }
                });
            }
            EnviarSemEsperar(new JObject { ["toolResponse"] = new JObject { ["functionResponses"] = respostas } }
                .ToString(Newtonsoft.Json.Formatting.None));
        }
    }

    // ---------- microfone ----------
    void CapturarMicrofone()
    {
        int pos = Microphone.GetPosition(null);
        if (pos == posMic || clipMic == null) return;
        int total = clipMic.samples;
        int n = pos > posMic ? pos - posMic : total - posMic + pos;
        var tmp = new float[n];
        if (pos > posMic) clipMic.GetData(tmp, posMic);
        else
        {
            var a = new float[total - posMic];
            clipMic.GetData(a, posMic);
            Array.Copy(a, tmp, a.Length);
            if (pos > 0)
            {
                var b = new float[pos];
                clipMic.GetData(b, 0);
                Array.Copy(b, 0, tmp, a.Length, pos);
            }
        }
        posMic = pos;
        if (clipMic.frequency != TaxaEntrada) tmp = Reamostrar(tmp, clipMic.frequency, TaxaEntrada);
        float soma = 0f;
        for (int k = 0; k < tmp.Length; k++) soma += tmp[k] * tmp[k];
        float rms = tmp.Length > 0 ? Mathf.Sqrt(soma / tmp.Length) : 0f;
        nivelMic = Mathf.Lerp(nivelMic, Mathf.Clamp01(rms * 8f), 0.5f);
        bool silenciado = silenciarMicEnquantoFala && (falando || Time.time - tempoFala < 0.6f);
        if (!pronto || silenciado) { bufferMic.Clear(); return; }
        bufferMic.AddRange(tmp);

        const int bloco = TaxaEntrada / 10; // 100 ms
        while (bufferMic.Count >= bloco)
        {
            var bytes = new byte[bloco * 2];
            for (int i = 0; i < bloco; i++)
            {
                short s = (short)(Mathf.Clamp(bufferMic[i], -1f, 1f) * 32767f);
                bytes[i * 2] = (byte)(s & 0xff);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }
            bufferMic.RemoveRange(0, bloco);
            var msg = new JObject
            {
                ["realtimeInput"] = new JObject
                {
                    ["audio"] = new JObject
                    {
                        ["data"] = Convert.ToBase64String(bytes),
                        ["mimeType"] = "audio/pcm;rate=16000"
                    }
                }
            };
            EnviarSemEsperar(msg.ToString(Newtonsoft.Json.Formatting.None));
        }
    }

    // ---------- reprodução ----------
    void EnfileirarAudio(byte[] pcm, int taxa)
    {
        var amostras = new float[pcm.Length / 2];
        for (int i = 0; i < amostras.Length; i++)
            amostras[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8)) / 32768f;
        if (taxa != TaxaSaida) amostras = Reamostrar(amostras, taxa, TaxaSaida);
        lock (trava)
        {
            foreach (var a in amostras)
            {
                if (contagem >= anel.Length) break;
                anel[escrita] = a;
                escrita = (escrita + 1) % anel.Length;
                contagem++;
            }
        }
    }

    void LimparReproducao()
    {
        lock (trava) { leitura = escrita = contagem = 0; }
    }

    // roda na thread de áudio
    void LerPcm(float[] dados)
    {
        float soma = 0f;
        lock (trava)
        {
            for (int i = 0; i < dados.Length; i++)
            {
                if (contagem > 0)
                {
                    dados[i] = anel[leitura];
                    leitura = (leitura + 1) % anel.Length;
                    contagem--;
                }
                else dados[i] = 0f;
                soma += dados[i] * dados[i];
            }
        }
        rmsAtual = dados.Length > 0 ? Mathf.Sqrt(soma / dados.Length) : 0f;
    }

    static float[] Reamostrar(float[] origem, int de, int para)
    {
        int n = Mathf.Max(1, (int)((long)origem.Length * para / de));
        var saida = new float[n];
        for (int i = 0; i < n; i++)
        {
            float pos = (float)i * de / para;
            int i0 = Mathf.Min((int)pos, origem.Length - 1);
            int i1 = Mathf.Min(i0 + 1, origem.Length - 1);
            saida[i] = Mathf.Lerp(origem[i0], origem[i1], pos - i0);
        }
        return saida;
    }

    // ---------- texto ----------
    void CriarTexto()
    {
        var existente = GameObject.Find("TextoResposta");
        if (existente != null) Destroy(existente);
        anelRef = FindAnyObjectByType<AnelHolografico>();
        var cam = Camera.main.transform;
        var obj = new GameObject("TextoResposta");
        obj.transform.position = AlvoTexto(cam);
        obj.transform.rotation = Quaternion.LookRotation(obj.transform.position - cam.position);
        texto = obj.AddComponent<TextMeshPro>();
        // AddComponent troca o Transform por RectTransform: guarda a referência nova
        alvoTexto = texto.rectTransform;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(0.2f, 0.9f, 1f);
        texto.rectTransform.sizeDelta = new Vector2(LarguraTexto, 0.26f);
        texto.alignment = TextAlignmentOptions.MidlineRight;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Truncate;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 0.05f;
        texto.fontSizeMax = 0.5f;
        texto.text = "Iniciando...";

        // rótulo de status pequeno embaixo do anel
        var existenteStatus = GameObject.Find("TextoStatus");
        if (existenteStatus != null) Destroy(existenteStatus);
        var objStatus = new GameObject("TextoStatus");
        objStatus.transform.position = AlvoStatus(cam);
        objStatus.transform.rotation = Quaternion.LookRotation(objStatus.transform.position - cam.position);
        textoStatus = objStatus.AddComponent<TextMeshPro>();
        alvoStatus = textoStatus.rectTransform;
        alvoStatus.sizeDelta = new Vector2(0.4f, 0.05f);
        textoStatus.alignment = TextAlignmentOptions.Center;
        textoStatus.textWrappingMode = TextWrappingModes.NoWrap;
        textoStatus.fontSize = 0.07f;
        textoStatus.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        textoStatus.text = "";

        // botão poke PARAR: aparece só enquanto a Zetta fala (o mic fica mudo nesse momento)
        botaoParar = BotaoHolo.Criar(alvoStatus, "PARAR", new Vector3(0f, -0.06f, 0f), new Vector2(0.11f, 0.045f), Interromper);
        botaoParar.gameObject.SetActive(false);
    }

    Vector3 AlvoStatus(Transform cam)
    {
        float x = 0f, y = -0.2f, d = distancia;
        if (anelRef != null) { x = anelRef.deslocamento.x; y = anelRef.deslocamento.y - 0.11f; d = anelRef.distancia; }
        return cam.position + cam.forward * d + cam.right * x + cam.up * y;
    }

    Vector3 AlvoTexto(Transform cam)
    {
        // ao lado (esquerda) do anel, na mesma altura
        float x = 0f, y = -offsetAbaixo, d = distancia;
        if (anelRef != null)
        {
            x = anelRef.deslocamento.x - LarguraTexto * 0.5f - 0.14f;
            y = anelRef.deslocamento.y;
            d = anelRef.distancia;
        }
        return cam.position + cam.forward * d + cam.right * x + cam.up * y;
    }

    void SeguirOlhar()
    {
        if (alvoTexto == null || Camera.main == null) return;
        var cam = Camera.main.transform;
        if (alvoStatus != null)
        {
            alvoStatus.position = Vector3.Lerp(alvoStatus.position, AlvoStatus(cam), Time.deltaTime * 2f);
            alvoStatus.rotation = Quaternion.Slerp(alvoStatus.rotation,
                Quaternion.LookRotation(alvoStatus.position - cam.position), Time.deltaTime * 4f);
        }
        alvoTexto.position = Vector3.Lerp(alvoTexto.position, AlvoTexto(cam), Time.deltaTime * 2f);
        alvoTexto.rotation = Quaternion.Slerp(alvoTexto.rotation,
            Quaternion.LookRotation(alvoTexto.position - cam.position), Time.deltaTime * 4f);
    }

    string Sanitizar(string s)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(chaveApi)) return s;
        return s.Replace(chaveApi, "***");
    }

    void OnDestroy()
    {
        try { cts?.Cancel(); } catch { }
        try { ws?.Dispose(); } catch { }
        if (Microphone.IsRecording(null)) Microphone.End(null);
    }
}
