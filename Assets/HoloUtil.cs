using System.Collections;
using TMPro;
using UnityEngine;

// Helpers de visual holográfico compartilhados (painéis, texto, animações).
public static class HoloUtil
{
    // as cores vêm do tema (TemaZetta); estes nomes ficam para os scripts que já os usam
    public static readonly Color Ciano = TemaZetta.AzulTec;

    static Material matSprite;
    public static Material NovoMaterialSprite(Color cor)
    {
        if (matSprite == null) matSprite = new Material(Shader.Find("Sprites/Default"));
        var m = new Material(matSprite);
        m.color = cor;
        return m;
    }

    public static TextMeshPro CriarTexto(Transform pai, string nome, string conteudo, Vector3 posLocal,
        Vector2 caixa, float tamanhoMax, Color cor, TextAlignmentOptions alinhamento = TextAlignmentOptions.Center)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = caixa;
        t.alignment = alinhamento;
        t.color = cor;
        if (TemaZetta.Fonte != null) t.font = TemaZetta.Fonte;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.03f;
        t.fontSizeMax = tamanhoMax;
        t.text = conteudo;
        return t;
    }

    // Painel de vidro holográfico (translucidez + borda + gradiente, tudo no shader). Origem no centro, olhando para +Z.
    public static Transform CriarPainel(Transform pai, string nome, Vector2 tamanho)
    {
        var raiz = new GameObject(nome).transform;
        raiz.SetParent(pai, false);

        var fundo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fundo.name = "Fundo";
        Object.Destroy(fundo.GetComponent<Collider>());
        fundo.transform.SetParent(raiz, false);
        fundo.transform.localScale = new Vector3(tamanho.x, tamanho.y, 1f);
        var rend = fundo.GetComponent<Renderer>();
        rend.material = TemaZetta.NovoVidro(tamanho);
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var vidro = raiz.gameObject.AddComponent<PainelVidro>();
        vidro.material = rend.material;
        vidro.tamanho = tamanho;
        return raiz;
    }

    // Ponto à frente da câmera, em coordenadas locais da vista (x direita, y cima, z frente), com yaw apenas.
    public static Vector3 PontoNaVista(Vector3 local)
    {
        var cam = Camera.main.transform;
        return cam.position + Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * local;
    }

    public static Quaternion OlharParaUsuario(Vector3 pos)
    {
        var cam = Camera.main.transform;
        var d = Vector3.ProjectOnPlane(pos - cam.position, Vector3.up);
        if (d.sqrMagnitude < 0.0001f) d = cam.forward;
        return Quaternion.LookRotation(d, Vector3.up);
    }

    // multiplica o alfa de todos os renderers e textos sob 'raiz'
    public static void DefinirAlfa(Transform raiz, float a)
    {
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r is TextMeshPro || r.GetComponent<TextMeshPro>() != null) continue;
            var m = r.material;
            if (m.HasProperty("_Color")) { var c = m.color; if (!baseAlfa.ContainsKey(m)) baseAlfa[m] = c.a; c.a = baseAlfa[m] * a; m.color = c; }
        }
        foreach (var t in raiz.GetComponentsInChildren<TextMeshPro>(true))
        {
            var c = t.color; c.a = a; t.color = c;
        }
    }
    static readonly System.Collections.Generic.Dictionary<Material, float> baseAlfa = new System.Collections.Generic.Dictionary<Material, float>();

    public static IEnumerator Surgir(Transform alvo, float atraso = 0f, float duracao = TemaZetta.TempoEntrada)
    {
        alvo.localScale = Vector3.zero;
        if (atraso > 0f) yield return new WaitForSeconds(atraso);
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = t / duracao;
            float e = TemaZetta.EaseOutBack(x);
            alvo.localScale = new Vector3(e, Mathf.Clamp01(x * 2f) * e, 1f);
            yield return null;
        }
        alvo.localScale = Vector3.one;
    }
}
