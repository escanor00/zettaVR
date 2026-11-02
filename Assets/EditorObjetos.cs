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
      ""cor"": { ""type"": ""STRING"", ""description"": ""Nova cor em português ou hexadecimal. Para acao=cor."" },
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
        Vector3 dir;
        switch ((a.Value<string>("direcao") ?? "").ToLowerInvariant())
        {
            case "esquerda": dir = Vector3.left; break;
            case "direita": dir = Vector3.right; break;
            case "cima": dir = Vector3.up; break;
            case "baixo": dir = Vector3.down; break;
            case "frente": dir = Vector3.forward; break;
            case "tras": case "trás": dir = Vector3.back; break;
            default: return new JObject { ["erro"] = "direção inválida" };
        }
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

    JObject MudarCor(HoloItem item, JObject a)
    {
        string nome = a.Value<string>("cor");
        if (string.IsNullOrWhiteSpace(nome)) return new JObject { ["erro"] = "cor não informada" };
        if (item.tipo != "primitiva") return new JObject { ["erro"] = "só dá para mudar a cor de formas simples (cubo, esfera...), não deste objeto" };
        Color cor = CriadorHolograma.ParseCor(nome);
        var r = item.go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial.color = Color.Lerp(cor, Color.white, 0.35f);
        var fill = item.go.transform.Find("Preenchimento");
        if (fill != null)
        {
            var fr = fill.GetComponent<Renderer>();
            if (fr != null) fr.sharedMaterial.color = new Color(cor.r, cor.g, cor.b, 0.3f);
        }
        item.cor = nome;
        item.descricao = item.forma + " " + nome;
        AvisarAncoras(item.go);
        return new JObject { ["result"] = "cor alterada para " + nome };
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
        HoloRegistro.Carregando = true;   // a mesa holográfica não faz a cópia subir do centro
        try { novo = fabrica(item, pos, item.go.transform.rotation); }
        finally { HoloRegistro.Carregando = false; }
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
