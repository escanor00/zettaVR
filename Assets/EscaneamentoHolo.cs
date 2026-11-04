using System.Collections;
using UnityEngine;

// Feixe de luz que varre o que o usuário está olhando (ou a área à frente) enquanto a Zetta "olha": quatro cantos de
// mira, uma barra vertical com rastro que vai da esquerda para a direita e some. Roda antes da resposta de "o que é isso?".
public class EscaneamentoHolo : MonoBehaviour
{
    public float distancia = 0.9f;
    static Texture2D rastro;

    // dura 'dur' segundos; o chamador pode esperar com yield return
    public IEnumerator Executar(float dur = 1.5f)
    {
        var cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) yield break;

        // área: em volta do objeto olhado, se houver; senão um retângulo à frente do olhar
        Vector3 centro = cam.position + cam.forward * distancia;
        float larg = 0.5f, alt = 0.38f;
        var alvo = HoloAlvo.ObjetoOlhado(12f, false);
        if (alvo != null && alvo.go != null)
        {
            centro = HoloAlvo.CentroDoObjeto(alvo.go);
            float t = Mathf.Clamp(InteracaoMaos.MaiorDimensao(alvo.go) * 1.25f, 0.2f, 0.9f);
            larg = t; alt = t * 0.8f;
        }
        var raiz = new GameObject("EscaneamentoHolo").transform;
        raiz.SetParent(transform, false);
        raiz.position = centro;
        raiz.rotation = HoloUtil.OlharParaUsuario(centro);
        SonsZetta.Tocar(SonsZetta.Abrir);

        var mat = new Material(Shader.Find("Sprites/Default"));
        // cantos de mira
        var cantos = new GameObject("Mira").transform;
        cantos.SetParent(raiz, false);
        float hx = larg * 0.5f, hy = alt * 0.5f, b = Mathf.Min(hx, hy) * 0.35f;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            {
                var lr = new GameObject("Canto").AddComponent<LineRenderer>();
                lr.transform.SetParent(cantos, false);
                lr.useWorldSpace = false;
                lr.sharedMaterial = mat;
                lr.startColor = lr.endColor = TemaZetta.Ciano;
                lr.widthMultiplier = 0.004f;
                lr.positionCount = 3;
                lr.SetPosition(0, new Vector3(sx * hx, sy * (hy - b), 0));
                lr.SetPosition(1, new Vector3(sx * hx, sy * hy, 0));
                lr.SetPosition(2, new Vector3(sx * (hx - b), sy * hy, 0));
                lr.sortingOrder = TemaZetta.OrdemTexto;
            }

        // barra + rastro (textura com alfa em rampa)
        if (rastro == null)
        {
            rastro = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 32; i++) rastro.SetPixel(i, 0, new Color(1, 1, 1, Mathf.Pow(i / 31f, 2f)));
            rastro.Apply();
        }
        var rast = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(rast.GetComponent<Collider>());
        rast.transform.SetParent(raiz, false);
        var matRastro = new Material(mat) { mainTexture = rastro, color = TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.55f) };
        rast.GetComponent<Renderer>().sharedMaterial = matRastro;
        rast.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemBotao;
        rast.transform.localScale = new Vector3(larg * 0.35f, alt, 1f);
        var barra = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(barra.GetComponent<Collider>());
        barra.transform.SetParent(raiz, false);
        barra.GetComponent<Renderer>().sharedMaterial = new Material(mat) { color = TemaZetta.Branco };
        barra.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemTexto;
        barra.transform.localScale = new Vector3(0.006f, alt * 1.05f, 1f);

        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float x = t / dur;
            // vai e vem: ida em 0..0,7, volta curta em 0,7..1 (a mira some no final)
            float passo = x < 0.7f ? Mathf.SmoothStep(0f, 1f, x / 0.7f) : 1f;
            float px = Mathf.Lerp(-hx, hx, passo);
            barra.transform.localPosition = new Vector3(px, 0f, -0.01f);
            rast.transform.localPosition = new Vector3(px - larg * 0.175f, 0f, -0.005f);
            float fade = x < 0.8f ? 1f : 1f - (x - 0.8f) / 0.2f;
            matRastro.color = TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.55f * fade);
            barra.GetComponent<Renderer>().sharedMaterial.color = TemaZetta.ComAlfa(TemaZetta.Branco, fade);
            foreach (var lr in cantos.GetComponentsInChildren<LineRenderer>()) lr.startColor = lr.endColor = TemaZetta.ComAlfa(TemaZetta.Ciano, fade);
            yield return null;
        }
        Destroy(raiz.gameObject);
    }
}
