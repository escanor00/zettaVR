using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// "Zetta, coloque no meu braço": prende o holograma olhado à mão/pulso (hand tracking); "solte do braço" devolve.
public class CorpoHolo : MonoBehaviour
{
    // posição relativa à palma (x lado, y para fora das costas da mão, z para os dedos)
    public Vector3 deslocamento = new Vector3(0f, 0.11f, -0.03f);
    public float tamanhoMaximoNaMao = 0.12f;

    const string Decl = @"{
  ""name"": ""prender_no_corpo"",
  ""description"": ""Prende o holograma que o usuário está olhando (ou apontando) ao pulso/mão dele, seguindo o movimento da mão, ou o devolve ao lugar de origem."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""prender"", ""soltar""] },
      ""mao"": { ""type"": ""STRING"", ""enum"": [""esquerda"", ""direita""], ""description"": ""Mão/pulso (padrão: esquerda)."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "'Coloque no meu braço' / 'no meu pulso' → prender_no_corpo acao=prender (mao esquerda por padrão); 'solte do braço' → acao=soltar.";

    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Controlar, Instrucao);
    }

    public JObject Controlar(JObject args)
    {
        string acao = (args.Value<string>("acao") ?? "").ToLowerInvariant();
        bool direita = (args.Value<string>("mao") ?? "esquerda").ToLowerInvariant().StartsWith("d");
        if (acao == "soltar") return Soltar();
        if (acao != "prender") return new JObject { ["erro"] = "acao inválida" };

        SubsystemManager.GetSubsystems(subs);
        if (subs.Count == 0 || !subs[0].running)
            return new JObject { ["erro"] = "rastreamento de mãos indisponível" };

        var alvo = HoloAlvo.ObjetoOlhado();
        if (alvo == null) return new JObject { ["erro"] = "não encontrei nenhum objeto que o usuário esteja olhando" };
        Prender(alvo.go, direita ? Handedness.Right : Handedness.Left);
        return new JObject { ["result"] = "'" + alvo.descricao + "' preso à mão " + (direita ? "direita" : "esquerda") };
    }

    public void Prender(GameObject go, Handedness lado)
    {
        var s = go.GetComponent<SeguirMao>();
        if (s == null) s = go.AddComponent<SeguirMao>();
        s.Configurar(lado, deslocamento, tamanhoMaximoNaMao);
    }

    JObject Soltar()
    {
        int n = 0;
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null) continue;
            var s = it.go.GetComponent<SeguirMao>();
            if (s == null) continue;
            s.Devolver();
            n++;
        }
        return n > 0 ? new JObject { ["result"] = n + " objeto(s) devolvido(s) ao lugar" }
                     : new JObject { ["erro"] = "nenhum objeto está preso ao braço" };
    }
}

// Acompanha a palma da mão a cada frame; ao devolver, volta voando para a pose original.
public class SeguirMao : MonoBehaviour
{
    Handedness lado;
    Vector3 desloc;
    float tamMax;
    Vector3 posOriginal, escalaOriginal;
    Quaternion rotOriginal;
    bool guardado, devolvendo;
    bool eraKinematic, usavaGravidade;
    XRGrabInteractable grab;
    Rigidbody rb;
    XROrigin origem;
    XRHandSubsystem sub;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();

    public void Configurar(Handedness l, Vector3 deslocamento, float tamanhoMaximo)
    {
        lado = l; desloc = deslocamento; tamMax = tamanhoMaximo;
        if (!guardado)
        {
            guardado = true;
            posOriginal = transform.position;
            rotOriginal = transform.rotation;
            escalaOriginal = transform.localScale;
            rb = GetComponent<Rigidbody>();
            if (rb != null) { eraKinematic = rb.isKinematic; usavaGravidade = rb.useGravity; rb.isKinematic = true; rb.useGravity = false; }
            grab = GetComponent<XRGrabInteractable>();
            if (grab != null) grab.enabled = false; // não competir com a mão
            // se for grande, encolhe para caber no pulso
            float tam = Mathf.Max(escalaOriginal.x, escalaOriginal.y, escalaOriginal.z);
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = new Bounds(rs[0].bounds.center, Vector3.zero);
                foreach (var r in rs) if (!(r is LineRenderer)) b.Encapsulate(r.bounds);
                float maior = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (maior > tamMax) transform.localScale = escalaOriginal * (tamMax / maior);
            }
        }
        devolvendo = false;
    }

    void LateUpdate()
    {
        if (devolvendo || !guardado) return;
        if (sub == null || !sub.running)
        {
            SubsystemManager.GetSubsystems(subs);
            sub = subs.Count > 0 ? subs[0] : null;
            if (sub == null) return;
        }
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        var mao = lado == Handedness.Right ? sub.rightHand : sub.leftHand;
        if (origem == null || !mao.isTracked) return;
        if (!mao.GetJoint(XRHandJointID.Palm).TryGetPose(out var pose)) return;

        var o = origem.Origin.transform;
        var posMundo = o.TransformPoint(pose.position);
        var rotMundo = o.rotation * pose.rotation;
        transform.position = Vector3.Lerp(transform.position, posMundo + rotMundo * desloc, Time.deltaTime * 20f);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotMundo, Time.deltaTime * 15f);
    }

    public void Devolver()
    {
        if (devolvendo) return;
        devolvendo = true;
        StartCoroutine(Voltar());
    }

    IEnumerator Voltar()
    {
        var p0 = transform.position; var r0 = transform.rotation; var e0 = transform.localScale;
        const float dur = 0.6f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float x = 1f - Mathf.Pow(1f - t / dur, 3f);
            transform.position = Vector3.Lerp(p0, posOriginal, x);
            transform.rotation = Quaternion.Slerp(r0, rotOriginal, x);
            transform.localScale = Vector3.Lerp(e0, escalaOriginal, x);
            yield return null;
        }
        transform.SetPositionAndRotation(posOriginal, rotOriginal);
        transform.localScale = escalaOriginal;
        if (rb != null) { rb.isKinematic = eraKinematic; rb.useGravity = usavaGravidade; }
        if (grab != null) grab.enabled = true;
        Destroy(this);
    }
}
