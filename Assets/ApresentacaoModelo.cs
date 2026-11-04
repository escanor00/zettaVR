using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Modo apresentação: "Zetta, me explica o sistema solar / a molécula / o globo". Monta o modelo e, peça por peça,
// destaca a peça (pulso de tamanho + brilho) enquanto a Zetta fala dela. Cada peça só avança quando a fala anterior termina.
public class ApresentacaoModelo : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""apresentar_modelo"",
  ""description"": ""Monta o modelo (sistema solar, molécula de água ou globo terrestre) e o apresenta peça por peça, destacando cada parte enquanto você a explica. Use para 'me explica o sistema solar', 'explique a molécula', 'fale sobre o globo'."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""modelo"": { ""type"": ""STRING"", ""enum"": [""sistema_solar"", ""molecula_agua"", ""globo_terrestre""] } },
    ""required"": [""modelo""]
  }
}";
    const string Instrucao =
        "Para 'me explica o sistema solar/a molécula/o globo' chame apresentar_modelo e responda só 'vamos lá'. Depois o sistema pedirá, uma peça por vez, " +
        "'explique <peça>'; responda com UMA frase curta e interessante sobre a peça, em português.";

    static readonly HashSet<string> Ignorar = new HashSet<string> { "Orbita", "Ligacao", "Eixo", "AnelSaturno", "Preenchimento" };

    AssistenteVoz voz;
    ModelosProntos modelos;
    Coroutine rotina;
    public bool Apresentando => rotina != null;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        modelos = FindAnyObjectByType<ModelosProntos>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        string nome = (a.Value<string>("modelo") ?? "").ToLowerInvariant();
        if (modelos == null) return new JObject { ["erro"] = "modelos indisponíveis" };
        if (rotina != null) StopCoroutine(rotina);
        rotina = StartCoroutine(Apresentar(nome));
        return new JObject { ["result"] = "montando o modelo; vou pedir cada peça em seguida" };
    }

    IEnumerator Apresentar(string nome)
    {
        var r = modelos.Mostrar(new JObject { ["modelo"] = nome });
        if (r["erro"] != null) { rotina = null; yield break; }
        var item = HoloRegistro.Itens.FindLast(i => i.tipo == "modelo" && i.extra == nome);
        if (item == null || item.go == null) { rotina = null; yield break; }
        yield return new WaitForSeconds(3.2f);   // montagem das peças

        var pecas = new List<(Transform t, string nome)>();
        Coletar(item.go.transform, pecas);
        if (nome == "globo_terrestre")
        {
            var globo = pecas.Count > 0 ? pecas[0].t : item.go.transform;
            pecas.Clear();
            pecas.Add((globo, "a forma e o tamanho da Terra"));
            pecas.Add((globo, "a rotação da Terra e o eixo inclinado, que causa as estações"));
        }

        foreach (var p in pecas)
        {
            if (p.t == null) continue;
            string desc = Humano(p.nome);
            voz.Falar("explique " + desc + " (modelo " + nome.Replace('_', ' ') + ") em UMA frase curta.");
            var pulso = StartCoroutine(Pulsar(p.t));
            // espera a fala começar (até 8 s) e terminar (até 25 s)
            float t0 = Time.time;
            while (!voz.Falando && Time.time - t0 < 8f) yield return null;
            t0 = Time.time;
            while (voz.Falando && Time.time - t0 < 25f) yield return null;
            StopCoroutine(pulso);
            if (p.t != null && escalaOriginal.TryGetValue(p.t, out var esc)) p.t.localScale = esc;
            yield return new WaitForSeconds(0.4f);
        }
        voz.Falar("a apresentação terminou; convide o usuário a perguntar mais, em uma frase.");
        rotina = null;
    }

    readonly Dictionary<Transform, Vector3> escalaOriginal = new Dictionary<Transform, Vector3>();

    static void Coletar(Transform raiz, List<(Transform, string)> saida)
    {
        foreach (Transform c in raiz)
        {
            if (Ignorar.Contains(c.name) || c.name.StartsWith("Pivo")) { if (c.name.StartsWith("Pivo") || c.childCount > 0) Coletar(c, saida); continue; }
            if (c.GetComponent<MeshFilter>() != null && c.GetComponent<Renderer>() != null) saida.Add((c, c.name));
            Coletar(c, saida);
        }
    }

    static string Humano(string n)
    {
        switch (n)
        {
            case "Oxigenio": return "o átomo de oxigênio";
            case "Hidrogenio1": return "o primeiro átomo de hidrogênio";
            case "Hidrogenio2": return "o segundo átomo de hidrogênio";
            case "Sol": return "o Sol";
        }
        return n.StartsWith("Planeta") ? n : "o planeta " + n;
    }

    // pulso de tamanho (1 → 1,35 → 1) até ser interrompido
    IEnumerator Pulsar(Transform t)
    {
        if (!escalaOriginal.ContainsKey(t)) escalaOriginal[t] = t.localScale;
        var baseEsc = escalaOriginal[t];
        float x = 0f;
        while (t != null)
        {
            x += Time.deltaTime * 3f;
            t.localScale = baseEsc * (1.18f + 0.17f * Mathf.Sin(x));
            yield return null;
        }
    }
}
