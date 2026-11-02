using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

public class TesteGemini : MonoBehaviour
{
    public ZettaSegredos segredos;
    string chaveApi => segredos != null ? segredos.chaveGemini : "";
    public string modelo = "gemini-3.6-flash";
    public string pergunta = "Apresente-se em uma frase curta, como um assistente futurista, em português.";

    public int tentativasMax = 3;
    public float esperaSegundos = 3f;
    public string[] modelosAlternativos = { "gemini-3.5-flash-lite" };
    public float distancia = 1.2f;
    public float offsetAbaixo = 0.4f;

    TextMeshPro texto;
    Transform alvoTexto;
    long ultimoCodigo;

    // Formatos da mensagem que o Gemini entende
    [System.Serializable] public class Parte { public string text; }
    [System.Serializable] public class Conteudo { public Parte[] parts; }
    [System.Serializable] public class Pedido { public Conteudo[] contents; }
    [System.Serializable] public class Candidato { public Conteudo content; }
    [System.Serializable] public class Resposta { public Candidato[] candidates; }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        // Cria o texto holográfico abaixo do anel
        var cam = Camera.main.transform;
        var obj = new GameObject("TextoResposta");
        var anel = FindAnyObjectByType<AnelHolografico>();
        if (anel != null) distancia = anel.distancia;
        obj.transform.position = AlvoTexto(cam);
        obj.transform.rotation = Quaternion.LookRotation(obj.transform.position - cam.position);
        alvoTexto = obj.transform;

        texto = obj.AddComponent<TextMeshPro>();
        texto.fontSize = 0.3f;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(0.2f, 0.9f, 1f);
        texto.rectTransform.sizeDelta = new Vector2(0.9f, 0.4f);
        // quebra de linha e ajuste automático para caber inteiro
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.overflowMode = TextOverflowModes.Truncate;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 0.05f;
        texto.fontSizeMax = 0.3f;
        texto.text = "Conectando ao Gemini...";

        yield return Perguntar();
    }

    Vector3 AlvoTexto(Transform cam)
    {
        // logo abaixo do anel
        return cam.position + cam.forward * distancia - cam.up * offsetAbaixo;
    }

    void Update()
    {
        if (alvoTexto == null || Camera.main == null) return;
        // segue o olhar suavemente, igual ao anel
        var cam = Camera.main.transform;
        alvoTexto.position = Vector3.Lerp(alvoTexto.position, AlvoTexto(cam), Time.deltaTime * 2f);
        alvoTexto.rotation = Quaternion.Slerp(alvoTexto.rotation,
            Quaternion.LookRotation(alvoTexto.position - cam.position), Time.deltaTime * 4f);
    }

    IEnumerator Perguntar()
    {
        // Modelo principal: até 3 tentativas em caso de 503, com 3 s entre elas
        for (int tentativa = 1; tentativa <= tentativasMax; tentativa++)
        {
            texto.text = "Modelo: " + modelo + "\nTentativa " + tentativa + "/" + tentativasMax;
            yield return Enviar(modelo);
            if (ultimoCodigo != 503) yield break;
            if (tentativa < tentativasMax) yield return new WaitForSeconds(esperaSegundos);
        }

        // Continua em 503: tenta os modelos alternativos (uma tentativa cada)
        foreach (var alt in modelosAlternativos)
        {
            if (alt == modelo) continue;
            texto.text = "503 em " + modelo + "\nTentando modelo: " + alt;
            yield return Enviar(alt);
            if (ultimoCodigo != 503) yield break;
        }

        texto.text = "Erro 503: todos os modelos estão indisponíveis.";
    }

    IEnumerator Enviar(string modeloUsado)
    {
        var pedido = new Pedido
        {
            contents = new[] { new Conteudo { parts = new[] { new Parte { text = pergunta } } } }
        };
        string json = JsonUtility.ToJson(pedido);
        string url = "https://generativelanguage.googleapis.com/v1beta/models/" + modeloUsado + ":generateContent";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("x-goog-api-key", chaveApi);

            yield return req.SendWebRequest();
            ultimoCodigo = req.responseCode;

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (ultimoCodigo != 503)
                    texto.text = "Erro: " + req.error + "\n" + req.downloadHandler.text;
                yield break;
            }

            var r = JsonUtility.FromJson<Resposta>(req.downloadHandler.text);
            string resposta = "";
            if (r.candidates != null && r.candidates.Length > 0)
                foreach (var p in r.candidates[0].content.parts)
                    resposta += p.text;

            texto.text = resposta != "" ? resposta : "Sem resposta.";
        }
    }
}