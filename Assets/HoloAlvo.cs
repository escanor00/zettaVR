using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

// Descobre qual holograma o usuário está olhando (ou apontando com o indicador).
public static class HoloAlvo
{
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();

    public static HoloItem ObjetoOlhado(float maxAngulo = 20f, bool tentarApontar = true)
    {
        var cam = Camera.main;
        if (cam == null) return null;
        var item = MelhorPorRaio(cam.transform.position, cam.transform.forward, maxAngulo);
        if (item == null && tentarApontar && TentarRaioDoDedo(out var origem, out var dir))
            item = MelhorPorRaio(origem, dir, maxAngulo * 0.75f);
        return item;
    }

    static HoloItem MelhorPorRaio(Vector3 origem, Vector3 dir, float maxAngulo)
    {
        HoloItem melhor = null;
        float melhorAng = maxAngulo;
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null || !it.go.activeInHierarchy) continue;
            var centro = CentroDoObjeto(it.go);
            var para = centro - origem;
            if (para.sqrMagnitude < 0.0001f) continue;
            float ang = Vector3.Angle(dir, para);
            // objetos grandes/próximos toleram um ângulo maior
            float tamanho = Mathf.Max(0.05f, TamanhoDoObjeto(it.go));
            float folga = Mathf.Atan2(tamanho * 0.5f, para.magnitude) * Mathf.Rad2Deg;
            ang = Mathf.Max(0f, ang - folga);
            if (ang < melhorAng) { melhorAng = ang; melhor = it; }
        }
        return melhor;
    }

    public static Vector3 CentroDoObjeto(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        var b = new Bounds(rs[0].bounds.center, Vector3.zero);
        foreach (var r in rs) if (!(r is LineRenderer)) b.Encapsulate(r.bounds);
        return b.center;
    }

    static float TamanhoDoObjeto(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0.1f;
        var b = new Bounds(rs[0].bounds.center, Vector3.zero);
        foreach (var r in rs) if (!(r is LineRenderer)) b.Encapsulate(r.bounds);
        return b.size.magnitude * 0.5f;
    }

    // raio saindo do dedo indicador da mão direita (ou esquerda)
    static bool TentarRaioDoDedo(out Vector3 origem, out Vector3 dir)
    {
        origem = default; dir = default;
        SubsystemManager.GetSubsystems(subs);
        if (subs.Count == 0 || !subs[0].running) return false;
        var xr = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (xr == null) return false;
        foreach (var mao in new[] { subs[0].rightHand, subs[0].leftHand })
        {
            if (!mao.isTracked) continue;
            if (!mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var ponta)) continue;
            if (!mao.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var base_)) continue;
            var t = xr.Origin.transform;
            origem = t.TransformPoint(ponta.position);
            dir = (origem - t.TransformPoint(base_.position)).normalized;
            return true;
        }
        return false;
    }
}
