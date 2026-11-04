using System.Collections.Generic;
using UnityEngine;

// Ícones minimalistas de traço fino (LineRenderers), no mesmo azul dos painéis. Sem texturas nem fontes de ícones.
public static class IconesZetta
{
    static Material matLinha;

    static Material Mat()
    {
        if (matLinha == null) matLinha = new Material(Shader.Find("Sprites/Default"));
        return matLinha;
    }

    // tipos: internet, cubo, nota, timer, lapis, info, mic. Desenhado em [-0,5; 0,5] x tam, centro em 'pos' (local do pai).
    public static Transform Criar(Transform pai, string tipo, Vector3 pos, float tam, Color cor)
    {
        var raiz = new GameObject("Icone_" + tipo).transform;
        raiz.SetParent(pai, false);
        raiz.localPosition = pos;
        float larg = Mathf.Max(0.0015f, tam * 0.06f);
        void Linha(bool fecha, params Vector2[] pts) => Tracar(raiz, pts, fecha, tam, larg, cor);

        switch (tipo)
        {
            case "internet":
                Linha(true, Elipse(0f, 0f, 0.46f, 0.46f));
                Linha(true, Elipse(0f, 0f, 0.2f, 0.46f));
                Linha(false, new Vector2(-0.46f, 0f), new Vector2(0.46f, 0f));
                break;
            case "cubo":
                Linha(true, new Vector2(0, 0.48f), new Vector2(0.42f, 0.24f), new Vector2(0.42f, -0.24f), new Vector2(0, -0.48f), new Vector2(-0.42f, -0.24f), new Vector2(-0.42f, 0.24f));
                Linha(false, new Vector2(-0.42f, 0.24f), new Vector2(0, 0), new Vector2(0.42f, 0.24f));
                Linha(false, new Vector2(0, 0), new Vector2(0, -0.48f));
                break;
            case "nota":
                Linha(true, new Vector2(-0.36f, -0.46f), new Vector2(-0.36f, 0.46f), new Vector2(0.36f, 0.46f), new Vector2(0.36f, -0.46f));
                Linha(false, new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f));
                Linha(false, new Vector2(-0.2f, 0f), new Vector2(0.2f, 0f));
                Linha(false, new Vector2(-0.2f, -0.2f), new Vector2(0.06f, -0.2f));
                break;
            case "timer":
                Linha(true, Elipse(0f, -0.06f, 0.4f, 0.4f));
                Linha(false, new Vector2(-0.1f, 0.46f), new Vector2(0.1f, 0.46f));
                Linha(false, new Vector2(0f, 0.46f), new Vector2(0f, 0.34f));
                Linha(false, new Vector2(0.18f, -0.06f), new Vector2(0f, -0.06f), new Vector2(0f, 0.2f));
                break;
            case "lapis":
                Linha(true, new Vector2(-0.46f, -0.46f), new Vector2(-0.38f, -0.12f), new Vector2(0.24f, 0.46f), new Vector2(0.46f, 0.24f), new Vector2(-0.12f, -0.38f));
                Linha(false, new Vector2(0.1f, 0.32f), new Vector2(0.32f, 0.1f));
                break;
            case "info":
                Linha(true, Elipse(0f, 0f, 0.46f, 0.46f));
                Linha(false, new Vector2(0f, -0.26f), new Vector2(0f, 0.04f));
                Linha(false, new Vector2(0f, 0.2f), new Vector2(0f, 0.24f));
                break;
            case "mic":
                Linha(true, new Vector2(-0.14f, 0.1f), new Vector2(-0.14f, 0.3f), new Vector2(-0.08f, 0.44f), new Vector2(0f, 0.48f), new Vector2(0.08f, 0.44f), new Vector2(0.14f, 0.3f), new Vector2(0.14f, 0.1f), new Vector2(0.08f, -0.04f), new Vector2(0f, -0.08f), new Vector2(-0.08f, -0.04f));
                Linha(false, Arco(0f, 0.1f, 0.3f, 180f, 360f));
                Linha(false, new Vector2(0f, -0.2f), new Vector2(0f, -0.42f));
                Linha(false, new Vector2(-0.16f, -0.42f), new Vector2(0.16f, -0.42f));
                break;
        }
        return raiz;
    }

    public static void Colorir(Transform icone, Color cor)
    {
        foreach (var lr in icone.GetComponentsInChildren<LineRenderer>(true)) lr.startColor = lr.endColor = cor;
    }

    static void Tracar(Transform pai, Vector2[] pts, bool fecha, float tam, float largura, Color cor)
    {
        var go = new GameObject("L");
        go.transform.SetParent(pai, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = fecha;
        lr.sharedMaterial = Mat();
        lr.startColor = lr.endColor = cor;
        lr.widthMultiplier = largura;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = TemaZetta.OrdemTexto;
        lr.positionCount = pts.Length;
        for (int i = 0; i < pts.Length; i++) lr.SetPosition(i, new Vector3(pts[i].x * tam, pts[i].y * tam, -0.001f));
    }

    static Vector2[] Elipse(float cx, float cy, float rx, float ry, int n = 28)
    {
        var l = new List<Vector2>();
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; l.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry)); }
        return l.ToArray();
    }

    static Vector2[] Arco(float cx, float cy, float r, float de, float ate, int n = 14)
    {
        var l = new List<Vector2>();
        for (int i = 0; i <= n; i++) { float a = Mathf.Deg2Rad * Mathf.Lerp(de, ate, i / (float)n); l.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); }
        return l.ToArray();
    }
}
