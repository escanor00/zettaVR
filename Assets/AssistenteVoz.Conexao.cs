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
// AssistenteVoz — conexão com a Gemini Live (WebSocket), setup, envio e tratamento das mensagens.
public partial class AssistenteVoz
{
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
        if (semInterrupcaoServidor)
            setup["realtimeInputConfig"] = new JObject
            {
                ["activityHandling"] = "NO_INTERRUPTION",
                ["automaticActivityDetection"] = new JObject { ["startOfSpeechSensitivity"] = "START_SENSITIVITY_LOW" }
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
                turnoAberto = false;
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
                turnoAberto = false;
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

    string Sanitizar(string s)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(chaveApi)) return s;
        return s.Replace(chaveApi, "***");
    }
}
