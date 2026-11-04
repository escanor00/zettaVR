using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// Gestos globais com as duas mãos (sem competir com o grab):
//  - abrir as duas mãos (palmas para a frente, afastando-se rápido) afasta os objetos soltos ~0,6 m;
//  - pinça dupla longa (as duas mãos em pinça por ~1 s) recolhe tudo para perto (~0,5 m à frente).
public class GestosDuplos : MonoBehaviour
{
    public float segundosPincaDupla = 1f;
    public float distanciaPinca = 0.025f;
    public float velocidadeAbrir = 0.9f;   // m/s de afastamento entre as mãos
    public float afastamento = 0.6f;
    public float distanciaRecolher = 0.5f;

    XRHandSubsystem sub;
    XROrigin origem;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    float desdePinca = -1f, ultimoGesto = -10f, distAnterior = -1f;

    void Update()
    {
        if (sub == null || !sub.running)
        {
            SubsystemManager.GetSubsystems(subs);
            sub = subs.Count > 0 ? subs[0] : null;
            if (sub == null || !sub.running) return;
        }
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        if (origem == null || Camera.main == null) return;
        var d = sub.rightHand; var e = sub.leftHand;
        if (!d.isTracked || !e.isTracked) { desdePinca = -1f; distAnterior = -1f; return; }

        // pinça dupla longa
        if (Pinca(d) && Pinca(e))
        {
            if (desdePinca < 0f) desdePinca = Time.time;
            else if (Time.time - desdePinca >= segundosPincaDupla && Time.time - ultimoGesto > 3f)
            {
                ultimoGesto = Time.time; desdePinca = -1f;
                Recolher();
            }
            return;
        }
        desdePinca = -1f;

        // abrir as duas mãos: palmas distantes se afastando depressa
        if (!Ponto(d, XRHandJointID.Palm, out var pd) || !Ponto(e, XRHandJointID.Palm, out var pe)) return;
        float dist = Vector3.Distance(pd, pe);
        if (distAnterior > 0f)
        {
            float vel = (dist - distAnterior) / Mathf.Max(0.001f, Time.deltaTime);
            if (vel > velocidadeAbrir && dist > 0.35f && Time.time - ultimoGesto > 2f && !(MaoFechada(d) || MaoFechada(e)))
            {
                ultimoGesto = Time.time;
                Afastar();
            }
        }
        distAnterior = dist;
    }

    bool Pinca(XRHand m)
    {
        return Ponto(m, XRHandJointID.ThumbTip, out var a) && Ponto(m, XRHandJointID.IndexTip, out var b) && Vector3.Distance(a, b) < distanciaPinca;
    }

    bool MaoFechada(XRHand m)
    {
        // pontas dos dedos perto da palma = punho
        return Ponto(m, XRHandJointID.Palm, out var p) && Ponto(m, XRHandJointID.MiddleTip, out var t) && Vector3.Distance(p, t) < 0.06f;
    }

    bool Ponto(XRHand m, XRHandJointID id, out Vector3 mundo)
    {
        mundo = default;
        if (!m.GetJoint(id).TryGetPose(out var pose)) return false;
        mundo = origem.Origin.transform.TransformPoint(pose.position);
        return true;
    }

    // empurra para longe (a partir da cabeça) todos os hologramas que não estão sendo segurados
    void Afastar()
    {
        var cam = Camera.main.transform;
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null || !it.go.activeInHierarchy || Segurado(it.go)) continue;
            var dir = it.go.transform.position - cam.position;
            dir.y *= 0.2f;
            if (dir.sqrMagnitude < 0.01f) dir = cam.forward;
            StartCoroutine(Mover(it.go.transform, it.go.transform.position + dir.normalized * afastamento));
        }
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    // traz tudo para perto, num leque à frente
    void Recolher()
    {
        var cam = Camera.main.transform;
        var itens = new List<HoloItem>();
        foreach (var it in HoloRegistro.Itens) if (it.go != null && it.go.activeInHierarchy && !Segurado(it.go)) itens.Add(it);
        int n = itens.Count;
        for (int i = 0; i < n; i++)
        {
            float ang = n == 1 ? 0f : Mathf.Lerp(-35f, 35f, i / (float)(n - 1));
            var dir = Quaternion.Euler(0f, cam.eulerAngles.y + ang, 0f) * Vector3.forward;
            var alvo = cam.position + dir * distanciaRecolher + Vector3.down * 0.15f;
            StartCoroutine(Mover(itens[i].go.transform, alvo));
        }
        if (n > 0) SonsZetta.Tocar(SonsZetta.Abrir);
    }

    static bool Segurado(GameObject go)
    {
        var g = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        return g != null && g.isSelected;
    }

    IEnumerator Mover(Transform t, Vector3 destino)
    {
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        var ini = t.position;
        for (float x = 0; x < 0.6f; x += Time.deltaTime)
        {
            if (t == null) yield break;
            float k = Mathf.SmoothStep(0f, 1f, x / 0.6f);
            t.position = Vector3.Lerp(ini, destino, k);
            if (rb != null) rb.position = t.position;
            yield return null;
        }
    }
}
