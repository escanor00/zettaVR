using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Alertas de erro em tempo real: todo Debug.LogError / exceção vira um aviso de vidro vermelho flutuando perto do sistema
// que falhou (esfera, interface, modelos 3D...), que some sozinho. "Zetta, mostre os erros" abre a lista com os últimos erros.
// Limites: o mesmo erro só gera um aviso a cada 10 s (o contador sobe), no máximo 4 avisos na tela e 6 novos a cada 10 s.
public class AlertasErros : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""mostrar_erros"",
  ""description"": ""Mostra (ou fecha/limpa) a lista dos últimos erros do app e devolve um resumo para você explicar."",
  ""parameters"": { ""type"": ""OBJECT"", ""properties"": { ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar"", ""limpar""] } } }
}";
    const string Instrucao = "'mostre os erros', 'deu algum erro?', 'o que quebrou?' → mostrar_erros e explique em UMA frase simples o último erro e o sistema afetado (sem termos técnicos). 'limpe os erros' → acao=limpar.";

    public class Erro { public DateTime hora; public string sistema, mensagem, arquivo; public int vezes = 1; public float ultimoAviso = -100f; }

    public static readonly List<Erro> Historico = new List<Erro>();
    readonly ConcurrentQueue<(string msg, string pilha, LogType tipo)> fila = new ConcurrentQueue<(string, string, LogType)>();
    readonly List<GameObject> avisos = new List<GameObject>();
    readonly Dictionary<GameObject, string> sistemaDoAviso = new Dictionary<GameObject, string>();
    readonly Queue<float> recentes = new Queue<float>();
    AssistenteVoz voz;
    EsferaZetta esfera;
    Transform painel;
    public bool Aberto => painel != null;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        esfera = FindAnyObjectByType<EsferaZetta>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    void OnEnable() { Application.logMessageReceivedThreaded += Receber; }
    void OnDisable() { Application.logMessageReceivedThreaded -= Receber; }

    // pode vir de outra thread: só enfileira
    void Receber(string msg, string pilha, LogType tipo)
    {
        if (tipo == LogType.Error || tipo == LogType.Exception || tipo == LogType.Assert) fila.Enqueue((msg, pilha, tipo));
    }

    // ---------------------------------------------------------------- classificação
    static readonly Regex Arquivo = new Regex(@"([A-Za-z0-9_]+(?:\.[A-Z][A-Za-z]+)?)\.cs[:\)]");

    // sistema pelo nome do script que aparece na pilha (ignora este próprio arquivo)
    public static string Sistema(string msg, string pilha, out string arquivo)
    {
        arquivo = "";
        foreach (Match m in Arquivo.Matches((pilha ?? "") + "\n" + (msg ?? "")))
        {
            string f = m.Groups[1].Value;
            if (f == "AlertasErros") continue;
            arquivo = f;
            break;
        }
        if (arquivo.Length == 0) return "motor Unity";
        string a = arquivo;
        if (a.StartsWith("Assistente")) return "voz";
        if (a.StartsWith("Gerador") || a.StartsWith("Gradio")) return "modelos 3D";
        if (a.StartsWith("Esfera") || a.StartsWith("Anel") || a.StartsWith("Nucleo") || a.StartsWith("Estado")) return "esfera";
        if (a.StartsWith("Notas")) return "notas";
        if (a.StartsWith("Timers")) return "timers";
        if (a.StartsWith("Camera") || a.StartsWith("Visao")) return "câmera";
        if (a.StartsWith("Central") || a.StartsWith("Painel") || a.StartsWith("Botao") || a.StartsWith("Holo") || a.StartsWith("Tema") || a.StartsWith("Icones") ||
            a.StartsWith("Codigo") || a.StartsWith("Mapa") || a.StartsWith("Ajustes") || a.StartsWith("Regras")) return "interface";
        return a;
    }

    static string Resumo(string msg)
    {
        string l = (msg ?? "").Trim();
        int q = l.IndexOf('\n');
        if (q > 0) l = l.Substring(0, q);
        return l.Length > 90 ? l.Substring(0, 89) + "…" : l;
    }

    // ---------------------------------------------------------------- fluxo
    void Update()
    {
        while (fila.TryDequeue(out var e))
        {
            string sis = Sistema(e.msg, e.pilha, out string arq);
            string resumo = Resumo(e.msg);
            var ja = Historico.Find(x => x.sistema == sis && x.mensagem == resumo);
            bool novo = ja == null;
            if (novo) { ja = new Erro { hora = DateTime.Now, sistema = sis, mensagem = resumo, arquivo = arq }; Historico.Add(ja); if (Historico.Count > 30) Historico.RemoveAt(0); }
            else { ja.vezes++; ja.hora = DateTime.Now; }

            while (recentes.Count > 0 && Time.unscaledTime - recentes.Peek() > 10f) recentes.Dequeue();
            if (Time.unscaledTime - ja.ultimoAviso < 10f || recentes.Count >= 6 || Camera.main == null) continue;
            ja.ultimoAviso = Time.unscaledTime;
            recentes.Enqueue(Time.unscaledTime);
            StartCoroutine(Aviso(ja));
        }
        if (painel != null && Camera.main != null) painel.rotation = HoloUtil.OlharParaUsuario(painel.position);
    }

    // perto do sistema com problema: esfera/voz ao lado da esfera; o resto à frente, em cantos diferentes por sistema
    Vector3 Ancora(string sistema)
    {
        var cam = Camera.main.transform;
        if ((sistema == "voz" || sistema == "esfera") && esfera != null)
            return esfera.transform.position + Vector3.up * 0.2f - cam.right * 0.16f;
        float h = Mathf.Abs(sistema.GetHashCode()) % 3;   // 0, 1, 2: esquerda, centro, direita
        return HoloUtil.PontoNaVista(new Vector3((h - 1f) * 0.34f, 0.3f - (h == 1 ? 0.06f : 0f), 1.0f));
    }

    IEnumerator Aviso(Erro er)
    {
        avisos.RemoveAll(a => a == null);
        var mortos = new List<GameObject>();
        foreach (var kv in sistemaDoAviso) if (kv.Key == null) mortos.Add(kv.Key);
        foreach (var m in mortos) sistemaDoAviso.Remove(m);
        while (avisos.Count >= 4) { Destroy(avisos[0]); avisos.RemoveAt(0); }
        var tam = new Vector2(0.34f, 0.1f);
        var raiz = HoloUtil.CriarPainel(transform, "Alerta", tam);
        // avisos do mesmo sistema se empilham para baixo em vez de se sobrepor
        int mesmos = 0;
        foreach (var kv in sistemaDoAviso) if (kv.Key != null && kv.Value == er.sistema) mesmos++;
        avisos.Add(raiz.gameObject);
        sistemaDoAviso[raiz.gameObject] = er.sistema;
        raiz.position = Ancora(er.sistema) + Vector3.down * (0.115f * mesmos);
        raiz.rotation = HoloUtil.OlharParaUsuario(raiz.position);
        raiz.GetComponent<PainelVidro>().DefinirBorda(TemaZetta.Erro, 1.6f);
        var tit = HoloUtil.CriarTexto(raiz, "Titulo", "ERRO · " + er.sistema.ToUpperInvariant(), new Vector3(0, 0.03f, TemaZetta.ProfundidadeTexto), new Vector2(0.31f, 0.03f), 0.22f, TemaZetta.Erro);
        tit.fontStyle = FontStyles.Bold; tit.fontSizeMin = 0.18f;
        var msg = HoloUtil.CriarTexto(raiz, "Msg", er.mensagem, new Vector3(0, -0.012f, TemaZetta.ProfundidadeTexto), new Vector2(0.31f, 0.05f), 0.2f, TemaZetta.Branco);
        msg.fontSizeMin = 0.18f;
        StartCoroutine(HoloUtil.Surgir(raiz));
        SonsZetta.Tocar(SonsZetta.Apagar);
        yield return new WaitForSeconds(8f);
        if (raiz == null) yield break;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (raiz == null) yield break;
            raiz.localScale = Vector3.one * Mathf.Lerp(1f, 0.01f, t / 0.3f);
            yield return null;
        }
        if (raiz != null) { sistemaDoAviso.Remove(raiz.gameObject); Destroy(raiz.gameObject); }
    }

    // ---------------------------------------------------------------- lista
    JObject Executar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        if (acao.StartsWith("f")) { Fechar(); return new JObject { ["result"] = "lista de erros fechada" }; }
        if (acao.StartsWith("l")) { Historico.Clear(); if (painel != null) Montar(); return new JObject { ["result"] = "erros limpos" }; }
        Montar();
        if (Historico.Count == 0) return new JObject { ["result"] = "nenhum erro registrado; está tudo certo" };
        var ult = Historico[Historico.Count - 1];
        return new JObject { ["result"] = Historico.Count + " erro(s) registrados; o último foi no sistema " + ult.sistema + ": " + ult.mensagem + " (explique em uma frase simples)" };
    }

    void Montar()
    {
        if (painel != null) Destroy(painel.gameObject);
        const int Linhas = 6;
        int n = Mathf.Min(Historico.Count, Linhas);
        var tam = new Vector2(0.9f, 0.22f + Mathf.Max(1, n) * 0.062f);
        painel = HoloUtil.CriarPainel(transform, "ListaErros", tam);
        var pos = HoloUtil.PontoNaVista(new Vector3(0f, 0.03f, 1.0f));
        painel.position = pos;
        painel.rotation = HoloUtil.OlharParaUsuario(pos);
        painel.GetComponent<PainelVidro>().DefinirBorda(Historico.Count > 0 ? TemaZetta.Erro : TemaZetta.Ok, 1.3f);
        var t = HoloUtil.CriarTexto(painel, "Titulo", Historico.Count == 0 ? "SEM ERROS" : "ÚLTIMOS ERROS (" + Historico.Count + ")", new Vector3(0, tam.y * 0.5f - 0.04f, TemaZetta.ProfundidadeTexto), new Vector2(0.6f, 0.05f), 0.3f, Historico.Count == 0 ? TemaZetta.Ok : TemaZetta.Erro);
        t.fontStyle = FontStyles.Bold;
        float y = tam.y * 0.5f - 0.11f;
        for (int i = 0; i < n; i++, y -= 0.062f)
        {
            var e = Historico[Historico.Count - 1 - i];
            string txt = "<color=#" + ColorUtility.ToHtmlStringRGB(TemaZetta.AzulTec) + ">" + e.hora.ToString("HH:mm") + "</color>  <b>" + e.sistema + "</b>  " + e.mensagem + (e.vezes > 1 ? "  ×" + e.vezes : "");
            var l = HoloUtil.CriarTexto(painel, "Erro" + i, txt, new Vector3(0, y, TemaZetta.ProfundidadeTexto), new Vector2(tam.x - 0.06f, 0.05f), 0.22f, TemaZetta.Branco, TextAlignmentOptions.Left);
            l.fontSizeMin = 0.18f; l.textWrappingMode = TextWrappingModes.NoWrap; l.overflowMode = TextOverflowModes.Ellipsis;
        }
        BotaoHolo.Criar(painel, "LIMPAR", new Vector3(-0.1f, -tam.y * 0.5f + 0.045f, -0.002f), new Vector2(0.17f, 0.06f), () => { Historico.Clear(); Montar(); });
        BotaoHolo.Criar(painel, "FECHAR", new Vector3(0.1f, -tam.y * 0.5f + 0.045f, -0.002f), new Vector2(0.17f, 0.06f), Fechar);
        StartCoroutine(HoloUtil.Surgir(painel));
    }

    public void Fechar()
    {
        if (painel == null) return;
        Destroy(painel.gameObject);
        painel = null;
        SonsZetta.Tocar(SonsZetta.Fechar);
    }
}
