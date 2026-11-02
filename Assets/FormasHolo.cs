using System.Collections.Generic;
using UnityEngine;

// Malhas procedurais que o Unity não traz prontas. Todas cabem numa caixa de 1 m, centradas na origem.
public static class FormasHolo
{
    // cone (lados >= 12) ou pirâmide (lados = 4) com a ponta para cima
    public static Mesh Cone(int lados)
    {
        float raio = lados == 4 ? 0.70710678f : 0.5f;     // pirâmide com base de 1 m x 1 m
        float giro = lados == 4 ? Mathf.PI / 4f : 0f;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();

        for (int i = 0; i <= lados; i++)
        {
            float a = giro + i / (float)lados * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * raio, -0.5f, Mathf.Sin(a) * raio)); uv.Add(new Vector2(i / (float)lados, 0f));
            v.Add(new Vector3(0f, 0.5f, 0f)); uv.Add(new Vector2(i / (float)lados, 1f));
        }
        for (int i = 0; i < lados; i++)
        {
            int b0 = i * 2, p0 = i * 2 + 1, b1 = (i + 1) * 2;
            t.Add(b0); t.Add(p0); t.Add(b1);
        }

        // tampa da base
        int centro = v.Count;
        v.Add(new Vector3(0f, -0.5f, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
        int ini = v.Count;
        for (int i = 0; i <= lados; i++)
        {
            float a = giro + i / (float)lados * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Cos(a) * raio, -0.5f, Mathf.Sin(a) * raio));
            uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 0; i < lados; i++) { t.Add(centro); t.Add(ini + i + 1); t.Add(ini + i); }

        return Montar("Cone" + lados, v, uv, t);
    }

    // toro (anel): raio maior 0,35 e do tubo 0,15 => 1 m de diâmetro externo, 0,3 m de altura
    public static Mesh Anel(int segmentos = 24, int tubo = 8, float raioMaior = 0.35f, float raioTubo = 0.15f)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int i = 0; i <= segmentos; i++)
        {
            float u = i / (float)segmentos;
            float a = u * Mathf.PI * 2f;
            var centro = new Vector3(Mathf.Cos(a) * raioMaior, 0f, Mathf.Sin(a) * raioMaior);
            var fora = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int j = 0; j <= tubo; j++)
            {
                float vv = j / (float)tubo;
                float b = vv * Mathf.PI * 2f;
                v.Add(centro + fora * (Mathf.Cos(b) * raioTubo) + Vector3.up * (Mathf.Sin(b) * raioTubo));
                uv.Add(new Vector2(u, vv));
            }
        }
        int largura = tubo + 1;
        for (int i = 0; i < segmentos; i++)
            for (int j = 0; j < tubo; j++)
            {
                int a0 = i * largura + j, a1 = (i + 1) * largura + j;
                t.Add(a0); t.Add(a1); t.Add(a0 + 1);
                t.Add(a1); t.Add(a1 + 1); t.Add(a0 + 1);
            }
        return Montar("Anel", v, uv, t);
    }

    static Mesh Montar(string nome, List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        var m = new Mesh { name = nome };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
