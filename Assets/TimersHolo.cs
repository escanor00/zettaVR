using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Ferramentas "criar_timer" e "cancelar_timer": contadores holográficos (vários ao mesmo tempo).
public class TimersHolo : MonoBehaviour
{
    public Vector2 tamanho = new Vector2(0.17f, 0.075f);
    // canto superior esquerdo da vista (esquerda, cima, distância)
    public Vector3 origemVista = new Vector3(-0.30f, 0.20f, 1.1f);
    public float espacamento = 0.088f;
    public float segundosPiscando = 6f;

    class Timer
    {
        public string nome;
        public float fim;
        public Transform raiz;
        public TextMeshPro rotulo, tempo;
        public PainelVidro vidro;
        public bool terminado;
        public float inicioFim;
    }

    readonly List<Timer> timers = new List<Timer>();

    // "café (09:12), pizza (01:30)" ou "nenhum timer ativo"
    public string Resumo()
    {
        if (timers.Count == 0) return "nenhum timer ativo";
        var partes = new List<string>();
        foreach (var t in timers) partes.Add(t.nome + " (" + Formatar(t.fim - Time.time) + ")");
        return string.Join(", ", partes);
    }
    AssistenteVoz voz;

    const string DeclCriar = @"{
  ""name"": ""criar_timer"",
  ""description"": ""Cria um timer/lembrete com contagem regressiva holográfica. Converta o tempo falado para segundos."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""nome"": { ""type"": ""STRING"", ""description"": ""Nome do lembrete, ex.: café."" },
      ""segundos"": { ""type"": ""NUMBER"", ""description"": ""Duração total em segundos (10 minutos = 600)."" }
    },
    ""required"": [""nome"", ""segundos""]
  }
}";
    const string DeclCancelar = @"{
  ""name"": ""cancelar_timer"",
  ""description"": ""Cancela um timer pelo nome (ou todos se o nome for 'todos')."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": { ""nome"": { ""type"": ""STRING"" } },
    ""required"": [""nome""]
  }
}";
    const string Instrucao =
        "Para timers e lembretes (ex.: 'Zetta, 10 minutos para o café') use criar_timer com o nome do lembrete e os segundos, " +
        "e confirme em uma frase curta. Podem existir vários timers ao mesmo tempo. Quando receber um aviso do sistema de que um timer acabou, " +
        "avise o usuário em uma frase curta e simpática.";

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclCriar, Criar, Instrucao);
        voz.RegistrarFerramenta(DeclCancelar, Cancelar);
    }

    public JObject Criar(JObject args)
    {
        string nome = args.Value<string>("nome") ?? "timer";
        float seg = Mathf.Clamp(args.Value<float?>("segundos") ?? 60f, 1f, 24f * 3600f);

        var t = new Timer { nome = nome, fim = Time.time + seg };
        t.raiz = HoloUtil.CriarPainel(transform, "Timer_" + nome, tamanho);
        t.rotulo = HoloUtil.CriarTexto(t.raiz, "Nome", nome, new Vector3(0, tamanho.y * 0.5f - 0.02f, -0.002f),
            new Vector2(tamanho.x - 0.02f, 0.026f), 0.12f, TemaZetta.AzulTec);
        t.tempo = HoloUtil.CriarTexto(t.raiz, "Tempo", Formatar(seg), new Vector3(0, -0.012f, -0.002f),
            new Vector2(tamanho.x - 0.02f, 0.05f), 0.34f, TemaZetta.Branco);
        t.vidro = t.raiz.GetComponent<PainelVidro>();
        var cam = Camera.main.transform;
        t.raiz.position = AlvoPosicao(timers.Count);
        t.raiz.rotation = Quaternion.LookRotation(t.raiz.position - cam.position);
        timers.Add(t);
        StartCoroutine(HoloUtil.Surgir(t.raiz));
        return new JObject { ["result"] = "timer '" + nome + "' criado para " + Mathf.RoundToInt(seg) + " segundos" };
    }

    public JObject Cancelar(JObject args)
    {
        string nome = (args.Value<string>("nome") ?? "").ToLowerInvariant();
        int n = 0;
        for (int i = timers.Count - 1; i >= 0; i--)
            if (nome == "todos" || timers[i].nome.ToLowerInvariant().Contains(nome))
            {
                Destroy(timers[i].raiz.gameObject);
                timers.RemoveAt(i);
                n++;
            }
        return new JObject { ["result"] = n + " timer(s) cancelado(s)" };
    }

    Vector3 AlvoPosicao(int indice)
    {
        var cam = Camera.main.transform;
        var local = origemVista + new Vector3(0f, -indice * espacamento, 0f);
        return cam.position + cam.rotation * local;
    }

    static string Formatar(float seg)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seg));
        return s >= 3600 ? $"{s / 3600}:{(s % 3600) / 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    void Update()
    {
        if (timers.Count == 0 || Camera.main == null) return;
        var cam = Camera.main.transform;
        for (int i = timers.Count - 1; i >= 0; i--)
        {
            var t = timers[i];
            float restante = t.fim - Time.time;
            if (!t.terminado)
            {
                t.tempo.text = Formatar(restante);
                if (restante <= 0f)
                {
                    t.terminado = true;
                    t.inicioFim = Time.time;
                    t.tempo.text = "00:00";
                    if (voz != null)
                        voz.Falar("o timer \"" + t.nome + "\" terminou. Avise o usuário em uma frase curta e simpática.");
                }
            }
            else
            {
                // pisca ciano/laranja
                bool alt = Mathf.FloorToInt((Time.time - t.inicioFim) * 4f) % 2 == 0;
                var cor = alt ? TemaZetta.Dourado : TemaZetta.AzulTec;
                t.tempo.color = alt ? cor : TemaZetta.Branco;
                t.rotulo.color = cor;
                t.vidro.DefinirBorda(cor, alt ? 1.6f : 1f);
                t.vidro.DefinirRealce(alt ? 1f : 0f);
                if (Time.time - t.inicioFim > segundosPiscando)
                {
                    Destroy(t.raiz.gameObject);
                    timers.RemoveAt(i);
                    continue;
                }
            }
            // acompanha o olhar suavemente, empilhado no canto superior esquerdo
            var alvo = AlvoPosicao(i);
            t.raiz.position = Vector3.Lerp(t.raiz.position, alvo, Time.deltaTime * 3f);
            t.raiz.rotation = Quaternion.Slerp(t.raiz.rotation,
                Quaternion.LookRotation(t.raiz.position - cam.position), Time.deltaTime * 4f);
        }
    }
}
