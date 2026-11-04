using System;
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
        { "laranja", new Color(1f, 0.5f, 0.05f) }, { "roxo", TemaZetta.Roxo },
        { "violeta", TemaZetta.Roxo }, { "rosa", new Color(1f, 0.4f, 0.75f) },
        { "ciano", TemaZetta.AzulTec }, { "branco", Color.white },
        { "magenta", new Color(1f, 0.1f, 0.9f) }, { "dourado", TemaZetta.Dourado },
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
      ""cor"": { ""type"": ""STRING"", ""description"": ""Cor única em português (azul, vermelho, verde, amarelo, laranja, roxo, rosa, ciano, branco...) ou hexadecimal como #FF8800. Sem cor pedida, deixe vazio: a Zetta escolhe uma cor da paleta."" },
      ""cores"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""STRING"" }, ""description"": ""Várias cores, uma por parte na ordem das partes (cubo: frente, tras, direita, esquerda, topo, base; cilindro: topo, base, lateral; piramide: base, lados; anel: fora, dentro; esfera: faixas de cima para baixo). 'cada face de uma cor' = 6 cores. Se houver menos cores que partes, repete."" },
      ""degrade"": { ""type"": ""BOOLEAN"", ""description"": ""true: as cores de 'cores' viram um degradê ao longo das partes."" },
      ""partes_cores"": { ""type"": ""ARRAY"", ""description"": ""Cor por parte nomeada, ex.: base dourada e resto azul = [{parte:'base',cor:'dourado'},{parte:'resto',cor:'azul'}]. 'resto' pinta as partes não citadas."", ""items"": { ""type"": ""OBJECT"", ""properties"": { ""parte"": { ""type"": ""STRING"" }, ""cor"": { ""type"": ""STRING"" } } } },
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
            CriarObjeto(new JObject { ["forma"] = item.forma, ["cor"] = item.cor, ["tamanho"] = item.tamanho }, pos, rot, item.estado);
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

    public JObject CriarObjeto(JObject args, Vector3? posMundo = null, Quaternion? rotMundo = null, string estadoSalvo = null)
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

        // objeto = raiz + uma parte (filho com malha própria) por face/região: tudo pintável e separável
        var go = new GameObject("Holograma_" + forma + "_" + (++contador));
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.rotation = rotMundo ?? Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        go.transform.localScale = new Vector3(tamanho, tamanho * alturaEsc, tamanho);

        var malhaBase = MalhaDaForma(forma, tipo);
        var oi = go.AddComponent<ObjetoInterativo>();
        ClassificadorDaForma(forma, out string[] nomesPartes, out Func3 classe, oi);
        var pedacos = MalhaPartes.Dividir(malhaBase, nomesPartes, (c, n, v) => classe(c, n, v));
        string corTxt = args.Value<string>("cor");
        bool varias = (args["cores"] is JArray) || (args["partes_cores"] is JArray);
        if (string.IsNullOrWhiteSpace(corTxt) && !varias) corTxt = CorDaPaleta();
        cor = ParseCor(corTxt);
        foreach (var pd in pedacos)
        {
            var parte = new GameObject(pd.nome);
            parte.transform.SetParent(go.transform, false);
            parte.AddComponent<MeshFilter>().sharedMesh = pd.malha;
            parte.AddComponent<MeshRenderer>();
            AplicarVisual(parte, cor, tiling, 1f, null, 0.3f, malhaBase.bounds);
            var ph = parte.AddComponent<ParteHolo>();
            ph.nome = pd.nome;
            oi.AdicionarParte(ph);
        }
        AplicarCoresDaCriacao(oi, args);

        // interação com as mãos (XR Interaction Toolkit)
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        go.AddComponent<ColisorPegada>();   // caixa pelos bounds das partes (antes do XRGrabInteractable)
        go.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(go);

        string corNome = corTxt ?? "ciano";
        var item = HoloRegistro.Adicionar(go, forma, corNome, forma + " " + (varias && string.IsNullOrWhiteSpace(args.Value<string>("cor")) ? "" : corNome), "primitiva", tamanho);
        if (!string.IsNullOrEmpty(estadoSalvo)) oi.AplicarEstado(estadoSalvo);
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
        Texture2D texPreench = null, float alfaPreench = 0.3f, Bounds? limites = null)
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
        m.Iniciar(malhaGrade, malhaPreench, matGrade, matFill, limites);
    }

    delegate int Func3(Vector3 centro, Vector3 normal, int vertice);

    static Mesh MalhaDaForma(string forma, PrimitiveType tipo)
    {
        switch (forma)
        {
            case "cone": return FormasHolo.Cone(32);
            case "piramide": return FormasHolo.Cone(4);
            case "anel": return FormasHolo.Anel();
        }
        var tmp = GameObject.CreatePrimitive(tipo);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        return m;
    }

    // nomes das partes de cada forma e como separar os triângulos entre elas
    static void ClassificadorDaForma(string forma, out string[] nomes, out Func3 classe, ObjetoInterativo oi)
    {
        switch (forma)
        {
            case "cubo":
                nomes = new[] { "frente", "tras", "direita", "esquerda", "topo", "base" };
                classe = (c, n, v) =>
                {
                    float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                    if (az >= ax && az >= ay) return n.z > 0 ? 0 : 1;
                    if (ax >= ay) return n.x > 0 ? 2 : 3;
                    return n.y > 0 ? 4 : 5;
                };
                oi.grupos["lados"] = new[] { "frente", "tras", "direita", "esquerda" };
                oi.grupos["laterais"] = oi.grupos["lados"];
                oi.grupos["face de cima"] = new[] { "topo" };
                oi.grupos["face de baixo"] = new[] { "base" };
                return;
            case "piramide":
                nomes = new[] { "base", "lados" };
                classe = (c, n, v) => (c.y < -0.45f && Mathf.Abs(n.y) > 0.9f) ? 0 : 1;   // normais da malha podem vir invertidas: usa a posição
                oi.grupos["lateral"] = new[] { "lados" };
                oi.grupos["laterais"] = new[] { "lados" };
                return;
            case "cone":
                nomes = new[] { "base", "lateral" };
                classe = (c, n, v) => (c.y < -0.45f && Mathf.Abs(n.y) > 0.9f) ? 0 : 1;
                oi.grupos["lados"] = new[] { "lateral" };
                oi.grupos["topo"] = new[] { "lateral" };
                return;
            case "cilindro":
                nomes = new[] { "topo", "base", "lateral" };
                classe = (c, n, v) => n.y > 0.9f ? 0 : (n.y < -0.9f ? 1 : 2);
                oi.grupos["lados"] = new[] { "lateral" };
                return;
            case "capsula":
                nomes = new[] { "topo", "meio", "base" };
                classe = (c, n, v) => c.y > 0.5f ? 0 : (c.y < -0.5f ? 2 : 1);
                oi.grupos["lateral"] = new[] { "meio" };
                return;
            case "anel":
                nomes = new[] { "fora", "dentro" };
                classe = (c, n, v) => new Vector2(c.x, c.z).magnitude >= 0.35f ? 0 : 1;   // raio maior do toro = 0,35
                oi.grupos["externa"] = new[] { "fora" };
                oi.grupos["interna"] = new[] { "dentro" };
                oi.grupos["lado de fora"] = new[] { "fora" };
                oi.grupos["lado de dentro"] = new[] { "dentro" };
                return;
            default: // esfera: 4 faixas de cima para baixo
                nomes = new[] { "faixa 1", "faixa 2", "faixa 3", "faixa 4" };
                classe = (c, n, v) => c.y > 0.25f ? 0 : (c.y > 0f ? 1 : (c.y > -0.25f ? 2 : 3));
                oi.grupos["norte"] = new[] { "faixa 1", "faixa 2" };
                oi.grupos["sul"] = new[] { "faixa 3", "faixa 4" };
                oi.grupos["hemisferio norte"] = oi.grupos["norte"];
                oi.grupos["hemisferio sul"] = oi.grupos["sul"];
                oi.grupos["hemisferio de cima"] = oi.grupos["norte"];
                oi.grupos["hemisferio de baixo"] = oi.grupos["sul"];
                oi.grupos["topo"] = new[] { "faixa 1" };
                oi.grupos["base"] = new[] { "faixa 4" };
                oi.grupos["equador"] = new[] { "faixa 2", "faixa 3" };
                return;
        }
    }

    // cores variadas da paleta quando o usuário não pede cor (uma por objeto, em sequência)
    static readonly string[] paleta = { "azul", "dourado", "verde", "laranja", "roxo", "ciano", "rosa", "amarelo", "vermelho", "magenta" };
    static int proximaCor = -1;
    public static string CorDaPaleta()
    {
        if (proximaCor < 0) proximaCor = UnityEngine.Random.Range(0, paleta.Length);
        return paleta[proximaCor++ % paleta.Length];
    }

    // aplica 'cores' (na ordem das partes, com degradê opcional) e 'partes_cores' ({parte, cor}; "resto" = as não citadas)
    public static void AplicarCoresDaCriacao(ObjetoInterativo oi, JObject args)
    {
        if (args["cores"] is JArray lista && lista.Count > 0)
        {
            var cores = new List<Color>();
            foreach (var t in lista) cores.Add(ParseCor(t.ToString()));
            oi.PintarEmOrdem(cores, args.Value<bool?>("degrade") == true);
        }
        if (args["partes_cores"] is JArray pc)
        {
            var pintadas = new HashSet<ParteHolo>();
            Color? resto = null;
            foreach (var e in pc)
            {
                string nome = e.Value<string>("parte") ?? "";
                var c = ParseCor(e.Value<string>("cor"));
                string n = ApagadorHolo.Normalizar(nome);
                if (n == "resto" || n == "demais" || n == "outras" || n == "o resto" || n == "restante") { resto = c; continue; }
                foreach (var p in oi.Resolver(nome)) { p.Pintar(c); pintadas.Add(p); }
            }
            if (resto.HasValue) foreach (var p in oi.partes) if (!pintadas.Contains(p)) p.Pintar(resto.Value);
        }
    }

    public static Color ParseCor(string s)
    {
        return TentarCor(s, out var c) ? c : cores["ciano"];
    }

    // reconhece nomes (também no feminino e plural), alguns tons extras e hexadecimal
    public static bool TentarCor(string s, out Color c)
    {
        c = cores["ciano"];
        if (string.IsNullOrWhiteSpace(s)) return false;
        string n = ApagadorHolo.Normalizar(s).Trim();
        if (cores.TryGetValue(n, out c)) return true;
        if (ColorUtility.TryParseHtmlString(s.Trim(), out c)) return true;
        if (n.EndsWith("s") && cores.TryGetValue(n.Substring(0, n.Length - 1), out c)) return true;
        if (n.EndsWith("a") && cores.TryGetValue(n.Substring(0, n.Length - 1) + "o", out c)) return true;
        if (n.EndsWith("as") && cores.TryGetValue(n.Substring(0, n.Length - 2) + "o", out c)) return true;
        switch (n)
        {
            case "turquesa": c = new Color(0.1f, 0.85f, 0.8f); return true;
            case "bege": c = new Color(0.9f, 0.82f, 0.65f); return true;
            case "vinho": c = new Color(0.5f, 0.05f, 0.2f); return true;
            case "lilas": c = new Color(0.78f, 0.62f, 1f); return true;
            case "salmao": c = new Color(1f, 0.55f, 0.45f); return true;
            case "prata": case "prateado": case "prateada": c = new Color(0.8f, 0.85f, 0.9f); return true;
            case "ouro": c = TemaZetta.Dourado; return true;
            case "azul marinho": c = new Color(0.05f, 0.15f, 0.5f); return true;
            case "azul claro": c = new Color(0.45f, 0.8f, 1f); return true;
        }
        c = cores["ciano"];
        return false;
    }

    // "mais claro", "mais escuro", "azul mais escuro": ajusta a cor atual (ou a cor citada)
    public static Color CorDeEdicao(string texto, Color atual)
    {
        string n = ApagadorHolo.Normalizar(texto ?? "");
        bool claro = n.Contains("claro") || n.Contains("clare"), escuro = n.Contains("escuro") || n.Contains("escure");
        if (!claro && !escuro) return TentarCor(texto, out var direta) ? direta : atual;
        var resto = new System.Text.StringBuilder();
        foreach (var w in n.Split(' ', ',', '.'))
            if (w.Length > 0 && w != "mais" && w != "bem" && w != "um" && w != "pouco" && w != "pouquinho" && !w.StartsWith("claro") && !w.StartsWith("escuro") && !w.StartsWith("clare") && !w.StartsWith("escure"))
                resto.Append(w).Append(' ');
        var baseCor = TentarCor(resto.ToString().Trim(), out var citada) ? citada : atual;
        return Color.Lerp(baseCor, claro ? Color.white : Color.black, 0.35f);
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

    public void Iniciar(Mesh grade, Mesh preench, Material mGrade, Material mFill, Bounds? limites = null)
    {
        malhaGrade = grade; malhaPreench = preench; matGrade = mGrade; matFill = mFill;
        escalaBase = transform.localScale;
        var v = grade.vertices;
        var b = limites ?? grade.bounds;   // partes usam a caixa do objeto inteiro: a montagem sobe junta
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
