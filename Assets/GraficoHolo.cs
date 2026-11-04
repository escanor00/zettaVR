using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Gráficos de barras 3D em luz sobre a mesa holográfica (ou ~0,8 m à frente), com dados reais.
//  - grafico_temperatura_semana: previsão de 7 dias (máxima e mínima) da cidade, via Open-Meteo (sem chave).
//  - criar_grafico: valores que a própria Zetta informa (ex.: números que ela acabou de pesquisar).
public class GraficoHolo : MonoBehaviour
{
    public Vector3 posicaoVista = new Vector3(0f, -0.1f, 0.8f);
    public float larguraTotal = 0.5f;
    public float alturaMax = 0.24f;

    const string DeclSemana = @"{
  ""name"": ""grafico_temperatura_semana"",
  ""description"": ""Mostra um gráfico 3D (sobre a mesa holográfica ou à frente) com a previsão de temperatura máxima dos próximos 7 dias de uma cidade, com dados reais."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""cidade"": { ""type"": ""STRING"", ""description"": ""Ex.: São Paulo."" } }, ""required"": [""cidade""] }
}";
    const string DeclGrafico = @"{
  ""name"": ""criar_grafico"",
  ""description"": ""Mostra um gráfico de barras 3D em luz com os valores informados (de 2 a 10 barras)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""titulo"": { ""type"": ""STRING"" },
      ""rotulos"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""STRING"" }, ""description"": ""Nome curto de cada barra."" },
      ""valores"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""NUMBER"" } },
      ""unidade"": { ""type"": ""STRING"", ""description"": ""Ex.: °C, %, R$."" }
    },
    ""required"": [""titulo"", ""rotulos"", ""valores""]
  }
}";
    const string Instrucao =
        "Para 'gráfico da temperatura da semana' chame grafico_temperatura_semana (dados reais). Para outros gráficos, use criar_grafico " +
        "somente com números reais que você tenha de uma ferramenta ou da busca; nunca invente valores.";

    static readonly string[] Dias = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };
    AssistenteVoz voz;
    GameObject atual;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclSemana, a => Semana(a), Instrucao);
        voz.RegistrarFerramenta(DeclGrafico, CriarGraficoTool, null);
    }

    JObject Semana(JObject a)
    {
        string cidade = (a.Value<string>("cidade") ?? "").Trim();
        if (cidade.Length == 0) return new JObject { ["erro"] = "cidade não informada" };
        StartCoroutine(SemanaRotina(cidade));
        return new JObject { ["result"] = "buscando a previsão; o gráfico aparece em instantes" };
    }

    JObject CriarGraficoTool(JObject a)
    {
        var rot = new List<string>(); var val = new List<float>();
        if (a["rotulos"] is JArray r) foreach (var x in r) rot.Add(x.ToString());
        if (a["valores"] is JArray v) foreach (var x in v) val.Add(x.Value<float>());
        int n = Mathf.Min(rot.Count, val.Count);
        if (n < 2) return new JObject { ["erro"] = "preciso de pelo menos 2 rótulos e 2 valores" };
        Criar(a.Value<string>("titulo") ?? "Gráfico", rot.GetRange(0, Mathf.Min(n, 10)), val.GetRange(0, Mathf.Min(n, 10)), a.Value<string>("unidade") ?? "");
        return new JObject { ["result"] = "gráfico criado" };
    }

    IEnumerator SemanaRotina(string cidade)
    {
        string geo = null;
        yield return Baixar("https://geocoding-api.open-meteo.com/v1/search?count=1&language=pt&name=" + UnityWebRequest.EscapeURL(cidade), t => geo = t);
        JToken local = null;
        try { if (geo != null) local = JObject.Parse(geo)["results"]?[0]; } catch { }
        if (local == null) { Falha(geo == null ? "o serviço de clima está sem resposta" : "não encontrei a cidade " + cidade); yield break; }
        string nome = local.Value<string>("name") ?? cidade;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + local.Value<double>("latitude").ToString(ci)
            + "&longitude=" + local.Value<double>("longitude").ToString(ci) + "&daily=temperature_2m_max&timezone=auto&forecast_days=7";
        string dados = null;
        yield return Baixar(url, t => dados = t);
        if (dados == null) { Falha("o serviço de clima está sem resposta"); yield break; }

        var rot = new List<string>(); var val = new List<float>();
        try
        {
            var d = JObject.Parse(dados)["daily"];
            var dias = d["time"]; var maxs = d["temperature_2m_max"];
            for (int i = 0; i < ((JArray)dias).Count; i++)
            {
                var dt = DateTime.Parse(dias[i].ToString(), ci);
                rot.Add(Dias[(int)dt.DayOfWeek]);
                val.Add(maxs[i].Value<float>());
            }
        }
        catch (Exception) { Falha("não consegui ler a previsão"); yield break; }

        Criar("Máxima da semana em " + nome, rot, val, "°C");
        int imax = 0; for (int i = 1; i < val.Count; i++) if (val[i] > val[imax]) imax = i;
        if (voz != null) voz.Falar("o gráfico da temperatura da semana em " + nome + " está na tela; o dia mais quente é " + rot[imax]
            + " com " + Mathf.RoundToInt(val[imax]) + " graus. Diga isso em uma frase curta.");
    }

    void Falha(string msg)
    {
        Debug.LogWarning("GraficoHolo: " + msg);
        if (voz != null) { voz.MostrarAviso(msg, 6f); voz.Falar("aviso: " + msg + ". Explique ao usuário em uma frase."); }
    }

    IEnumerator Baixar(string url, Action<string> aoTerminar)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            aoTerminar(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
        }
    }

    // ---------- construção ----------
    public GameObject Criar(string titulo, List<string> rotulos, List<float> valores, string unidade)
    {
        if (atual != null) { HoloRegistro.Remover(atual); Destroy(atual); }

        var mesa = FindAnyObjectByType<MesaHolografica>();
        Vector3 pos = mesa != null && mesa.ativa ? mesa.Centro + Vector3.up * 0.03f : HoloUtil.PontoNaVista(posicaoVista);
        var raiz = new GameObject("Grafico_" + titulo);
        raiz.transform.SetParent(transform, true);
        raiz.transform.position = pos;
        raiz.transform.rotation = HoloUtil.OlharParaUsuario(pos);   // +Z para longe do usuário: as faces legíveis olham para ele

        int n = valores.Count;
        float min = Mathf.Min(0f, Mathf.Min(valores.ToArray())), max = Mathf.Max(valores.ToArray());
        float faixa = Mathf.Max(0.0001f, max - min);
        float passo = larguraTotal / n;
        float largBarra = passo * 0.6f;
        for (int i = 0; i < n; i++)
        {
            float altura = Mathf.Max(0.01f, (valores[i] - min) / faixa * alturaMax);
            float x = -larguraTotal * 0.5f + passo * (i + 0.5f);
            var cor = Color.Lerp(TemaZetta.AzulTec, TemaZetta.Dourado, Mathf.InverseLerp(min, max, valores[i]));
            var barra = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barra.name = "Barra" + i;
            Destroy(barra.GetComponent<Collider>());
            barra.transform.SetParent(raiz.transform, false);
            barra.transform.localScale = new Vector3(largBarra, altura, largBarra);
            barra.transform.localPosition = new Vector3(x, altura * 0.5f, 0f);
            CriadorHolograma.AplicarVisual(barra, cor, new Vector2(2, 2), 0.7f + 0.12f * i, null, 0.3f);

            var rotulo = HoloUtil.CriarTexto(raiz.transform, "Valor" + i, Mathf.RoundToInt(valores[i]) + unidade,
                new Vector3(x, altura + 0.025f, -largBarra * 0.5f - 0.01f), new Vector2(passo, 0.04f), 0.14f, TemaZetta.Branco);
            rotulo.fontSizeMin = 0.1f;
            var eixo = HoloUtil.CriarTexto(raiz.transform, "Rotulo" + i, rotulos[i],
                new Vector3(x, -0.03f, -largBarra * 0.5f - 0.01f), new Vector2(passo, 0.04f), 0.14f, TemaZetta.Ciano);
            eixo.fontSizeMin = 0.1f;
        }
        HoloUtil.CriarTexto(raiz.transform, "Titulo", titulo, new Vector3(0f, alturaMax + 0.1f, -0.02f),
            new Vector2(larguraTotal * 1.1f, 0.05f), 0.2f, TemaZetta.AzulTec).fontSizeMin = 0.12f;

        // linha de base
        var baseGo = new GameObject("Base");
        baseGo.transform.SetParent(raiz.transform, false);
        var lr = baseGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = HoloUtil.NovoMaterialSprite(TemaZetta.AzulTec);
        lr.startColor = lr.endColor = TemaZetta.AzulTec;
        lr.widthMultiplier = 0.003f;
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(-larguraTotal * 0.55f, 0f, 0f));
        lr.SetPosition(1, new Vector3(larguraTotal * 0.55f, 0f, 0f));

        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false; rb.linearDamping = 3f; rb.angularDamping = 3f;
        raiz.AddComponent<ColisorPegada>();
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);
        HoloRegistro.Adicionar(raiz, "grafico", "", "gráfico: " + titulo, "grafico", 0.5f);
        atual = raiz;
        return raiz;
    }
}
