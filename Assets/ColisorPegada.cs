using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Colisor de caixa do tamanho exato do que se vê (bounds dos renderers, incluindo filhos), para objetos compostos
// (modelos prontos e .glb). Fica no espaço local da raiz, então acompanha qualquer mudança de escala; o conteúdo
// que ainda monta ou carrega é remedido de tempos em tempos. Deve ser adicionado ANTES do XRGrabInteractable.
public class ColisorPegada : MonoBehaviour
{
    const float EspessuraMin = 0.03f;   // peças finas continuam fáceis de pegar

    BoxCollider caixa;
    XRGrabInteractable grab;
    float proxima;

    void Awake()
    {
        caixa = gameObject.AddComponent<BoxCollider>();
        Atualizar();
    }

    void Update()
    {
        if (Time.time < proxima) return;
        proxima = Time.time + 0.5f;
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (grab != null && grab.isSelected) return;
        Atualizar();
    }

    public void Atualizar()
    {
        if (caixa == null) return;
        bool achou = false;
        Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!InteracaoMaos.ContaNosLimites(r) || !r.enabled) continue;
            var w = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var canto = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var l = transform.InverseTransformPoint(canto);
                if (!achou) { b = new Bounds(l, Vector3.zero); achou = true; }
                else b.Encapsulate(l);
            }
        }
        if (!achou) return;
        var e = transform.lossyScale;
        var s = b.size;
        s.x = Mathf.Max(s.x, EspessuraMin / Mathf.Max(0.0001f, Mathf.Abs(e.x)));
        s.y = Mathf.Max(s.y, EspessuraMin / Mathf.Max(0.0001f, Mathf.Abs(e.y)));
        s.z = Mathf.Max(s.z, EspessuraMin / Mathf.Max(0.0001f, Mathf.Abs(e.z)));
        caixa.center = b.center;
        caixa.size = s;
    }
}
