using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Estilo "modelo científico" (visual original): wireframe translúcido azul-ciano com nervuras internas, 2 a 4 traços de luz
// verde/ciano correndo pela superfície (rastro curto), montagem progressiva ao longo da forma, marcadores com rótulos de dados
// (nome, parâmetros reais, % de cálculo até 100%) e painel "modelo concluído". Linhas numa malha só por parte e um material
// de linha por parte; sem pós-processamento.
public class EstiloCientifico : MonoBehaviour
{
    public class Info
    {
        public string nome;
        public string[] parametros;   // fatos reais e curtos
    }

    // dados reais por forma/modelo
    public static readonly Dictionary<string, Info> Infos = new Dictionary<string, Info>
    {
        { "mobius", new Info { nome = "Fita de Möbius", parametros = new[] { "1 face e 1 borda", "Meia volta (180°) na fita", "Característica de Euler: 0" } } },
        { "klein", new Info { nome = "Garrafa de Klein", parametros = new[] { "Sem borda e de 1 lado", "Não cabe em 3D sem se cruzar", "Característica de Euler: 0" } } },
        { "no_toroidal", new Info { nome = "Nó tórico (2,3)", parametros = new[] { "Nó trifólio", "3 cruzamentos", "Dá 2 voltas por um eixo e 3 pelo outro" } } },
        { "toro", new Info { nome = "Toro", parametros = new[] { "Gênero 1 (1 buraco)", "Característica de Euler: 0", "Área = 4π²·R·r" } } },
        { "fractal", new Info { nome = "Tetraedro de Sierpinski", parametros = new[] { "Nível 3: 64 tetraedros", "Dimensão fractal = log 4 / log 2 = 2", "Volume tende a 0 a cada nível" } } },
        { "dna", new Info { nome = "Hélice de DNA", parametros = new[] { "Dupla hélice, ~2 nm de largura", "~10,5 pares de bases por volta", "A-T (2 ligações) e C-G (3 ligações)" } } },
        { "sistema_solar", new Info { nome = "Sistema Solar", parametros = new[] { "8 planetas em órbita do Sol", "Órbitas e escalas ilustrativas", "Terra: 1 volta em 365,25 dias" } } },
        { "molecula_agua", new Info { nome = "Molécula de água (H₂O)", parametros = new[] { "1 oxigênio e 2 hidrogênios", "Ângulo H–O–H ≈ 104,5°", "Comprimento da ligação O–H ≈ 0,096 nm" } } },
        { "globo_terrestre", new Info { nome = "Globo terrestre", parametros = new[] { "Eixo inclinado 23,5°", "Raio médio ≈ 6.371 km", "Uma rotação a cada ~24 h" } } },
    };

    static readonly Color Verde = new Color(0.42f, 1f, 0.69f, 1f);
    const float DuracaoMontagem = 2.4f;

    class Parte { public ParteHolo parte; public Mesh linhas, nervuras; public Material matL, matN; public GameObject goL, goN; public float alfaFill; }

    readonly List<Parte> partes = new List<Parte>();
    readonly List<LineRenderer> tracos = new List<LineRenderer>();
    readonly List<float[]> caminhosAcum = new List<float[]>();
    readonly List<Vector3[]> caminhos = new List<Vector3[]>();
    readonly List<float> cabecas = new List<float>();
    Transform espaco, rotulos;
    TextMeshPro txtNome, txtParam, txtPct;
    LineRenderer[] chamadas;
    Transform[] marcadores;
    Transform painelConcluido;
    Material matTraco, matMarca;
    Info info;
    bool leve;
    float inicio, progresso;
    Vector3 centroLocal, tamanhoLocal;
    int eixo;
    float minEixo, tamEixo;
    bool concluido, semPainel;
    public bool Concluido => concluido;

    public static EstiloCientifico Aplicar(GameObject raiz, Transform espacoMalha, string chave, bool leve = false)
    {
        var existente = raiz.GetComponent<EstiloCientifico>();
        if (existente != null) return existente;
        var e = raiz.AddComponent<EstiloCientifico>();
        e.Iniciar(espacoMalha != null ? espacoMalha : raiz.transform, chave, leve);
        return e;
    }

    public static bool Remover(GameObject raiz)
    {
        var e = raiz.GetComponent<EstiloCientifico>();
        if (e == null) return false;
        e.Desfazer();
        Destroy(e);
        return true;
    }

    void Iniciar(Transform esp, string chave, bool lev)
    {
        espaco = esp;
        leve = lev;
        info = chave != null && Infos.TryGetValue(chave, out var i) ? i : new Info { nome = chave ?? "Modelo", parametros = new string[0] };
        var oi = ObjetoInterativo.Garantir(gameObject);
        inicio = Time.time;
        if (HoloRegistro.Carregando) { inicio = Time.time - DuracaoMontagem * 2f; semPainel = true; }   // objeto recarregado da âncora: sem reanimar

        // caixa do objeto no espaço da malha
        var b = LimitesLocais(esp, oi);
        centroLocal = b.center; tamanhoLocal = b.size;
        eixo = b.size.y >= b.size.x && b.size.y >= b.size.z ? 1 : (b.size.x >= b.size.z ? 0 : 2);
        minEixo = b.min[eixo]; tamEixo = Mathf.Max(0.0001f, b.size[eixo]);

        matTraco = HoloUtil.NovoMaterialSprite(Color.white);
        matMarca = HoloUtil.NovoMaterialSprite(TemaZetta.ComAlfa(Verde, 0.95f));
        if (!leve)
        {
            foreach (var p in oi.partes) CriarLinhas(p, esp);
            CriarTracos(esp);
        }
        CriarRotulos();
        StartCoroutine(Montagem());
    }

    static Bounds LimitesLocais(Transform esp, ObjetoInterativo oi)
    {
        Bounds? bb = null;
        foreach (var p in oi.partes)
        {
            var mf = p.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            // cantos da caixa da malha da parte, passados para o espaço da malha comum
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var l = esp.InverseTransformPoint(p.transform.TransformPoint(c));
                if (bb == null) bb = new Bounds(l, Vector3.zero); else { var x = bb.Value; x.Encapsulate(l); bb = x; }
            }
        }
        return bb ?? new Bounds(Vector3.zero, Vector3.one * 0.2f);
    }

    // ---------- wireframe + nervuras ----------
    void CriarLinhas(ParteHolo p, Transform esp)
    {
        var mf = p.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;
        var src = mf.sharedMesh;
        var v = src.vertices; var t = src.triangles;
        var arestas = new HashSet<long>();
        var lista = new List<int>();
        for (int i = 0; i + 2 < t.Length; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], c = t[i + (k + 1) % 3];
                long chave = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                if (arestas.Add(chave)) { lista.Add(a); lista.Add(c); }
            }
        var idx = SelecionarArestas(lista, p.origem);

        var ml = new Mesh { name = "Linhas_" + p.nome, indexFormat = v.Length > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        ml.vertices = v;
        ml.SetIndices(idx, MeshTopology.Lines, 0);
        ml.colors = CoresReveal(v, p.transform, esp, 0f);
        ml.bounds = src.bounds;

        // nervuras internas: cópia reduzida (55%) e metade das arestas, mais fios ligando alguns vértices à cópia
        var vn = new List<Vector3>(); var idn = new List<int>();
        var centro = centroLocal;
        var fio = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            var mundo = esp.InverseTransformPoint(p.transform.TransformPoint(v[i]));
            var interno = centro + (mundo - centro) * 0.55f;
            vn.Add(p.transform.InverseTransformPoint(esp.TransformPoint(interno)));
        }
        for (int e = 0; e + 1 < idx.Count; e += 4) { idn.Add(idx[e]); idn.Add(idx[e + 1]); }
        int total = v.Length;
        int saltoFio = Mathf.Max(1, total / 36);
        var todos = new List<Vector3>(vn);
        for (int i = 0; i < total; i += saltoFio) { todos.Add(v[i]); idn.Add(todos.Count - 1); idn.Add(i); }
        // os vértices externos usados pelos fios entram depois dos internos
        var mn = new Mesh { name = "Nervuras_" + p.nome, indexFormat = todos.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        mn.vertices = todos.ToArray();
        // reindexa os fios: pares (externo novo, interno i)
        var idn2 = new List<int>();
        for (int e = 0; e + 1 < idn.Count; e += 2) { idn2.Add(idn[e]); idn2.Add(idn[e + 1]); }
        mn.SetIndices(idn2, MeshTopology.Lines, 0);
        var corN = new Color[todos.Count];
        for (int i = 0; i < corN.Length; i++) corN[i] = new Color(1, 1, 1, 0f);
        mn.colors = corN;
        mn.bounds = src.bounds;

        var cor = p.pintada ? p.cor : TemaZetta.AzulTec;
        var entrada = new Parte { parte = p, linhas = ml, nervuras = mn };
        entrada.goL = NovoRenderer(p.transform, "Linhas", ml, entrada.matL = MaterialLinha(cor, 0.85f));
        entrada.goN = NovoRenderer(p.transform, "Nervuras", mn, entrada.matN = MaterialLinha(cor, 0.35f));
        // a parte passa a ser desenhada por linhas: grade sólida desligada e preenchimento bem leve
        p.estiloLinhas = true;
        var rg = p.GetComponent<Renderer>(); if (rg != null) rg.enabled = false;
        var fill = p.transform.Find("Preenchimento");
        if (fill != null)
        {
            var fr = fill.GetComponent<Renderer>();
            if (fr != null && fr.sharedMaterial != null)
            {
                var c = fr.sharedMaterial.color; entrada.alfaFill = c.a; c.a = Mathf.Min(c.a, 0.07f); fr.sharedMaterial.color = c;
            }
        }
        partes.Add(entrada);
    }

    // Malhas em grade (u,v) trazem a diagonal de cada célula: some com ela e afina as linhas restantes para o wireframe não virar uma mancha.
    // O passo da grade (P) vem do histograma da diferença de índices; malhas sem grade só são afinadas por salto.
    static List<int> SelecionarArestas(List<int> pares, int[] origem)
    {
        int n = pares.Count / 2;
        Func<int, int> orig = i => origem != null && i < origem.Length ? origem[i] : i;
        var hist = new Dictionary<int, int>();
        for (int e = 0; e < pares.Count; e += 2)
        {
            int d = Mathf.Abs(orig(pares[e]) - orig(pares[e + 1]));
            hist[d] = hist.TryGetValue(d, out var c) ? c + 1 : 1;
        }
        int P = 0, melhor = 0;
        foreach (var kv in hist) if (kv.Key > 1 && kv.Value > melhor) { melhor = kv.Value; P = kv.Key; }
        hist.TryGetValue(1, out int c1);
        bool grade = P > 2 && c1 > 0.2f * n && melhor > 0.2f * n;
        var saida = new List<int>();
        if (grade)
        {
            int s = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n / 1300f)));
            for (int e = 0; e < pares.Count; e += 2)
            {
                int a = orig(pares[e]), b = orig(pares[e + 1]);
                int d = Mathf.Abs(a - b), menor = Mathf.Min(a, b);
                bool fica;
                if (d == 1) fica = (menor / P) % s == 0;
                else if (d == P) fica = (menor % P) % s == 0;
                else if (d == P - 1) fica = false;   // diagonal da célula
                else fica = true;
                if (fica) { saida.Add(pares[e]); saida.Add(pares[e + 1]); }
            }
            if (saida.Count >= 12) return saida;
            saida.Clear();
        }
        int passo = Mathf.Max(1, n / 2500);
        for (int e = 0; e < pares.Count; e += 2) if ((e / 2) % passo == 0) { saida.Add(pares[e]); saida.Add(pares[e + 1]); }
        return saida;
    }

    static Material MaterialLinha(Color cor, float alfa)
    {
        var m = HoloUtil.NovoMaterialSprite(new Color(cor.r, cor.g, cor.b, alfa));
        return m;
    }

    static GameObject NovoRenderer(Transform pai, string nome, Mesh malha, Material mat)
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

    // alfa por vértice: aparece à medida que a frente de montagem passa pelo eixo maior
    Color[] CoresReveal(Vector3[] v, Transform parte, Transform esp, float frente)
    {
        var c = new Color[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            float h = (esp.InverseTransformPoint(parte.TransformPoint(v[i]))[eixo] - minEixo) / tamEixo;
            c[i] = new Color(1f, 1f, 1f, Mathf.Clamp01((frente - h) / 0.12f));
        }
        return c;
    }

    // ---------- traços de luz ----------
    void CriarTracos(Transform esp)
    {
        var fontes = new List<Parte>(partes);
        if (fontes.Count == 0) return;
        int n = Mathf.Clamp(2 + (int)(tamanhoLocal.magnitude), 2, 4);
        var rnd = new System.Random(7);
        for (int k = 0; k < n; k++)
        {
            var origem = fontes[rnd.Next(fontes.Count)];
            var mf = origem.parte.GetComponent<MeshFilter>();
            var caminho = CaminhoNaSuperficie(mf.sharedMesh, rnd, 260);
            if (caminho == null || caminho.Length < 8) continue;
            for (int i = 0; i < caminho.Length; i++) caminho[i] = esp.InverseTransformPoint(origem.parte.transform.TransformPoint(caminho[i]));
            var acum = new float[caminho.Length];
            for (int i = 1; i < caminho.Length; i++) acum[i] = acum[i - 1] + Vector3.Distance(caminho[i], caminho[i - 1]);
            var go = new GameObject("Traco" + k);
            go.transform.SetParent(esp, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = matTraco;
            lr.positionCount = 14;
            lr.widthMultiplier = 0.012f * Mathf.Max(0.3f, tamanhoLocal.magnitude);
            lr.numCapVertices = 2;
            var grad = new Gradient();
            var cor = k % 2 == 0 ? Verde : TemaZetta.Ciano;
            grad.SetKeys(new[] { new GradientColorKey(cor, 0f), new GradientColorKey(cor, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 1f) });
            lr.colorGradient = grad;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 1f));
            lr.enabled = false;
            tracos.Add(lr); caminhos.Add(caminho); caminhosAcum.Add(acum);
            cabecas.Add((float)rnd.NextDouble() * acum[acum.Length - 1]);
        }
    }

    // caminho aleatório pela malha: segue arestas preferindo manter a direção
    static Vector3[] CaminhoNaSuperficie(Mesh m, System.Random rnd, int passos)
    {
        if (m == null || !m.isReadable) return null;
        var v = m.vertices; var t = m.triangles;
        var viz = new List<int>[v.Length];
        for (int i = 0; i + 2 < t.Length; i += 3)
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + (k + 1) % 3];
                (viz[a] ??= new List<int>()).Add(b);
                (viz[b] ??= new List<int>()).Add(a);
            }
        int atual = rnd.Next(v.Length);
        int guarda = 0;
        while ((viz[atual] == null || viz[atual].Count == 0) && guarda++ < 50) atual = rnd.Next(v.Length);
        var pts = new List<Vector3> { v[atual] };
        var dir = Vector3.zero;
        int anterior = -1;
        for (int s = 0; s < passos; s++)
        {
            var vs = viz[atual];
            if (vs == null || vs.Count == 0) break;
            int melhor = -1; float nota = float.MinValue;
            foreach (int c in vs)
            {
                if (c == anterior) continue;
                var d = (v[c] - v[atual]).normalized;
                float x = (dir == Vector3.zero ? 0f : Vector3.Dot(d, dir)) + (float)rnd.NextDouble() * 0.5f;
                if (x > nota) { nota = x; melhor = c; }
            }
            if (melhor < 0) break;
            dir = (v[melhor] - v[atual]).normalized;
            anterior = atual; atual = melhor;
            pts.Add(v[atual]);
        }
        return pts.ToArray();
    }

    // ---------- rótulos, marcadores e painel ----------
    void CriarRotulos()
    {
        rotulos = new GameObject("RotulosDados").transform;
        rotulos.SetParent(transform, false);
        rotulos.gameObject.AddComponent<ExcluirDosLimites>();
        // posições no espaço local da raiz; o tamanho é compensado a cada quadro para ficar legível
        var altura = ExtensaoRaiz();
        txtNome = HoloUtil.CriarTexto(rotulos, "Nome", info.nome, new Vector3(0f, altura.y * 0.5f + 0.06f, 0f), new Vector2(0.5f, 0.045f), 0.3f, TemaZetta.Branco);
        txtNome.fontStyle = FontStyles.Bold;
        txtParam = HoloUtil.CriarTexto(rotulos, "Parametros", string.Join("\n", info.parametros), new Vector3(altura.x * 0.5f + 0.2f, 0f, 0f), new Vector2(0.36f, 0.1f), 0.2f, TemaZetta.Ciano, TextAlignmentOptions.Left);
        txtParam.textWrappingMode = TextWrappingModes.Normal;
        txtPct = HoloUtil.CriarTexto(rotulos, "Calculo", "calculando 0%", new Vector3(0f, -altura.y * 0.5f - 0.05f, 0f), new Vector2(0.4f, 0.03f), 0.22f, TemaZetta.ComAlfa(Verde, 1f));

        // três marcadores (losangos) nos cantos da caixa + linhas de chamada até os rótulos
        marcadores = new Transform[3];
        chamadas = new LineRenderer[3];
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "Marcador" + i;
            q.transform.SetParent(rotulos, false);
            q.GetComponent<Renderer>().sharedMaterial = matMarca;
            q.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemTexto;
            marcadores[i] = q.transform;
            var lo = new GameObject("Chamada" + i);
            lo.transform.SetParent(rotulos, false);
            var lr = lo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = matTraco;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.0012f;
            lr.startColor = lr.endColor = TemaZetta.ComAlfa(Verde, 0.7f);
            chamadas[i] = lr;
        }
        rotulos.gameObject.SetActive(true);
    }

    Vector2 ExtensaoRaiz()
    {
        var s = transform.lossyScale;
        float k = Mathf.Max(0.0001f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
        float maior = InteracaoMaos.MaiorDimensao(gameObject) / k;
        return new Vector2(maior, maior);
    }

    void LateUpdate()
    {
        if (rotulos == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var s = transform.lossyScale;
        float k = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
        // o objeto pode ser escalado: os rótulos ficam do mesmo tamanho no mundo e afastam-se pelo tamanho atual
        rotulos.localScale = Vector3.one / k;
        var bounds = new Bounds(); InteracaoMaos.Limites(gameObject, out bounds);
        Vector3 c = bounds.center;
        var olhar = Quaternion.LookRotation(rotulos.position - cam.transform.position, Vector3.up);

        Posicionar(txtNome.transform, c + Vector3.up * (bounds.extents.y + 0.07f), olhar);
        var direita = cam.transform.right; direita.y = 0f; direita.Normalize();
        Posicionar(txtParam.transform, c + direita * (bounds.extents.magnitude * 0.75f + 0.2f), olhar);
        Posicionar(txtPct.transform, c - Vector3.up * (bounds.extents.y + 0.06f), olhar);

        // marcadores: três pontos da caixa visível, ligados por linha ao rótulo mais próximo
        var alvos = new[] { txtNome.transform.position, txtParam.transform.position - direita * 0.12f, txtPct.transform.position };
        var pontos = new[]
        {
            c + new Vector3(0f, bounds.extents.y, 0f),
            c + direita * bounds.extents.x * 0.9f + Vector3.up * bounds.extents.y * 0.2f,
            c - new Vector3(0f, bounds.extents.y, 0f)
        };
        float vis = Mathf.Clamp01(progresso * 1.5f);
        for (int i = 0; i < 3; i++)
        {
            marcadores[i].position = pontos[i];
            marcadores[i].rotation = olhar * Quaternion.Euler(0, 0, 45f);
            marcadores[i].localScale = Vector3.one * (0.010f + 0.003f * Mathf.Sin(Time.time * 4f + i)) * vis;
            chamadas[i].SetPosition(0, pontos[i]);
            chamadas[i].SetPosition(1, Vector3.Lerp(pontos[i], alvos[i], vis));
            chamadas[i].enabled = vis > 0.01f;
        }
        txtNome.alpha = vis; txtParam.alpha = vis;

        if (painelConcluido != null)
        {
            painelConcluido.position = c + (-direita) * (bounds.extents.magnitude * 0.75f + 0.22f) + Vector3.up * 0.02f;
            painelConcluido.rotation = olhar;
            painelConcluido.localScale = Vector3.one * painelEscala;
        }
    }

    float painelEscala;

    void Posicionar(Transform t, Vector3 mundo, Quaternion rot)
    {
        t.position = mundo;
        t.rotation = rot;
    }

    // ---------- animação ----------
    IEnumerator Montagem()
    {
        var oi = GetComponent<ObjetoInterativo>();
        while (progresso < 1f)
        {
            progresso = Mathf.Clamp01((Time.time - inicio) / DuracaoMontagem);
            float frente = Mathf.SmoothStep(0f, 1f, progresso) * 1.2f;
            foreach (var p in partes)
            {
                if (p.parte == null || p.linhas == null) continue;
                AtualizarReveal(p, frente);
            }
            if (txtPct != null) txtPct.text = "calculando " + Mathf.RoundToInt(progresso * 100f) + "%";
            yield return null;
        }
        foreach (var p in partes) if (p.parte != null) AtualizarReveal(p, 1.3f);
        if (txtPct != null) txtPct.text = "cálculo 100%";
        concluido = true;
        foreach (var lr in tracos) lr.enabled = true;
        if (!semPainel) yield return StartCoroutine(PainelConcluido());
    }

    void AtualizarReveal(Parte p, float frente)
    {
        var v = p.linhas.vertices;
        p.linhas.colors = CoresReveal(v, p.parte.transform, espaco, frente);
        var cn = new Color[p.nervuras.vertexCount];
        var vn = p.nervuras.vertices;
        for (int i = 0; i < cn.Length; i++)
        {
            float h = (espaco.InverseTransformPoint(p.parte.transform.TransformPoint(vn[i]))[eixo] - minEixo) / tamEixo;
            cn[i] = new Color(1f, 1f, 1f, Mathf.Clamp01((frente - h - 0.1f) / 0.12f));
        }
        p.nervuras.colors = cn;
    }

    IEnumerator PainelConcluido()
    {
        var pai = rotulos;
        var tam = new Vector2(0.26f, 0.12f);
        painelConcluido = HoloUtil.CriarPainel(pai, "ModeloConcluido", tam);
        var vidro = painelConcluido.GetComponent<PainelVidro>();
        vidro.DefinirBorda(Verde, 1.4f);
        var cab = HoloUtil.CriarTexto(painelConcluido, "Titulo", "MODELO CONCLUÍDO", new Vector3(0f, 0.032f, TemaZetta.ProfundidadeTexto), new Vector2(0.24f, 0.03f), 0.2f, Verde);
        cab.fontStyle = FontStyles.Bold;
        HoloUtil.CriarTexto(painelConcluido, "Nome", info.nome, new Vector3(0f, 0.004f, TemaZetta.ProfundidadeTexto), new Vector2(0.24f, 0.03f), 0.2f, TemaZetta.Branco);
        HoloUtil.CriarTexto(painelConcluido, "Dados", info.parametros.Length > 0 ? info.parametros[0] : "cálculo 100%", new Vector3(0f, -0.03f, TemaZetta.ProfundidadeTexto), new Vector2(0.24f, 0.04f), 0.15f, TemaZetta.Ciano);
        for (float t = 0; t < 0.4f; t += Time.deltaTime) { painelEscala = TemaZetta.EaseOutBack(t / 0.4f); yield return null; }
        painelEscala = 1f;
        yield return new WaitForSeconds(9f);
        for (float t = 0; t < 0.3f; t += Time.deltaTime) { painelEscala = 1f - t / 0.3f; yield return null; }
        if (painelConcluido != null) Destroy(painelConcluido.gameObject);
        painelConcluido = null;
    }

    void Update()
    {
        if (!concluido) return;
        for (int i = 0; i < tracos.Count; i++)
        {
            var lr = tracos[i];
            var acum = caminhosAcum[i]; var pts = caminhos[i];
            float total = acum[acum.Length - 1];
            cabecas[i] = Mathf.Repeat(cabecas[i] + Time.deltaTime * 0.55f * Mathf.Max(0.3f, tamanhoLocal.magnitude), total);
            for (int k = 0; k < lr.positionCount; k++)
                lr.SetPosition(k, Amostrar(pts, acum, Mathf.Repeat(cabecas[i] - (lr.positionCount - 1 - k) * 0.02f * Mathf.Max(0.3f, tamanhoLocal.magnitude), total)));
        }
    }

    static Vector3 Amostrar(Vector3[] pts, float[] acum, float s)
    {
        int lo = 0, hi = acum.Length - 1;
        while (lo < hi - 1) { int mid = (lo + hi) / 2; if (acum[mid] <= s) lo = mid; else hi = mid; }
        float seg = Mathf.Max(0.00001f, acum[hi] - acum[lo]);
        return Vector3.Lerp(pts[lo], pts[hi], (s - acum[lo]) / seg);
    }

    // volta ao visual de grade e preenchimento
    void Desfazer()
    {
        StopAllCoroutines();
        foreach (var p in partes)
        {
            if (p.goL != null) Destroy(p.goL);
            if (p.goN != null) Destroy(p.goN);
            if (p.parte == null) continue;
            p.parte.estiloLinhas = false;
            var rg = p.parte.GetComponent<Renderer>(); if (rg != null && !p.parte.oculta) rg.enabled = true;
            var fill = p.parte.transform.Find("Preenchimento");
            if (fill != null && p.alfaFill > 0f)
            {
                var fr = fill.GetComponent<Renderer>();
                if (fr != null && fr.sharedMaterial != null) { var c = fr.sharedMaterial.color; c.a = p.alfaFill; fr.sharedMaterial.color = c; }
            }
        }
        foreach (var lr in tracos) if (lr != null) Destroy(lr.gameObject);
        if (rotulos != null) Destroy(rotulos.gameObject);
    }

    void OnDestroy()
    {
        if (rotulos != null) Destroy(rotulos.gameObject);
    }
}

// "Zetta, modo estrutura": liga/desliga o estilo de modelo científico no objeto olhado (ou citado).
public class ModoEstrutura : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""modo_estrutura"",
  ""description"": ""Liga ou desliga o modo estrutura (modelo científico: wireframe azul-ciano translúcido com nervuras internas, traços de luz percorrendo a superfície, rótulos de dados e painel 'modelo concluído') no holograma que o usuário olha ou cita."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""ligar"", ""desligar""] },
      ""objeto"": { ""type"": ""STRING"", ""description"": ""Descrição do objeto; vazio = o que o usuário olha."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'modo estrutura' / 'mostre a estrutura disso' use modo_estrutura com acao=ligar; para 'volte ao normal' acao=desligar. Comente em uma frase.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    public JObject Executar(JObject a)
    {
        var item = ObjetoInterativo.Alvo(a, out string erro);
        if (item == null || item.go == null) return new JObject { ["erro"] = erro ?? "objeto não encontrado" };
        bool ligar = (a.Value<string>("acao") ?? "ligar").ToLowerInvariant().StartsWith("l");
        if (!ligar) return new JObject { ["result"] = EstiloCientifico.Remover(item.go) ? "modo estrutura desligado" : "já estava no visual normal" };
        var oi = ObjetoInterativo.Garantir(item.go);
        if (oi.partes.Count == 0) return new JObject { ["erro"] = "esse objeto ainda não tem partes (pode estar carregando)" };
        var corpo = item.go.transform.Find("Corpo");
        string chave = item.tipo == "forma" || item.tipo == "modelo" ? item.extra : null;
        if (chave == null) chave = item.forma;
        EstiloCientifico.Aplicar(item.go, corpo, chave);
        return new JObject { ["result"] = "modo estrutura ligado em " + item.descricao };
    }
}
