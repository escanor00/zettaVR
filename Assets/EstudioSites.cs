using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Estúdio de Sites por voz: o Gemini escreve um HTML autocontido (A-Frame quando for 3D/AR), o app o salva no óculos
// e o publica no GitHub Pages (repositório público "zetta-sites", uma pasta por site). Token e usuário vêm do ZettaSegredos.
public class EstudioSites : MonoBehaviour
{
    public string modeloGemini = "gemini-3.6-flash";
    public string repositorio = "zetta-sites";
    public string versaoAFrame = "1.6.0";

    const string Decl = @"[
 {""name"":""criar_site"",""description"":""Cria um site (uma página HTML) sobre o assunto pedido e publica na internet. Leva cerca de 1 minuto."",
  ""parameters"":{""type"":""OBJECT"",""properties"":{
   ""nome"":{""type"":""STRING"",""description"":""Nome curto do site, 1 a 3 palavras.""},
   ""descricao"":{""type"":""STRING"",""description"":""O que o site deve ter: assunto, estilo, seções.""},
   ""modo"":{""type"":""STRING"",""enum"":[""normal"",""3d""],""description"":""3d = cena 3D/AR com A-Frame.""}},
   ""required"":[""nome"",""descricao""]}},
 {""name"":""atualizar_site"",""description"":""Muda ou acrescenta algo num site já criado, ou o transforma em 3D (modo=3d)."",
  ""parameters"":{""type"":""OBJECT"",""properties"":{
   ""nome"":{""type"":""STRING""},""mudanca"":{""type"":""STRING"",""description"":""O que mudar ou acrescentar.""},
   ""modo"":{""type"":""STRING"",""enum"":[""normal"",""3d""]}},
   ""required"":[""nome"",""mudanca""]}},
 {""name"":""listar_sites"",""description"":""Lista os sites criados.""},
 {""name"":""abrir_site"",""description"":""Abre um site no navegador do Quest."",
  ""parameters"":{""type"":""OBJECT"",""properties"":{""nome"":{""type"":""STRING""}},""required"":[""nome""]}},
 {""name"":""apagar_site"",""description"":""Apaga um site (do óculos e da internet)."",
  ""parameters"":{""type"":""OBJECT"",""properties"":{""nome"":{""type"":""STRING""}},""required"":[""nome""]}}
]";
    const string Instrucao =
        "Estúdio de sites: 'crie um site de...' → criar_site; 'mude/adicione...' → atualizar_site; 'transforme em 3D' → atualizar_site com modo=3d; " +
        "'liste meus sites' → listar_sites; 'abra o site X' → abrir_site; 'apague o site X' → apagar_site. A criação leva cerca de 1 minuto: avise em uma frase e continue a conversa. " +
        "Os sites são públicos na internet: nunca peça nem coloque dados pessoais (nome completo, endereço, telefone, e-mail, fotos do usuário) neles.";

    const string PromptSistema =
        "Você é um designer web. Responda SOMENTE com o código de UM único arquivo HTML completo e autocontido (CSS e JavaScript embutidos), sem explicações e sem cercas de código. " +
        "Todo o texto do site em português do Brasil. Visual moderno, responsivo, legível, com bom contraste. " +
        "Não use dados pessoais reais, não colete dados, não use formulários que enviem informações, não use imagens externas nem outras bibliotecas externas além das citadas. " +
        "Para imagens use SVG, gradientes ou emojis.";

    [Serializable]
    class Site { public string slug, titulo, modo, url; public bool publicado; }

    class Resp { public long codigo; public string texto; public bool Ok => codigo >= 200 && codigo < 300; }

    AssistenteVoz voz;
    PainelInfo painel;
    readonly List<Site> sites = new List<Site>();
    string pasta, indice;
    bool ocupado, repoPronto;

    void Awake()
    {
        pasta = Path.Combine(Application.persistentDataPath, "Sites");
        indice = Path.Combine(pasta, "sites.json");
        Directory.CreateDirectory(pasta);
        try { if (File.Exists(indice)) sites.AddRange(JsonConvert.DeserializeObject<List<Site>>(File.ReadAllText(indice)) ?? new List<Site>()); }
        catch (Exception e) { Debug.LogWarning("EstudioSites: índice ilegível: " + e.Message); }

        voz = FindAnyObjectByType<AssistenteVoz>();
        painel = FindAnyObjectByType<PainelInfo>();
        if (voz == null) return;
        var defs = JArray.Parse(Decl);
        voz.RegistrarFerramenta(defs[0].ToString(), CriarSite, Instrucao);
        voz.RegistrarFerramenta(defs[1].ToString(), AtualizarSite, null);
        voz.RegistrarFerramenta(defs[2].ToString(), ListarSites, null);
        voz.RegistrarFerramenta(defs[3].ToString(), AbrirSite, null);
        voz.RegistrarFerramenta(defs[4].ToString(), ApagarSite, null);
    }

    // ------------------------------------------------------------------ ferramentas
    ZettaSegredos Seg => voz != null ? voz.segredos : null;
    bool TemGitHub => Seg != null && !string.IsNullOrWhiteSpace(Seg.tokenGitHub) && !string.IsNullOrWhiteSpace(Seg.usuarioGitHub);

    JObject CriarSite(JObject a)
    {
        if (ocupado) return new JObject { ["erro"] = "já estou trabalhando em um site; espere terminar" };
        string nome = (a.Value<string>("nome") ?? "").Trim();
        string desc = (a.Value<string>("descricao") ?? "").Trim();
        if (nome.Length == 0 || desc.Length == 0) return new JObject { ["erro"] = "faltou o nome ou a descrição do site" };
        string modo = (a.Value<string>("modo") ?? "normal").ToLowerInvariant();
        StartCoroutine(Fluxo(nome, null, desc, modo));
        return new JObject { ["result"] = "criando o site " + nome + "; leva cerca de 1 minuto" };
    }

    JObject AtualizarSite(JObject a)
    {
        if (ocupado) return new JObject { ["erro"] = "já estou trabalhando em um site; espere terminar" };
        var s = Achar(a.Value<string>("nome"));
        if (s == null) return new JObject { ["erro"] = "não encontrei esse site; use listar_sites" };
        string mudanca = (a.Value<string>("mudanca") ?? "").Trim();
        if (mudanca.Length == 0) return new JObject { ["erro"] = "faltou dizer o que mudar" };
        string htmlAtual = null;
        try { htmlAtual = File.ReadAllText(Path.Combine(pasta, s.slug, "index.html")); } catch { }
        if (htmlAtual == null) return new JObject { ["erro"] = "não tenho a cópia deste site no óculos" };
        string modo = (a.Value<string>("modo") ?? s.modo ?? "normal").ToLowerInvariant();
        StartCoroutine(Fluxo(s.titulo, s, mudanca, modo, htmlAtual));
        return new JObject { ["result"] = "atualizando o site " + s.titulo + "; leva cerca de 1 minuto" };
    }

    JObject ListarSites(JObject a)
    {
        if (sites.Count == 0) return new JObject { ["result"] = "você ainda não tem sites" };
        var arr = new JArray();
        foreach (var s in sites) arr.Add(s.titulo + (s.modo == "3d" ? " (3D)" : "") + (s.publicado ? "" : " (não publicado)"));
        return new JObject { ["sites"] = arr };
    }

    JObject AbrirSite(JObject a)
    {
        var s = Achar(a.Value<string>("nome"));
        if (s == null) return new JObject { ["erro"] = "não encontrei esse site" };
        if (!s.publicado || string.IsNullOrEmpty(s.url)) return new JObject { ["erro"] = "este site não foi publicado, então não dá para abrir" };
        Application.OpenURL(s.url);   // no Quest vira um intent ACTION_VIEW: abre no navegador, ao lado do app
        return new JObject { ["result"] = "abrindo " + s.titulo + " no navegador" };
    }

    JObject ApagarSite(JObject a)
    {
        var s = Achar(a.Value<string>("nome"));
        if (s == null) return new JObject { ["erro"] = "não encontrei esse site" };
        if (ocupado) return new JObject { ["erro"] = "estou ocupada com outro site; tente em instantes" };
        StartCoroutine(FluxoApagar(s));
        return new JObject { ["result"] = "apagando o site " + s.titulo };
    }

    // ------------------------------------------------------------------ fluxos
    IEnumerator Fluxo(string titulo, Site existente, string pedido, string modo, string htmlAtual = null)
    {
        ocupado = true;
        Painel("Site: " + titulo, "gerando o código com o Gemini...", 120);

        var resp = new Resp();
        yield return GerarHtml(titulo, pedido, modo, htmlAtual, resp);
        if (!resp.Ok || string.IsNullOrWhiteSpace(resp.texto))
        {
            ocupado = false;
            Painel("Site: " + titulo, "não consegui gerar agora", 15);
            voz?.Falar("não consegui gerar o site " + titulo + (resp.codigo == 503 || resp.codigo == 429 ? ", o serviço do Gemini está ocupado" : "") + ". Avise em uma frase e sugira tentar de novo daqui a pouco.");
            yield break;
        }

        string slug = existente != null ? existente.slug : SlugUnico(titulo);
        string dir = Path.Combine(pasta, slug);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), resp.texto, new UTF8Encoding(false));
        var site = existente ?? new Site { slug = slug, titulo = titulo };
        site.modo = modo;
        if (existente == null) sites.Add(site);
        Gravar();

        if (!TemGitHub)
        {
            site.publicado = false;
            Gravar();
            ocupado = false;
            Painel("Site: " + titulo, "salvo no óculos, mas não publicado (falta o token do GitHub)", 20);
            voz?.Falar("o site " + titulo + " foi criado e salvo no óculos, mas não consegui publicar porque falta configurar o token do GitHub. Avise em uma frase.");
            yield break;
        }

        Painel("Site: " + titulo, "publicando...", 120);
        string erro = null;
        yield return Publicar(site, resp.texto, e => erro = e);
        ocupado = false;
        if (erro != null)
        {
            Debug.LogWarning("EstudioSites: publicação falhou: " + erro);
            Painel("Site: " + titulo, "falha ao publicar", 15);
            voz?.Falar("o site " + titulo + " foi criado, mas a publicação falhou. Avise em uma frase e diga que está salvo no óculos.");
            yield break;
        }
        Painel("Site: " + titulo, "publicado!\n" + site.url, 40);
        voz?.Falar("o site " + titulo + " foi publicado. Diga em uma frase que a internet leva cerca de um minuto para liberar a página e ofereça abrir no navegador.");
    }

    IEnumerator FluxoApagar(Site s)
    {
        ocupado = true;
        string erro = null;
        if (s.publicado && TemGitHub) yield return ApagarRemoto(s, e => erro = e);
        try { Directory.Delete(Path.Combine(pasta, s.slug), true); } catch { }
        if (erro == null) { sites.Remove(s); Gravar(); }
        ocupado = false;
        voz?.Falar(erro == null ? "o site " + s.titulo + " foi apagado. Confirme em poucas palavras."
                                : "não consegui apagar o site " + s.titulo + " da internet. Avise em uma frase.");
    }

    // ------------------------------------------------------------------ Gemini
    IEnumerator GerarHtml(string titulo, string pedido, string modo, string htmlAtual, Resp r)
    {
        var sb = new StringBuilder();
        if (htmlAtual == null) sb.Append("Crie o site \"" + titulo + "\". Pedido: " + pedido + ".");
        else sb.Append("Este é o HTML atual do site \"" + titulo + "\":\n" + htmlAtual + "\n\nAplique esta mudança e devolva o HTML completo atualizado: " + pedido + ".");
        if (modo == "3d")
            sb.Append("\nFaça uma cena 3D/AR usando A-Frame: carregue <script src=\"https://aframe.io/releases/" + versaoAFrame + "/aframe.min.js\"></script>, " +
                      "use primitivas (a-box, a-sphere, a-text, a-sky com cor sólida), animações e interação por olhar/clique. Para realidade aumentada não use câmera externa.");

        var corpo = new JObject
        {
            ["system_instruction"] = new JObject { ["parts"] = new JArray(new JObject { ["text"] = PromptSistema }) },
            ["contents"] = new JArray(new JObject { ["role"] = "user", ["parts"] = new JArray(new JObject { ["text"] = sb.ToString() }) }),
            ["generationConfig"] = new JObject { ["temperature"] = 0.8, ["maxOutputTokens"] = 32000 }
        };
        var cab = new Dictionary<string, string> { ["x-goog-api-key"] = Seg != null ? Seg.chaveGemini : "" };
        yield return Chamar("POST", "https://generativelanguage.googleapis.com/v1beta/models/" + modeloGemini + ":generateContent", corpo.ToString(Formatting.None), cab, r);
        if (!r.Ok) { r.texto = null; yield break; }
        try
        {
            var j = JObject.Parse(r.texto);
            var partes = j["candidates"]?[0]?["content"]?["parts"] as JArray;
            var t = new StringBuilder();
            if (partes != null) foreach (var p in partes) t.Append(p["text"]?.ToString());
            r.texto = LimparHtml(t.ToString());
        }
        catch { r.texto = null; }
    }

    static string LimparHtml(string s)
    {
        s = s.Trim();
        var m = Regex.Match(s, "```(?:html)?\\s*(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (m.Success) s = m.Groups[1].Value.Trim();
        int i = s.IndexOf("<!doctype", StringComparison.OrdinalIgnoreCase);
        if (i < 0) i = s.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? s.Substring(i) : s;
    }

    // ------------------------------------------------------------------ GitHub (API REST)
    Dictionary<string, string> CabGit() => new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer " + Seg.tokenGitHub,
        ["Accept"] = "application/vnd.github+json",
        ["X-GitHub-Api-Version"] = "2022-11-28",
        ["User-Agent"] = "ZettaMR"
    };

    string Api(string caminho) => "https://api.github.com/repos/" + Seg.usuarioGitHub + "/" + repositorio + caminho;

    IEnumerator GarantirRepo(Action<string> falha)
    {
        if (repoPronto) yield break;
        var r = new Resp();
        yield return Chamar("GET", Api(""), null, CabGit(), r);
        if (r.codigo == 404)
        {
            var novo = new JObject { ["name"] = repositorio, ["description"] = "Sites criados pela Zetta", ["auto_init"] = true, ["private"] = false };
            yield return Chamar("POST", "https://api.github.com/user/repos", novo.ToString(Formatting.None), CabGit(), r);
        }
        if (!r.Ok) { falha("repositório: HTTP " + r.codigo); yield break; }

        // liga o GitHub Pages (409 = já ligado)
        var pg = new Resp();
        var src = new JObject { ["source"] = new JObject { ["branch"] = "main", ["path"] = "/" } };
        yield return Chamar("POST", Api("/pages"), src.ToString(Formatting.None), CabGit(), pg);
        if (!pg.Ok && pg.codigo != 409) Debug.LogWarning("EstudioSites: Pages HTTP " + pg.codigo);
        repoPronto = true;
    }

    IEnumerator Publicar(Site site, string html, Action<string> falha)
    {
        string erro = null;
        yield return GarantirRepo(e => erro = e);
        if (erro != null) { falha(erro); yield break; }

        string caminho = "/contents/" + site.slug + "/index.html";
        var r = new Resp();
        yield return Chamar("GET", Api(caminho), null, CabGit(), r);
        string sha = null;
        if (r.Ok) { try { sha = JObject.Parse(r.texto).Value<string>("sha"); } catch { } }

        var corpo = new JObject
        {
            ["message"] = (sha == null ? "cria " : "atualiza ") + site.slug,
            ["content"] = Convert.ToBase64String(new UTF8Encoding(false).GetBytes(html))
        };
        if (sha != null) corpo["sha"] = sha;
        yield return Chamar("PUT", Api(caminho), corpo.ToString(Formatting.None), CabGit(), r);
        if (!r.Ok) { falha("publicação: HTTP " + r.codigo); yield break; }

        site.url = "https://" + Seg.usuarioGitHub.ToLowerInvariant() + ".github.io/" + repositorio + "/" + site.slug + "/";
        site.publicado = true;
        Gravar();
    }

    IEnumerator ApagarRemoto(Site site, Action<string> falha)
    {
        string caminho = "/contents/" + site.slug + "/index.html";
        var r = new Resp();
        yield return Chamar("GET", Api(caminho), null, CabGit(), r);
        if (r.codigo == 404) yield break;   // já não existe
        if (!r.Ok) { falha("HTTP " + r.codigo); yield break; }
        string sha = JObject.Parse(r.texto).Value<string>("sha");
        var corpo = new JObject { ["message"] = "apaga " + site.slug, ["sha"] = sha };
        yield return Chamar("DELETE", Api(caminho), corpo.ToString(Formatting.None), CabGit(), r);
        if (!r.Ok) falha("HTTP " + r.codigo);
    }

    // ------------------------------------------------------------------ utilidades
    IEnumerator Chamar(string metodo, string url, string corpo, Dictionary<string, string> cab, Resp saida)
    {
        using (var req = new UnityWebRequest(url, metodo))
        {
            if (corpo != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(corpo));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (cab != null) foreach (var kv in cab) req.SetRequestHeader(kv.Key, kv.Value);
            req.timeout = 120;
            yield return req.SendWebRequest();
            saida.codigo = req.responseCode;
            saida.texto = req.downloadHandler != null ? req.downloadHandler.text : null;
        }
    }

    void Painel(string titulo, string linha, float segundos)
    {
        if (painel == null) painel = FindAnyObjectByType<PainelInfo>();
        if (painel == null) return;
        painel.Mostrar(new JObject { ["titulo"] = titulo, ["linhas"] = new JArray(linha.Split('\n')), ["duracao"] = segundos });
    }

    void Gravar()
    {
        try { File.WriteAllText(indice, JsonConvert.SerializeObject(sites, Formatting.Indented)); }
        catch (Exception e) { Debug.LogWarning("EstudioSites: não consegui gravar o índice: " + e.Message); }
    }

    static string Normalizar(string s)
    {
        s = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in s)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    static string Slug(string s)
    {
        string t = Regex.Replace(Normalizar(s), "[^a-z0-9]+", "-").Trim('-');
        return t.Length == 0 ? "site" : (t.Length > 40 ? t.Substring(0, 40).Trim('-') : t);
    }

    string SlugUnico(string titulo)
    {
        string baseSlug = Slug(titulo), slug = baseSlug;
        for (int i = 2; sites.Exists(x => x.slug == slug); i++) slug = baseSlug + "-" + i;
        return slug;
    }

    Site Achar(string nome)
    {
        string n = Normalizar(nome).Trim();
        if (n.Length == 0) return sites.Count > 0 ? sites[sites.Count - 1] : null;
        Site melhor = null;
        foreach (var s in sites)
        {
            string t = Normalizar(s.titulo);
            if (t == n || s.slug == Slug(nome)) return s;
            if (t.Contains(n) || n.Contains(t)) melhor = s;
        }
        return melhor;
    }
}
