using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Excluir hologramas: por voz (apagar_objeto), por arremesso e pela lixeira holográfica.
// A saída é o inverso da montagem: a grade se desfaz em ~0,5 s e a âncora é removida.
public class ApagadorHolo : MonoBehaviour
{
    public float duracaoSaida = 0.5f;
    public float velocidadeArremesso = 2.2f;   // m/s ao soltar
    public float confirmacaoSegundos = 60f;

    [Header("Lixeira")]
    public bool usarLixeira = true;
    public Vector3 posicaoVista = new Vector3(0.55f, 0f, 0.5f); // à direita e um pouco à frente
    public float alturaSobreChao = 0.25f;

    float pedidoTudoEm = -999f;
    Transform lixeira;
    Transform lixeiraVisual;
    Vector3 lixeiraEscala = Vector3.one;
    float lixeiraPulso;

    const string Decl = @"{
  ""name"": ""apagar_objeto"",
  ""description"": ""Apaga hologramas criados. alvo='descricao' apaga pela descrição (ex.: cubo azul); alvo='isso' apaga o objeto que o usuário está olhando ou apontando; alvo='tudo' apaga todos e exige confirmação do usuário por voz antes."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""alvo"": { ""type"": ""STRING"", ""enum"": [""descricao"", ""isso"", ""tudo""] },
      ""descricao"": { ""type"": ""STRING"", ""description"": ""Descrição do objeto, ex.: cubo azul, esfera vermelha, sistema solar. Só para alvo=descricao."" },
      ""confirmado"": { ""type"": ""BOOLEAN"", ""description"": ""true somente depois que o usuário confirmou por voz o pedido de apagar tudo."" }
    },
    ""required"": [""alvo""]
  }
}";
    const string Instrucao =
        "Para apagar: 'apague o cubo azul' → apagar_objeto alvo=descricao com a descrição; 'apague isso' → alvo=isso; " +
        "'apague tudo' → chame alvo=tudo SEM confirmado; a ferramenta responderá pedindo confirmação: pergunte ao usuário em voz " +
        "'Tem certeza que quer apagar todos os objetos?' e SÓ se ele responder sim chame de novo alvo=tudo com confirmado=true.";

    static readonly HashSet<string> Ignorar = new HashSet<string> { "o", "a", "os", "as", "de", "do", "da", "dos", "das", "um", "uma", "isso", "esse", "essa", "objeto", "holograma", "por", "favor", "que" };

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Controlar, Instrucao);
        HoloRegistro.aoAdicionar += AoAdicionar;
    }

    void OnDestroy() { HoloRegistro.aoAdicionar -= AoAdicionar; }

    IEnumerator Start()
    {
        if (!usarLixeira) yield break;
        yield return new WaitForSeconds(2.5f);
        CriarLixeira();
    }

    // ---------------- voz ----------------
    public JObject Controlar(JObject args)
    {
        string alvo = (args.Value<string>("alvo") ?? "").ToLowerInvariant();
        HoloRegistro.Itens.RemoveAll(i => i.go == null);

        if (alvo == "isso")
        {
            var it = HoloAlvo.ObjetoOlhado();
            if (it == null) return new JObject { ["erro"] = "não encontrei nenhum objeto sendo olhado ou apontado" };
            string d = it.descricao;
            Apagar(it.go);
            return new JObject { ["result"] = "apaguei " + d };
        }

        if (alvo == "tudo")
        {
            int n = HoloRegistro.Itens.Count;
            if (n == 0) return new JObject { ["result"] = "não há objetos para apagar" };
            bool confirmado = args.Value<bool?>("confirmado") ?? false;
            if (!confirmado || Time.time - pedidoTudoEm > confirmacaoSegundos)
            {
                pedidoTudoEm = Time.time;
                return new JObject { ["result"] = "NADA FOI APAGADO. Pergunte ao usuário se ele tem certeza de que quer apagar todos os " + n + " objetos; se disser sim, chame de novo com confirmado=true." };
            }
            pedidoTudoEm = -999f;
            foreach (var it in new List<HoloItem>(HoloRegistro.Itens)) if (it.go != null) Apagar(it.go);
            FindAnyObjectByType<AncorasHolo>()?.ApagarTodasAncoras();
            return new JObject { ["result"] = n + " objetos apagados" };
        }

        if (alvo == "descricao")
        {
            string consulta = args.Value<string>("descricao") ?? "";
            var it = Buscar(consulta);
            if (it == null) return new JObject { ["erro"] = "não achei nenhum objeto parecido com '" + consulta + "'" };
            string d = it.descricao;
            Apagar(it.go);
            return new JObject { ["result"] = "apaguei " + d };
        }
        return new JObject { ["erro"] = "alvo inválido" };
    }

    public static string Normalizar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    // acha o objeto cujas palavras cobrem a consulta; em empate, o mais perto do olhar
    public static HoloItem Buscar(string consulta)
    {
        var tokens = new List<string>();
        foreach (var t in Normalizar(consulta).Split(new[] { ' ', ',', '.' }, System.StringSplitOptions.RemoveEmptyEntries))
            if (!Ignorar.Contains(t)) tokens.Add(t.Length > 3 && t.EndsWith("s") ? t.Substring(0, t.Length - 1) : t);
        if (tokens.Count == 0) return null;

        var cam = Camera.main != null ? Camera.main.transform : null;
        HoloItem melhor = null;
        float melhorAng = float.MaxValue;
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null) continue;
            string texto = Normalizar(it.descricao + " " + it.forma + " " + it.cor + " " + it.extra);
            bool tudo = true;
            foreach (var t in tokens) if (!texto.Contains(t)) { tudo = false; break; }
            if (!tudo) continue;
            float ang = cam != null ? Vector3.Angle(cam.forward, HoloAlvo.CentroDoObjeto(it.go) - cam.position) : 0f;
            if (ang < melhorAng) { melhorAng = ang; melhor = it; }
        }
        return melhor;
    }

    // ---------------- saída animada ----------------
    public void Apagar(GameObject go)
    {
        if (go == null) return;
        HoloRegistro.Remover(go); // dispara a remoção da âncora
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;
        var seg = go.GetComponent<SeguirMao>();
        if (seg != null) Destroy(seg);
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; }
        StartCoroutine(Saida(go));
    }

    IEnumerator Saida(GameObject go)
    {
        var partes = go.GetComponentsInChildren<HologramaMontagem>();
        foreach (var p in partes) p.Desfazer(duracaoSaida);
        foreach (var lr in go.GetComponentsInChildren<LineRenderer>())
            StartCoroutine(ApagarLinha(lr, duracaoSaida));

        if (partes.Length == 0)
        {
            // sem grade (ex.: GLB): encolhe
            var e0 = go.transform.localScale;
            for (float t = 0; t < duracaoSaida && go != null; t += Time.deltaTime)
            {
                go.transform.localScale = e0 * (1f - t / duracaoSaida);
                yield return null;
            }
        }
        else yield return new WaitForSeconds(duracaoSaida);
        if (go != null) Destroy(go);
    }

    IEnumerator ApagarLinha(LineRenderer lr, float dur)
    {
        var c0 = lr.startColor;
        for (float t = 0; t < dur && lr != null; t += Time.deltaTime)
        {
            var c = c0; c.a = c0.a * (1f - t / dur);
            lr.startColor = lr.endColor = c;
            yield return null;
        }
    }

    // ---------------- arremesso ----------------
    void AoAdicionar(GameObject go)
    {
        if (go == null || go.GetComponent<DetectorArremesso>() != null) return;
        go.AddComponent<DetectorArremesso>().Configurar(this);
    }

    public void AoSoltarRapido(GameObject go, float velocidade)
    {
        if (go != null && velocidade >= velocidadeArremesso) Apagar(go);
    }

    // ---------------- lixeira ----------------
    void CriarLixeira()
    {
        var raiz = new GameObject("Lixeira");
        raiz.transform.SetParent(transform, false);
        lixeira = raiz.transform;
        lixeira.position = PosicaoLixeira();
        lixeira.rotation = HoloUtil.OlharParaUsuario(lixeira.position);

        var vis = new GameObject("Visual").transform;
        vis.SetParent(lixeira, false);
        lixeiraVisual = vis;
        var vermelho = new Color(1f, 0.3f, 0.35f);

        // corpo (cilindro afunilado aproximado), tampa e alça
        Parte(vis, PrimitiveType.Cylinder, "Corpo", new Vector3(0, 0.0f, 0), new Vector3(0.11f, 0.065f, 0.11f), vermelho, new Vector2(10, 4));
        Parte(vis, PrimitiveType.Cylinder, "Tampa", new Vector3(0, 0.07f, 0), new Vector3(0.13f, 0.006f, 0.13f), vermelho, new Vector2(10, 1));
        Parte(vis, PrimitiveType.Cube, "Alca", new Vector3(0, 0.088f, 0), new Vector3(0.04f, 0.012f, 0.012f), vermelho, new Vector2(2, 1));
        // faixas verticais (listras da lata)
        for (int i = -1; i <= 1; i++)
            Parte(vis, PrimitiveType.Cube, "Listra", new Vector3(i * 0.028f, 0f, -0.056f), new Vector3(0.006f, 0.1f, 0.004f), Color.white, new Vector2(1, 4));
        lixeiraEscala = vis.localScale;
        StartCoroutine(HoloUtil.Surgir(vis, 0f, 0.6f));
    }

    void Parte(Transform pai, PrimitiveType tipo, string nome, Vector3 pos, Vector3 esc, Color cor, Vector2 tiling)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(pai, false);
        go.transform.localPosition = pos;
        go.transform.localScale = esc;
        CriadorHolograma.AplicarVisual(go, cor, tiling, 0.8f, null, 0.22f);
    }

    Vector3 PosicaoLixeira()
    {
        var cam = Camera.main.transform;
        var sala = FindAnyObjectByType<SalaAR>();
        float chao = sala != null ? sala.AlturaChao() : 0f;
        var yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        var p = cam.position + yaw * new Vector3(posicaoVista.x, 0f, posicaoVista.z);
        p.y = chao + alturaSobreChao;
        return p;
    }

    void Update()
    {
        if (lixeira == null || Camera.main == null) return;
        // acompanha o usuário devagar, sempre à direita e perto do chão
        lixeira.position = Vector3.Lerp(lixeira.position, PosicaoLixeira(), Time.deltaTime * 0.8f);
        lixeira.rotation = Quaternion.Slerp(lixeira.rotation, HoloUtil.OlharParaUsuario(lixeira.position), Time.deltaTime * 2f);

        // objetos soltos dentro da lixeira são apagados; objetos segurados perto dela a destacam
        bool algumPerto = false;
        var centro = lixeira.position + Vector3.up * 0.04f;
        foreach (var c in Physics.OverlapSphere(centro, 0.22f))
        {
            var rb = c.attachedRigidbody;
            if (rb == null) continue;
            var item = HoloRegistro.Buscar(rb.gameObject);
            if (item == null) continue;
            var grab = rb.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected) { algumPerto = true; continue; }
            if (rb.GetComponent<SeguirMao>() != null) continue;
            Apagar(item.go);
        }
        lixeiraPulso = Mathf.Lerp(lixeiraPulso, algumPerto ? 1f : 0f, Time.deltaTime * 8f);
        if (lixeiraVisual != null && lixeiraVisual.localScale.x > 0.99f * lixeiraEscala.x)
            lixeiraVisual.localScale = lixeiraEscala * (1f + 0.35f * lixeiraPulso);
    }
}

// Mede a velocidade do objeto enquanto é segurado e, ao soltar rápido, pede para apagá-lo.
public class DetectorArremesso : MonoBehaviour
{
    ApagadorHolo apagador;
    XRGrabInteractable grab;
    readonly Queue<(float t, Vector3 p)> amostras = new Queue<(float, Vector3)>();

    public void Configurar(ApagadorHolo a)
    {
        apagador = a;
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectExited.AddListener(AoSoltar);
    }

    void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(AoSoltar);
    }

    void Update()
    {
        amostras.Enqueue((Time.time, transform.position));
        while (amostras.Count > 0 && Time.time - amostras.Peek().t > 0.2f) amostras.Dequeue();
    }

    void AoSoltar(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _)
    {
        if (amostras.Count < 2) return;
        var primeira = amostras.Peek();
        var ultima = default((float t, Vector3 p));
        foreach (var a in amostras) ultima = a;
        float dt = ultima.t - primeira.t;
        if (dt <= 0.0001f) return;
        float v = Vector3.Distance(ultima.p, primeira.p) / dt;
        apagador.AoSoltarRapido(gameObject, v);
    }
}
