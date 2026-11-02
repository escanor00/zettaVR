using System;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// Botão holográfico tipo "poke": afunda e brilha quando a ponta do indicador encosta nele e dispara ao atravessar a superfície.
// Detecta a ponta do dedo direto pelo XR Hands (o mesmo sistema do restante dos hologramas), sem competir com o XRI.
// A face do botão olha para -Z (como os textos do projeto); o dedo chega vindo de -Z.
public class BotaoHolo : MonoBehaviour
{
    public Action aoApertar;
    public float profundidadeMax = 0.012f;

    Vector2 tamanho;
    Transform visual;
    Material matFundo, matContorno;
    TextMeshPro rotulo;
    bool apertado;
    float afund, brilho;

    static XRHandSubsystem sub;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    XROrigin origem;

    public static BotaoHolo Criar(Transform pai, string texto, Vector3 posLocal, Vector2 tamanho, Action aoApertar)
    {
        var go = new GameObject("Botao_" + texto);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        var b = go.AddComponent<BotaoHolo>();
        b.aoApertar = aoApertar;
        b.tamanho = tamanho;
        b.Montar(texto);
        return b;
    }

    void Montar(string texto)
    {
        var painel = HoloUtil.CriarPainel(transform, "Visual", tamanho);
        visual = painel;
        var fundo = painel.Find("Fundo").GetComponent<Renderer>();
        matFundo = fundo.material;
        matFundo.color = new Color(0.1f, 0.6f, 0.7f, 0.22f);
        matContorno = painel.Find("Contorno").GetComponent<LineRenderer>().material;
        rotulo = HoloUtil.CriarTexto(painel, "Rotulo", texto, new Vector3(0, 0, -0.002f),
            new Vector2(tamanho.x * 0.9f, tamanho.y * 0.8f), 0.15f, HoloUtil.Ciano);
    }

    void Update()
    {
        if (visual == null) return;
        bool dentro = false;
        float zMax = -1f;
        if (ObterSubsistema())
        {
            if (origem == null) origem = FindAnyObjectByType<XROrigin>();
            if (origem != null)
            {
                Testar(sub.rightHand, ref dentro, ref zMax);
                Testar(sub.leftHand, ref dentro, ref zMax);
            }
        }

        // dispara ao atravessar a superfície; só rearma depois de afastar o dedo
        if (!apertado && dentro && zMax > 0.002f) { apertado = true; aoApertar?.Invoke(); }
        else if (apertado && (!dentro || zMax < -0.012f)) apertado = false;

        float alvoAfund = dentro ? Mathf.Clamp(zMax, 0f, profundidadeMax) : 0f;
        afund = Mathf.Lerp(afund, alvoAfund, Time.deltaTime * 20f);
        float alvoBrilho = apertado ? 1f : dentro ? 0.5f : 0f;
        brilho = Mathf.Lerp(brilho, alvoBrilho, Time.deltaTime * 15f);

        visual.localPosition = new Vector3(0, 0, afund);
        var f = matFundo.color; f.a = Mathf.Lerp(0.22f, 0.65f, brilho); matFundo.color = f;
        rotulo.color = Color.Lerp(HoloUtil.Ciano, Color.white, brilho);
    }

    void Testar(XRHand mao, ref bool dentro, ref float zMax)
    {
        if (!mao.isTracked) return;
        if (!mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose)) return;
        var mundo = origem.Origin.transform.TransformPoint(pose.position);
        var l = transform.InverseTransformPoint(mundo);
        if (Mathf.Abs(l.x) > tamanho.x * 0.5f + 0.01f || Mathf.Abs(l.y) > tamanho.y * 0.5f + 0.01f) return;
        if (l.z < -0.04f || l.z > 0.03f) return;
        dentro = true;
        zMax = Mathf.Max(zMax, l.z);
    }

    static bool ObterSubsistema()
    {
        if (sub != null && sub.running) return true;
        SubsystemManager.GetSubsystems(subs);
        sub = subs.Count > 0 ? subs[0] : null;
        return sub != null && sub.running;
    }
}
