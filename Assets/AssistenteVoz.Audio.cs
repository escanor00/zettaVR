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
// AssistenteVoz — áudio: permissões e captura do microfone e reprodução da voz da Zetta.
public partial class AssistenteVoz
{
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
        bool silenciado = silenciarMicEnquantoFala && (falando || Time.time - tempoFala < 0.9f);
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
        turnoAberto = true;
        ultimoAudioEm = Time.time;
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
}
