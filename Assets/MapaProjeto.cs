using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// "Zetta, mostre o mapa do projeto": cada script vira um bloco de luz numa constelação 3D; fios ligam cada script aos que ele usa
// (dependências geradas no Editor por ExportarCodigoZetta, em Resources/Codigo/_mapa.txt). Tocar num bloco abre o código dele.
// Custo: 1 material por categoria (5), todos os fios numa única malha de linhas, rótulo único que segue o bloco apontado.
public class MapaProjeto : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""mostrar_mapa_projeto"",
  ""description"": ""Mostra ou esconde o mapa 3D do projeto: cada script é um bloco de luz, ligado por fios aos scripts que usa. Com 'parte', destaca esse script e abre o código dele."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] },
      ""parte"": { ""type"": ""STRING"", ""description"": ""Opcional: script a destacar, ex.: central, esfera."" }
    }
  }
}";
    const string Instrucao = "'mostre o mapa do projeto/do código' → mostrar_mapa_projeto acao=abrir; 'feche o mapa' → acao=fechar. Explique em UMA frase que cada bloco é um script e tocar abre o código.";

    class No
    {
        public string nome; public int linhas; public Vector3 pos; public Transform t; public int cat;
        public readonly List<int> usa = new List<int>(), usadoPor = new List<int>();
    }

    AssistenteVoz voz;
    Transform raiz, nuvem;
    TextMeshPro rotulo;
    PainelVidro vidroRotulo;
    readonly List<No> nos = new List<No>();
    readonly List<Vector2Int> arestas = new List<Vector2Int>();
    Mesh malhaFios, malhaRealce;
    MeshFilter realceFilter;
    Material[] matCat;
    Material matFio, matRealce;
    int hover = -1, selecionado = -1;
    float tocouEm = -10f;
    XROrigin origem;
    public bool Aberto => raiz != null;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "mapa fechado" }; }
        if (!Abrir()) return new JObject { ["erro"] = "o mapa do projeto não está empacotado neste build" };
        string parte = a.Value<string>("parte");
        if (!string.IsNullOrWhiteSpace(parte))
        {
            string nome = CodigoHolo.Achar(parte);
            int i = nos.FindIndex(n => n.nome == nome);
            if (i >= 0) { Selecionar(i); return new JObject { ["result"] = "mapa aberto, destacando " + nome }; }
        }
        return new JObject { ["result"] = "mapa do projeto aberto: " + nos.Count + " scripts e " + arestas.Count + " ligações" };
    }

    // ---------------------------------------------------------------- dados e layout
    bool Carregar()
    {
        nos.Clear(); arestas.Clear();
        var ta = Resources.Load<TextAsset>("Codigo/_mapa");
        if (ta == null) return false;
        var j = JObject.Parse(ta.text);
        var indice = new Dictionary<string, int>();
        foreach (var n in (JArray)j["nos"])
        {
            string nome = n.Value<string>("n");
            indice[nome] = nos.Count;
            nos.Add(new No { nome = nome, linhas = n.Value<int>("l"), cat = Categoria(nome) });
        }
        foreach (var e in (JArray)j["arestas"])
        {
            if (!indice.TryGetValue(e[0].ToString(), out int de) || !indice.TryGetValue(e[1].ToString(), out int para)) continue;
            arestas.Add(new Vector2Int(de, para));
            nos[de].usa.Add(para); nos[para].usadoPor.Add(de);
        }
        Organizar();
        return nos.Count > 0;
    }

    static int Categoria(string n)
    {
        if (n.StartsWith("Assistente") || n.StartsWith("Gradio") || n.StartsWith("Gerador") || n.StartsWith("Camera") || n.StartsWith("Visao")) return 0;   // voz e IA
        if (n.StartsWith("Esfera") || n.StartsWith("Anel") || n.StartsWith("Nucleo") || n.StartsWith("Estado")) return 1;                                  // esfera
        if (n.StartsWith("Central") || n.StartsWith("Painel") || n.StartsWith("Botao") || n.StartsWith("Tema") || n.StartsWith("Icones") || n.StartsWith("Holo")) return 2;   // interface
        if (n.StartsWith("Forma") || n.StartsWith("Criador") || n.StartsWith("Modelo") || n.StartsWith("Biblioteca") || n.StartsWith("Vista") || n.StartsWith("Grafico")) return 3;   // criação
        return 4;
    }

    // layout de forças em 3D (determinístico): repulsão entre todos, mola nas ligações, esfera como ponto de partida
    void Organizar()
    {
        int n = nos.Count;
        const float golden = 2.39996323f;
        for (int i = 0; i < n; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / n, r = Mathf.Sqrt(1f - y * y), ang = golden * i;
            nos[i].pos = new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
        }
        var forca = new Vector3[n];
        float temp = 0.25f;
        for (int it = 0; it < 90; it++)
        {
            for (int i = 0; i < n; i++) forca[i] = Vector3.zero;
            for (int i = 0; i < n; i++)
                for (int k = i + 1; k < n; k++)
                {
                    var d = nos[i].pos - nos[k].pos;
                    float m = Mathf.Max(0.05f, d.magnitude);
                    var f = d / m * (0.08f / (m * m));
                    forca[i] += f; forca[k] -= f;
                }
            foreach (var e in arestas)
            {
                var d = nos[e.y].pos - nos[e.x].pos;
                float m = d.magnitude;
                var f = d * (m * 0.18f);
                forca[e.x] += f; forca[e.y] -= f;
            }
            for (int i = 0; i < n; i++)
            {
                forca[i] -= nos[i].pos * 0.35f;   // gravidade para o centro
                if (forca[i].magnitude > temp) forca[i] = forca[i].normalized * temp;
                nos[i].pos += forca[i] * 0.1f;
            }
            temp *= 0.97f;
        }
        // normaliza para caber numa esfera de 0,5 m
        float maior = 0.001f;
        foreach (var no in nos) maior = Mathf.Max(maior, no.pos.magnitude);
        foreach (var no in nos) no.pos = no.pos / maior * 0.5f;
    }

    // ---------------------------------------------------------------- montagem
    public bool Abrir()
    {
        if (raiz != null) return true;
        if (!Carregar()) return false;
        if (CentralZetta.Instancia != null) CentralZetta.Instancia.Fechar("mapa");
        raiz = new GameObject("MapaProjeto").transform;
        raiz.SetParent(transform, false);
        raiz.position = HoloUtil.PontoNaVista(new Vector3(0f, 0.05f, 1.35f));
        nuvem = new GameObject("Nuvem").transform;
        nuvem.SetParent(raiz, false);

        var cores = new[] { TemaZetta.Ciano, TemaZetta.Dourado, TemaZetta.AzulTec, TemaZetta.Roxo, Color.Lerp(TemaZetta.Branco, TemaZetta.AzulTec, 0.4f) };
        matCat = new Material[cores.Length];
        for (int i = 0; i < cores.Length; i++) matCat[i] = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(cores[i], 0.95f) };
        matFio = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        matRealce = new Material(Shader.Find("Sprites/Default")) { color = Color.white };

        var cubo = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        foreach (var no in nos)
        {
            var go = new GameObject("Bloco_" + no.nome);
            go.transform.SetParent(nuvem, false);
            go.transform.localPosition = no.pos;
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.012f, 0.05f, Mathf.Clamp01(Mathf.Sqrt(no.linhas) / 24f));
            go.transform.localRotation = Quaternion.Euler(35f, 45f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = cubo;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matCat[no.cat];
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            no.t = go.transform;
        }

        // todos os fios numa só malha de linhas (cores por vértice: forte na origem, fraco no destino)
        var vs = new List<Vector3>(); var cs = new List<Color>(); var idx = new List<int>();
        var fraco = TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.16f);
        var forte = TemaZetta.ComAlfa(TemaZetta.Ciano, 0.38f);
        foreach (var e in arestas)
        {
            idx.Add(vs.Count); idx.Add(vs.Count + 1);
            vs.Add(nos[e.x].pos); cs.Add(forte);
            vs.Add(nos[e.y].pos); cs.Add(fraco);
        }
        malhaFios = new Mesh { name = "FiosMapa" };
        malhaFios.SetVertices(vs); malhaFios.SetColors(cs); malhaFios.SetIndices(idx, MeshTopology.Lines, 0);
        var fios = new GameObject("Fios");
        fios.transform.SetParent(nuvem, false);
        fios.AddComponent<MeshFilter>().sharedMesh = malhaFios;
        fios.AddComponent<MeshRenderer>().sharedMaterial = matFio;

        malhaRealce = new Mesh { name = "RealceMapa" };
        var re = new GameObject("Realce");
        re.transform.SetParent(nuvem, false);
        realceFilter = re.AddComponent<MeshFilter>();
        realceFilter.sharedMesh = malhaRealce;
        re.AddComponent<MeshRenderer>().sharedMaterial = matRealce;

        var painelRot = HoloUtil.CriarPainel(raiz, "Rotulo", new Vector2(0.4f, 0.1f));
        painelRot.localPosition = new Vector3(0, -0.62f, 0);
        vidroRotulo = painelRot.GetComponent<PainelVidro>();
        rotulo = HoloUtil.CriarTexto(painelRot, "Texto", "Toque num bloco", new Vector3(0, 0, TemaZetta.ProfundidadeTexto), new Vector2(0.37f, 0.08f), 0.3f, TemaZetta.Branco);
        rotulo.fontSizeMin = 0.18f;
        BotaoHolo.Criar(raiz, "FECHAR", new Vector3(0.34f, -0.62f, 0), new Vector2(0.14f, 0.07f), Fechar);

        hover = selecionado = -1;
        StartCoroutine(Entrada());
        SonsZetta.Tocar(SonsZetta.Abrir);
        return true;
    }

    System.Collections.IEnumerator Entrada()
    {
        for (float t = 0f; t < 0.9f; t += Time.deltaTime)
        {
            if (nuvem == null) yield break;
            float k = 1f - Mathf.Pow(1f - t / 0.9f, 3f);
            nuvem.localScale = Vector3.one * Mathf.Max(0.01f, k);
            yield return null;
        }
        if (nuvem != null) nuvem.localScale = Vector3.one;
    }

    public void Fechar()
    {
        if (raiz == null) return;
        Destroy(raiz.gameObject);
        raiz = null;
        foreach (var m in matCat ?? new Material[0]) Destroy(m);
        if (matFio != null) Destroy(matFio);
        if (matRealce != null) Destroy(matRealce);
        if (malhaFios != null) Destroy(malhaFios);
        if (malhaRealce != null) Destroy(malhaRealce);
        nos.Clear(); arestas.Clear();
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    void OnDestroy() { Fechar(); }

    // ---------------------------------------------------------------- interação
    void Selecionar(int i)
    {
        selecionado = i;
        RealcarFios(i);
        var n = nos[i];
        rotulo.text = n.nome + "\n<size=70%>" + n.linhas + " linhas · usa " + n.usa.Count + " · usado por " + n.usadoPor.Count + "</size>";
        vidroRotulo.DefinirBorda(TemaZetta.Ciano, 1.4f);
        if (CodigoHolo.Instancia != null) CodigoHolo.Instancia.Abrir(n.nome, out _);
    }

    void RealcarFios(int i)
    {
        malhaRealce.Clear();
        if (i < 0) return;
        var vs = new List<Vector3>(); var cs = new List<Color>(); var idx = new List<int>();
        foreach (var e in arestas)
        {
            if (e.x != i && e.y != i) continue;
            bool saida = e.x == i;   // dourado: o que ele usa; ciano: quem o usa
            var c = saida ? TemaZetta.ComAlfa(TemaZetta.Dourado, 0.95f) : TemaZetta.ComAlfa(TemaZetta.Ciano, 0.95f);
            idx.Add(vs.Count); idx.Add(vs.Count + 1);
            vs.Add(nos[e.x].pos); cs.Add(c);
            vs.Add(nos[e.y].pos); cs.Add(c);
        }
        malhaRealce.SetVertices(vs); malhaRealce.SetColors(cs); malhaRealce.SetIndices(idx, MeshTopology.Lines, 0);
    }

    void Update()
    {
        if (raiz == null || Camera.main == null) return;
        // volta-se para o usuário no eixo vertical e a constelação gira devagar (para ao apontar um bloco)
        raiz.rotation = HoloUtil.OlharParaUsuario(raiz.position);
        nuvem.Rotate(0f, (hover >= 0 ? 0f : 7f) * Time.deltaTime, 0f, Space.Self);
        for (int i = 0; i < nos.Count; i++)
        {
            float alvo = i == selecionado ? 1.7f : i == hover ? 1.35f : 1f;
            var t = nos[i].t;
            float baseTam = Mathf.Lerp(0.012f, 0.05f, Mathf.Clamp01(Mathf.Sqrt(nos[i].linhas) / 24f));
            t.localScale = Vector3.Lerp(t.localScale, Vector3.one * baseTam * alvo, Time.deltaTime * 12f);
        }

        // ponta do indicador
        var maos = BotaoHolo.MaosAtivas();
        int perto = -1; float melhor = 0.045f; bool encostou = false;
        if (maos != null)
        {
            if (origem == null) origem = FindAnyObjectByType<XROrigin>();
            if (origem != null)
                foreach (var mao in new[] { maos.rightHand, maos.leftHand })
                {
                    if (!mao.isTracked || !mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var pose)) continue;
                    var mundo = origem.Origin.transform.TransformPoint(pose.position);
                    for (int i = 0; i < nos.Count; i++)
                    {
                        float d = Vector3.Distance(mundo, nos[i].t.position);
                        if (d < melhor) { melhor = d; perto = i; encostou = d < 0.022f + nos[i].t.localScale.x * 0.5f; }
                    }
                }
        }
        if (perto != hover)
        {
            hover = perto;
            if (hover >= 0 && selecionado < 0) rotulo.text = nos[hover].nome;
        }
        if (hover >= 0 && encostou && Time.time - tocouEm > 0.8f)
        {
            tocouEm = Time.time;
            SonsZetta.Tocar(SonsZetta.Clique);
            Selecionar(hover);
        }
    }
}
