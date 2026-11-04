using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// "Zetta, bom dia": briefing em sequência (hora e data, notas, timers, clima e notícias).
public class BriefingBomDia : MonoBehaviour
{
    public string cidadePadrao = "São Paulo";

    const string Decl = @"{
  ""name"": ""bom_dia"",
  ""description"": ""Faz o briefing da manhã em sequência: hora e data, notas, timers ativos, clima e notícias."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""cidade"": { ""type"": ""STRING"", ""description"": ""Cidade para o clima (padrão São Paulo)."" } } }
}";
    const string Instrucao = "Quando o usuário disser 'bom dia' (ou pedir o briefing/resumo do dia), chame bom_dia e siga as instruções que ela devolver.";

    AssistenteVoz voz;
    PainelInfo painel;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        painel = FindAnyObjectByType<PainelInfo>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject args)
    {
        string cidade = (args.Value<string>("cidade") ?? "").Trim();
        if (cidade.Length == 0) cidade = cidadePadrao;
        var agora = DateTime.Now;
        string[] dias = { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
        string hora = agora.ToString("HH:mm"), data = dias[(int)agora.DayOfWeek] + ", " + agora.ToString("dd/MM");

        var notas = new List<string>();
        foreach (var it in HoloRegistro.Itens)
            if (it.go != null && it.tipo == "nota") notas.Add(NotasHolo.LerDados(it).texto);
        var timers = FindAnyObjectByType<TimersHolo>();
        string resumoTimers = timers != null ? timers.Resumo() : "nenhum timer ativo";

        var linhas = new List<string> { hora + " • " + data };
        linhas.Add(notas.Count == 0 ? "Sem notas" : notas.Count + " nota(s): " + Curto(notas[0]));
        linhas.Add(resumoTimers == "nenhum timer ativo" ? "Sem timers" : "Timers: " + resumoTimers);
        if (painel != null) painel.Mostrar(new JObject { ["titulo"] = "Bom dia!", ["linhas"] = new JArray(linhas), ["duracao"] = 25 });

        string leituraNotas = notas.Count == 0 ? "não há notas" : "notas: " + string.Join("; ", notas);
        return new JObject
        {
            ["result"] = "Faça o briefing em sequência, uma frase curta por item: (1) cumprimente e diga que agora são " + hora + " de " + data + "; (2) " +
                leituraNotas + "; (3) timers: " + resumoTimers + "; (4) chame consultar_clima para " + cidade + " e resuma; (5) depois chame buscar_noticias " +
                "(principais do Brasil) e leia as duas primeiras manchetes."
        };
    }

    static string Curto(string t) => t.Length > 40 ? t.Substring(0, 40) + "…" : t;
}

// "Zetta, modo foco": só um timer grande no centro da vista; o resto (central, painéis, timers pequenos e hologramas) some
// e volta com "sair do modo foco" (ou quando o timer acaba).
public class ModoFoco : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""modo_foco"",
  ""description"": ""Entra ou sai do modo foco: mostra só um timer grande e esconde todo o resto até sair."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""entrar"", ""sair""] },
      ""minutos"": { ""type"": ""NUMBER"", ""description"": ""Duração do timer (padrão 25)."" },
      ""nome"": { ""type"": ""STRING"", ""description"": ""Nome do foco, ex.: estudar."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "'modo foco' ou 'quero focar' → modo_foco acao=entrar (minutos opcional); 'sair do modo foco' → acao=sair.";

    AssistenteVoz voz;
    Transform painel;
    TextMeshPro tempo, rotulo;
    PainelVidro vidro;
    float fim;
    bool ativo, terminou;
    readonly List<GameObject> escondidos = new List<GameObject>();
    public bool Ativo => ativo;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "entrar").ToLowerInvariant();
        if (acao.StartsWith("s")) { Sair(); return new JObject { ["result"] = "modo foco encerrado" }; }
        float min = Mathf.Clamp(a.Value<float?>("minutos") ?? 25f, 1f, 180f);
        Entrar(min * 60f, a.Value<string>("nome") ?? "FOCO");
        return new JObject { ["result"] = "modo foco por " + Mathf.RoundToInt(min) + " minutos" };
    }

    public void Entrar(float segundos, string nome)
    {
        if (ativo) Sair();
        ativo = true; terminou = false;
        fim = Time.time + segundos;
        // esconde o que está na cena: painéis, timers pequenos e hologramas (guardados para voltar)
        escondidos.Clear();
        foreach (var it in HoloRegistro.Itens) if (it.go != null && it.go.activeSelf) { escondidos.Add(it.go); it.go.SetActive(false); }
        var info = FindAnyObjectByType<PainelInfo>();
        if (info != null) foreach (Transform t in info.transform) if (t.gameObject.activeSelf) { escondidos.Add(t.gameObject); t.gameObject.SetActive(false); }
        var tm = FindAnyObjectByType<TimersHolo>();
        if (tm != null) foreach (Transform t in tm.transform) if (t.gameObject.activeSelf) { escondidos.Add(t.gameObject); t.gameObject.SetActive(false); }

        var tam = new Vector2(0.56f, 0.28f);
        painel = HoloUtil.CriarPainel(transform, "TimerFoco", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0.05f, 1.0f));
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        vidro = painel.GetComponent<PainelVidro>();
        rotulo = HoloUtil.CriarTexto(painel, "Nome", nome.ToUpperInvariant(), new Vector3(0, 0.1f, TemaZetta.ProfundidadeTexto), new Vector2(0.5f, 0.05f), 0.3f, TemaZetta.AzulTec);
        rotulo.fontSizeMin = 0.18f;
        tempo = HoloUtil.CriarTexto(painel, "Tempo", Formatar(segundos), new Vector3(0, -0.02f, TemaZetta.ProfundidadeTexto), new Vector2(0.52f, 0.16f), 1.2f, TemaZetta.Branco);
        tempo.fontStyle = FontStyles.Bold;
        StartCoroutine(HoloUtil.Surgir(painel));
        SonsZetta.Tocar(SonsZetta.Abrir);
    }

    public void Sair()
    {
        if (!ativo) return;
        ativo = false;
        StopAllCoroutines();   // a animação de entrada não pode continuar sobre o painel destruído
        if (painel != null) Destroy(painel.gameObject);
        foreach (var g in escondidos) if (g != null) g.SetActive(true);
        escondidos.Clear();
        SonsZetta.Tocar(SonsZetta.Fechar);
    }

    static string Formatar(float seg)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seg));
        return s >= 3600 ? $"{s / 3600}:{(s % 3600) / 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    void Update()
    {
        if (!ativo || painel == null || Camera.main == null) return;
        float resta = fim - Time.time;
        if (resta > 0f) tempo.text = Formatar(resta);
        else if (!terminou)
        {
            terminou = true;
            tempo.text = "00:00";
            if (voz != null) voz.Falar("o tempo do modo foco acabou. Avise o usuário em uma frase curta e simpática.");
            StartCoroutine(SairDepois(6f));
        }
        if (terminou) { bool alt = Mathf.FloorToInt(Time.time * 3f) % 2 == 0; vidro.DefinirBorda(alt ? TemaZetta.Dourado : TemaZetta.AzulTec, 1.5f); }
        // acompanha o olhar bem devagar (sem fugir da vista)
        var cam = Camera.main.transform;
        var alvo = HoloUtil.PontoNaVista(new Vector3(0f, 0.05f, 1.0f));
        painel.position = Vector3.Lerp(painel.position, alvo, Time.deltaTime * 1.5f);
        painel.rotation = Quaternion.Slerp(painel.rotation, HoloUtil.OlharParaUsuario(painel.position), Time.deltaTime * 3f);
    }

    IEnumerator SairDepois(float s) { yield return new WaitForSeconds(s); Sair(); }
}

// Sons espaciais ligados ao registro de hologramas: criar e apagar.
public class SonsAmbiente : MonoBehaviour
{
    void OnEnable()
    {
        HoloRegistro.aoAdicionar += Criou;
        HoloRegistro.aoRemover += Apagou;
    }
    void OnDisable()
    {
        HoloRegistro.aoAdicionar -= Criou;
        HoloRegistro.aoRemover -= Apagou;
    }
    static void Criou(GameObject go) { if (!HoloRegistro.Carregando) SonsZetta.Tocar(SonsZetta.Criar); }
    static void Apagou(GameObject go) { SonsZetta.Tocar(SonsZetta.Apagar); }
}
