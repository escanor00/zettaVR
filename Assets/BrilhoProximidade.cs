using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;

// Leve brilho quando a mão se aproxima de qualquer parte do holograma (ou enquanto ele é segurado).
// Clareia a cor dos materiais do objeto e aumenta um pouco a opacidade; respeita mudanças de cor feitas por outros scripts.
public class BrilhoProximidade : MonoBehaviour
{
    public float distanciaInicio = 0.10f;   // começa a brilhar a 10 cm da superfície
    public float distanciaCheio = 0.02f;    // brilho máximo a 2 cm
    public float forca = 0.55f;

    class Entrada { public Material m; public Color baseC, aplicada; }

    // posições das pontas dos dedos (mundo), calculadas uma vez por quadro para todos os objetos
    static readonly List<Vector3> pontas = new List<Vector3>();
    static int quadro = -1;
    static XRHandSubsystem sub;
    static XROrigin origem;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    static readonly XRHandJointID[] dedos = { XRHandJointID.IndexTip, XRHandJointID.ThumbTip };

    static void AtualizarPontas()
    {
        if (quadro == Time.frameCount) return;
        quadro = Time.frameCount;
        pontas.Clear();
        if (sub == null || !sub.running)
        {
            SubsystemManager.GetSubsystems(subs);
            sub = subs.Count > 0 ? subs[0] : null;
            if (sub == null || !sub.running) return;
        }
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        if (origem == null) return;
        foreach (var mao in new[] { sub.rightHand, sub.leftHand })
        {
            if (!mao.isTracked) continue;
            foreach (var d in dedos)
                if (mao.GetJoint(d).TryGetPose(out var p)) pontas.Add(origem.Origin.transform.TransformPoint(p.position));
        }
    }

    readonly List<Entrada> entradas = new List<Entrada>();
    Collider[] colisores;
    XRGrabInteractable grab;
    float brilho;
    float proximaBusca;

    void Start() { Capturar(); }

    void Capturar()
    {
        entradas.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_Color")) continue;
            if (entradas.Exists(e => e.m == m)) continue;
            entradas.Add(new Entrada { m = m, baseC = m.color, aplicada = m.color });
        }
        colisores = GetComponentsInChildren<Collider>();
        grab = GetComponent<XRGrabInteractable>();
    }

    void Update()
    {
        if (Time.time >= proximaBusca) { proximaBusca = Time.time + 2f; Capturar(); }   // conteúdo que carrega depois

        float alvo = 0f;
        if (grab != null && grab.isSelected) alvo = 1f;
        else
        {
            AtualizarPontas();
            float menor = float.MaxValue;
            foreach (var p in pontas)
                foreach (var c in colisores)
                {
                    if (c == null || !c.enabled) continue;
                    // ClosestPoint só vale para colisores convexos (caixa, esfera, cápsula, malha convexa)
                    if (c is MeshCollider mc && !mc.convex) continue;
                    float d = Vector3.Distance(c.ClosestPoint(p), p);
                    if (d < menor) menor = d;
                }
            if (menor < float.MaxValue) alvo = Mathf.Clamp01(Mathf.InverseLerp(distanciaInicio, distanciaCheio, menor));
        }

        brilho = Mathf.MoveTowards(brilho, alvo, Time.deltaTime * 6f);
        if (brilho <= 0.001f && alvo == 0f && !AlgumAplicado()) return;
        foreach (var e in entradas)
        {
            if (e.m == null) continue;
            if (e.m.color != e.aplicada) e.baseC = e.m.color;   // alguém mudou a cor: essa é a nova base
            var c = Color.Lerp(e.baseC, Color.white, brilho * forca);
            c.a = Mathf.Min(1f, e.baseC.a + brilho * 0.3f);
            e.m.color = c;
            e.aplicada = e.m.color;
        }
    }

    bool AlgumAplicado()
    {
        foreach (var e in entradas) if (e.m != null && e.m.color != e.baseC) return true;
        return false;
    }
}
