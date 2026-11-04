using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Notas holográficas: "Zetta, anote: ..." cria um cartão ciano à frente do usuário (~0,7 m). Dá para pegar com a mão;
// ao soltar perto de parede ou mesa ele gruda na superfície. Cada nota é um holograma do registro (tipo "nota"),
// então tem âncora espacial (reaparece no mesmo lugar, com texto, letra, cor e escala), pode ser apagada pela voz,
// pela lixeira ou jogada fora, e editada por voz (editar_nota).
public class NotasHolo : MonoBehaviour
{
    public Vector3 posicaoVista = new Vector3(0f, -0.02f, 0.7f);
    public float larguraBase = 0.30f;          // m
    public float alturaMaxBase = 0.45f;        // m
    public float letraBase = 0.115f;           // mesmo tamanho aparente do PainelInfo (0,18 a 1,1 m ≈ 0,16 por metro) a 0,7 m
    public float alcanceGrudar = 0.25f;
    public int maxCaracteres = 400;

    static readonly Color Tinta = new Color(0.01f, 0.16f, 0.22f);

    AssistenteVoz voz;
    int contador;

    // o que fica salvo na âncora (HoloItem.extra, em JSON)
    public class Dados
    {
        [JsonProperty("t")] public string texto = "";
        [JsonProperty("f")] public float letra = 1f;     // multiplicador da letra (0,5 a 2,5)
        [JsonProperty("c")] public string cor = "ciano";
        [JsonProperty("k")] public float cartao = 1f;    // multiplicador do cartão (0,5 a 2,5)
        [JsonProperty("e")] public float escala = 1f;    // escala feita com as duas mãos
    }

    public static Dados LerDados(HoloItem it)
    {
        try
        {
            if (!string.IsNullOrEmpty(it.extra) && it.extra.StartsWith("{")) return JsonConvert.DeserializeObject<Dados>(it.extra) ?? new Dados();
        }
        catch { }
        return new Dados { texto = it.extra ?? "" };   // notas antigas guardavam só o texto
    }

    const string DeclAnotar = @"{
  ""name"": ""anotar"",
  ""description"": ""Cria uma nota holográfica (cartão ciano) à frente do usuário com o texto ditado, que depois pode ser pega e colada numa parede ou mesa."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""texto"": { ""type"": ""STRING"", ""description"": ""Texto da nota, em português."" } },
    ""required"": [""texto""]
  }
}";
    const string DeclGerenciar = @"{
  ""name"": ""gerenciar_notas"",
  ""description"": ""Lê em voz alta, destaca ou apaga as notas holográficas do usuário. acao=ler devolve os textos; acao=mostrar faz todas as notas brilharem por 5 segundos; acao=apagar remove a nota cujo texto contém o assunto."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""ler"", ""mostrar"", ""apagar""] },
      ""assunto"": { ""type"": ""STRING"", ""description"": ""Palavra(s) do texto da nota a apagar. Só para acao=apagar."" }
    },
    ""required"": [""acao""]
  }
}";
    const string DeclEditar = @"{
  ""name"": ""editar_nota"",
  ""description"": ""Edita uma nota existente: letra maior/menor, trocar o texto, acrescentar texto, mudar a cor do cartão, cartão maior/menor. A nota é a que o usuário está olhando/tocando ou a que combina com o assunto."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""letra_maior"", ""letra_menor"", ""trocar_texto"", ""acrescentar"", ""cor"", ""cartao_maior"", ""cartao_menor""] },
      ""assunto"": { ""type"": ""STRING"", ""description"": ""Palavras do texto da nota a editar. Vazio = a nota olhada ou, se só houver uma, ela."" },
      ""texto"": { ""type"": ""STRING"", ""description"": ""Novo texto (trocar_texto) ou texto a acrescentar (acrescentar)."" },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Cor do cartão em português, ex.: amarelo, verde, rosa, azul. Só para acao=cor."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao =
        "'Zetta, anote: ...' → anotar com o texto; confirme em poucas palavras. 'leia minhas notas' → gerenciar_notas acao=ler e leia cada nota em voz alta; " +
        "'mostre minhas notas' → acao=mostrar; 'apague a nota sobre X' → acao=apagar com assunto=X. 'Apague isso' olhando uma nota usa apagar_objeto alvo=isso. " +
        "Para mudar uma nota existente (letra maior/menor, trocar ou acrescentar texto, cor, cartão maior/menor) use editar_nota.";

    void Awake()
    {
        AncorasHolo.Fabricas["nota"] = (item, pos, rot) => CriarCartao(LerDados(item), pos, rot);
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclAnotar, Anotar, Instrucao);
        voz.RegistrarFerramenta(DeclGerenciar, Gerenciar, "");
        voz.RegistrarFerramenta(DeclEditar, Editar, "");
    }

    // ---------------- voz ----------------
    public JObject Anotar(JObject args)
    {
        string texto = (args.Value<string>("texto") ?? "").Trim();
        if (texto.Length == 0) return new JObject { ["erro"] = "texto da nota vazio" };
        if (texto.Length > maxCaracteres) texto = texto.Substring(0, maxCaracteres).TrimEnd() + "…";
        var pos = HoloUtil.PontoCriacao(0.2f, 0.7f);
        var go = CriarCartao(new Dados { texto = texto }, pos, HoloUtil.OlharParaUsuario(pos));
        if (go == null) return new JObject { ["erro"] = "não consegui criar a nota" };
        return new JObject { ["result"] = "nota criada: " + texto };
    }

    public JObject Gerenciar(JObject args)
    {
        string acao = (args.Value<string>("acao") ?? "").ToLowerInvariant();
        var notas = Notas();
        if (notas.Count == 0) return new JObject { ["result"] = "o usuário não tem nenhuma nota" };

        if (acao == "ler")
        {
            var textos = new JArray();
            var linhas = new JArray();
            int i = 1;
            foreach (var n in notas) { string t = LerDados(n).texto; textos.Add(t); linhas.Add(i++ + ". " + t); }
            FindAnyObjectByType<PainelInfo>()?.Mostrar(new JObject { ["titulo"] = "Minhas notas", ["linhas"] = linhas, ["duracao"] = 20f });
            return new JObject { ["result"] = "Leia em voz alta cada nota, em ordem.", ["notas"] = textos };
        }
        if (acao == "mostrar")
        {
            foreach (var n in notas) n.go.GetComponent<BrilhoProximidade>()?.Destacar(5f);
            return new JObject { ["result"] = notas.Count + " notas destacadas por 5 segundos" };
        }
        if (acao == "apagar")
        {
            var apagador = FindAnyObjectByType<ApagadorHolo>();
            if (apagador == null) return new JObject { ["erro"] = "apagador indisponível" };
            var achadas = Achar(notas, args.Value<string>("assunto"), out var tokens);
            if (achadas.Count == 0) return new JObject { ["erro"] = "nenhuma nota combina com esse assunto", ["notas"] = TextosDe(notas) };
            if (achadas.Count > 1 && tokens.Count > 0) return new JObject { ["erro"] = "mais de uma nota combina; pergunte qual", ["notas"] = TextosDe(achadas) };
            if (achadas.Count > 1) return new JObject { ["erro"] = "há " + achadas.Count + " notas; diga o assunto da que quer apagar" };
            string apagada = LerDados(achadas[0]).texto;
            apagador.Apagar(achadas[0].go);
            return new JObject { ["result"] = "apaguei a nota: " + apagada };
        }
        return new JObject { ["erro"] = "acao inválida" };
    }

    public JObject Editar(JObject args)
    {
        string acao = (args.Value<string>("acao") ?? "").ToLowerInvariant();
        var notas = Notas();
        if (notas.Count == 0) return new JObject { ["erro"] = "o usuário não tem nenhuma nota" };

        HoloItem alvo = null;
        string assunto = args.Value<string>("assunto");
        if (string.IsNullOrWhiteSpace(assunto))
        {
            var olhado = HoloAlvo.ObjetoOlhado();
            if (olhado != null && olhado.tipo == "nota") alvo = olhado;
            else if (notas.Count == 1) alvo = notas[0];
            else return new JObject { ["erro"] = "não sei qual nota; peça o assunto dela ou olhe para ela", ["notas"] = TextosDe(notas) };
        }
        else
        {
            var achadas = Achar(notas, assunto, out _);
            if (achadas.Count == 0) return new JObject { ["erro"] = "nenhuma nota combina com esse assunto", ["notas"] = TextosDe(notas) };
            if (achadas.Count > 1) return new JObject { ["erro"] = "mais de uma nota combina; pergunte qual", ["notas"] = TextosDe(achadas) };
            alvo = achadas[0];
        }

        var nc = alvo.go.GetComponent<NotaCartao>();
        if (nc == null) return new JObject { ["erro"] = "nota inválida" };
        var d = nc.dados;
        string texto = (args.Value<string>("texto") ?? "").Trim();
        switch (acao)
        {
            case "letra_maior": d.letra = Mathf.Clamp(d.letra * 1.25f, 0.5f, 2.5f); break;
            case "letra_menor": d.letra = Mathf.Clamp(d.letra / 1.25f, 0.5f, 2.5f); break;
            case "cartao_maior": d.cartao = Mathf.Clamp(d.cartao * 1.25f, 0.5f, 2.5f); break;
            case "cartao_menor": d.cartao = Mathf.Clamp(d.cartao / 1.25f, 0.5f, 2.5f); break;
            case "trocar_texto":
                if (texto.Length == 0) return new JObject { ["erro"] = "faltou o novo texto" };
                d.texto = Limitar(texto); break;
            case "acrescentar":
                if (texto.Length == 0) return new JObject { ["erro"] = "faltou o texto a acrescentar" };
                d.texto = Limitar(d.texto + " " + texto); break;
            case "cor":
                if (string.IsNullOrWhiteSpace(args.Value<string>("cor"))) return new JObject { ["erro"] = "faltou a cor" };
                d.cor = args.Value<string>("cor").Trim().ToLowerInvariant(); break;
            default: return new JObject { ["erro"] = "acao inválida" };
        }
        nc.Aplicar(true);
        return new JObject { ["result"] = "nota atualizada: " + d.texto };
    }

    string Limitar(string t) => t.Length > maxCaracteres ? t.Substring(0, maxCaracteres).TrimEnd() + "…" : t;

    static JArray TextosDe(List<HoloItem> l)
    {
        var a = new JArray();
        foreach (var n in l) a.Add(LerDados(n).texto);
        return a;
    }

    static List<HoloItem> Achar(List<HoloItem> notas, string assunto, out List<string> tokens)
    {
        tokens = Tokens(assunto);
        var achadas = new List<HoloItem>();
        foreach (var n in notas)
        {
            string t = ApagadorHolo.Normalizar(LerDados(n).texto);
            bool todas = true;
            foreach (var tk in tokens) if (!t.Contains(tk)) { todas = false; break; }
            if (todas) achadas.Add(n);
        }
        return achadas;
    }

    static List<string> Tokens(string s)
    {
        var l = new List<string>();
        foreach (var t in ApagadorHolo.Normalizar(s).Split(new[] { ' ', ',', '.' }, System.StringSplitOptions.RemoveEmptyEntries))
            if (t.Length > 2 && t != "nota" && t != "sobre" && t != "que" && t != "para" && t != "com" && t != "uma" && t != "das" && t != "dos")
                l.Add(t.Length > 3 && t.EndsWith("s") ? t.Substring(0, t.Length - 1) : t);
        return l;
    }

    static List<HoloItem> Notas()
    {
        var l = new List<HoloItem>();
        foreach (var i in HoloRegistro.Itens) if (i.go != null && i.tipo == "nota") l.Add(i);
        return l;
    }

    // ---------------- cartão ----------------
    public GameObject CriarCartao(Dados dados, Vector3 pos, Quaternion rot)
    {
        var raiz = new GameObject("Nota_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, rot);
        raiz.transform.localScale = Vector3.one * Mathf.Clamp(dados.escala, 0.1f, 4f);

        // "Visual" anima a entrada; a escala da raiz é só a do usuário (duas mãos)
        var visual = new GameObject("Visual").transform;
        visual.SetParent(raiz.transform, false);

        var cartao = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cartao.name = "Cartao";
        Destroy(cartao.GetComponent<Collider>());
        cartao.transform.SetParent(visual, false);
        CriadorHolograma.AplicarVisual(cartao, CriadorHolograma.ParseCor(dados.cor), new Vector2(1, 1), 0.6f, null, 0.62f);

        var t = HoloUtil.CriarTexto(visual, "Texto", dados.texto, new Vector3(0, 0, -0.0035f),
            new Vector2(0.2f, 0.1f), letraBase, Tinta, TextAlignmentOptions.TopLeft);
        t.enableAutoSizing = false;                       // nunca encolhe sozinho: o cartão é que cresce
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.fontStyle = FontStyles.Bold;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sortingOrder = 5;

        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        raiz.AddComponent<BoxCollider>();
        raiz.AddComponent<XRGrabInteractable>();

        var nota = raiz.AddComponent<NotaCartao>();
        nota.Configurar(this, dados, cartao.transform, t, visual);   // dimensiona cartão, texto e colisor
        InteracaoMaos.Aplicar(raiz);                                  // depois: LimiteEscala mede o cartão já dimensionado

        string curto = dados.texto.Length > 60 ? dados.texto.Substring(0, 60) + "…" : dados.texto;
        var item = HoloRegistro.Adicionar(raiz, "nota", dados.cor, "nota: " + curto, "nota", nota.MaiorLado(), JsonConvert.SerializeObject(dados));
        nota.item = item;
        StartCoroutine(HoloUtil.Surgir(visual, 0f, 0.35f));
        return raiz;
    }

    public GameObject CriarCartao(string texto, Vector3 pos, Quaternion rot) => CriarCartao(new Dados { texto = texto }, pos, rot);

    // Procura a superfície detectada (plano do AR Foundation) mais próxima nas 6 direções; devolve ponto e normal.
    public bool AcharSuperficie(Vector3 centro, out Vector3 ponto, out Vector3 normal)
    {
        ponto = default; normal = default;
        float melhor = float.MaxValue;
        foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.down, Vector3.up })
        {
            foreach (var h in Physics.RaycastAll(centro, dir, alcanceGrudar, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<ARPlane>() == null) continue;
                if (h.distance < melhor) { melhor = h.distance; ponto = h.point; normal = h.normal; }
            }
        }
        return melhor < float.MaxValue;
    }

    public float Largura => larguraBase;
    public float AlturaMax => alturaMaxBase;
    public float Letra => letraBase;
}

// Comportamento de uma nota: layout do texto, salvar estado, colar na parede/mesa ao ser solta.
public class NotaCartao : MonoBehaviour
{
    public NotasHolo.Dados dados;
    public HoloItem item;
    NotasHolo dono;
    XRGrabInteractable grab;
    Coroutine rotina;
    Transform cartao, visual;
    TextMeshPro texto;
    float largura, altura;
    float proximaChecagem;
    float escalaSalva = 1f;

    public void Configurar(NotasHolo n, NotasHolo.Dados d, Transform cartaoT, TextMeshPro t, Transform vis)
    {
        dono = n; dados = d; cartao = cartaoT; texto = t; visual = vis;
        escalaSalva = d.escala;
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectExited.AddListener(AoSoltar);
        Aplicar(false);
        proximaChecagem = Time.time + 1.5f;
    }

    void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(AoSoltar);
    }

    public void DefinirCor(string cor) { dados.cor = cor; Aplicar(true); }

    public float MaiorLado() => Mathf.Max(largura, altura);

    // layout: letra com o tamanho aparente dos painéis; o cartão cresce em altura (até o máximo) em vez de encolher o texto
    public void Aplicar(bool salvar)
    {
        float k = dados.cartao;
        largura = dono.Largura * k;
        float caixaL = largura * 0.9f;
        float maxAlt = dono.AlturaMax * k;
        const float margem = 0.045f;

        texto.text = dados.texto;
        texto.fontSize = dono.Letra * dados.letra;
        float ph = texto.GetPreferredValues(dados.texto, caixaL, 0f).y;
        float alt = ph + margem;
        if (alt > maxAlt)
        {
            texto.fontSize *= 0.8f;                    // no máximo 20% menor
            ph = texto.GetPreferredValues(dados.texto, caixaL, 0f).y;
            alt = Mathf.Min(maxAlt, ph + margem);
        }
        altura = Mathf.Clamp(alt, 0.10f * k, maxAlt);

        cartao.localScale = new Vector3(largura, altura, 0.004f);
        texto.rectTransform.sizeDelta = new Vector2(caixaL, altura - 0.03f);
        texto.transform.localPosition = new Vector3(0, 0, -0.0035f);
        var bc = GetComponent<BoxCollider>();
        if (bc != null) bc.size = new Vector3(largura, altura, 0.03f);

        var cor = CriadorHolograma.ParseCor(dados.cor);
        var rend = cartao.GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null) rend.sharedMaterial.color = Color.Lerp(cor, Color.white, 0.35f);
        var fill = cartao.Find("Preenchimento");
        if (fill != null)
        {
            var fr = fill.GetComponent<Renderer>();
            if (fr != null && fr.sharedMaterial != null) fr.sharedMaterial.color = new Color(cor.r, cor.g, cor.b, 0.62f);
        }
        float lum = 0.299f * cor.r + 0.587f * cor.g + 0.114f * cor.b;
        texto.color = lum < 0.4f ? Color.white : new Color(0.01f, 0.16f, 0.22f);

        if (item != null)
        {
            item.cor = dados.cor;
            item.tamanho = MaiorLado();
            string curto = dados.texto.Length > 60 ? dados.texto.Substring(0, 60) + "…" : dados.texto;
            item.descricao = "nota: " + curto;
        }
        var lim = GetComponent<LimiteEscala>();
        if (lim != null) lim.Medir();
        if (salvar) Salvar();
    }

    // grava o estado no registro e pede nova gravação da âncora
    public void Salvar()
    {
        if (item == null) return;
        dados.escala = transform.localScale.x;
        escalaSalva = dados.escala;
        item.extra = JsonConvert.SerializeObject(dados);
        FindAnyObjectByType<AncorasHolo>()?.MarcarAlterado(gameObject);
    }

    void Update()
    {
        // escala feita com as duas mãos: salva quando o usuário solta e a escala mudou
        if (Time.time < proximaChecagem) return;
        proximaChecagem = Time.time + 1f;
        if (grab != null && grab.isSelected) return;
        if (Mathf.Abs(transform.localScale.x - escalaSalva) > 0.02f * escalaSalva) Salvar();
    }

    void AoSoltar(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _)
    {
        if (rotina != null) StopCoroutine(rotina);
        rotina = StartCoroutine(Grudar());
    }

    IEnumerator Grudar()
    {
        yield return new WaitForSeconds(0.45f);   // deixa a física/arremesso assentar
        if (grab != null && grab.isSelected) yield break;   // pegou de novo
        if (!dono.AcharSuperficie(transform.position, out var ponto, out var n)) yield break;

        var cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 cima = Mathf.Abs(n.y) > 0.9f && cam != null ? Vector3.ProjectOnPlane(cam.forward, n) : Vector3.up;
        if (cima.sqrMagnitude < 0.0001f) cima = Vector3.up;
        var rotFim = Quaternion.LookRotation(-n, cima);
        var posFim = ponto + n * 0.006f;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;   // colada: a física não a empurra para fora da parede
        }
        var p0 = transform.position; var r0 = transform.rotation;
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            if (grab != null && grab.isSelected) yield break;
            float k = Mathf.SmoothStep(0, 1, t / 0.25f);
            transform.SetPositionAndRotation(Vector3.Lerp(p0, posFim, k), Quaternion.Slerp(r0, rotFim, k));
            yield return null;
        }
        transform.SetPositionAndRotation(posFim, rotFim);
        FindAnyObjectByType<AncorasHolo>()?.MarcarAlterado(gameObject);
    }
}
