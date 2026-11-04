using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

// Interação com as mãos para todos os hologramas, num único sistema (XR Interaction Toolkit + XR Hands do template):
// pegar de perto ou à distância (Near-Far), duas mãos para escalar e girar (5 cm a 2 m) e arremesso com física.
public static class InteracaoMaos
{
    public const float TamanhoMin = 0.05f;
    public const float TamanhoMax = 2f;

    public static void Aplicar(GameObject go)
    {
        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null) return;

        grab.selectMode = InteractableSelectMode.Multiple;   // permite as duas mãos juntas
        grab.throwOnDetach = true;                            // arremesso: usa a velocidade da mão ao soltar
        grab.throwVelocityScale = 1f;
        grab.throwAngularVelocityScale = 0.5f;
        grab.throwSmoothingDuration = 0.2f;

        // pegada dinâmica: o objeto mantém o ponto e o ângulo onde os dedos tocaram (não pula para o centro da mão)
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = true;
        grab.reinitializeDynamicAttachEverySingleGrab = true;

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearDamping = 1.2f; rb.angularDamping = 1.5f; }  // voa e vai parando no ar

        if (go.GetComponent<BrilhoProximidade>() == null) go.AddComponent<BrilhoProximidade>();

        var tr = go.GetComponent<XRGeneralGrabTransformer>();
        if (tr == null)
        {
            tr = go.AddComponent<XRGeneralGrabTransformer>();
            grab.startingSingleGrabTransformers.Add(tr);
            grab.startingMultipleGrabTransformers.Add(tr);
        }
        tr.allowTwoHandedScaling = true;
        tr.allowTwoHandedRotation = XRGeneralGrabTransformer.TwoHandedRotationMode.FirstHandDirectedTowardsSecondHand;
        tr.clampScaling = false;   // o limite absoluto é garantido pelo LimiteEscala (vale mesmo após vários gestos)

        var lim = go.GetComponent<LimiteEscala>();
        if (lim == null) lim = go.AddComponent<LimiteEscala>();
        lim.Medir();
    }

    // rótulos, marcadores e painéis de dados (ExcluirDosLimites) não contam como corpo do objeto
    public static bool ContaNosLimites(Renderer r)
        => !(r is LineRenderer) && !(r is TMPro.TextMeshPro) && r.GetComponentInParent<ExcluirDosLimites>() == null;

    // caixa do objeto no mundo (renderers visíveis; se não houver, colisores)
    public static bool Limites(GameObject go, out Bounds limites)
    {
        Bounds? b = null;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!ContaNosLimites(r)) continue;
            if (b == null) b = r.bounds; else { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
        }
        if (b == null)
        {
            var c = go.GetComponentInChildren<Collider>();
            if (c != null) b = c.bounds;
        }
        limites = b ?? new Bounds(go.transform.position, Vector3.one * 0.3f);
        return b != null;
    }

    // maior dimensão do objeto no mundo
    public static float MaiorDimensao(GameObject go)
    {
        if (!Limites(go, out var b)) return 0.3f;
        var s = b.size;
        return Mathf.Max(0.001f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
    }
}

// Mantém o tamanho do holograma entre 5 cm e 2 m enquanto ele é segurado (escala com as duas mãos).
public class LimiteEscala : MonoBehaviour
{
    float dimensaoPorEscala = 1f;   // maior dimensão para escala local = 1
    XRGrabInteractable grab;
    float proximaMedida;

    // o conteúdo pode montar/carregar depois da criação: remede de vez em quando enquanto ninguém segura
    void Update()
    {
        if (grab == null || grab.isSelected || Time.time < proximaMedida) return;
        proximaMedida = Time.time + 1.5f;
        Medir();
    }

    public void Medir()
    {
        grab = GetComponent<XRGrabInteractable>();
        var e = transform.lossyScale;
        float maiorEscala = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(e.x), Mathf.Max(Mathf.Abs(e.y), Mathf.Abs(e.z))));
        dimensaoPorEscala = InteracaoMaos.MaiorDimensao(gameObject) / maiorEscala;
    }

    void LateUpdate()
    {
        if (grab == null || !grab.isSelected) return;
        var e = transform.lossyScale;
        float maiorEscala = Mathf.Max(Mathf.Abs(e.x), Mathf.Max(Mathf.Abs(e.y), Mathf.Abs(e.z)));
        float dim = dimensaoPorEscala * maiorEscala;
        float alvo = Mathf.Clamp(dim, InteracaoMaos.TamanhoMin, InteracaoMaos.TamanhoMax);
        if (Mathf.Abs(alvo - dim) > 0.0005f) transform.localScale *= alvo / dim;
    }
}

// Marca um filho (rótulos, marcadores, painel de dados) para não entrar nas caixas de pegada, tamanho e centro do objeto.
public class ExcluirDosLimites : MonoBehaviour { }
