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
// AssistenteVoz — rótulos de resposta e de status (texto seguindo o olhar).
public partial class AssistenteVoz
{
    // Rótulo pequeno e sempre visível embaixo do anel.
    void AtualizarStatusVisivel()
    {
        if (textoStatus == null) return;
        if (texto != null) texto.color = TemaZetta.Branco;   // resposta em #EAF6FF
        if (botaoParar != null && botaoParar.gameObject.activeSelf != falando) botaoParar.gameObject.SetActive(falando);
        string s;
        Color cor = new Color(corFala.r, corFala.g, corFala.b, 0.85f);
        if (Application.internetReachability == NetworkReachability.NotReachable) { s = "sem internet"; cor = new Color(1f, 0.6f, 0.2f); }
        else if (estadoMic == "microfone negado" || estadoMic == "sem microfone" || estadoMic == "erro no microfone")
        { s = estadoMic; cor = new Color(1f, 0.4f, 0.3f); }
        else if (!pronto && !string.IsNullOrEmpty(ultimoErro))
        { s = "erro: " + (ultimoErro.Length > 34 ? ultimoErro.Substring(0, 34) : ultimoErro); cor = new Color(1f, 0.4f, 0.3f); }
        else if (Time.time < avisoAte) { s = avisoTexto; cor = new Color(1f, 0.9f, 0.3f, 0.95f); }
        else s = estadoMic;
        textoStatus.color = cor;
        if (s == statusMostrado) return;
        statusMostrado = s;
        textoStatus.text = s;
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
        if (TemaZetta.Fonte != null) texto.font = TemaZetta.Fonte;
        // AddComponent troca o Transform por RectTransform: guarda a referência nova
        alvoTexto = texto.rectTransform;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(0.2f, 0.9f, 1f);
        texto.rectTransform.sizeDelta = new Vector2(LarguraTexto, 0.24f);
        texto.alignment = TextAlignmentOptions.MidlineRight;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Truncate;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 0.18f;
        texto.fontSizeMax = 0.6f;
        texto.text = "Iniciando...";

        // rótulo de status pequeno embaixo do anel
        var existenteStatus = GameObject.Find("TextoStatus");
        if (existenteStatus != null) Destroy(existenteStatus);
        var objStatus = new GameObject("TextoStatus");
        objStatus.transform.position = AlvoStatus(cam);
        objStatus.transform.rotation = Quaternion.LookRotation(objStatus.transform.position - cam.position);
        textoStatus = objStatus.AddComponent<TextMeshPro>();
        if (TemaZetta.Fonte != null) textoStatus.font = TemaZetta.Fonte;
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
            // acima da esfera, alinhado à direita (nunca sobre ela nem sobre a central)
            x = anelRef.deslocamento.x + 0.2f - LarguraTexto * 0.5f;
            y = anelRef.deslocamento.y + 0.33f;
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
}
