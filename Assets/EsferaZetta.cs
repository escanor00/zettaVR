using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Esfera holográfica da Zetta (visual original): 10 anéis/arcos finos em eixos próprios (giroscópio), fragmentos de "dados"
// piscando na superfície, faíscas com rastro curto percorrendo as órbitas e um núcleo em espiral.
// Custo: 1 material compartilhado (instancing), 3 malhas de anel compartilhadas + 2 malhas (fragmentos e partículas),
// ~190 quads no total, sem pós-processamento. Estados vêm do AssistenteVoz.
public class EsferaZetta : MonoBehaviour
{
    public AssistenteVoz voz;
    public bool medirCusto = true;
    public float custoMs { get; private set; }   // CPU média de Update (ms)

    // ---- cores configuráveis ----
    struct Paleta { public Color nucleo, borda; }
    static readonly Dictionary<string, Paleta> Paletas = new Dictionary<string, Paleta>
    {
        { "dourado",  new Paleta { nucleo = new Color(1f, 0.95f, 0.78f), borda = new Color(1f, 0.45f, 0.04f) } },
        { "ciano",    new Paleta { nucleo = new Color(0.85f, 1f, 1f),    borda = new Color(0.1f, 0.7f, 1f) } },
        { "verde",    new Paleta { nucleo = new Color(0.85f, 1f, 0.85f), borda = new Color(0.1f, 0.9f, 0.3f) } },
        { "vermelho", new Paleta { nucleo = new Color(1f, 0.85f, 0.8f),  borda = new Color(1f, 0.12f, 0.1f) } },
        { "roxo",     new Paleta { nucleo = new Color(0.95f, 0.85f, 1f), borda = new Color(0.55f, 0.2f, 1f) } },
        { "azul",     new Paleta { nucleo = new Color(0.85f, 0.92f, 1f), borda = new Color(0.12f, 0.3f, 1f) } },
        { "laranja",  new Paleta { nucleo = new Color(1f, 0.9f, 0.7f),   borda = new Color(1f, 0.3f, 0.02f) } },
        { "rosa",     new Paleta { nucleo = new Color(1f, 0.9f, 0.95f),  borda = new Color(1f, 0.25f, 0.65f) } },
        { "branco",   new Paleta { nucleo = Color.white,                 borda = new Color(0.75f, 0.85f, 1f) } },
    };
    const string ChaveCor = "zetta_cor_esfera";
    string corAtual = "dourado";
    Color nucleoAtual, bordaAtual;

    const string Decl = @"{
  ""name"": ""mudar_cor_esfera"",
  ""description"": ""Muda a cor da esfera holográfica da Zetta (ela mesma). A escolha fica salva."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""cor"": { ""type"": ""STRING"", ""enum"": [""dourado"", ""ciano"", ""verde"", ""vermelho"", ""roxo"", ""azul"", ""laranja"", ""rosa"", ""branco""] } },
    ""required"": [""cor""]
  }
}";
    const string Instrucao = "'Zetta, mude sua cor para X' → mudar_cor_esfera (dourado, ciano, verde, vermelho, roxo, azul, laranja, rosa, branco); confirme em poucas palavras.";

    // ---- anéis ----
    const int NAneis = 14, NFaiscas = 40, Trilha = 3, NEspiral = 40, NFragmentos = 90;
    class Anel
    {
        public Transform t; public float raio, raioAtual; public Vector3 eixo; public float vel, velPropria, ang, angPropria;
        public Quaternion q0, q;
    }
    class Faisca { public int anel; public float theta, vel, tamanho; }
    // tipo: 0 contínuo, 1 tracejado, 2 arco
    static readonly int[] Tipos = { 2, 1, 0, 2, 1, 0, 2, 0, 1, 2, 3, 3, 2, 0 };
    static readonly float[] Raios = { 1.00f, 0.94f, 0.88f, 0.82f, 0.76f, 0.70f, 0.62f, 0.55f, 0.48f, 0.40f, 1.10f, 0.66f, 0.34f, 0.30f };

    readonly List<Anel> aneis = new List<Anel>();
    readonly List<Faisca> faiscas = new List<Faisca>();
    Material mat;
    Mesh[] malhasAnel = new Mesh[4];
    Mesh malhaParticulas, malhaFragmentos;
    Transform fragmentos;
    Vector3[] verts; Color[] cores; Vector2[] uvs; int[] tris;
    int quadsParticulas;

    // ---- estado ----
    float giro = 0.3f, brilho = 0.35f, contracao = 1f, pulso, rajada, volAnterior, erroAte = -1f, tempoEspiral;
    string ultimoErroVisto = "";
    float ultimaVozUsuario = -99f;
    int quadros; float somaMs;
    System.Diagnostics.Stopwatch cron = new System.Diagnostics.Stopwatch();

    void Awake()
    {
        corAtual = PlayerPrefs.GetString(ChaveCor, "dourado");
        if (!Paletas.ContainsKey(corAtual)) corAtual = "dourado";
        nucleoAtual = Nucleo(Paletas[corAtual]); bordaAtual = Paletas[corAtual].borda;
        if (voz == null) voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, MudarCor, Instrucao);
    }

    // chama/núcleo na mesma cor da esfera (só um pouco mais clara), nunca branca
    static Color Nucleo(Paleta p) { return Color.Lerp(p.borda, Color.white, 0.22f); }

    // rajada curta de faíscas (ex.: ao abrir a central), sem mexer no estado da esfera
    public void Realcar() { rajada = Mathf.Max(rajada, 0.7f); }

    public JObject MudarCor(JObject args)
    {
        string c = ApagadorHolo.Normalizar(args.Value<string>("cor") ?? "").Trim();
        if (c == "amarelo" || c == "ambar" || c == "dourada") c = "dourado";
        if (c == "vermelha") c = "vermelho";
        if (c == "roxa" || c == "violeta" || c == "lilas") c = "roxo";
        if (c == "azul-claro") c = "ciano";
        if (!Paletas.ContainsKey(c)) return new JObject { ["erro"] = "cor inválida; use dourado, ciano, verde, vermelho, roxo, azul, laranja, rosa ou branco" };
        corAtual = c;
        PlayerPrefs.SetString(ChaveCor, c);
        PlayerPrefs.Save();
        return new JObject { ["result"] = "cor da esfera mudada para " + c };
    }

    // ------------------------------------------------------------------ construção
    void Start()
    {
        var shader = Resources.Load<Shader>("EsferaAditiva");
        if (shader == null) { Debug.LogWarning("EsferaZetta: shader EsferaAditiva não encontrado"); shader = Shader.Find("Sprites/Default"); }
        mat = new Material(shader) { enableInstancing = true, mainTexture = TexturaRadial(64) };
        mat.renderQueue = 3100;

        malhasAnel[0] = CriarFita(0, 72, 0f, Mathf.PI * 2f, 0.022f, 0.8f, 0.8f);
        malhasAnel[1] = CriarFita(1, 48, 0f, Mathf.PI * 2f, 0.03f, 1f, 1f);
        malhasAnel[2] = CriarFita(2, 40, 0f, 2.4f, 0.04f, 0.1f, 1f);
        malhasAnel[3] = CriarMarcas(96);

        var rnd = new System.Random(7);
        for (int i = 0; i < NAneis; i++)
        {
            var go = new GameObject("Anel" + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = malhasAnel[Tipos[i]];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var eixo = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
            float sinal = i % 2 == 0 ? 1f : -1f;
            aneis.Add(new Anel
            {
                t = go.transform, raio = Raios[i], raioAtual = Raios[i], eixo = eixo,
                vel = sinal * (14f + 9f * (float)rnd.NextDouble() * (1 + i % 3)),
                velPropria = -sinal * (8f + 20f * (float)rnd.NextDouble()),
                ang = (float)rnd.NextDouble() * 360f,
                q0 = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f)
            });
        }
        for (int i = 0; i < NFaiscas; i++)
            faiscas.Add(new Faisca
            {
                anel = rnd.Next(NAneis), theta = (float)rnd.NextDouble() * 6.28f,
                vel = (0.9f + 1.8f * (float)rnd.NextDouble()) * (rnd.Next(2) == 0 ? 1f : -1f),
                tamanho = 0.04f + 0.03f * (float)rnd.NextDouble()
            });

        CriarFragmentos(rnd);
        CriarParticulas();
    }

    static Texture2D TexturaRadial(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - d, 2f)));
            }
        tex.Apply();
        return tex;
    }

    // fita de raio 1 no plano XY com seção em cruz (visível de qualquer ângulo)
    static Mesh CriarFita(int tipo, int seg, float a0, float a1, float larg, float alfaIni, float alfaFim)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j < seg; j++)
        {
            if (tipo == 1 && j % 2 == 1) continue;
            float fa = j / (float)seg, fb = (j + 1) / (float)seg;
            if (tipo == 1) fb = (j + 0.7f) / seg;
            float aA = Mathf.Lerp(a0, a1, fa), aB = Mathf.Lerp(a0, a1, fb);
            var pA = new Vector3(Mathf.Cos(aA), Mathf.Sin(aA), 0); var pB = new Vector3(Mathf.Cos(aB), Mathf.Sin(aB), 0);
            float alA = Mathf.Lerp(alfaIni, alfaFim, fa), alB = Mathf.Lerp(alfaIni, alfaFim, fb);
            float h = larg * 0.5f;
            Quad(v, c, uv, t, pA * (1 - h), pA * (1 + h), pB * (1 + h), pB * (1 - h), alA, alB, tipo == 1 ? 0.35f : 0f);
            Quad(v, c, uv, t, pA + Vector3.forward * h, pA - Vector3.forward * h, pB - Vector3.forward * h, pB + Vector3.forward * h, alA, alB, tipo == 1 ? 0.35f : 0f);
        }
        var m = new Mesh { name = "FitaAnel" + tipo };
        m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2.4f);
        return m;
    }

    // anel de marcas radiais (estilo régua/HUD); a cada 6ª marca é mais longa
    static Mesh CriarMarcas(int n)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j < n; j++)
        {
            float a = j / (float)n * Mathf.PI * 2f, l = j % 6 == 0 ? 0.075f : 0.035f, w = 0.007f;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); var tg = new Vector3(-d.y, d.x, 0);
            Quad(v, c, uv, t, d * (1 - l) - tg * w, d * (1 - l) + tg * w, d + tg * w, d - tg * w, 1f, 1f, j % 6 == 0 ? 0f : 0.2f);
        }
        var m = new Mesh { name = "Marcas" };
        m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 2.4f);
        return m;
    }

    static void Quad(List<Vector3> v, List<Color> c, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 d, Vector3 e, float alA, float alB, float pisca)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(d); v.Add(e);
        c.Add(new Color(0, 0.3f, pisca, alA)); c.Add(new Color(0, 0.3f, pisca, alA));
        c.Add(new Color(0, 0.3f, pisca, alB)); c.Add(new Color(0, 0.3f, pisca, alB));
        for (int k = 0; k < 4; k++) uv.Add(new Vector2(0.5f, 0.5f));
        t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    // retângulos e traços tangentes à superfície, piscando
    void CriarFragmentos(System.Random rnd)
    {
        var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i < NFragmentos; i++)
        {
            var n = Random.onUnitSphere;
            var u = Vector3.Cross(n, Random.onUnitSphere).normalized;
            var w2 = Vector3.Cross(n, u);
            float roll = Random.value * 6.28f;
            var uu = u * Mathf.Cos(roll) + w2 * Mathf.Sin(roll); var vv = Vector3.Cross(n, uu);
            bool traco = Random.value < 0.45f;
            float larg = traco ? 0.08f + Random.value * 0.1f : 0.03f + Random.value * 0.05f;
            float alt = traco ? 0.005f : 0.012f + Random.value * 0.025f;
            var ce = n * (0.9f + Random.value * 0.12f);
            float fase = Random.value;
            float al = 0.45f + Random.value * 0.45f;
            int k = v.Count;
            v.Add(ce - uu * larg / 2 - vv * alt / 2); v.Add(ce + uu * larg / 2 - vv * alt / 2);
            v.Add(ce + uu * larg / 2 + vv * alt / 2); v.Add(ce - uu * larg / 2 + vv * alt / 2);
            for (int q = 0; q < 4; q++) { c.Add(new Color(1f, fase, 0.85f, al)); uv.Add(new Vector2(0.5f, 0.5f)); }
            t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3);
        }
        malhaFragmentos = new Mesh { name = "Fragmentos" };
        malhaFragmentos.SetVertices(v); malhaFragmentos.SetColors(c); malhaFragmentos.SetUVs(0, uv); malhaFragmentos.SetTriangles(t, 0);
        malhaFragmentos.bounds = new Bounds(Vector3.zero, Vector3.one * 2.6f);
        var go = new GameObject("Fragmentos");
        go.transform.SetParent(transform, false);
        fragmentos = go.transform;
        go.AddComponent<MeshFilter>().sharedMesh = malhaFragmentos;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    // malha dinâmica: brilho central (1) + espiral do núcleo (48) + faíscas com rastro (28 × 3)
    void CriarParticulas()
    {
        quadsParticulas = 3 + NEspiral * 2 + NFaiscas * Trilha;
        verts = new Vector3[quadsParticulas * 4]; cores = new Color[verts.Length]; uvs = new Vector2[verts.Length];
        tris = new int[quadsParticulas * 6];
        for (int q = 0; q < quadsParticulas; q++)
        {
            int k = q * 4;
            uvs[k] = new Vector2(0, 0); uvs[k + 1] = new Vector2(1, 0); uvs[k + 2] = new Vector2(1, 1); uvs[k + 3] = new Vector2(0, 1);
            int ti = q * 6;
            tris[ti] = k; tris[ti + 1] = k + 1; tris[ti + 2] = k + 2; tris[ti + 3] = k; tris[ti + 4] = k + 2; tris[ti + 5] = k + 3;
        }
        malhaParticulas = new Mesh { name = "Particulas" };
        malhaParticulas.MarkDynamic();
        malhaParticulas.vertices = verts; malhaParticulas.uv = uvs; malhaParticulas.triangles = tris; malhaParticulas.colors = cores;
        malhaParticulas.bounds = new Bounds(Vector3.zero, Vector3.one * 3f);
        var go = new GameObject("Particulas");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = malhaParticulas;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
    }

    void Billboard(int q, Vector3 c, Vector3 r, Vector3 u, float s, float alfa)
    {
        int k = q * 4;
        verts[k] = c - r * s - u * s; verts[k + 1] = c + r * s - u * s; verts[k + 2] = c + r * s + u * s; verts[k + 3] = c - r * s + u * s;
        var col = new Color(0, 0, 0, alfa);
        cores[k] = cores[k + 1] = cores[k + 2] = cores[k + 3] = col;
    }

    // ------------------------------------------------------------------ estados e animação
    void Update()
    {
        if (malhaParticulas == null || Camera.main == null) return;
        if (medirCusto) cron.Restart();
        float dt = Time.deltaTime;
        float tempo = Time.time;

        // --- estado a partir da voz ---
        float alvoGiro, alvoBrilho, alvoContr = 1f, alvoPulso = 0f;
        bool acordada = voz != null && voz.acordada;
        bool falando = voz != null && (voz.Falando || voz.volumeSaida > 0.03f);
        if (voz != null && voz.nivelMic > 0.04f && !falando) ultimaVozUsuario = tempo;
        bool ouvindo = acordada && !falando && tempo - ultimaVozUsuario < 0.6f;
        bool processando = acordada && !falando && !ouvindo && tempo - ultimaVozUsuario < 6f;
        if (!acordada && !falando) { alvoGiro = 0.35f; alvoBrilho = 0.7f; }
        else if (falando) { alvoGiro = 1.3f; alvoBrilho = 1f; alvoPulso = voz.volumeSaida; }
        else if (ouvindo) { alvoGiro = 0.9f; alvoBrilho = 1f; alvoContr = 0.72f; }
        else if (processando) { alvoGiro = 3.2f; alvoBrilho = 1.2f; alvoContr = 0.92f; }
        else { alvoGiro = 0.7f; alvoBrilho = 0.85f; }

        float vol = voz != null ? voz.volumeSaida : 0f;
        if (falando && vol - volAnterior > 0.12f) rajada = 1f;       // pico de voz → faíscas extras
        volAnterior = Mathf.Lerp(volAnterior, vol, dt * 12f);
        rajada = Mathf.MoveTowards(rajada, 0f, dt * 2.5f);

        float k = dt * 4f;
        giro = Mathf.Lerp(giro, alvoGiro, k); brilho = Mathf.Lerp(brilho, alvoBrilho, k);
        contracao = Mathf.Lerp(contracao, alvoContr, dt * 5f);
        pulso = Mathf.Lerp(pulso, alvoPulso, dt * 14f);

        // --- erro: pisca em vermelho uma vez ---
        if (voz != null && !string.IsNullOrEmpty(voz.ultimoErro) && voz.ultimoErro != ultimoErroVisto) { ultimoErroVisto = voz.ultimoErro; erroAte = tempo + 0.9f; }
        float mixErro = erroAte > tempo ? Mathf.Sin(Mathf.PI * (1f - (erroAte - tempo) / 0.9f)) : 0f;

        // --- cores (transição suave entre paletas; erro mistura vermelho) ---
        var alvoP = Paletas[corAtual];
        nucleoAtual = Color.Lerp(nucleoAtual, Nucleo(alvoP), dt * 5f); bordaAtual = Color.Lerp(bordaAtual, alvoP.borda, dt * 5f);
        var red = Paletas["vermelho"];
        mat.SetColor("_CorNucleo", Color.Lerp(nucleoAtual, Nucleo(red), mixErro));
        mat.SetColor("_CorBorda", Color.Lerp(bordaAtual, red.borda, mixErro));
        if (voz != null) { var cf = Color.Lerp(Color.Lerp(bordaAtual, Color.white, 0.2f), red.borda, mixErro); cf.a = 1f; voz.corFala = cf; }
        mat.SetFloat("_Brilho", (brilho + 0.9f * pulso + 0.8f * mixErro) * 1.9f);
        mat.SetVector("_Centro", transform.position);
        mat.SetFloat("_Raio", Mathf.Max(0.0001f, transform.lossyScale.x));

        // --- anéis ---
        for (int i = 0; i < aneis.Count; i++)
        {
            var a = aneis[i];
            a.ang += a.vel * giro * dt;
            a.angPropria += a.velPropria * giro * dt;
            a.q = Quaternion.AngleAxis(a.ang, a.eixo) * a.q0 * Quaternion.AngleAxis(a.angPropria, Vector3.forward);
            a.t.localRotation = a.q;
            a.raioAtual = a.raio * contracao * (1f + pulso * 0.22f * (0.4f + 0.6f * ((i * 37) % 10) / 10f));
            a.t.localScale = Vector3.one * a.raioAtual;
        }
        fragmentos.Rotate(new Vector3(0.3f, 1f, 0.2f).normalized, 6f * giro * dt, Space.Self);

        // --- partículas (billboards no espaço local) ---
        var cam = Camera.main.transform;
        var r = transform.InverseTransformDirection(cam.right).normalized;
        var u = transform.InverseTransformDirection(cam.up).normalized;
        int q = 0;

        // brilho central
        float respira = 1f + 0.25f * pulso + 0.1f * Mathf.Sin(tempo * 3f);
        Billboard(q++, Vector3.zero, r, u, 0.62f * respira, 0.8f);
        Billboard(q++, Vector3.zero, r, u, 0.3f * respira, 1f);
        Billboard(q++, Vector3.zero, r, u, 0.13f * respira, 1f);

        // espiral do núcleo
        tempoEspiral += dt * (2.2f + 2.5f * giro);
        var inclina = Quaternion.Euler(24f, 0f, 18f);
        for (int i = 0; i < NEspiral * 2; i++)
        {
            float f = (i % NEspiral) / (float)NEspiral;
            float ang = f * 6.2f * 2.2f + (i < NEspiral ? tempoEspiral : -tempoEspiral * 0.8f + 3.14f);
            float rr = (0.04f + 0.3f * f) * contracao * (1f + 0.3f * pulso);
            var p = inclina * new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang * 0.5f) * 0.05f, Mathf.Sin(ang) * rr);
            Billboard(q++, p, r, u, 0.04f * (1.2f - f * 0.5f), (1f - 0.45f * f) * 1.3f);
        }

        // faíscas pelas órbitas, rastro de 3 pontos
        int ativas = (acordada || falando) ? NFaiscas : NFaiscas / 3;
        float velMult = (0.5f + giro * 0.6f) * (1f + rajada * 2f);
        for (int i = 0; i < NFaiscas; i++)
        {
            var f = faiscas[i];
            f.theta += f.vel * velMult * dt;
            var an = aneis[f.anel];
            bool viva = i < ativas || rajada > 0.2f;
            for (int tt = 0; tt < Trilha; tt++)
            {
                float th = f.theta - Mathf.Sign(f.vel) * tt * 0.09f;
                var p = an.q * new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f) * an.raioAtual;
                float s = viva ? f.tamanho * (1f - tt * 0.28f) * (1f + rajada * 0.8f) : 0f;
                Billboard(q++, p, r, u, s, viva ? (1f - tt * 0.35f) * (1f + rajada) : 0f);
            }
        }
        malhaParticulas.vertices = verts;
        malhaParticulas.colors = cores;

        if (medirCusto)
        {
            cron.Stop();
            somaMs += (float)cron.Elapsed.TotalMilliseconds; quadros++;
            if (quadros == 600)
            {
                custoMs = somaMs / quadros;
                Debug.Log("EsferaZetta: CPU média de Update " + custoMs.ToString("0.000") + " ms/quadro; " + quadsParticulas + " quads dinâmicos + " + NFragmentos +
                          " fragmentos; " + NAneis + " anéis em 3 malhas compartilhadas; 1 material com instancing");
                somaMs = 0; quadros = 0; medirCusto = false;
            }
        }
    }

    void OnDestroy()
    {
        if (mat != null) { if (mat.mainTexture != null) Destroy(mat.mainTexture); Destroy(mat); }
    }
}
