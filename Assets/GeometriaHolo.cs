using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Geometria interativa: triângulo, quadrilátero ou tetraedro com vértices que se pegam e movem com os dedos.
// Lados, ângulos, área (e volume) são recalculados ao vivo e mostrados em rótulos e num painel de medidas. Medidas em cm.
public class GeometriaHolo : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""geometria_interativa"",
  ""description"": ""Cria uma figura geométrica com vértices que o usuário pega e arrasta com as mãos; mostra ao vivo as medidas (lados, ângulos, área, volume). Figuras: triangulo, quadrilatero, tetraedro."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""criar"", ""fechar""] },
      ""figura"": { ""type"": ""STRING"", ""enum"": [""triangulo"", ""quadrilatero"", ""tetraedro""] }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'triângulo/quadrilátero/tetraedro interativo' ou 'geometria' use geometria_interativa; as medidas aparecem ao vivo quando o usuário mexe nos vértices.";

    class Figura
    {
        public string tipo;
        public Transform raiz;
        public Transform[] v;
        public LineRenderer[] arestas;
        public int[,] pares;
        public TextMeshPro[] rotArestas, rotAngulos;
        public TextMeshPro painel;
        public Vector3[] ultimo;
    }
    readonly List<Figura> figuras = new List<Figura>();
    int contador;

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    public JObject Executar(JObject a)
    {
        if ((a.Value<string>("acao") ?? "criar").ToLowerInvariant().StartsWith("f"))
        {
            foreach (var f in new List<Figura>(figuras)) Fechar(f);
            return new JObject { ["result"] = "figuras fechadas" };
        }
        string tipo = ApagadorHolo.Normalizar(a.Value<string>("figura") ?? "triangulo");
        if (tipo.StartsWith("quad")) tipo = "quadrilatero"; else if (tipo.StartsWith("tetra")) tipo = "tetraedro"; else tipo = "triangulo";
        Criar(tipo);
        return new JObject { ["result"] = tipo + " criado; pegue um vértice com a pinça e arraste para ver as medidas mudarem" };
    }

    void Criar(string tipo)
    {
        while (figuras.Count >= 2) Fechar(figuras[0]);
        var cam = Camera.main.transform;
        var centro = HoloUtil.PontoNaVista(new Vector3(0f, -0.02f, 0.65f));
        var yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        Vector3[] locais;
        int[,] pares;
        switch (tipo)
        {
            case "quadrilatero":
                locais = new[] { new Vector3(-0.12f, -0.08f, 0), new Vector3(0.13f, -0.07f, 0), new Vector3(0.1f, 0.1f, 0), new Vector3(-0.09f, 0.09f, 0) };
                pares = new[,] { { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 } }; break;
            case "tetraedro":
                locais = new[] { new Vector3(-0.1f, -0.08f, -0.06f), new Vector3(0.11f, -0.08f, -0.06f), new Vector3(0f, -0.08f, 0.12f), new Vector3(0f, 0.1f, 0f) };
                pares = new[,] { { 0, 1 }, { 1, 2 }, { 2, 0 }, { 0, 3 }, { 1, 3 }, { 2, 3 } }; break;
            default:
                locais = new[] { new Vector3(-0.12f, -0.07f, 0), new Vector3(0.12f, -0.07f, 0), new Vector3(0.02f, 0.11f, 0) };
                pares = new[,] { { 0, 1 }, { 1, 2 }, { 2, 0 } }; break;
        }
        var f = new Figura { tipo = tipo, pares = pares };
        var raiz = new GameObject("Geometria_" + tipo + "_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(centro, yaw);
        f.raiz = raiz.transform;
        var oi = raiz.AddComponent<ObjetoInterativo>();

        f.v = new Transform[locais.Length];
        f.ultimo = new Vector3[locais.Length];
        for (int i = 0; i < locais.Length; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "V" + (i + 1);
            DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(raiz.transform, false);
            s.transform.localPosition = locais[i];
            s.transform.localScale = Vector3.one * 0.022f;
            s.GetComponent<Renderer>().sharedMaterial = HoloUtil.NovoMaterialSprite(TemaZetta.Dourado);
            var col = s.AddComponent<SphereCollider>();
            col.radius = 1.8f;   // pinça fácil: alvo bem maior que a bolinha
            var rb = s.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var g = s.AddComponent<XRGrabInteractable>();
            g.movementType = XRBaseInteractable.MovementType.Instantaneous;
            g.selectMode = InteractableSelectMode.Single;
            g.throwOnDetach = false;
            g.trackRotation = false;
            g.useDynamicAttach = false;
            var ph = s.AddComponent<ParteHolo>();
            ph.nome = "vertice " + (i + 1);
            oi.AdicionarParte(ph);
            f.v[i] = s.transform;
            var r = HoloMat.Rotulo(raiz.transform, ((char)('A' + i)).ToString(), Vector3.zero, 0.18f, TemaZetta.Dourado);
            r.transform.SetParent(s.transform, false);   // rótulo da letra do vértice acompanha a bolinha
            r.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        }
        int n = pares.GetLength(0);
        f.arestas = new LineRenderer[n];
        f.rotArestas = new TextMeshPro[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("aresta" + (i + 1));
            go.transform.SetParent(raiz.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = HoloUtil.NovoMaterialSprite(TemaZetta.AzulTec);
            lr.widthMultiplier = 0.004f;
            lr.positionCount = 2;
            f.arestas[i] = lr;
            f.rotArestas[i] = HoloMat.Rotulo(raiz.transform, "", Vector3.zero, 0.13f, TemaZetta.Ciano);
        }
        f.rotAngulos = new TextMeshPro[locais.Length];
        for (int i = 0; i < locais.Length; i++) f.rotAngulos[i] = HoloMat.Rotulo(raiz.transform, "", Vector3.zero, 0.12f, new Color(0.42f, 1f, 0.69f));

        var pt = new GameObject("Medidas");
        pt.transform.SetParent(raiz.transform, false);
        pt.transform.localPosition = new Vector3(0.3f, 0f, 0f);
        f.painel = HoloUtil.CriarTexto(pt.transform, "Texto", "", Vector3.zero, new Vector2(0.3f, 0.25f), 0.03f, TemaZetta.Branco, TextAlignmentOptions.Left);
        f.painel.enableAutoSizing = false; f.painel.fontSize = 0.026f; f.painel.textWrappingMode = TextWrappingModes.NoWrap; f.painel.overflowMode = TextOverflowModes.Overflow;
        pt.AddComponent<RotuloFace>();

        HoloRegistro.Adicionar(raiz, tipo, "", tipo + " interativo", "geometria", 0.3f);
        figuras.Add(f);
        Atualizar(f, true);
    }

    void Fechar(Figura f)
    {
        figuras.Remove(f);
        if (f.raiz != null) { HoloRegistro.Remover(f.raiz.gameObject); Destroy(f.raiz.gameObject); }
    }

    void Update()
    {
        figuras.RemoveAll(f => f.raiz == null);
        foreach (var f in figuras) Atualizar(f, false);
    }

    void Atualizar(Figura f, bool forcar)
    {
        bool mudou = forcar;
        for (int i = 0; i < f.v.Length; i++)
            if ((f.v[i].position - f.ultimo[i]).sqrMagnitude > 1e-9f) { mudou = true; f.ultimo[i] = f.v[i].position; }
        if (!mudou) return;
        var p = new Vector3[f.v.Length];
        for (int i = 0; i < p.Length; i++) p[i] = f.v[i].position;

        var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        for (int i = 0; i < f.arestas.Length; i++)
        {
            var a = p[f.pares[i, 0]]; var b = p[f.pares[i, 1]];
            f.arestas[i].SetPosition(0, a); f.arestas[i].SetPosition(1, b);
            f.rotArestas[i].transform.position = (a + b) * 0.5f + Vector3.up * 0.02f;
            f.rotArestas[i].text = Cm(Vector3.Distance(a, b));
        }
        var c = Vector3.zero; foreach (var q in p) c += q; c /= p.Length;
        for (int i = 0; i < p.Length; i++)
        {
            var viz = Vizinhos(f, i);
            float ang = Vector3.Angle(p[viz[0]] - p[i], p[viz[1]] - p[i]);
            var dentro = ((p[viz[0]] - p[i]).normalized + (p[viz[1]] - p[i]).normalized).normalized;
            f.rotAngulos[i].transform.position = p[i] + dentro * 0.035f;
            f.rotAngulos[i].text = ang.ToString("0") + "°";
        }

        var sb = new StringBuilder();
        string[] nomes = { "A", "B", "C", "D" };
        sb.Append("<b>").Append(f.tipo == "triangulo" ? "TRIÂNGULO" : f.tipo == "quadrilatero" ? "QUADRILÁTERO" : "TETRAEDRO").Append("</b>\n");
        if (f.tipo == "triangulo")
        {
            float a = Vector3.Distance(p[1], p[2]), b = Vector3.Distance(p[0], p[2]), cc = Vector3.Distance(p[0], p[1]);
            float area = Vector3.Cross(p[1] - p[0], p[2] - p[0]).magnitude * 0.5f;
            sb.Append("Lados: ").Append(Cm(a)).Append(" · ").Append(Cm(b)).Append(" · ").Append(Cm(cc)).Append('\n');
            sb.Append("Perímetro: ").Append(Cm(a + b + cc)).Append('\n');
            sb.Append("Área: ").Append(Cm2(area)).Append('\n');
            float[] ang = { Vector3.Angle(p[1] - p[0], p[2] - p[0]), Vector3.Angle(p[0] - p[1], p[2] - p[1]), Vector3.Angle(p[0] - p[2], p[1] - p[2]) };
            sb.Append("Ângulos: ").Append(ang[0].ToString("0")).Append("° · ").Append(ang[1].ToString("0")).Append("° · ").Append(ang[2].ToString("0")).Append("°  (soma ").Append((ang[0] + ang[1] + ang[2]).ToString("0")).Append("°)\n");
            sb.Append("Tipo: ").Append(ClassificarTriangulo(a, b, cc, ang));
        }
        else if (f.tipo == "quadrilatero")
        {
            float perim = 0; for (int i = 0; i < 4; i++) perim += Vector3.Distance(p[i], p[(i + 1) % 4]);
            // área pelas duas diagonais: |d1 x d2| / 2 (vale para quadriláteros planos e convexos)
            float area = Vector3.Cross(p[2] - p[0], p[3] - p[1]).magnitude * 0.5f;
            sb.Append("Perímetro: ").Append(Cm(perim)).Append('\n');
            sb.Append("Área: ").Append(Cm2(area)).Append('\n');
            sb.Append("Diagonais: ").Append(Cm(Vector3.Distance(p[0], p[2]))).Append(" · ").Append(Cm(Vector3.Distance(p[1], p[3]))).Append('\n');
            float soma = 0; for (int i = 0; i < 4; i++) { var v = Vizinhos(f, i); soma += Vector3.Angle(p[v[0]] - p[i], p[v[1]] - p[i]); }
            sb.Append("Soma dos ângulos: ").Append(soma.ToString("0")).Append("°");
        }
        else
        {
            float vol = Mathf.Abs(Vector3.Dot(p[1] - p[0], Vector3.Cross(p[2] - p[0], p[3] - p[0]))) / 6f;
            float[] faces = { Area(p[0], p[1], p[2]), Area(p[0], p[1], p[3]), Area(p[1], p[2], p[3]), Area(p[0], p[2], p[3]) };
            float total = faces[0] + faces[1] + faces[2] + faces[3];
            sb.Append("Volume: ").Append((vol * 1e6f).ToString("0.0")).Append(" cm³\n");
            sb.Append("Área total: ").Append(Cm2(total)).Append('\n');
            sb.Append("Faces: ");
            for (int i = 0; i < 4; i++) sb.Append(Cm2(faces[i])).Append(i < 3 ? " · " : "");
            sb.Append("\nArestas: ").Append(f.arestas.Length).Append("  Vértices: 4  Faces: 4");
        }
        f.painel.text = sb.ToString();
    }

    static float Area(Vector3 a, Vector3 b, Vector3 c) => Vector3.Cross(b - a, c - a).magnitude * 0.5f;
    static string Cm(float m) => (m * 100f).ToString("0.0") + " cm";
    static string Cm2(float m2) => (m2 * 10000f).ToString("0.0") + " cm²";

    static int[] Vizinhos(Figura f, int i)
    {
        var l = new List<int>();
        for (int k = 0; k < f.pares.GetLength(0); k++)
        {
            if (f.pares[k, 0] == i) l.Add(f.pares[k, 1]);
            else if (f.pares[k, 1] == i) l.Add(f.pares[k, 0]);
        }
        // tetraedro: cada vértice tem 3 vizinhos; o ângulo mostrado é o da face de base (dois primeiros)
        return new[] { l[0], l[1] };
    }

    static string ClassificarTriangulo(float a, float b, float c, float[] ang)
    {
        const float tol = 0.03f;
        float m = Mathf.Max(a, Mathf.Max(b, c));
        bool eq = Mathf.Abs(a - b) < tol * m && Mathf.Abs(b - c) < tol * m;
        bool iso = Mathf.Abs(a - b) < tol * m || Mathf.Abs(b - c) < tol * m || Mathf.Abs(a - c) < tol * m;
        string lados = eq ? "equilátero" : iso ? "isósceles" : "escaleno";
        float maior = Mathf.Max(ang[0], Mathf.Max(ang[1], ang[2]));
        string angs = Mathf.Abs(maior - 90f) < 1.5f ? "retângulo" : maior > 90f ? "obtusângulo" : "acutângulo";
        return lados + " e " + angs;
    }
}
