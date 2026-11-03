using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Ferramenta "mostrar_painel": painel holográfico temporário com clima, notícias e assuntos atuais.
public class PainelInfo : MonoBehaviour
{
    public float duracaoPadrao = 20f;
    public Vector2 tamanho = new Vector2(0.44f, 0.30f);
    // posição relativa à vista: esquerda e um pouco acima do centro
    public Vector3 posicaoVista = new Vector3(-0.28f, 0.06f, 1.1f);

    const string Declaracao = @"{
  ""name"": ""mostrar_painel"",
  ""description"": ""Mostra um painel holográfico temporário com informações atuais (clima, notícias, resultados de busca)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""titulo"": { ""type"": ""STRING"", ""description"": ""Título curto, ex.: Clima em São Paulo."" },
      ""linhas"": { ""type"": ""ARRAY"", ""items"": { ""type"": ""STRING"" }, ""description"": ""De 1 a 4 linhas curtas (até ~50 caracteres cada)."" },
      ""duracao"": { ""type"": ""NUMBER"", ""description"": ""Segundos na tela (padrão 20)."" }
    },
    ""required"": [""titulo"", ""linhas""]
  }
}";

    const string InstrucaoComBusca =
        "Para clima, notícias, resultados esportivos, cotações e outros assuntos atuais, use a busca do Google. " +
        "Depois de buscar, chame mostrar_painel com um título e de 1 a 4 linhas bem curtas e responda por voz em uma ou duas frases.";
    const string InstrucaoSemBusca =
        "Para clima use a ferramenta consultar_clima e para notícias use buscar_noticias (elas já mostram o painel). " +
        "Você NÃO tem acesso a outros dados em tempo real (cotações, placares): se pedirem isso, avise com educação " +
        "que a busca ao vivo não está disponível e não invente números. Quando pedirem um resumo ou lista de conhecimento geral " +
        "para ver na tela, chame mostrar_painel com um título e de 1 a 4 linhas bem curtas e responda por voz em uma ou duas frases.";

    Transform painelAtual;
    TMPro.TMP_Text corpoAtual;
    string tituloAtual;
    float fimEm;

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Declaracao, Mostrar, voz.usarBuscaGoogle ? InstrucaoComBusca : InstrucaoSemBusca);
    }

    public JObject Mostrar(JObject args)
    {
        string titulo = args.Value<string>("titulo") ?? "Informação";
        var linhas = new List<string>();
        if (args["linhas"] is JArray arr) foreach (var l in arr) linhas.Add(l.ToString());
        float duracao = Mathf.Clamp(args.Value<float?>("duracao") ?? duracaoPadrao, 5f, 120f);

        var corpoTexto = new StringBuilder();
        foreach (var l in linhas) corpoTexto.AppendLine("• " + l);

        // mesmo título (ex.: "publicando..." com contador): só atualiza o texto e renova o tempo, sem recriar o painel
        if (painelAtual != null && corpoAtual != null && tituloAtual == titulo)
        {
            corpoAtual.text = corpoTexto.ToString();
            fimEm = Time.time + duracao;
            return new JObject { ["result"] = "painel atualizado por " + duracao + " s" };
        }

        StopAllCoroutines();   // a animação do painel antigo não pode continuar sobre um objeto destruído
        if (painelAtual != null) Destroy(painelAtual.gameObject);
        var raiz = HoloUtil.CriarPainel(transform, "PainelInfo", tamanho);
        var pos = HoloUtil.PontoNaVista(posicaoVista);
        raiz.position = pos;
        raiz.rotation = HoloUtil.OlharParaUsuario(pos);

        // central e painel de informação não ficam abertos juntos (um não cobre o outro)
        if (CentralZetta.Instancia != null) CentralZetta.Instancia.Fechar();

        IconesZetta.Criar(raiz, "info", new Vector3(-tamanho.x * 0.5f + 0.035f, tamanho.y * 0.5f - 0.035f, TemaZetta.ProfundidadeTexto), 0.03f, TemaZetta.Dourado);
        HoloUtil.CriarTexto(raiz, "Titulo", titulo, new Vector3(0.012f, tamanho.y * 0.5f - 0.035f, TemaZetta.ProfundidadeTexto),
            new Vector2(tamanho.x - 0.14f, 0.05f), 0.24f, TemaZetta.AzulTec, TMPro.TextAlignmentOptions.Left);
        HoloUtil.CriarTexto(raiz, "Corpo", corpoTexto.ToString(), new Vector3(0, -0.03f, TemaZetta.ProfundidadeTexto),
            new Vector2(tamanho.x - 0.05f, tamanho.y - 0.1f), 0.22f, TemaZetta.Branco, TMPro.TextAlignmentOptions.TopLeft);
        var tCorpo = raiz.Find("Corpo");
        corpoAtual = tCorpo != null ? tCorpo.GetComponent<TMPro.TMP_Text>() : null;
        tituloAtual = titulo;

        // botão poke para fechar o painel
        BotaoHolo.Criar(raiz, "X", new Vector3(tamanho.x * 0.5f - 0.03f, tamanho.y * 0.5f - 0.03f, 0f),
            new Vector2(0.045f, 0.045f), () => { if (raiz != null) Destroy(raiz.gameObject); });

        painelAtual = raiz;
        StartCoroutine(Ciclo(raiz, duracao));
        return new JObject { ["result"] = "painel mostrado por " + duracao + " s" };
    }

    IEnumerator Ciclo(Transform raiz, float duracao)
    {
        yield return HoloUtil.Surgir(raiz);
        fimEm = Time.time + duracao;
        while (raiz != null && Time.time < fimEm) yield return null;
        // encolhe e some
        for (float t = 0; t < 0.4f && raiz != null; t += Time.deltaTime)
        {
            float k = 1f - t / 0.4f;
            raiz.localScale = new Vector3(1f, Mathf.Max(0.02f, k), 1f);
            yield return null;
        }
        if (raiz != null) Destroy(raiz.gameObject);
    }
}
