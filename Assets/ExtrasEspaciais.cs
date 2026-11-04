using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Diário do dia: registra o que a Zetta e o usuário fizeram (objetos, notas, sites, modelos) para a linha do tempo.
// Guardado em PlayerPrefs por data; só hoje importa, dias antigos são descartados.
public static class DiarioZetta
{
    public class Entrada { [JsonProperty("h")] public string hora; [JsonProperty("t")] public string tipo; [JsonProperty("x")] public string texto; }
    static List<Entrada> lista;
    static string Chave => "zetta_diario_" + DateTime.Now.ToString("yyyyMMdd");

    static void Carregar()
    {
        if (lista != null) return;
        try { lista = JsonConvert.DeserializeObject<List<Entrada>>(PlayerPrefs.GetString(Chave, "[]")) ?? new List<Entrada>(); }
        catch { lista = new List<Entrada>(); }
    }

    public static void Registrar(string tipo, string texto)
    {
        Carregar();
        lista.Add(new Entrada { hora = DateTime.Now.ToString("HH:mm"), tipo = tipo, texto = texto ?? "" });
        if (lista.Count > 60) lista.RemoveAt(0);
        PlayerPrefs.SetString(Chave, JsonConvert.SerializeObject(lista));
        PlayerPrefs.Save();
    }

    public static List<Entrada> Hoje() { Carregar(); return lista; }
}

// "Mostre o que fizemos hoje": linha do tempo de luz à frente, com um marco por atividade do dia.
public class LinhaDoTempo : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""linha_do_tempo"",
  ""description"": ""Mostra (ou fecha) uma linha do tempo de luz com o que foi feito hoje: objetos, notas, sites e modelos."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""mostrar"", ""fechar""] } } }
}";
    const string Instrucao = "'Mostre o que fizemos hoje' → linha_do_tempo acao=mostrar; 'feche a linha do tempo' → acao=fechar. Resuma em uma frase.";

    AssistenteVoz voz;
    Transform raiz;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
        HoloRegistro.aoAdicionar += Registrar;
    }

    void OnDestroy() { HoloRegistro.aoAdicionar -= Registrar; }

    static void Registrar(GameObject go)
    {
        if (HoloRegistro.Carregando) return;
        var it = HoloRegistro.Buscar(go);
        if (it == null) return;
        string tipo = it.tipo == "nota" ? "nota" : "objeto";
        string texto = string.IsNullOrWhiteSpace(it.descricao) ? it.forma : it.descricao;
        DiarioZetta.Registrar(tipo, texto);
    }

    JObject Executar(JObject a)
    {
        if ((a.Value<string>("acao") ?? "mostrar").StartsWith("f")) { Fechar(); return new JObject { ["result"] = "linha do tempo fechada" }; }
        var dados = DiarioZetta.Hoje();
        if (dados.Count == 0) return new JObject { ["result"] = "ainda não fizemos nada hoje; diga isso em uma frase" };
        Montar(dados);
        var resumo = new List<string>();
        foreach (var e in dados) resumo.Add(e.hora + " " + e.tipo + " " + e.texto);
        return new JObject { ["result"] = "linha do tempo mostrada; resuma em uma frase o dia: " + string.Join("; ", resumo.GetRange(Mathf.Max(0, resumo.Count - 8), Mathf.Min(8, resumo.Count))) };
    }

    public void Fechar()
    {
        if (raiz != null) Destroy(raiz.gameObject);
        raiz = null;
        StopAllCoroutines();
    }

    void Montar(List<DiarioZetta.Entrada> dados)
    {
        Fechar();
        raiz = new GameObject("LinhaDoTempo").transform;
        raiz.SetParent(transform, false);
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0.02f, 0.95f));
        raiz.position = pos;
        raiz.rotation = HoloUtil.OlharParaUsuario(pos);

        int n = Mathf.Min(8, dados.Count);
        var mostrar = dados.GetRange(dados.Count - n, n);
        const float larg = 0.9f;

        var matLinha = HoloUtil.NovoMaterialSprite(TemaZetta.AzulTec);
        var linha = new GameObject("Eixo").AddComponent<LineRenderer>();
        linha.transform.SetParent(raiz, false);
        linha.useWorldSpace = false;
        linha.sharedMaterial = matLinha;
        linha.startColor = linha.endColor = TemaZetta.AzulTec;
        linha.widthMultiplier = 0.004f;
        linha.positionCount = 2;
        linha.SetPosition(0, new Vector3(-larg * 0.5f - 0.05f, 0f, 0f));
        linha.SetPosition(1, new Vector3(larg * 0.5f + 0.05f, 0f, 0f));
        linha.sortingOrder = TemaZetta.OrdemBotao;

        HoloUtil.CriarTexto(raiz, "Titulo", "HOJE · " + DateTime.Now.ToString("dd/MM"), new Vector3(0f, 0.2f, TemaZetta.ProfundidadeTexto),
            new Vector2(0.6f, 0.05f), 0.3f, TemaZetta.AzulTec).fontSizeMin = 0.18f;

        for (int i = 0; i < n; i++)
        {
            var e = mostrar[i];
            float x = n == 1 ? 0f : Mathf.Lerp(-larg * 0.5f, larg * 0.5f, i / (float)(n - 1));
            bool acima = i % 2 == 0;
            Color cor = e.tipo == "nota" ? TemaZetta.Dourado : e.tipo == "site" ? TemaZetta.Roxo : TemaZetta.Ciano;

            var marco = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(marco.GetComponent<Collider>());
            marco.transform.SetParent(raiz, false);
            marco.transform.localPosition = new Vector3(x, 0f, 0f);
            marco.transform.localScale = Vector3.one * 0.022f;
            marco.GetComponent<Renderer>().sharedMaterial = HoloUtil.NovoMaterialSprite(cor);

            var haste = new GameObject("Haste").AddComponent<LineRenderer>();
            haste.transform.SetParent(raiz, false);
            haste.useWorldSpace = false;
            haste.sharedMaterial = matLinha;
            haste.startColor = haste.endColor = TemaZetta.ComAlfa(cor, 0.8f);
            haste.widthMultiplier = 0.002f;
            haste.positionCount = 2;
            haste.SetPosition(0, new Vector3(x, 0f, 0f));
            haste.SetPosition(1, new Vector3(x, acima ? 0.07f : -0.07f, 0f));

            string txt = e.hora + "\n" + (e.texto.Length > 22 ? e.texto.Substring(0, 22) + "…" : e.texto);
            var t = HoloUtil.CriarTexto(raiz, "Marco" + i, txt, new Vector3(x, acima ? 0.12f : -0.12f, TemaZetta.ProfundidadeTexto),
                new Vector2(0.16f, 0.07f), 0.18f, cor);
            t.fontSizeMin = 0.14f;
        }
        StartCoroutine(Surgir());
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    IEnumerator Surgir()
    {
        var t = raiz;
        for (float x = 0; x < 0.8f; x += Time.deltaTime)
        {
            if (t == null) yield break;
            t.localScale = new Vector3(Mathf.SmoothStep(0f, 1f, x / 0.8f), 1f, 1f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
        yield return new WaitForSeconds(40f);
        Fechar();
    }
}

// Etiquetas holográficas discretas nos planos detectados pelo AR Foundation (mesa, chão, parede, teto, assento).
public class EtiquetasSala : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""etiquetas_sala"",
  ""description"": ""Liga ou desliga etiquetas holográficas discretas nos móveis e superfícies detectados na sala (mesa, parede, chão, teto, assento)."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""ligar"", ""desligar""] } }, ""required"": [""acao""] }
}";
    const string Instrucao = "'Zetta, anote a sala' / 'mostre as etiquetas' → etiquetas_sala acao=ligar; 'tire as etiquetas' → acao=desligar.";

    public int maximo = 10;
    bool ligado;
    ARPlaneManager planos;
    readonly Dictionary<ARPlane, TextMeshPro> etiquetas = new Dictionary<ARPlane, TextMeshPro>();
    float proxima;

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        ligado = !(a.Value<string>("acao") ?? "ligar").StartsWith("d");
        if (!ligado) Limpar();
        else
        {
            planos = FindAnyObjectByType<ARPlaneManager>();
            if (planos == null) { ligado = false; return new JObject { ["erro"] = "a detecção de planos da sala não está disponível" }; }
        }
        SonsZetta.Tocar(ligado ? SonsZetta.Abrir : SonsZetta.Fechar);
        return new JObject { ["result"] = ligado ? "etiquetas ligadas" : "etiquetas desligadas" };
    }

    void Limpar()
    {
        foreach (var kv in etiquetas) if (kv.Value != null) Destroy(kv.Value.gameObject);
        etiquetas.Clear();
    }

    static string Nome(PlaneClassifications c)
    {
        if ((c & PlaneClassifications.Table) != 0) return "Mesa";
        if ((c & PlaneClassifications.Seat) != 0) return "Assento";
        if ((c & PlaneClassifications.Floor) != 0) return "Chão";
        if ((c & PlaneClassifications.Ceiling) != 0) return "Teto";
        if ((c & PlaneClassifications.WallFace) != 0) return "Parede";
        return null;
    }

    void Update()
    {
        if (!ligado || planos == null || Time.time < proxima || Camera.main == null) return;
        proxima = Time.time + 1f;
        var cam = Camera.main.transform;
        int n = 0;
        foreach (var p in planos.trackables)
        {
            string nome = Nome(p.classifications);
            if (nome == null || p.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.None) continue;
            if (n++ >= maximo) break;
            if (!etiquetas.TryGetValue(p, out var t) || t == null)
            {
                t = HoloUtil.CriarTexto(transform, "Etiqueta_" + nome, nome, Vector3.zero, new Vector2(0.3f, 0.05f), 0.2f, TemaZetta.ComAlfa(TemaZetta.Ciano, 0.85f));
                t.fontSizeMin = 0.14f;
                etiquetas[p] = t;
            }
            var pos = p.center + Vector3.zero;
            pos = p.transform.TransformPoint(new Vector3(p.center.x, p.center.y, p.center.z)) + p.normal * 0.08f;
            t.transform.position = pos;
            var dir = Vector3.ProjectOnPlane(pos - cam.position, Vector3.up);
            if (dir.sqrMagnitude > 0.001f) t.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }
}

// Portal do clima: um pequeno globo do céu (0,2 m) sobre a mesa holográfica (ou à frente) com sol, nuvens, chuva ou neve
// conforme o tempo agora na cidade, mais a temperatura e a chance de chuva.
public class PortalClima : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""portal_clima"",
  ""description"": ""Mostra um pequeno globo do céu sobre a mesa com o tempo de agora e a previsão de uma cidade (sol, nuvens, chuva, neve)."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""cidade"": { ""type"": ""STRING"" }, ""acao"": { ""type"": ""STRING"", ""enum"": [""mostrar"", ""fechar""] } } }
}";
    const string Instrucao = "'Portal do clima' / 'globo do tempo' → portal_clima com a cidade (São Paulo se não souber); 'feche o portal' → acao=fechar.";

    AssistenteVoz voz;
    Transform globo;
    Transform[] gotas;
    Vector3[] gotasBase;
    string tipo = "";
    static Texture2D texSol;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        if ((a.Value<string>("acao") ?? "mostrar").StartsWith("f")) { Fechar(); return new JObject { ["result"] = "portal fechado" }; }
        string cidade = (a.Value<string>("cidade") ?? "São Paulo").Trim();
        StartCoroutine(Rotina(cidade));
        return new JObject { ["result"] = "abrindo o portal do clima; os dados chegam em instantes" };
    }

    void Fechar() { if (globo != null) Destroy(globo.gameObject); globo = null; StopAllCoroutines(); }

    IEnumerator Baixar(string url, Action<string> fim)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            fim(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
        }
    }

    IEnumerator Rotina(string cidade)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string geo = null;
        yield return Baixar("https://geocoding-api.open-meteo.com/v1/search?count=1&language=pt&name=" + UnityWebRequest.EscapeURL(cidade), t => geo = t);
        JToken local = null;
        try { if (geo != null) local = JObject.Parse(geo)["results"]?[0]; } catch { }
        if (local == null) { Erro(geo == null ? "o serviço de clima está sem resposta" : "não encontrei a cidade " + cidade); yield break; }
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + local.Value<double>("latitude").ToString(ci) + "&longitude=" + local.Value<double>("longitude").ToString(ci)
            + "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=1";
        string dados = null;
        yield return Baixar(url, t => dados = t);
        if (dados == null) { Erro("o serviço de clima está sem resposta"); yield break; }
        int temp, max, min, chuva, codigo;
        try
        {
            var j = JObject.Parse(dados);
            temp = Mathf.RoundToInt(j["current"].Value<float>("temperature_2m"));
            codigo = j["current"].Value<int>("weather_code");
            max = Mathf.RoundToInt(j["daily"]["temperature_2m_max"][0].Value<float>());
            min = Mathf.RoundToInt(j["daily"]["temperature_2m_min"][0].Value<float>());
            chuva = Mathf.RoundToInt(j["daily"]["precipitation_probability_max"][0].Value<float>());
        }
        catch (Exception) { Erro("não consegui ler a previsão"); yield break; }

        Montar(local.Value<string>("name") ?? cidade, codigo, temp, min, max, chuva);
        if (voz != null) voz.Falar("o portal do clima de " + (local.Value<string>("name") ?? cidade) + " está aberto: " + temp + " graus agora, mínima " + min + " e máxima " + max + ", chuva " + chuva + " por cento. Comente em uma frase.");
    }

    void Erro(string m) { if (voz != null) { voz.MostrarAviso(m, 6f); voz.Falar("aviso: " + m + ". Explique em uma frase."); } }

    static string Tipo(int c)
    {
        if (c <= 1) return "sol";
        if (c <= 3) return "nuvem";
        if (c == 45 || c == 48) return "nevoa";
        if ((c >= 71 && c <= 77) || c == 85 || c == 86) return "neve";
        if (c >= 95) return "tempestade";
        return "chuva";
    }

    void Montar(string cidade, int codigo, int temp, int min, int max, int chuva)
    {
        Fechar();
        tipo = Tipo(codigo);
        var mesa = FindAnyObjectByType<MesaHolografica>();
        Vector3 pos = mesa != null && mesa.ativa ? mesa.Centro + Vector3.up * 0.22f : HoloUtil.PontoNaVista(new Vector3(0f, -0.02f, 0.75f));
        globo = new GameObject("PortalClima").transform;
        globo.SetParent(transform, false);
        globo.position = pos;
        globo.rotation = HoloUtil.OlharParaUsuario(pos);

        // casca do globo
        var casca = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        casca.name = "Casca";
        Destroy(casca.GetComponent<Collider>());
        casca.transform.SetParent(globo, false);
        casca.transform.localScale = Vector3.one * 0.2f;
        CriadorHolograma.AplicarVisual(casca, TemaZetta.AzulTec, new Vector2(16, 8), 1.2f, null, 0.08f);

        Color corCeu = tipo == "sol" ? TemaZetta.Dourado : tipo == "neve" ? TemaZetta.Branco : TemaZetta.Ciano;
        if (tipo == "sol" || tipo == "nevoa")
        {
            var sol = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(sol.GetComponent<Collider>());
            sol.transform.SetParent(globo, false);
            sol.transform.localPosition = new Vector3(0.02f, 0.03f, 0f);
            sol.transform.localScale = Vector3.one * (tipo == "sol" ? 0.12f : 0.08f);
            sol.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = TexturaSol(), color = tipo == "sol" ? TemaZetta.Dourado : TemaZetta.ComAlfa(TemaZetta.Branco, 0.5f) };
            sol.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemBotao;
        }
        if (tipo != "sol")
        {
            for (int i = 0; i < 4; i++)
            {
                var n = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(n.GetComponent<Collider>());
                n.transform.SetParent(globo, false);
                n.transform.localPosition = new Vector3(Mathf.Lerp(-0.04f, 0.04f, i / 3f), 0.045f + (i % 2) * 0.012f, 0f);
                n.transform.localScale = new Vector3(0.05f, 0.028f, 0.03f);
                n.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(tipo == "tempestade" ? TemaZetta.AzulProfundo : TemaZetta.Branco, 0.55f) };
            }
        }
        if (tipo == "chuva" || tipo == "neve" || tipo == "tempestade")
        {
            int g = 28;
            gotas = new Transform[g]; gotasBase = new Vector3[g];
            var mat = new Material(Shader.Find("Sprites/Default")) { color = TemaZetta.ComAlfa(corCeu, 0.8f) };
            var rnd = new System.Random(5);
            for (int i = 0; i < g; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(globo, false);
                q.GetComponent<Renderer>().sharedMaterial = mat;
                q.GetComponent<Renderer>().sortingOrder = TemaZetta.OrdemBotao;
                bool neve = tipo == "neve";
                q.transform.localScale = neve ? new Vector3(0.004f, 0.004f, 1f) : new Vector3(0.0015f, 0.012f, 1f);
                float ang = (float)rnd.NextDouble() * 6.28f, r = Mathf.Sqrt((float)rnd.NextDouble()) * 0.075f;
                gotasBase[i] = new Vector3(Mathf.Cos(ang) * r, (float)rnd.NextDouble() * 0.12f - 0.06f, Mathf.Sin(ang) * r * 0.5f);
                gotas[i] = q.transform;
            }
        }

        HoloUtil.CriarTexto(globo, "Cidade", cidade + " · " + temp + "°C", new Vector3(0f, 0.15f, TemaZetta.ProfundidadeTexto), new Vector2(0.4f, 0.04f), 0.2f, TemaZetta.Branco).fontSizeMin = 0.16f;
        HoloUtil.CriarTexto(globo, "Previsao", min + "° / " + max + "° · chuva " + chuva + "%", new Vector3(0f, -0.15f, TemaZetta.ProfundidadeTexto), new Vector2(0.4f, 0.04f), 0.18f, TemaZetta.Ciano).fontSizeMin = 0.16f;
        StartCoroutine(HoloUtil.Surgir(globo));
        SonsZetta.Tocar(SonsZetta.Abrir);
        StartCoroutine(FecharDepois(60f));
    }

    IEnumerator FecharDepois(float s) { yield return new WaitForSeconds(s); Fechar(); }

    static Texture2D TexturaSol()
    {
        if (texSol != null) return texSol;
        const int n = 64;
        texSol = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                texSol.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d * 0.3f)));
            }
        texSol.Apply();
        return texSol;
    }

    void Update()
    {
        if (globo == null || gotas == null) return;
        bool neve = tipo == "neve";
        for (int i = 0; i < gotas.Length; i++)
        {
            if (gotas[i] == null) continue;
            var p = gotasBase[i];
            float v = neve ? 0.03f : 0.18f;
            p.y = Mathf.Repeat(p.y + 0.06f - Time.time * v, 0.12f) - 0.06f;
            if (neve) p.x += Mathf.Sin(Time.time + i) * 0.004f;
            gotas[i].localPosition = p;
        }
        if (Camera.main != null) foreach (var q in gotas) if (q != null) q.rotation = Quaternion.LookRotation(q.position - Camera.main.transform.position, Vector3.up);
    }
}
