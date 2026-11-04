using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Rótulo que sempre olha para o usuário e mantém o tamanho no mundo, mesmo com a raiz escalada.
public class RotuloFace : MonoBehaviour
{
    public float tamanhoMundo = 1f;
    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var s = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float k = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
        transform.localScale = Vector3.one * (tamanhoMundo / k);
        var d = transform.position - cam.transform.position;
        if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }
}

public static class HoloMat
{
    // paleta de séries/curvas (azul-ciano primeiro, depois cores bem distintas)
    public static readonly Color[] Serie =
    {
        TemaZetta.AzulTec, TemaZetta.Dourado, new Color(0.42f, 1f, 0.69f), TemaZetta.Roxo, new Color(1f, 0.45f, 0.75f), new Color(1f, 0.6f, 0.25f)
    };

    public static TextMeshPro Rotulo(Transform pai, string txt, Vector3 localPos, float tamanho, Color cor, bool face = true)
    {
        var t = HoloUtil.CriarTexto(pai, "Rotulo", txt, localPos, new Vector2(0.4f, 0.1f), tamanho, cor);
        t.enableAutoSizing = false;
        t.fontSize = tamanho;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        if (face) t.gameObject.AddComponent<RotuloFace>();
        return t;
    }

    // linhas (pares de pontos) numa malha só
    public static Mesh MalhaDeLinhas(List<Vector3> pts, List<Color> cores, string nome)
    {
        var m = new Mesh { name = nome, indexFormat = pts.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.SetVertices(pts);
        m.SetColors(cores);
        var idx = new int[pts.Count];
        for (int i = 0; i < idx.Length; i++) idx[i] = i;
        m.SetIndices(idx, MeshTopology.Lines, 0);
        m.RecalculateBounds();
        return m;
    }

    public static GameObject NovoMeshGO(Transform pai, string nome, Mesh malha, Material mat)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.AddComponent<MeshFilter>().sharedMesh = malha;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // deixa o objeto pegável (uma mão: pegar/mover/girar; duas: escalar/girar), registrado e com âncora
    public static void TornarPegavel(GameObject raiz)
    {
        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        raiz.AddComponent<ColisorPegada>();
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);
    }

    public static bool Numero(JObject a, string chave, out double v)
    {
        v = 0;
        var t = a[chave];
        if (t == null) return false;
        if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) { v = t.Value<double>(); return true; }
        return ExpressaoMatematica.Tentar(t.ToString(), out var e, out _) && !double.IsNaN(v = e.Avaliar());
    }

    public static double[] Percentis(List<double> v, double pa, double pb)
    {
        v.Sort();
        int n = v.Count;
        return new[] { v[Mathf.Clamp((int)(pa * (n - 1)), 0, n - 1)], v[Mathf.Clamp((int)(pb * (n - 1)), 0, n - 1)] };
    }

    public static Color Degrade(float t, Color baseCor, bool padrao)
    {
        t = Mathf.Clamp01(t);
        if (padrao)
        {
            // azul → ciano → verde-menta → dourado
            var a = new Color(0.18f, 0.36f, 1f); var b = TemaZetta.AzulTec; var c = new Color(0.42f, 1f, 0.69f); var d = TemaZetta.Dourado;
            return t < 0.33f ? Color.Lerp(a, b, t / 0.33f) : t < 0.66f ? Color.Lerp(b, c, (t - 0.33f) / 0.33f) : Color.Lerp(c, d, (t - 0.66f) / 0.34f);
        }
        return Color.Lerp(baseCor * 0.45f, Color.Lerp(baseCor, Color.white, 0.5f), t);
    }
}

// "Zetta, desenhe z = f(x,y)": superfície 3D de luz com eixos numerados e cores por altura, que se pega, gira e escala.
public class SuperficieHolo : MonoBehaviour
{
    public float tamanhoPadrao = 0.4f;
    const int N = 40;

    const string Decl = @"{
  ""name"": ""desenhar_superficie"",
  ""description"": ""Desenha em luz a superfície 3D z = f(x,y) sobre a mesa (ou à frente do usuário), com eixos numerados e cores por altura. Use x e y como variáveis; funções: sen, cos, tan, exp, ln, log (base 10), raiz, abs, potência com ^, pi, e."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""expressao"": { ""type"": ""STRING"", ""description"": ""Só o lado direito: ex. sen(x)*cos(y), x^2 - y^2, raiz(x^2+y^2)."" },
      ""x_min"": { ""type"": ""NUMBER"" }, ""x_max"": { ""type"": ""NUMBER"" },
      ""y_min"": { ""type"": ""NUMBER"" }, ""y_max"": { ""type"": ""NUMBER"" },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Opcional: uma cor única em vez das cores por altura."" }
    },
    ""required"": [""expressao""]
  }
}";
    const string Instrucao = "Para 'desenhe z = ...' / 'mostre a superfície de ...' use desenhar_superficie com a expressão em x e y (escreva sen, cos, raiz, ^). Se a expressão for recusada, diga o motivo em uma frase.";

    int contador;

    void Awake()
    {
        AncorasHolo.Fabricas["superficie"] = (item, pos, rot) =>
        {
            try { return Montar(JObject.Parse(item.extra), pos, rot, out _); } catch { return null; }
        };
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, a =>
        {
            var go = Montar(a, null, null, out string erro);
            return go != null ? new JObject { ["result"] = "superfície desenhada; dá para pegar, girar e escalar" } : new JObject { ["erro"] = erro };
        }, Instrucao);
    }

    public GameObject Montar(JObject a, Vector3? posMundo, Quaternion? rotMundo, out string erro)
    {
        erro = null;
        string txt = (a.Value<string>("expressao") ?? "").Trim();
        int ig = txt.IndexOf('=');
        if (ig >= 0) txt = txt.Substring(ig + 1);   // aceita "z = ..." e "f(x,y) = ..."
        if (!ExpressaoMatematica.Tentar(txt, out var f, out string e)) { erro = "expressão inválida: " + e; return null; }
        double x0 = -3, x1 = 3, y0 = -3, y1 = 3;
        if (HoloMat.Numero(a, "x_min", out var v)) x0 = v;
        if (HoloMat.Numero(a, "x_max", out v)) x1 = v;
        if (HoloMat.Numero(a, "y_min", out v)) y0 = v;
        if (HoloMat.Numero(a, "y_max", out v)) y1 = v;
        if (!(x1 > x0) || !(y1 > y0) || x1 - x0 > 1e6 || y1 - y0 > 1e6) { erro = "intervalo inválido (o máximo precisa ser maior que o mínimo)"; return null; }

        // amostragem
        var z = new double[N + 1, N + 1];
        var finitos = new List<double>();
        for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                double r = f.Avaliar(x0 + (x1 - x0) * i / N, y0 + (y1 - y0) * j / N);
                z[i, j] = r;
                if (!double.IsNaN(r) && !double.IsInfinity(r)) finitos.Add(r);
            }
        if (finitos.Count < 16) { erro = "a função não tem valores reais nesse intervalo"; return null; }
        var pc = HoloMat.Percentis(new List<double>(finitos), 0.02, 0.98);
        double amp = Math.Max(pc[1] - pc[0], 1e-9);
        double lo = pc[0] - 1.5 * amp, hi = pc[1] + 1.5 * amp;   // picos absurdos (1/x perto de 0) viram buracos
        var valido = new bool[N + 1, N + 1];
        double zmin = double.MaxValue, zmax = double.MinValue;
        for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                double r = z[i, j];
                valido[i, j] = !double.IsNaN(r) && !double.IsInfinity(r) && r >= lo && r <= hi;
                if (valido[i, j]) { zmin = Math.Min(zmin, r); zmax = Math.Max(zmax, r); }
            }
        double faixa = Math.Max(zmax - zmin, 1e-9);
        bool plano = zmax - zmin < 1e-7;

        var corUnica = string.IsNullOrWhiteSpace(a.Value<string>("cor")) ? (Color?)null : CriadorHolograma.ParseCor(a.Value<string>("cor"));

        // malhas: vértice (i,j) → (x, altura, y) no cubo local [-0.5,0.5] x [-0.3,0.3] x [-0.5,0.5]
        var verts = new Vector3[(N + 1) * (N + 1)];
        var tcor = new float[verts.Length];
        for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                int k = j * (N + 1) + i;
                float t = plano ? 0.5f : (float)((z[i, j] - zmin) / faixa);
                tcor[k] = valido[i, j] ? t : 0f;
                verts[k] = new Vector3(i / (float)N - 0.5f, valido[i, j] ? (t - 0.5f) * 0.6f : 0f, j / (float)N - 0.5f);
            }
        var tris = new List<int>(); var linhas = new List<int>();
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int k = j * (N + 1) + i;
                if (valido[i, j] && valido[i + 1, j] && valido[i, j + 1] && valido[i + 1, j + 1])
                { tris.Add(k); tris.Add(k + N + 1); tris.Add(k + 1); tris.Add(k + 1); tris.Add(k + N + 1); tris.Add(k + N + 2); }
                if (valido[i, j] && valido[i + 1, j]) { linhas.Add(k); linhas.Add(k + 1); }
                if (valido[i, j] && valido[i, j + 1]) { linhas.Add(k); linhas.Add(k + N + 1); }
            }
        if (tris.Count == 0) { erro = "a função não tem região contínua nesse intervalo"; return null; }

        var cam = Camera.main.transform;
        var mesa = FindAnyObjectByType<MesaHolografica>();
        var pos = posMundo ?? HoloUtil.PontoCriacao(0.4f);
        var raiz = new GameObject("Superficie_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, rotMundo ?? Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
        raiz.transform.localScale = Vector3.one * tamanhoPadrao;
        var oi = raiz.AddComponent<ObjetoInterativo>();

        // parte "superficie": plano translúcido + malha de linhas, as duas coloridas por altura
        var mSup = new Mesh { name = "Superficie", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
        mSup.vertices = verts; mSup.SetTriangles(tris, 0);
        var mLin = new Mesh { name = "SuperficieLinhas", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
        mLin.vertices = verts; mLin.SetIndices(linhas, MeshTopology.Lines, 0);
        AplicarCores(mSup, tcor, corUnica, 0.4f);
        AplicarCores(mLin, tcor, corUnica, 0.95f);
        mSup.RecalculateBounds(); mLin.RecalculateBounds();

        var parteSup = HoloMat.NovoMeshGO(raiz.transform, "superficie", mSup, HoloUtil.NovoMaterialSprite(Color.white));
        var goLin = HoloMat.NovoMeshGO(parteSup.transform, "Linhas", mLin, HoloUtil.NovoMaterialSprite(Color.white));
        var ph = parteSup.AddComponent<ParteHolo>();
        ph.nome = "superficie";
        ph.pintorPersonalizado = c => { AplicarCores(mSup, tcor, c, 0.4f); AplicarCores(mLin, tcor, c, 0.95f); };
        oi.AdicionarParte(ph);

        // parte "eixos": três eixos nas arestas do cubo com marcas e números
        var eixos = new GameObject("eixos"); eixos.transform.SetParent(raiz.transform, false);
        var pts = new List<Vector3>(); var cs = new List<Color>();
        var cEixo = TemaZetta.ComAlfa(TemaZetta.Branco, 0.85f);
        Action<Vector3, Vector3> seg = (p, q) => { pts.Add(p); pts.Add(q); cs.Add(cEixo); cs.Add(cEixo); };
        var o = new Vector3(-0.5f, -0.3f, -0.5f);
        seg(o, o + Vector3.right); seg(o + Vector3.right, o + new Vector3(1f, 0f, 1f)); seg(o, o + Vector3.up * 0.6f);
        for (int k = 0; k <= 4; k++)
        {
            float u = k / 4f;
            seg(o + Vector3.right * u, o + Vector3.right * u + new Vector3(0, 0, -0.025f));
            seg(o + new Vector3(1f, 0f, u), o + new Vector3(1f, 0f, u) + new Vector3(0.025f, 0, 0));
            seg(o + Vector3.up * (0.6f * u), o + Vector3.up * (0.6f * u) + new Vector3(-0.025f, 0, 0));
            HoloMat.Rotulo(eixos.transform, ExpressaoMatematica.Formatar(Math.Round(x0 + (x1 - x0) * u, 3)), o + Vector3.right * u + new Vector3(0, -0.02f, -0.06f), 0.45f, TemaZetta.Ciano).transform.SetParent(eixos.transform, false);
            HoloMat.Rotulo(eixos.transform, ExpressaoMatematica.Formatar(Math.Round(y0 + (y1 - y0) * u, 3)), o + new Vector3(1.07f, 0f, u), 0.45f, new Color(0.42f, 1f, 0.69f));
            HoloMat.Rotulo(eixos.transform, ExpressaoMatematica.Formatar(Math.Round(zmin + (zmax - zmin) * u, 3)), o + new Vector3(-0.07f, 0.6f * u, 0f), 0.45f, TemaZetta.Dourado);
        }
        HoloMat.Rotulo(eixos.transform, "x", o + new Vector3(1.06f, 0f, -0.04f), 0.7f, TemaZetta.Ciano);
        HoloMat.Rotulo(eixos.transform, "y", o + new Vector3(1.0f, 0f, 1.08f), 0.7f, new Color(0.42f, 1f, 0.69f));
        HoloMat.Rotulo(eixos.transform, "z", o + new Vector3(0f, 0.66f, 0f), 0.7f, TemaZetta.Dourado);
        var mEixos = HoloMat.MalhaDeLinhas(pts, cs, "Eixos");
        var mr = HoloMat.NovoMeshGO(eixos.transform, "LinhasEixos", mEixos, HoloUtil.NovoMaterialSprite(Color.white));
        var phe = eixos.AddComponent<ParteHolo>();
        phe.nome = "eixos";
        phe.pintorPersonalizado = c => { foreach (var r in eixos.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial.color = c; foreach (var t in eixos.GetComponentsInChildren<TextMeshPro>()) t.color = c; };
        oi.AdicionarParte(phe);

        HoloMat.TornarPegavel(raiz);
        var item = HoloRegistro.Adicionar(raiz, "superficie", "", "superfície z = " + f.Texto, "superficie", tamanhoPadrao,
            new JObject { ["expressao"] = f.Texto, ["x_min"] = x0, ["x_max"] = x1, ["y_min"] = y0, ["y_max"] = y1 }.ToString(Newtonsoft.Json.Formatting.None));
        StartCoroutine(Surgir(raiz.transform));
        return raiz;
    }

    static void AplicarCores(Mesh m, float[] t, Color? unica, float alfa)
    {
        var c = new Color[t.Length];
        for (int i = 0; i < c.Length; i++)
        {
            var cor = HoloMat.Degrade(t[i], unica ?? Color.white, !unica.HasValue);
            cor.a = alfa;
            c[i] = cor;
        }
        m.colors = c;
    }

    static void AplicarCores(Mesh m, float[] t, Color unica, float alfa) => AplicarCores(m, t, (Color?)unica, alfa);

    // a superfície "cresce" do plano até a altura final
    IEnumerator Surgir(Transform raiz)
    {
        var fim = raiz.localScale;
        for (float t = 0; t < 0.9f && raiz != null; t += Time.deltaTime)
        {
            float x = Mathf.SmoothStep(0f, 1f, t / 0.9f);
            raiz.localScale = new Vector3(fim.x * (0.6f + 0.4f * x), fim.y * Mathf.Max(0.02f, x), fim.z * (0.6f + 0.4f * x));
            yield return null;
        }
        if (raiz != null) raiz.localScale = fim;
    }
}

// "Zetta, gráfico de sen(x) e cos(x)": curvas 2D no ar, várias sobrepostas em cores diferentes, eixos numerados e legenda.
public class GraficoFuncoes : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""grafico_funcoes"",
  ""description"": ""Mostra no ar o gráfico 2D de uma ou mais funções de x sobrepostas, cada uma de uma cor, com eixos numerados e legenda. Funções: sen, cos, tan, exp, ln, log (base 10), raiz, abs, potência com ^, pi, e."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""expressoes"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""STRING"" }, ""description"": ""Lado direito de y = f(x), uma por curva (até 6). Ex.: ['sen(x)', 'x^2/10']."" },
      ""x_min"": { ""type"": ""NUMBER"" }, ""x_max"": { ""type"": ""NUMBER"" },
      ""y_min"": { ""type"": ""NUMBER"" }, ""y_max"": { ""type"": ""NUMBER"" }
    },
    ""required"": [""expressoes""]
  }
}";
    const string Instrucao = "Para 'gráfico de f(x)' / 'plote ... e ...' use grafico_funcoes com todas as funções numa só chamada. Para superfícies z=f(x,y) use desenhar_superficie.";

    int contador;
    const float L = 0.62f, A = 0.44f;
    static readonly Rect Plot = new Rect(-0.27f, -0.14f, 0.41f, 0.26f);

    void Awake()
    {
        AncorasHolo.Fabricas["grafico_fn"] = (item, pos, rot) =>
        {
            try { return Montar(JObject.Parse(item.extra), pos, rot, out _); } catch { return null; }
        };
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, a =>
        {
            var go = Montar(a, null, null, out string erro);
            return go != null ? new JObject { ["result"] = "gráfico mostrado" } : new JObject { ["erro"] = erro };
        }, Instrucao);
    }

    public GameObject Montar(JObject a, Vector3? posMundo, Quaternion? rotMundo, out string erro)
    {
        erro = null;
        var lista = new List<ExpressaoMatematica>();
        if (a["expressoes"] is JArray arr)
            foreach (var t in arr)
            {
                string s = t.ToString().Trim();
                int ig = s.IndexOf('=');
                if (ig >= 0) s = s.Substring(ig + 1);
                if (!ExpressaoMatematica.Tentar(s, out var f, out string e)) { erro = "expressão inválida (" + s + "): " + e; return null; }
                lista.Add(f);
            }
        if (lista.Count == 0) { erro = "nenhuma função informada"; return null; }
        if (lista.Count > 6) lista.RemoveRange(6, lista.Count - 6);
        double x0 = -10, x1 = 10;
        if (HoloMat.Numero(a, "x_min", out var v)) x0 = v;
        if (HoloMat.Numero(a, "x_max", out v)) x1 = v;
        if (!(x1 > x0) || x1 - x0 > 1e6) { erro = "intervalo inválido"; return null; }

        const int N = 200;
        var ys = new double[lista.Count][];
        var todos = new List<double>();
        for (int k = 0; k < lista.Count; k++)
        {
            ys[k] = new double[N + 1];
            for (int i = 0; i <= N; i++)
            {
                double y = lista[k].Avaliar(x0 + (x1 - x0) * i / N);
                ys[k][i] = y;
                if (!double.IsNaN(y) && !double.IsInfinity(y)) todos.Add(y);
            }
        }
        if (todos.Count < 4) { erro = "as funções não têm valores reais nesse intervalo"; return null; }
        double y0, y1;
        if (HoloMat.Numero(a, "y_min", out var ymn) && HoloMat.Numero(a, "y_max", out var ymx) && ymx > ymn) { y0 = ymn; y1 = ymx; }
        else
        {
            var pc = HoloMat.Percentis(todos, 0.03, 0.97);
            double m = Math.Max((pc[1] - pc[0]) * 0.12, 1e-6);
            y0 = pc[0] - m; y1 = pc[1] + m;
            if (y1 - y0 < 1e-6) { y0 -= 1; y1 += 1; }
        }

        var cam = Camera.main.transform;
        var pos = posMundo ?? HoloUtil.PontoCriacao(0.4f);
        var raiz = new GameObject("GraficoFn_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, rotMundo ?? Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
        var oi = raiz.AddComponent<ObjetoInterativo>();

        var painel = HoloUtil.CriarPainel(raiz.transform, "Painel", new Vector2(L, A));
        var phP = painel.gameObject.AddComponent<ParteHolo>();
        phP.nome = "painel";
        phP.pintorPersonalizado = c => painel.GetComponent<PainelVidro>().DefinirBorda(c, 1.3f);
        oi.AdicionarParte(phP);
        float zT = TemaZetta.ProfundidadeTexto;

        // eixos, grade fraca e números
        var eixosGO = new GameObject("eixos"); eixosGO.transform.SetParent(raiz.transform, false);
        var pts = new List<Vector3>(); var cs = new List<Color>();
        var cGrade = TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.18f); var cEixo = TemaZetta.ComAlfa(TemaZetta.Branco, 0.9f);
        Action<Vector3, Vector3, Color> seg = (p, q, c) => { pts.Add(p); pts.Add(q); cs.Add(c); cs.Add(c); };
        Func<double, float> px = x => (float)(Plot.xMin + (x - x0) / (x1 - x0) * Plot.width);
        Func<double, float> py = y => (float)(Plot.yMin + (y - y0) / (y1 - y0) * Plot.height);
        for (int k = 0; k <= 4; k++)
        {
            double xv = x0 + (x1 - x0) * k / 4.0, yv = y0 + (y1 - y0) * k / 4.0;
            seg(new Vector3(px(xv), Plot.yMin, zT * 0.3f), new Vector3(px(xv), Plot.yMax, zT * 0.3f), cGrade);
            seg(new Vector3(Plot.xMin, py(yv), zT * 0.3f), new Vector3(Plot.xMax, py(yv), zT * 0.3f), cGrade);
            Rot(eixosGO.transform, ExpressaoMatematica.Formatar(Math.Round(xv, 3)), new Vector3(px(xv), Plot.yMin - 0.022f, zT), TemaZetta.Ciano, TextAlignmentOptions.Center);
            Rot(eixosGO.transform, ExpressaoMatematica.Formatar(Math.Round(yv, 3)), new Vector3(Plot.xMin - 0.03f, py(yv), zT), TemaZetta.Ciano, TextAlignmentOptions.Right);
        }
        if (x0 < 0 && x1 > 0) seg(new Vector3(px(0), Plot.yMin, zT * 0.5f), new Vector3(px(0), Plot.yMax, zT * 0.5f), cEixo);
        if (y0 < 0 && y1 > 0) seg(new Vector3(Plot.xMin, py(0), zT * 0.5f), new Vector3(Plot.xMax, py(0), zT * 0.5f), cEixo);
        seg(new Vector3(Plot.xMin, Plot.yMin, zT * 0.5f), new Vector3(Plot.xMax, Plot.yMin, zT * 0.5f), cEixo);
        seg(new Vector3(Plot.xMin, Plot.yMin, zT * 0.5f), new Vector3(Plot.xMin, Plot.yMax, zT * 0.5f), cEixo);
        var mEixos = HoloMat.MalhaDeLinhas(pts, cs, "EixosGrafico");
        HoloMat.NovoMeshGO(eixosGO.transform, "LinhasEixos", mEixos, HoloUtil.NovoMaterialSprite(Color.white));
        var phE = eixosGO.AddComponent<ParteHolo>();
        phE.nome = "eixos";
        phE.pintorPersonalizado = c => { foreach (var r in eixosGO.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial.color = c; };
        oi.AdicionarParte(phE);

        // curvas: uma parte por função; trechos fora da janela ou indefinidos quebram a linha
        var leg = new System.Text.StringBuilder();
        for (int k = 0; k < lista.Count; k++)
        {
            var cor = HoloMat.Serie[k % HoloMat.Serie.Length];
            var curva = new GameObject("f" + (k + 1)); curva.transform.SetParent(raiz.transform, false);
            var corrida = new List<Vector3>();
            Action fecha = () =>
            {
                if (corrida.Count >= 2)
                {
                    var go = new GameObject("trecho"); go.transform.SetParent(curva.transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.sharedMaterial = HoloUtil.NovoMaterialSprite(Color.white);
                    lr.startColor = lr.endColor = cor;
                    lr.widthMultiplier = 0.0035f;
                    lr.numCapVertices = 2; lr.numCornerVertices = 2;
                    lr.positionCount = corrida.Count;
                    lr.SetPositions(corrida.ToArray());
                }
                corrida.Clear();
            };
            for (int i = 0; i <= N; i++)
            {
                double y = ys[k][i];
                bool ok = !double.IsNaN(y) && !double.IsInfinity(y) && y >= y0 && y <= y1;
                // salto grande entre pontos vizinhos (assíntota da tangente) também quebra
                if (ok && corrida.Count > 0 && Math.Abs(py(y) - corrida[corrida.Count - 1].y) > Plot.height * 0.6f) fecha();
                if (ok) corrida.Add(new Vector3(px(x0 + (x1 - x0) * i / N), py(y), zT * 0.8f));
                else fecha();
            }
            fecha();
            var phC = curva.AddComponent<ParteHolo>();
            phC.nome = "f" + (k + 1);
            phC.apelidos = new[] { "funcao " + (k + 1), "curva " + (k + 1), lista[k].Texto };
            phC.pintorPersonalizado = c => { foreach (var lr in curva.GetComponentsInChildren<LineRenderer>()) lr.startColor = lr.endColor = c; };
            phC.pintada = false;
            oi.AdicionarParte(phC);
            leg.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(cor)).Append(">f").Append(k + 1).Append(" = ").Append(lista[k].Texto).Append("</color>\n");
        }
        Rot(raiz.transform, "GRÁFICO DE FUNÇÕES", new Vector3(-0.05f, 0.19f, zT), TemaZetta.Branco, TextAlignmentOptions.Center, 0.05f).fontStyle = FontStyles.Bold;
        var tl = HoloUtil.CriarTexto(raiz.transform, "Legenda", leg.ToString().TrimEnd('\n'), new Vector3(0.23f, 0.02f, zT), new Vector2(0.14f, 0.24f), 0.2f, TemaZetta.Branco, TextAlignmentOptions.TopLeft);
        tl.fontSizeMin = 0.15f; tl.fontSizeMax = 0.3f;
        tl.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        HoloMat.TornarPegavel(raiz);
        var cfg = new JObject { ["expressoes"] = new JArray(lista.ConvertAll(e => e.Texto)), ["x_min"] = x0, ["x_max"] = x1, ["y_min"] = y0, ["y_max"] = y1 };
        HoloRegistro.Adicionar(raiz, "grafico_fn", "", "gráfico de " + string.Join(" e ", lista.ConvertAll(e => e.Texto)), "grafico_fn", L, cfg.ToString(Newtonsoft.Json.Formatting.None));
        StartCoroutine(HoloUtil.Surgir(raiz.transform));
        return raiz;
    }

    static TextMeshPro Rot(Transform pai, string txt, Vector3 pos, Color cor, TextAlignmentOptions al, float tam = 0.03f)
    {
        var t = HoloUtil.CriarTexto(pai, "Rotulo", txt, pos, new Vector2(0.14f, 0.04f), tam, cor, al);
        t.enableAutoSizing = false; t.fontSize = tam;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }
}
