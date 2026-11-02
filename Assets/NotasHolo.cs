using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Notas holográficas: "Zetta, anote: ..." cria um cartão fino ciano à frente do usuário. Dá para pegar com a mão;
// ao soltar perto de parede ou mesa ele gruda na superfície. Cada nota é um holograma do registro (tipo "nota"),
// então tem âncora espacial (reaparece no mesmo lugar), pode ser apagada pela voz, pela lixeira ou jogada fora.
public class NotasHolo : MonoBehaviour
{
    public Vector3 posicaoVista = new Vector3(0f, -0.02f, 0.55f);
    public Vector2 tamanhoCartao = new Vector2(0.20f, 0.14f);
    public float alcanceGrudar = 0.25f;
    public int maxCaracteres = 280;

    static readonly Color Papel = new Color(0.72f, 0.97f, 1f);
    static readonly Color Tinta = new Color(0.01f, 0.16f, 0.22f);

    AssistenteVoz voz;
    int contador;

    const string DeclAnotar = @"{
  ""name"": ""anotar"",
  ""description"": ""Cria uma nota holográfica (cartão ciano) à frente do usuário com o texto ditado, que depois pode ser pega e colada numa parede ou mesa."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""texto"": { ""type"": ""STRING"", ""description"": ""Texto da nota, curto, em português."" } },
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
    const string Instrucao =
        "'Zetta, anote: ...' → anotar com o texto; confirme em poucas palavras. 'leia minhas notas' → gerenciar_notas acao=ler e leia cada nota em voz alta; " +
        "'mostre minhas notas' → acao=mostrar; 'apague a nota sobre X' → acao=apagar com assunto=X. 'Apague isso' olhando uma nota usa apagar_objeto alvo=isso.";

    void Awake()
    {
        AncorasHolo.Fabricas["nota"] = (item, pos, rot) => CriarCartao(item.extra, pos, rot);
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclAnotar, Anotar, Instrucao);
        voz.RegistrarFerramenta(DeclGerenciar, Gerenciar, "");
    }

    // ---------------- voz ----------------
    public JObject Anotar(JObject args)
    {
        string texto = (args.Value<string>("texto") ?? "").Trim();
        if (texto.Length == 0) return new JObject { ["erro"] = "texto da nota vazio" };
        if (texto.Length > maxCaracteres) texto = texto.Substring(0, maxCaracteres).TrimEnd() + "…";
        var pos = HoloUtil.PontoNaVista(posicaoVista);
        var go = CriarCartao(texto, pos, HoloUtil.OlharParaUsuario(pos));
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
            foreach (var n in notas) { textos.Add(n.extra); linhas.Add(i++ + ". " + n.extra); }
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
            var tokens = Tokens(args.Value<string>("assunto"));
            var achadas = new List<HoloItem>();
            foreach (var n in notas)
            {
                string t = ApagadorHolo.Normalizar(n.extra);
                bool todas = true;
                foreach (var tk in tokens) if (!t.Contains(tk)) { todas = false; break; }
                if (todas) achadas.Add(n);
            }
            if (achadas.Count == 0)
            {
                var textos = new JArray();
                foreach (var n in notas) textos.Add(n.extra);
                return new JObject { ["erro"] = "nenhuma nota combina com esse assunto", ["notas"] = textos };
            }
            if (achadas.Count > 1 && tokens.Count > 0)
            {
                var textos = new JArray();
                foreach (var n in achadas) textos.Add(n.extra);
                return new JObject { ["erro"] = "mais de uma nota combina; pergunte qual", ["notas"] = textos };
            }
            if (achadas.Count > 1)   // sem assunto: só apaga se for a única nota
                return new JObject { ["erro"] = "há " + achadas.Count + " notas; diga o assunto da que quer apagar" };
            string apagada = achadas[0].extra;
            apagador.Apagar(achadas[0].go);
            return new JObject { ["result"] = "apaguei a nota: " + apagada };
        }
        return new JObject { ["erro"] = "acao inválida" };
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
    public GameObject CriarCartao(string texto, Vector3 pos, Quaternion rot)
    {
        var raiz = new GameObject("Nota_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, rot);

        var w = tamanhoCartao.x; var h = tamanhoCartao.y;
        var cartao = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cartao.name = "Cartao";
        Destroy(cartao.GetComponent<Collider>());
        cartao.transform.SetParent(raiz.transform, false);
        cartao.transform.localScale = new Vector3(w, h, 0.004f);
        CriadorHolograma.AplicarVisual(cartao, HoloUtil.Ciano, new Vector2(1, 1), 0.6f, null, 0.62f);

        // papel claro e texto escuro na frente (lado -Z, voltado para quem olha)
        var t = HoloUtil.CriarTexto(raiz.transform, "Texto", texto, new Vector3(0, 0, -0.0035f),
            new Vector2(w * 0.88f, h * 0.84f), 0.05f, Tinta);
        t.fontSizeMin = 0.012f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.TopLeft;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sortingOrder = 5;

        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        var caixa = raiz.AddComponent<BoxCollider>();
        caixa.size = new Vector3(w, h, 0.03f);
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);

        var nota = raiz.AddComponent<NotaCartao>();
        nota.Configurar(this);

        string curto = texto.Length > 40 ? texto.Substring(0, 40) + "…" : texto;
        HoloRegistro.Adicionar(raiz, "nota", "ciano", "nota: " + curto, "nota", Mathf.Max(w, h), texto);
        StartCoroutine(HoloUtil.Surgir(raiz.transform, 0f, 0.35f));
        return raiz;
    }

    // ---------------- grudar na superfície ----------------
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
}

// Comportamento de uma nota: ao ser solta, cola na parede/mesa mais próxima.
public class NotaCartao : MonoBehaviour
{
    NotasHolo dono;
    XRGrabInteractable grab;
    Coroutine rotina;

    public void Configurar(NotasHolo n)
    {
        dono = n;
        grab = GetComponent<XRGrabInteractable>();
        if (grab != null) grab.selectExited.AddListener(AoSoltar);
    }

    void OnDestroy()
    {
        if (grab != null) grab.selectExited.RemoveListener(AoSoltar);
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
