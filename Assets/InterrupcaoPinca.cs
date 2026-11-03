using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Hands;

// Enquanto a Zetta fala o microfone fica mudo (sem cancelamento de eco do Android), então a forma de
// interromper é segurar a pinça por cerca de 1 s com qualquer mão.
public class InterrupcaoPinca : MonoBehaviour
{
    public float segundos = 1f;
    public float distanciaMax = 0.025f;

    AssistenteVoz voz;
    XRHandSubsystem sub;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    float desde = -1f;

    AnelHolografico anel;
    XROrigin origem;
    bool pincaAnterior;
    public float raioAnel = 0.22f;

    void Update()
    {
        if (voz == null) voz = GetComponent<AssistenteVoz>();
        if (voz == null) return;
        if (sub == null || !sub.running)
        {
            SubsystemManager.GetSubsystems(subs);
            sub = subs.Count > 0 ? subs[0] : null;
            if (sub == null || !sub.running) return;
        }
        if (!voz.Falando) { desde = -1f; PincaNoAnel(); return; }
        if (!(Pinca(sub.rightHand) || Pinca(sub.leftHand))) { desde = -1f; return; }
        if (desde < 0f) desde = Time.time;
        else if (Time.time - desde >= segundos)
        {
            desde = -1f;
            voz.Interromper();
        }
    }

    // início de uma pinça com a mão perto do anel: acorda a Zetta sem precisar do nome
    void PincaNoAnel()
    {
        bool agora = false;
        foreach (var mao in new[] { sub.rightHand, sub.leftHand })
        {
            if (!Pinca(mao)) continue;
            if (anel == null) anel = FindAnyObjectByType<AnelHolografico>();
            if (origem == null) origem = FindAnyObjectByType<XROrigin>();
            if (anel == null || origem == null || Camera.main == null) continue;
            mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var p);
            var mundo = origem.Origin.transform.TransformPoint(p.position);
            if (Vector3.Distance(mundo, anel.AlvoPosicao(Camera.main.transform)) < raioAnel) agora = true;
        }
        if (agora && !pincaAnterior)
        {
            voz.Acordar();
            if (CentralZetta.Instancia != null) CentralZetta.Instancia.Alternar();   // pinça na esfera também abre/fecha a central
        }
        pincaAnterior = agora;
    }

    bool Pinca(XRHand mao)
    {
        if (!mao.isTracked) return false;
        if (!mao.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var a)) return false;
        if (!mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var b)) return false;
        return Vector3.Distance(a.position, b.position) < distanciaMax;
    }
}
