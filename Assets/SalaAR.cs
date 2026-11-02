using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Detecção de planos da sala (AR Foundation + Meta OpenXR): mesa, chão e paredes.
// Também cuida da física dos hologramas: flutuam sem gravidade; "solte" faz cair e quicar nas superfícies reais.
public class SalaAR : MonoBehaviour
{
    public bool mostrarPlanos = false;
    public float quicar = 0.55f;

    ARPlaneManager pm;
    GameObject chaoVirtual;
    PhysicsMaterial materialQuica;
    float proximaVarredura;
    bool temChaoReal;
    public bool solto { get; private set; }

    const string Decl = @"{
  ""name"": ""fisica_objetos"",
  ""description"": ""Liga ou desliga a gravidade dos hologramas criados. 'soltar' faz os objetos caírem e quicarem nas superfícies reais da sala (chão, mesa); 'flutuar' faz voltarem a flutuar."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""soltar"", ""flutuar""] }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'solte' / 'solte os objetos' use fisica_objetos com acao=soltar; para fazê-los flutuar de novo, acao=flutuar.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Controlar, Instrucao);
        materialQuica = new PhysicsMaterial("HoloQuica")
        {
            bounciness = quicar,
            dynamicFriction = 0.4f,
            staticFriction = 0.4f,
            bounceCombine = PhysicsMaterialCombine.Maximum
        };
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        pm = FindAnyObjectByType<ARPlaneManager>();
        if (pm != null)
        {
            pm.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            pm.enabled = true;
            pm.trackablesChanged.AddListener(AoMudarPlanos);
        }
        // chão virtual de reserva até aparecer um chão real
        chaoVirtual = new GameObject("ChaoVirtual");
        chaoVirtual.transform.SetParent(transform, false);
        var box = chaoVirtual.AddComponent<BoxCollider>();
        box.size = new Vector3(30f, 0.2f, 30f);
        box.sharedMaterial = materialQuica;
        chaoVirtual.transform.position = new Vector3(0f, AlturaChao() - 0.1f, 0f);
    }

    // O prefab ARPlane do template desenha o véu azul de pontos sobre a sala. Os planos só servem de física/posição:
    // forceRenderingOff vale mesmo que o template reative o renderer.
    void EsconderVisual(ARPlane p)
    {
        foreach (var r in p.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = !mostrarPlanos;
    }

    void AoMudarPlanos(ARTrackablesChangedEventArgs<ARPlane> e)
    {
        foreach (var p in e.added) EsconderVisual(p);
        foreach (var p in e.updated) EsconderVisual(p);
    }

    void OnDestroy() { if (pm != null) pm.trackablesChanged.RemoveListener(AoMudarPlanos); }

    void Update()
    {
        if (pm == null || Time.time < proximaVarredura) return;
        proximaVarredura = Time.time + 1f;
        temChaoReal = false;
        foreach (var p in pm.trackables)
        {
            if ((p.classifications & PlaneClassifications.Floor) != 0) temChaoReal = true;
            EsconderVisual(p);
            var mc = p.GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMaterial != materialQuica) mc.sharedMaterial = materialQuica;
        }
        if (chaoVirtual != null)
        {
            chaoVirtual.SetActive(!temChaoReal);
            if (!temChaoReal) chaoVirtual.transform.position = new Vector3(0f, AlturaChao() - 0.1f, 0f);
        }
        // objetos que escaparam do mundo voltam para cima do chão
        foreach (var it in HoloRegistro.Itens)
            if (it.go != null && it.go.transform.position.y < AlturaChao() - 3f)
            {
                var cam = Camera.main.transform.position;
                it.go.transform.position = new Vector3(cam.x, AlturaChao() + 0.5f, cam.z + 0.5f);
                var rb = it.go.GetComponent<Rigidbody>();
                if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
    }

    // altura do chão: plano classificado como chão, ou a origem de rastreamento (y = 0 com origem no piso)
    public float AlturaChao()
    {
        float y = 0f;
        if (pm != null)
            foreach (var p in pm.trackables)
                if ((p.classifications & PlaneClassifications.Floor) != 0) y = p.transform.position.y;
        return y;
    }

    // Procura a mesa detectada mais próxima. Se não houver classificação, usa o plano horizontal acima do chão.
    public bool TentarMesa(out Vector3 centro, out Vector2 tamanho)
    {
        centro = default;
        tamanho = default;
        if (pm == null) return false;
        var cam = Camera.main.transform.position;
        float chao = AlturaChao();
        ARPlane melhor = null;
        float melhorDist = float.MaxValue;
        foreach (var p in pm.trackables)
        {
            if (p.alignment != PlaneAlignment.HorizontalUp) continue;
            bool mesa = (p.classifications & PlaneClassifications.Table) != 0;
            float h = p.transform.position.y - chao;
            if (!mesa && (h < 0.45f || h > 1.3f)) continue;
            float d = Vector3.Distance(cam, p.transform.TransformPoint(p.center));
            if (mesa) d -= 5f; // prefere o que o sistema classificou como mesa
            if (d < melhorDist) { melhorDist = d; melhor = p; }
        }
        if (melhor == null) return false;
        centro = melhor.transform.TransformPoint(melhor.center);
        tamanho = melhor.size;
        return true;
    }

    public JObject Controlar(JObject args)
    {
        string acao = (args.Value<string>("acao") ?? "").ToLowerInvariant();
        if (acao == "soltar") { Soltar(true); return new JObject { ["result"] = "objetos soltos: vão cair e quicar" }; }
        if (acao == "flutuar") { Soltar(false); return new JObject { ["result"] = "objetos flutuando" }; }
        return new JObject { ["erro"] = "acao inválida" };
    }

    public void Soltar(bool cair)
    {
        solto = cair;
        foreach (var it in HoloRegistro.Itens)
            if (it.go != null) AplicarFisica(it.go, cair);
    }

    public void AplicarFisica(GameObject go, bool cair)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.isKinematic = false;
        rb.useGravity = cair;
        rb.linearDamping = cair ? 0.05f : 3f;
        rb.angularDamping = cair ? 0.05f : 3f;
        rb.collisionDetectionMode = cair ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
        if (!cair) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            c.sharedMaterial = materialQuica;
        rb.WakeUp();
    }
}
