using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Registra a ferramenta "criar_objeto" no Gemini Live e cria hologramas que podem ser pegos com as mãos.
public class CriadorHolograma : MonoBehaviour
{
    public int maxObjetos = 30;
    public float tamanhoMin = 0.03f;
    public float tamanhoMax = 1f;

    AssistenteVoz voz;
    readonly List<GameObject> criados = new List<GameObject>();
    int contador;
    static Texture2D texturaGrade;

    static readonly Dictionary<string, Color> cores = new Dictionary<string, Color>
    {
        { "azul", new Color(0.1f, 0.4f, 1f) }, { "vermelho", new Color(1f, 0.15f, 0.15f) },
        { "verde", new Color(0.1f, 1f, 0.3f) }, { "amarelo", new Color(1f, 0.92f, 0.1f) },
        { "laranja", new Color(1f, 0.5f, 0.05f) }, { "roxo", new Color(0.6f, 0.2f, 1f) },
        { "violeta", new Color(0.6f, 0.2f, 1f) }, { "rosa", new Color(1f, 0.4f, 0.75f) },
        { "ciano", new Color(0.1f, 0.95f, 1f) }, { "branco", Color.white },
        { "magenta", new Color(1f, 0.1f, 0.9f) }, { "dourado", new Color(1f, 0.78f, 0.2f) },
        { "marrom", new Color(0.6f, 0.35f, 0.15f) }, { "cinza", new Color(0.65f, 0.65f, 0.7f) },
        { "preto", new Color(0.15f, 0.15f, 0.2f) }
    };

    const string Declaracao = @"{
  ""name"": ""criar_objeto"",
  ""description"": ""Cria um objeto holográfico 3D na frente do usuário, que ele pode pegar e girar com as mãos."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""forma"": { ""type"": ""STRING"", ""enum"": [""cubo"", ""esfera"", ""cilindro"", ""capsula"", ""cone"", ""piramide"", ""anel""], ""description"": ""Forma do objeto."" },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Cor em português (azul, vermelho, verde, amarelo, laranja, roxo, rosa, ciano, branco...) ou hexadecimal como #FF8800."" },
      ""tamanho"": { ""type"": ""NUMBER"", ""description"": ""Tamanho em metros (padrão 0.15)."" },
      ""posicao"": {
        ""type"": ""OBJECT"",
        ""description"": ""Posição em metros relativa à cabeça do usuário: x direita, y cima, z frente. Padrão x=0, y=-0.05, z=0.6."",
        ""properties"": { ""x"": { ""type"": ""NUMBER"" }, ""y"": { ""type"": ""NUMBER"" }, ""z"": { ""type"": ""NUMBER"" } }
      }
    },
    ""required"": [""forma""]
  }
}";

    void Awake()
    {
        AncorasHolo.Fabricas["primitiva"] = (item, pos, rot) =>
        {
            CriarObjeto(new JObject { ["forma"] = item.forma, ["cor"] = item.cor, ["tamanho"] = item.tamanho }, pos, rot);
            return HoloRegistro.Itens.Count > 0 ? HoloRegistro.Itens[HoloRegistro.Itens.Count - 1].go : null;
        };
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) { Debug.LogWarning("CriadorHolograma: AssistenteVoz não encontrado."); return; }
        voz.declaracoesFuncoes.Add(JObject.Parse(Declaracao));
        var anterior = voz.aoChamarFuncao;
        voz.aoChamarFuncao = (nome, args) =>
        {
            if (nome == "criar_objeto") return CriarObjeto(args);
            return anterior != null ? anterior(nome, args) : null;
        };
    }

    public JObject CriarObjeto(JObject args, Vector3? posMundo = null, Quaternion? rotMundo = null)
    {
        string forma = (args.Value<string>("forma") ?? "").ToLowerInvariant();
        PrimitiveType tipo;
        Vector2 tiling;
        float alturaEsc = 1f;
        switch (forma)
        {
            case "cubo": tipo = PrimitiveType.Cube; tiling = new Vector2(4, 4); break;
            case "esfera": tipo = PrimitiveType.Sphere; tiling = new Vector2(16, 8); break;
            case "cilindro": tipo = PrimitiveType.Cylinder; tiling = new Vector2(16, 3); alturaEsc = 0.5f; break;
            case "capsula": tipo = PrimitiveType.Capsule; tiling = new Vector2(16, 6); alturaEsc = 0.5f; break;
            case "cone": tipo = PrimitiveType.Cube; tiling = new Vector2(16, 4); break;
            case "piramide": tipo = PrimitiveType.Cube; tiling = new Vector2(4, 3); break;
            case "anel": tipo = PrimitiveType.Cube; tiling = new Vector2(24, 8); break;
            default: return new JObject { ["erro"] = "forma inválida; use cubo, esfera, cilindro, capsula, cone, piramide ou anel" };
        }

        float tamanho = Mathf.Clamp(args.Value<float?>("tamanho") ?? 0.15f, tamanhoMin, tamanhoMax);
        Color cor = ParseCor(args.Value<string>("cor"));

        var cam = Camera.main.transform;
        var local = new Vector3(0f, -0.05f, 0.6f);
        if (args["posicao"] is JObject p)
            local = new Vector3(p.Value<float?>("x") ?? 0f, p.Value<float?>("y") ?? -0.05f, p.Value<float?>("z") ?? 0.6f);
        var pos = posMundo ?? (cam.position + Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * local);

        GameObject go;
        Mesh malhaProcedural = forma == "cone" ? FormasHolo.Cone(32) : forma == "piramide" ? FormasHolo.Cone(4)
            : forma == "anel" ? FormasHolo.Anel() : null;
        if (malhaProcedural != null)
        {
            go = new GameObject();
            go.AddComponent<MeshFilter>().sharedMesh = malhaProcedural;
            go.AddComponent<MeshRenderer>();
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = malhaProcedural;
            mc.convex = true;
        }
        else go = GameObject.CreatePrimitive(tipo);
        go.name = "Holograma_" + forma + "_" + (++contador);
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.rotation = rotMundo ?? Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        go.transform.localScale = new Vector3(tamanho, tamanho * alturaEsc, tamanho);

        AplicarVisual(go, cor, tiling, 1f);

        // interação com as mãos (XR Interaction Toolkit)
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        go.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(go);


        HoloRegistro.Adicionar(go, forma, args.Value<string>("cor") ?? "ciano", forma + " " + (args.Value<string>("cor") ?? ""), "primitiva", tamanho);
        criados.Add(go);
        while (criados.Count > maxObjetos)
        {
            if (criados[0] != null) Destroy(criados[0]);
            criados.RemoveAt(0);
        }
        return new JObject { ["result"] = "ok", ["objeto"] = go.name };
    }

    // Visual holográfico: grade de linhas (renderer do próprio objeto) + preenchimento translúcido (filho),
    // com o efeito de montagem. Funciona para qualquer primitiva com MeshFilter/MeshRenderer.
    public static void AplicarVisual(GameObject go, Color cor, Vector2 tiling, float duracaoMontagem,
        Texture2D texPreench = null, float alfaPreench = 0.3f)
    {
        var malhaBase = go.GetComponent<MeshFilter>().sharedMesh;
        var malhaGrade = Instantiate(malhaBase);
        var malhaPreench = Instantiate(malhaBase);
        go.GetComponent<MeshFilter>().sharedMesh = malhaGrade;

        var shader = Shader.Find("Sprites/Default");
        var matGrade = new Material(shader) { mainTexture = TexturaGrade(), mainTextureScale = tiling };
        matGrade.color = Color.Lerp(cor, Color.white, 0.35f);
        go.GetComponent<Renderer>().sharedMaterial = matGrade;

        var fill = new GameObject("Preenchimento");
        fill.transform.SetParent(go.transform, false);
        fill.AddComponent<MeshFilter>().sharedMesh = malhaPreench;
        var matFill = new Material(shader);
        if (texPreench != null) { matFill.mainTexture = texPreench; matFill.color = new Color(1f, 1f, 1f, alfaPreench); }
        else matFill.color = new Color(cor.r, cor.g, cor.b, alfaPreench);
        fill.AddComponent<MeshRenderer>().sharedMaterial = matFill;

        var m = go.AddComponent<HologramaMontagem>();
        m.duracao = duracaoMontagem;
        m.Iniciar(malhaGrade, malhaPreench, matGrade, matFill);
    }

    public static Color ParseCor(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return cores["ciano"];
        s = s.Trim().ToLowerInvariant();
        if (cores.TryGetValue(s, out var c)) return c;
        if (ColorUtility.TryParseHtmlString(s, out c)) return c;
        return cores["ciano"];
    }

    // célula de grade: linhas opacas nas bordas, interior transparente
    static Texture2D TexturaGrade()
    {
        if (texturaGrade != null) return texturaGrade;
        const int n = 64, borda = 3;
        texturaGrade = new Texture2D(n, n, TextureFormat.RGBA32, true);
        texturaGrade.wrapMode = TextureWrapMode.Repeat;
        texturaGrade.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool linha = x < borda || y < borda || x >= n - borda || y >= n - borda;
                texturaGrade.SetPixel(x, y, new Color(1f, 1f, 1f, linha ? 1f : 0f));
            }
        texturaGrade.Apply(true);
        return texturaGrade;
    }
}

// Efeito de montagem (~1 s): a grade surge de baixo para cima e o preenchimento a segue.
public class HologramaMontagem : MonoBehaviour
{
    public float duracao = 1f;

    Mesh malhaGrade, malhaPreench;
    Material matGrade, matFill;
    float[] alturaNorm;
    Vector3 escalaBase;
    Color[] corGrade, corFill;

    public void Iniciar(Mesh grade, Mesh preench, Material mGrade, Material mFill)
    {
        malhaGrade = grade; malhaPreench = preench; matGrade = mGrade; matFill = mFill;
        escalaBase = transform.localScale;
        var v = grade.vertices;
        var b = grade.bounds;
        alturaNorm = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
            alturaNorm[i] = Mathf.Clamp01((v[i].y - b.min.y) / Mathf.Max(0.0001f, b.size.y));
        corGrade = new Color[v.Length];
        corFill = new Color[v.Length];
        Aplicar(0f);
        StartCoroutine(Animar());
    }

    // Inverso da montagem: o preenchimento some e a grade se desfaz de cima para baixo.
    public void Desfazer(float dur)
    {
        StopAllCoroutines();
        StartCoroutine(AnimarSaida(dur));
    }

    IEnumerator AnimarSaida(float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float x = 1f - t / dur;
            Aplicar(x);
            transform.localScale = escalaBase * Mathf.Lerp(0.9f, 1f, x);
            yield return null;
        }
        Aplicar(0f);
    }

    public void Reiniciar()
    {
        StopAllCoroutines();
        transform.localScale = escalaBase;
        Aplicar(0f);
        StartCoroutine(Animar());
    }

    IEnumerator Animar()
    {
        var escalaFinal = escalaBase;
        for (float t = 0f; t < duracao; t += Time.deltaTime)
        {
            float x = t / duracao;
            Aplicar(x);
            // leve crescimento durante a montagem
            transform.localScale = escalaFinal * Mathf.Lerp(0.92f, 1f, Mathf.SmoothStep(0f, 1f, x));
            yield return null;
        }
        transform.localScale = escalaFinal;
        Aplicar(1f);
    }

    void Aplicar(float x)
    {
        // a grade avança na frente do preenchimento
        float fGrade = Mathf.Clamp01(x * 1.6f) * 1.15f;
        float fFill = x * 1.3f - 0.15f;
        for (int i = 0; i < alturaNorm.Length; i++)
        {
            float h = alturaNorm[i];
            corGrade[i] = new Color(1f, 1f, 1f, Mathf.Clamp01((fGrade - h) / 0.15f));
            corFill[i] = new Color(1f, 1f, 1f, Mathf.Clamp01((fFill - h) / 0.15f));
        }
        malhaGrade.colors = corGrade;
        malhaPreench.colors = corFill;
    }
}
