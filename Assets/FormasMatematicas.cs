using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Ferramenta "criar_forma_matematica": fita de Möbius, garrafa de Klein, nó tórico, hélice de DNA, toro e fractal
// (tetraedro de Sierpinski) como malhas procedurais em luz (mesmo visual dos outros hologramas), girando devagar
// e manipuláveis como qualquer holograma (pegar, escalar com duas mãos, âncora, apagar).
public class FormasMatematicas : MonoBehaviour
{
    public Vector3 posicaoVista = new Vector3(0f, 0.0f, 0.8f);
    public float tamanhoPadrao = 0.3f;

    public static readonly string[] Nomes = { "mobius", "klein", "no_toroidal", "dna", "toro", "fractal" };

    const string Decl = @"{
  ""name"": ""criar_forma_matematica"",
  ""description"": ""Mostra uma forma matemática em luz, girando, que o usuário pode pegar: fita de Möbius, garrafa de Klein, nó tórico, hélice de DNA, toro ou fractal (tetraedro de Sierpinski)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""forma"": { ""type"": ""STRING"", ""enum"": [""mobius"", ""klein"", ""no_toroidal"", ""dna"", ""toro"", ""fractal""] },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Cor em português (padrão: ciano)."" },
      ""tamanho"": { ""type"": ""NUMBER"", ""description"": ""Maior dimensão em metros (padrão 0.3)."" }
    },
    ""required"": [""forma""]
  }
}";
    const string Instrucao = "Para fita de Möbius, garrafa de Klein, nó tórico, hélice de DNA, toro ou fractal use criar_forma_matematica e comente em uma frase curta.";

    int contador;

    void Awake()
    {
        AncorasHolo.Fabricas["forma"] = (item, pos, rot) =>
        {
            Criar(new JObject { ["forma"] = item.extra, ["cor"] = item.cor, ["tamanho"] = item.tamanho }, pos, rot, item.estado);
            return HoloRegistro.Itens.Count > 0 ? HoloRegistro.Itens[HoloRegistro.Itens.Count - 1].go : null;
        };
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, a => Criar(a), Instrucao);
    }

    public JObject Criar(JObject args, Vector3? posMundo = null, Quaternion? rotMundo = null, string estadoSalvo = null)
    {
        string forma = ApagadorHolo.Normalizar(args.Value<string>("forma") ?? "").Replace(' ', '_').Replace("-", "_");
        if (forma.Contains("mobius")) forma = "mobius";
        else if (forma.Contains("klein")) forma = "klein";
        else if (forma.Contains("no")) forma = "no_toroidal";
        else if (forma.Contains("dna") || forma.Contains("helice")) forma = "dna";
        else if (forma.Contains("fractal") || forma.Contains("sierpinski")) forma = "fractal";
        else if (forma.Contains("toro")) forma = "toro";
        // o DNA agora e o infografico (DnaHolo): fitas finas, degraus separados, base de aneis e chamadas
        if (forma == "dna" && posMundo == null && string.IsNullOrEmpty(estadoSalvo))
        {
            var dna = FindAnyObjectByType<DnaHolo>();
            if (dna != null) { dna.Montar(null, null); return new JObject { ["result"] = "hélice de DNA criada (infográfico)" }; }
        }
        var malha = Gerar(forma);
        if (malha == null) return new JObject { ["erro"] = "forma inválida; use mobius, klein, no_toroidal, dna, toro ou fractal" };

        float tamanho = Mathf.Clamp(args.Value<float?>("tamanho") ?? tamanhoPadrao, InteracaoMaos.TamanhoMin, InteracaoMaos.TamanhoMax);
        var corTxt = args.Value<string>("cor");
        var cor = CriadorHolograma.ParseCor(corTxt);

        var raiz = new GameObject("Forma_" + forma + "_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.position = posMundo ?? HoloUtil.PontoNaVista(posicaoVista);
        raiz.transform.rotation = rotMundo ?? Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        raiz.transform.localScale = Vector3.one * tamanho;

        // a malha gira num filho; a raiz (com colisor e pegada) fica parada
        var corpo = new GameObject("Corpo");
        corpo.transform.SetParent(raiz.transform, false);
        // partes: DNA = fita A, fita B e degraus; as outras formas, 3 regiões ao longo do maior eixo
        var oi = raiz.AddComponent<ObjetoInterativo>();
        var pedacos = forma == "dna"
            ? MalhaPartes.Dividir(malha, new[] { "fita A", "fita B", "degraus" }, (c, n, i) => i < dnaFimA ? 0 : (i < dnaFimB ? 1 : 2))
            : MalhaPartes.Regioes(malha);
        if (forma == "dna")
        {
            oi.grupos["pares"] = new[] { "degraus" };
            oi.grupos["bases"] = new[] { "degraus" };
            oi.grupos["fitas"] = new[] { "fita A", "fita B" };
            oi.grupos["fita"] = new[] { "fita A", "fita B" };
            oi.acoesExtra = new[] { "desenrolar", "enrolar", "voltas" };
            oi.extras["voltas"] = VoltasPadrao;
            oi.extras["desenrolo"] = 0f;
            oi.acaoExtra = (acao, valor) => AcaoDna(oi, corpo.transform, acao, valor);
        }
        foreach (var pd in pedacos)
        {
            var parte = new GameObject(pd.nome);
            parte.transform.SetParent(corpo.transform, false);
            parte.AddComponent<MeshFilter>().sharedMesh = pd.malha;
            parte.AddComponent<MeshRenderer>();
            CriadorHolograma.AplicarVisual(parte, cor, new Vector2(1f, 1f), 1.2f, null, 0.22f, malha.bounds);
            var ph = parte.AddComponent<ParteHolo>();
            ph.nome = pd.nome;
            ph.origem = pd.origem;
            oi.AdicionarParte(ph);
        }
        var g = corpo.AddComponent<Girar>();
        g.eixo = Vector3.up;
        g.grausPorSegundo = 24f;

        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        raiz.AddComponent<ColisorPegada>();
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);

        HoloRegistro.Adicionar(raiz, forma, corTxt ?? "ciano", NomeBonito(forma), "forma", tamanho, forma);
        if (!string.IsNullOrEmpty(estadoSalvo)) oi.AplicarEstado(estadoSalvo);
        EstiloCientifico.Aplicar(raiz, corpo.transform, forma);   // wireframe, nervuras, traços de luz, rótulos e painel
        return new JObject { ["result"] = NomeBonito(forma) + " criada" };
    }

    static string NomeBonito(string f)
    {
        switch (f)
        {
            case "mobius": return "fita de Möbius";
            case "klein": return "garrafa de Klein";
            case "no_toroidal": return "nó tórico";
            case "dna": return "hélice de DNA";
            case "toro": return "toro";
            default: return "fractal";
        }
    }

    // ---------- malhas (normalizadas para caber numa caixa de 1 m, centradas) ----------
    public const float VoltasPadrao = 2.5f;
    static int dnaFimA, dnaFimB;   // fim (em vértices) da fita A e da fita B na malha do DNA

    // ações do DNA: desenrolar/enrolar (anima a torção até a escada reta) e voltas (número de voltas da hélice)
    string AcaoDna(ObjetoInterativo oi, Transform corpo, string acao, float valor)
    {
        acao = (acao ?? "").ToLowerInvariant();
        if (acao.StartsWith("direto:"))   // estado restaurado da âncora: sem animação
        {
            oi.extras[acao.Substring(7)] = valor;
            AtualizarMalhaDna(oi, corpo);
            return null;
        }
        if (acao.StartsWith("desenrol")) { IniciarDna(oi, corpo, null, 1f); return "DNA desenrolando"; }
        if (acao.StartsWith("enrol")) { IniciarDna(oi, corpo, null, 0f); return "DNA enrolando"; }
        if (acao.StartsWith("volta") || acao.StartsWith("desenrolo"))
        {
            if (acao.StartsWith("desenrolo")) { oi.extras["desenrolo"] = Mathf.Clamp01(valor); AtualizarMalhaDna(oi, corpo); return null; }
            float v = Mathf.Clamp(valor, 0.5f, 8f);
            IniciarDna(oi, corpo, v, null);
            return "DNA com " + v.ToString("0.#") + " voltas";
        }
        return null;
    }

    readonly Dictionary<ObjetoInterativo, Coroutine> animDna = new Dictionary<ObjetoInterativo, Coroutine>();

    void IniciarDna(ObjetoInterativo oi, Transform corpo, float? voltas, float? desenrolo)
    {
        if (animDna.TryGetValue(oi, out var c) && c != null) StopCoroutine(c);
        animDna[oi] = StartCoroutine(AnimarDna(oi, corpo, voltas, desenrolo));
    }

    System.Collections.IEnumerator AnimarDna(ObjetoInterativo oi, Transform corpo, float? voltas, float? desenrolo)
    {
        float v0 = oi.extras["voltas"], d0 = oi.extras["desenrolo"];
        float v1 = voltas ?? v0, d1 = desenrolo ?? d0;
        const float dur = 1.2f;
        for (float t = 0; t < dur && oi != null; t += Time.deltaTime)
        {
            float x = Mathf.SmoothStep(0f, 1f, t / dur);
            oi.extras["voltas"] = Mathf.Lerp(v0, v1, x);
            oi.extras["desenrolo"] = Mathf.Lerp(d0, d1, x);
            AtualizarMalhaDna(oi, corpo);
            yield return null;
        }
        if (oi == null) yield break;
        oi.extras["voltas"] = v1; oi.extras["desenrolo"] = d1;
        AtualizarMalhaDna(oi, corpo);
    }

    // mesma topologia, vértices novos: reescreve a posição nas malhas de grade e de preenchimento de cada parte
    static void AtualizarMalhaDna(ObjetoInterativo oi, Transform corpo)
    {
        var nova = Gerar("dna", oi.extras["voltas"], oi.extras["desenrolo"]).vertices;
        foreach (var p in oi.partes)
        {
            if (p == null || p.origem == null) continue;
            var pos = new Vector3[p.origem.Length];
            for (int i = 0; i < pos.Length; i++) pos[i] = nova[p.origem[i]];
            Atualizar(p.GetComponent<MeshFilter>(), pos);
            var f = p.transform.Find("Preenchimento");
            if (f != null) Atualizar(f.GetComponent<MeshFilter>(), pos);
        }
    }

    static void Atualizar(MeshFilter mf, Vector3[] pos)
    {
        if (mf == null || mf.sharedMesh == null) return;
        var m = mf.sharedMesh;
        m.vertices = pos;
        m.RecalculateNormals();
        m.RecalculateBounds();
    }

    public static Mesh Gerar(string forma, float voltas = VoltasPadrao, float desenrolo = 0f)
    {
        Mesh m;
        switch (forma)
        {
            case "mobius": m = Grade(160, 8, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, w = (v - 0.5f) * 1.1f;
                float r = 1f + w * 0.5f * Mathf.Cos(a * 0.5f);
                return new Vector3(r * Mathf.Cos(a), w * 0.5f * Mathf.Sin(a * 0.5f), r * Mathf.Sin(a));
            }, true, false, 6f, 1f); break;
            case "klein": m = Grade(90, 60, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                float r = 2f + Mathf.Cos(a * 0.5f) * Mathf.Sin(b) - Mathf.Sin(a * 0.5f) * Mathf.Sin(2f * b);
                return new Vector3(r * Mathf.Cos(a), Mathf.Sin(a * 0.5f) * Mathf.Sin(b) + Mathf.Cos(a * 0.5f) * Mathf.Sin(2f * b), r * Mathf.Sin(a));
            }, true, true, 10f, 6f); break;
            case "toro": m = Grade(64, 28, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
                float r = 1f + 0.38f * Mathf.Cos(b);
                return new Vector3(r * Mathf.Cos(a), 0.38f * Mathf.Sin(b), r * Mathf.Sin(a));
            }, true, true, 16f, 6f); break;
            case "no_toroidal": m = Tubo(Curva(400, f =>
            {
                float p = 2f, q = 3f, a = f * Mathf.PI * 2f;
                float r = Mathf.Cos(q * a) + 2f;
                return new Vector3(r * Mathf.Cos(p * a), -Mathf.Sin(q * a), r * Mathf.Sin(p * a));
            }), 0.28f, 12, true); break;
            case "dna": m = Dna(voltas, desenrolo); break;
            case "fractal": m = Sierpinski(3); break;
            default: return null;
        }
        Normalizar(m);
        return m;
    }

    static Mesh Grade(int nu, int nv, Func<float, float, Vector3> f, bool fechaU, bool fechaV, float tilingU, float tilingV)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var t = new List<int>();
        for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                float u = i / (float)nu, w = j / (float)nv;
                v.Add(f(u, w));
                uv.Add(new Vector2(u * tilingU, w * tilingV));
            }
        int pitch = nu + 1;
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * pitch + i, b = a + 1, c = a + pitch, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }
        return Montar(v, uv, t);
    }

    static Mesh Montar(List<Vector3> v, List<Vector2> uv, List<int> t)
    {
        var m = new Mesh { name = "FormaMatematica", indexFormat = v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static List<Vector3> Curva(int n, Func<float, Vector3> f)
    {
        var l = new List<Vector3>(n);
        for (int i = 0; i < n; i++) l.Add(f(i / (float)n));
        return l;
    }

    // tubo ao redor de uma curva (transporte paralelo do referencial)
    static Mesh Tubo(List<Vector3> pts, float raio, int lados, bool fechado)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        AdicionarTubo(v, uv, t, pts, raio, lados, fechado, 24f);
        return Montar(v, uv, t);
    }

    static void AdicionarTubo(List<Vector3> v, List<Vector2> uv, List<int> t, List<Vector3> pts, float raio, int lados, bool fechado, float tilingU)
    {
        int n = pts.Count;
        int baseIdx = v.Count;
        var T0 = (pts[1] - pts[0]).normalized;
        var N = Vector3.Cross(T0, Mathf.Abs(T0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        int total = fechado ? n + 1 : n;
        for (int i = 0; i < total; i++)
        {
            var p = pts[i % n];
            var prox = pts[(i + 1) % n];
            var ant = pts[(i - 1 + n) % n];
            var T = fechado || (i > 0 && i < n - 1) ? (prox - ant).normalized : (i == 0 ? (pts[1] - pts[0]) : (pts[n - 1] - pts[n - 2])).normalized;
            N = (N - Vector3.Dot(N, T) * T).normalized;
            var B = Vector3.Cross(T, N);
            for (int k = 0; k <= lados; k++)
            {
                float a = k / (float)lados * Mathf.PI * 2f;
                v.Add(p + (N * Mathf.Cos(a) + B * Mathf.Sin(a)) * raio);
                uv.Add(new Vector2(i / (float)total * tilingU, k / (float)lados * 2f));
            }
        }
        int pitch = lados + 1;
        for (int i = 0; i < total - 1; i++)
            for (int k = 0; k < lados; k++)
            {
                int a = baseIdx + i * pitch + k, b = a + 1, c = a + pitch, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }
    }

    static Mesh Dna(float voltas, float desenrolo)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        const int n = 120;
        const float altura = 3f, raioH = 0.6f;
        var fitaA = new List<Vector3>(); var fitaB = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1), a = f * voltas * Mathf.PI * 2f * (1f - desenrolo);
            float y = (f - 0.5f) * altura;
            fitaA.Add(new Vector3(Mathf.Cos(a) * raioH, y, Mathf.Sin(a) * raioH));
            fitaB.Add(new Vector3(Mathf.Cos(a + Mathf.PI) * raioH, y, Mathf.Sin(a + Mathf.PI) * raioH));
        }
        AdicionarTubo(v, uv, t, fitaA, 0.07f, 8, false, 10f);
        dnaFimA = v.Count;
        AdicionarTubo(v, uv, t, fitaB, 0.07f, 8, false, 10f);
        dnaFimB = v.Count;
        for (int i = 4; i < n - 4; i += 5)   // degraus entre as fitas
            AdicionarTubo(v, uv, t, new List<Vector3> { fitaA[i], Vector3.Lerp(fitaA[i], fitaB[i], 0.5f), fitaB[i] }, 0.035f, 6, false, 2f);
        return Montar(v, uv, t);
    }

    static Mesh Sierpinski(int nivel)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float s = 1f;
        var p0 = new Vector3(1, 1, 1) * s; var p1 = new Vector3(1, -1, -1) * s;
        var p2 = new Vector3(-1, 1, -1) * s; var p3 = new Vector3(-1, -1, 1) * s;
        Tetra(v, uv, t, p0, p1, p2, p3, nivel);
        return Montar(v, uv, t);
    }

    static void Tetra(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int nivel)
    {
        if (nivel == 0)
        {
            AddTri(v, uv, t, a, c, b); AddTri(v, uv, t, a, b, d); AddTri(v, uv, t, a, d, c); AddTri(v, uv, t, b, c, d);
            return;
        }
        Tetra(v, uv, t, a, (a + b) / 2, (a + c) / 2, (a + d) / 2, nivel - 1);
        Tetra(v, uv, t, (a + b) / 2, b, (b + c) / 2, (b + d) / 2, nivel - 1);
        Tetra(v, uv, t, (a + c) / 2, (b + c) / 2, c, (c + d) / 2, nivel - 1);
        Tetra(v, uv, t, (a + d) / 2, (b + d) / 2, (c + d) / 2, d, nivel - 1);
    }

    static void AddTri(List<Vector3> v, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0.5f, 1));
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static void Normalizar(Mesh m)
    {
        var v = m.vertices;
        var b = m.bounds;
        float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maior < 0.0001f) return;
        for (int i = 0; i < v.Length; i++) v[i] = (v[i] - b.center) / maior;
        m.vertices = v;
        m.RecalculateNormals();
        m.RecalculateBounds();
    }
}
