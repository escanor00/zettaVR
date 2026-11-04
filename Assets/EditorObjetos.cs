using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Ferramenta "editar_objeto": mover, mudar a cor, aumentar/diminuir e duplicar hologramas já criados, por voz.
public class EditorObjetos : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""editar_objeto"",
  ""description"": ""Edita um holograma que já existe: mover, mudar a cor, aumentar ou diminuir, ou duplicar."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""mover"", ""cor"", ""tamanho"", ""duplicar""] },
      ""alvo"": { ""type"": ""STRING"", ""enum"": [""descricao"", ""isso""], ""description"": ""'isso' = o objeto que o usuário está olhando ou apontando."" },
      ""descricao"": { ""type"": ""STRING"", ""description"": ""Descrição do objeto, ex.: cubo azul, esfera vermelha, sistema solar. Só para alvo=descricao."" },
      ""direcao"": { ""type"": ""STRING"", ""enum"": [""esquerda"", ""direita"", ""cima"", ""baixo"", ""frente"", ""tras""], ""description"": ""Para acao=mover, relativa ao usuário."" },
      ""distancia"": { ""type"": ""NUMBER"", ""description"": ""Metros a mover (padrão 0.25)."" },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Nova cor em português ou hexadecimal, ou relativa (mais claro, mais escuro, azul mais escuro). Para acao=cor. Vale para qualquer objeto (formas, modelos, desenhos, notas, modelos 3D)."" },
      ""fator"": { ""type"": ""NUMBER"", ""description"": ""Multiplicador de tamanho: 1.5 aumenta 50%, 0.5 reduz à metade. Para acao=tamanho."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao =
        "Para mexer em objetos que já existem use editar_objeto: 'mova o cubo para a direita' → acao=mover; 'pinte de verde' → acao=cor; " +
        "'aumente'/'diminua' → acao=tamanho com fator; 'duplique' → acao=duplicar. Use alvo=isso quando disserem 'isso' ou 'esse'; " +
        "senão alvo=descricao com a descrição do objeto. Confirme em poucas palavras.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Editar, Instrucao);
    }

    public JObject Editar(JObject a)
    {
        var desenho = EditarDesenho(a);
        if (desenho != null) return desenho;
        var item = ResolverAlvo(a, out string erro);
        if (item == null || item.go == null) return new JObject { ["erro"] = erro ?? "objeto não encontrado" };
        switch ((a.Value<string>("acao") ?? "").ToLowerInvariant())
        {
            case "mover": return Mover(item, a);
            case "cor": return MudarCor(item, a);
            case "tamanho": return MudarTamanho(item, a);
            case "duplicar": return Duplicar(item);
        }
        return new JObject { ["erro"] = "acao inválida" };
    }

    HoloItem ResolverAlvo(JObject a, out string erro)
    {
        erro = null;
        if ((a.Value<string>("alvo") ?? "descricao") == "isso")
        {
            var it = HoloAlvo.ObjetoOlhado();
            if (it == null) erro = "não vi qual objeto você está olhando; peça pela descrição";
            return it;
        }
        var achado = ApagadorHolo.Buscar(a.Value<string>("descricao") ?? "");
        if (achado == null) erro = "não encontrei esse objeto";
        return achado;
    }

    JObject Mover(HoloItem item, JObject a)
    {
        var yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        var d = DirecaoMover(a.Value<string>("direcao"));
        if (!d.HasValue) return new JObject { ["erro"] = "direção inválida" };
        var dir = d.Value;
        float dist = Mathf.Clamp(a.Value<float?>("distancia") ?? 0.25f, 0.05f, 3f);
        StartCoroutine(Deslizar(item.go, item.go.transform.position + yaw * dir * dist));
        return new JObject { ["result"] = "movendo " + item.descricao };
    }

    IEnumerator Deslizar(GameObject go, Vector3 fim)
    {
        var rb = go.GetComponent<Rigidbody>();
        bool eraCinematico = rb != null && rb.isKinematic;
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
        var ini = go.transform.position;
        const float dur = 0.5f;
        for (float t = 0; t < dur && go != null; t += Time.deltaTime)
        {
            float x = t / dur;
            go.transform.position = Vector3.Lerp(ini, fim, 1f - Mathf.Pow(1f - x, 3f));
            yield return null;
        }
        if (go == null) yield break;
        go.transform.position = fim;
        if (rb != null) rb.isKinematic = eraCinematico;
    }

    // desenhos no ar não ficam no registro: "pinte o desenho de verde", "mova/aumente o desenho"
    JObject EditarDesenho(JObject a)
    {
        string d = ApagadorHolo.Normalizar(a.Value<string>("descricao") ?? "");
        if (!(d.Contains("desenho") || d.Contains("traco") || d.Contains("risco") || d.Contains("rabisco"))) return null;
        var des = FindAnyObjectByType<DesenhoNoAr>();
        if (des == null || des.QuantidadeTracos == 0) return new JObject { ["erro"] = "não há desenhos no ar" };
        switch ((a.Value<string>("acao") ?? "").ToLowerInvariant())
        {
            case "cor":
                des.Tingir(CriadorHolograma.CorDeEdicao(a.Value<string>("cor"), des.CorAtual));
                return new JObject { ["result"] = "desenho pintado" };
            case "tamanho":
                des.Transformar(Vector3.zero, Mathf.Clamp(a.Value<float?>("fator") ?? 1.5f, 0.3f, 3f));
                return new JObject { ["result"] = "desenho redimensionado" };
            case "mover":
                var dir = DirecaoMover(a.Value<string>("direcao"));
                if (!dir.HasValue) return new JObject { ["erro"] = "direção inválida" };
                var yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
                des.Transformar(yaw * dir.Value * Mathf.Clamp(a.Value<float?>("distancia") ?? 0.25f, 0.05f, 3f), 1f);
                return new JObject { ["result"] = "desenho movido" };
        }
        return new JObject { ["erro"] = "não dá para duplicar um desenho" };
    }

    static Vector3? DirecaoMover(string d)
    {
        switch ((d ?? "").ToLowerInvariant())
        {
            case "esquerda": return Vector3.left;
            case "direita": return Vector3.right;
            case "cima": return Vector3.up;
            case "baixo": return Vector3.down;
            case "frente": return Vector3.forward;
            case "tras": case "trás": return Vector3.back;
        }
        return null;
    }

    // vale para TODOS os tipos: formas, modelos prontos, .glb (tinge o material base), notas, gráficos e o que mais tiver renderers
    JObject MudarCor(HoloItem item, JObject a)
    {
        string nome = a.Value<string>("cor");
        if (string.IsNullOrWhiteSpace(nome)) return new JObject { ["erro"] = "cor não informada" };
        var oi = ObjetoInterativo.Garantir(item.go);
        Color atual = oi.partes.Count > 0 ? oi.partes[0].CorVisual() : CriadorHolograma.ParseCor(item.cor);
        Color cor = CriadorHolograma.CorDeEdicao(nome, atual);
        string hex = "#" + ColorUtility.ToHtmlStringRGB(cor);
        var nota = item.go.GetComponent<NotaCartao>();
        if (nota != null) nota.DefinirCor(hex);
        else if (oi.partes.Count > 0)
        {
            if (ApagadorHolo.Normalizar(nome).Contains("claro") || ApagadorHolo.Normalizar(nome).Contains("escuro")) oi.PintarTexto("tudo", nome);   // cada parte ajusta a própria cor
            else oi.PintarTudo(cor);
        }
        else TingirGenerico(item.go, cor);
        bool relativo = ApagadorHolo.Normalizar(nome).Contains("claro") || ApagadorHolo.Normalizar(nome).Contains("escuro");
        item.cor = relativo ? hex : nome;
        if (item.tipo == "primitiva") item.descricao = item.forma + " " + item.cor;
        AvisarAncoras(item.go);
        return new JObject { ["result"] = "cor alterada para " + nome };
    }

    // objetos sem partes (gráficos, etc.): tinge todos os renderers e linhas
    static void TingirGenerico(GameObject go, Color cor)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is TMPro.TextMeshPro || r.GetComponent<TMPro.TextMeshPro>() != null) continue;
            if (r is LineRenderer lr)
            {
                var c0 = lr.startColor;
                lr.startColor = lr.endColor = new Color(cor.r, cor.g, cor.b, c0.a);
                if (lr.sharedMaterial != null && lr.sharedMaterial.HasProperty("_Color"))
                {
                    var m = lr.sharedMaterial.color; lr.sharedMaterial.color = new Color(cor.r, cor.g, cor.b, m.a);
                }
                continue;
            }
            ParteHolo.AplicarCor(r, cor, r.gameObject.name == "Preenchimento");
        }
    }

    JObject MudarTamanho(HoloItem item, JObject a)
    {
        float fator = Mathf.Clamp(a.Value<float?>("fator") ?? 1.5f, 0.2f, 5f);
        float dim = InteracaoMaos.MaiorDimensao(item.go);
        float novo = Mathf.Clamp(dim * fator, InteracaoMaos.TamanhoMin, InteracaoMaos.TamanhoMax);
        float real = novo / dim;
        StartCoroutine(Escalar(item.go, item.go.transform.localScale * real));
        item.tamanho *= real;
        AvisarAncoras(item.go);
        string aviso = Mathf.Abs(real - fator) > 0.01f ? " (limitado entre 5 cm e 2 m)" : "";
        return new JObject { ["result"] = "tamanho " + (real >= 1f ? "aumentado" : "reduzido") + " " + Mathf.RoundToInt(real * 100f) + "%" + aviso };
    }

    IEnumerator Escalar(GameObject go, Vector3 fim)
    {
        var ini = go.transform.localScale;
        const float dur = 0.35f;
        for (float t = 0; t < dur && go != null; t += Time.deltaTime)
        {
            go.transform.localScale = Vector3.Lerp(ini, fim, t / dur);
            yield return null;
        }
        if (go != null) go.transform.localScale = fim;
    }

    JObject Duplicar(HoloItem item)
    {
        if (!AncorasHolo.Fabricas.TryGetValue(item.tipo, out var fabrica))
            return new JObject { ["erro"] = "não sei duplicar este tipo de objeto" };
        var yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        var pos = item.go.transform.position + yaw * Vector3.right * Mathf.Max(0.25f, InteracaoMaos.MaiorDimensao(item.go) * 1.2f);
        GameObject novo;
        item.estado = ObjetoInterativo.Garantir(item.go).Serializar();   // a cópia leva as cores das partes
        HoloRegistro.Carregando = true;   // a mesa holográfica não faz a cópia subir do centro
        try { novo = fabrica(item, pos, item.go.transform.rotation); }
        finally { HoloRegistro.Carregando = false; }
        if (novo != null && !string.IsNullOrEmpty(item.estado)) ObjetoInterativo.Garantir(novo).AplicarEstado(item.estado);
        if (novo == null) return new JObject { ["erro"] = "não consegui duplicar" };
        if (item.tipo != "glb") novo.transform.localScale = item.go.transform.localScale;
        return new JObject { ["result"] = "duplicado: " + item.descricao };
    }

    static void AvisarAncoras(GameObject go)
    {
        var a = FindAnyObjectByType<AncorasHolo>();
        if (a != null) a.MarcarAlterado(go);
    }
}
