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
    PainelVidro vidro;
    TextMeshPro rotulo;
    bool apertado;
    float afund, brilho;

    static XRHandSubsystem sub;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    XROrigin origem;

    // última vez que um dedo tocou em algum botão (a central se recolhe sozinha depois de ~15 s sem isso)
    public static float UltimaInteracao;

    Transform icone;
    LineRenderer[] linhasIcone;
    float brilhoIcone = -1f;
    const float ProfundidadeIcone = -0.002f;

    public static BotaoHolo Criar(Transform pai, string texto, Vector3 posLocal, Vector2 tamanho, Action aoApertar, string icone = null)
    {
        var go = new GameObject("Botao_" + texto);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        var b = go.AddComponent<BotaoHolo>();
        b.aoApertar = aoApertar;
        b.tamanho = tamanho;
        b.Montar(texto, icone);
        return b;
    }

    void Montar(string texto, string nomeIcone)
    {
        var painel = HoloUtil.CriarPainel(transform, "Visual", tamanho);
        visual = painel;
        vidro = painel.GetComponent<PainelVidro>();
        rotulo = HoloUtil.CriarTexto(painel, "Rotulo", texto, new Vector3(0, 0, -0.002f),
            new Vector2(tamanho.x * 0.9f, tamanho.y * 0.8f), 0.2f, TemaZetta.Ciano);
        if (!string.IsNullOrEmpty(nomeIcone))
        {
            // ícone em cima, rótulo embaixo
            float lado = Mathf.Min(tamanho.x * 0.4f, tamanho.y * 0.42f);
            icone = IconesZetta.Criar(painel, nomeIcone, new Vector3(0, tamanho.y * 0.16f, ProfundidadeIcone), lado, TemaZetta.AzulTec);
            linhasIcone = icone.GetComponentsInChildren<LineRenderer>(true);
            rotulo.rectTransform.sizeDelta = new Vector2(tamanho.x * 0.94f, tamanho.y * 0.3f);
            rotulo.transform.localPosition = new Vector3(0, -tamanho.y * 0.3f, TemaZetta.ProfundidadeTexto);
        }
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

        if (dentro) UltimaInteracao = Time.time;

        // dispara ao atravessar a superfície; só rearma depois de afastar o dedo
        if (!apertado && dentro && zMax > 0.002f) { apertado = true; aoApertar?.Invoke(); }
        else if (apertado && (!dentro || zMax < -0.012f)) apertado = false;

        float alvoAfund = dentro ? Mathf.Clamp(zMax, 0f, profundidadeMax) : 0f;
        afund = Mathf.Lerp(afund, alvoAfund, Time.deltaTime * 20f);
        float alvoBrilho = apertado ? 1f : dentro ? 0.5f : 0f;
        brilho = Mathf.Lerp(brilho, alvoBrilho, Time.deltaTime * 15f);

        visual.localPosition = new Vector3(0, 0, afund);
        vidro.DefinirRealce(brilho);
        vidro.DefinirBorda(Color.Lerp(TemaZetta.AzulTec, TemaZetta.Ciano, brilho), 1f + brilho * 0.5f);
        rotulo.color = Color.Lerp(TemaZetta.Ciano, TemaZetta.Branco, brilho);
        if (linhasIcone != null && Mathf.Abs(brilho - brilhoIcone) > 0.01f)
        {
            brilhoIcone = brilho;
            var c = Color.Lerp(TemaZetta.AzulTec, TemaZetta.Branco, brilho);
            foreach (var lr in linhasIcone) lr.startColor = lr.endColor = c;
        }
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
