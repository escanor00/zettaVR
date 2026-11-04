using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Ferramenta "gerar_modelo_3d": foto do que o usuário olha (Passthrough Camera API) ou descrição → imagem → TRELLIS.2
// (Space gratuito do Hugging Face, via Gradio) → .glb → carregado em tempo de execução com glTFast.
public class GeradorModelo3D : MonoBehaviour
{
    [Header("Spaces gratuitos (sem token)")]
    public string espacoTrellis = "https://microsoft-trellis-2.hf.space";
    public string espacoImagem = "https://black-forest-labs-flux-1-schnell.hf.space"; // texto → imagem
    public string espacoShapE = "https://hysts-shap-e.hf.space";                       // texto → 3D (reserva)

    [Header("Qualidade")]
    public string resolucao = "512";
    public int decimacao = 100000;   // faces (mínimo do Space)
    public int textura = 1024;
    public float tamanhoFinal = 0.30f;
    public int ladoFoto = 1024;

    public bool ocupado { get; private set; }

    const string Decl = @"{
  ""name"": ""gerar_modelo_3d"",
  ""description"": ""Gera um modelo 3D realista (leva de 1 a 3 minutos). origem='olhando' fotografa o que o usuário está olhando pela câmera do óculos; origem='descricao' cria o objeto descrito."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""origem"": { ""type"": ""STRING"", ""enum"": [""olhando"", ""descricao""] },
      ""descricao"": { ""type"": ""STRING"", ""description"": ""Para origem=descricao: o objeto em inglês, curto e visual (ex.: a red leather armchair). Para origem=olhando: um nome curto em português para o objeto, se souber."" }
    },
    ""required"": [""origem""]
  }
}";
    const string Instrucao =
        "'Zetta, transforme isso em 3D' → gerar_modelo_3d origem=olhando. 'Zetta, crie um <objeto> realista' → origem=descricao, com a descrição do objeto em inglês. " +
        "A geração demora de 1 a 3 minutos: avise isso em uma frase e siga a conversa normalmente; o app avisará quando terminar.";

    AssistenteVoz voz;
    GradioClient clienteTrellis;
    string TokenHF => voz != null && voz.segredos != null ? voz.segredos.tokenHuggingFace : null;
    public string ultimaFalha = "";   // "etapa: motivo" da última geração que falhou (aparece nos painéis)

    void Awake()
    {
        AncorasHolo.Fabricas["glb"] = (item, pos, rot) => CriarRaizGlb(pos, rot, item.extra, item.forma, item.tamanho);
        voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Iniciar, Instrucao);
    }

    JObject Iniciar(JObject args)
    {
        if (ocupado) return new JObject { ["erro"] = "já existe uma geração em andamento; peça para esperar terminar" };
        string origem = (args.Value<string>("origem") ?? "").ToLowerInvariant();
        string desc = args.Value<string>("descricao") ?? "";
        if (origem != "olhando" && origem != "descricao") return new JObject { ["erro"] = "origem inválida" };
        if (origem == "descricao" && string.IsNullOrWhiteSpace(desc)) return new JObject { ["erro"] = "faltou a descrição do objeto" };
        if (origem == "descricao" && TentarCache(desc, out var emCache))
        {
            var posC = HoloUtil.PontoNaVista(new Vector3(0, 0, 0.8f));
            CriarRaizGlb(posC, HoloUtil.OlharParaUsuario(posC), emCache, desc.Trim(), tamanhoFinal, true);
            return new JObject { ["result"] = "esse modelo já estava guardado; apareceu na hora, sem gerar de novo" };
        }
        StartCoroutine(Fluxo(origem, desc));
        return new JObject { ["result"] = "geração iniciada; leva de 1 a 3 minutos; aparecerá um holograma 'gerando...' no lugar" };
    }

    // ------------------------------------------------------------------ fluxo principal
    IEnumerator Fluxo(string origem, string descricao)
    {
        ocupado = true;
        float inicio = Time.time;
        string nome = string.IsNullOrWhiteSpace(descricao) ? "objeto" : descricao.Trim();
        var holo = CriarMarcador();
        string etapa = "preparando";
        string erro = null;
        byte[] imagem = null, glb = null;
        string etapaFalha = null;
        clienteTrellis = new GradioClient(espacoTrellis, TokenHF);
        GradioClient clienteAtual = clienteTrellis;
        voz?.Falar("começou a gerar o modelo 3D de " + nome + "; avise em uma frase curta que leva de um a três minutos.");

        // enquanto roda, atualiza o texto do marcador
        var relogio = StartCoroutine(AtualizarMarcador(holo, inicio, () => etapa + (clienteAtual.estado.Length > 0 ? "\n" + clienteAtual.estado : "")));

        // 1) imagem de entrada
        if (origem == "olhando")
        {
            etapa = "fotografando";
            string e1 = null;
            yield return CapturarFoto(b => imagem = b, m => e1 = m);
            if (imagem == null) { erro = e1 ?? "não consegui usar a câmera"; etapaFalha = "foto da câmera"; }
        }
        else
        {
            etapa = "criando a imagem";
            var cImg = new GradioClient(espacoImagem, TokenHF);
            clienteAtual = cImg;
            var t = GerarImagem(cImg, nome);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted)
            {
                erro = t.Exception.GetBaseException().Message;
                etapaFalha = "geração da imagem (FLUX.1-schnell)";
                Debug.LogWarning("GeradorModelo3D: [" + etapaFalha + "] " + erro);
            }
            else imagem = t.Result;
            clienteAtual = clienteTrellis;
        }

        Debug.Log("GeradorModelo3D: [tempo] imagem de entrada " + (Time.time - inicio).ToString("0.0") + " s (" + (imagem != null ? imagem.Length / 1024 + " KB" : "falhou") + ")");
        float tTrellis = Time.time;

        // 2) imagem → 3D (TRELLIS.2)
        if (imagem != null && erro == null)
        {
            etapa = "TRELLIS.2: modelando";
            var t = TrellisParaGlb(clienteTrellis, imagem);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted)
            {
                erro = t.Exception.GetBaseException().Message;
                etapaFalha = "TRELLIS.2";
                Debug.LogWarning("GeradorModelo3D: [" + etapaFalha + "] " + erro);
            }
            else glb = t.Result;
        }

        // o TRELLIS.2 exporta texturas WebP (EXT_texture_webp) e o glTFast não decodifica WebP sem um plugin nativo:
        // troca as texturas por uma cor única tirada da imagem de entrada, para o modelo ao menos abrir
        if (glb != null) glb = AdaptarGlbSemWebP(glb, CorDominante(imagem));

        if (imagem != null && erro == null)
            Debug.Log("GeradorModelo3D: [tempo] TRELLIS.2 (inclui envio, geração, exportação e download) " + (Time.time - tTrellis).ToString("0.0") + " s" + (glb != null ? ", GLB " + glb.Length / 1024 + " KB" : ", falhou"));

        // 3) reserva: texto → 3D direto (Shap-E) se a imagem ou o TRELLIS falharam
        if (glb == null && origem == "descricao")
        {
            etapa = "plano B: Shap-E (texto → 3D)";
            var cShap = new GradioClient(espacoShapE, TokenHF);
            clienteAtual = cShap;
            var t = ShapEParaGlb(cShap, nome);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted)
            {
                string e2 = t.Exception.GetBaseException().Message;
                Debug.LogWarning("GeradorModelo3D: [Shap-E] " + e2);
                if (erro == null) { erro = e2; etapaFalha = "Shap-E (plano B)"; }   // mantém o erro da etapa principal
            }
            else { glb = t.Result; erro = null; etapaFalha = null; }
        }

        StopCoroutine(relogio);
        var pos = holo != null ? holo.transform.position : HoloUtil.PontoNaVista(new Vector3(0, 0, 0.8f));
        var rot = holo != null ? holo.transform.rotation : Quaternion.identity;
        if (holo != null) ApagarMarcador(holo);

        if (glb == null)
        {
            ocupado = false;
            ReportarFalha(nome, etapaFalha ?? "geração", erro ?? "erro desconhecido", clienteTrellis.UsaToken);
            yield break;
        }

        // 4) salva e carrega
        string pasta = Path.Combine(Application.persistentDataPath, "Modelos3D");
        Directory.CreateDirectory(pasta);
        string caminho = Path.Combine(pasta, NomeArquivo(nome) + "_" + DateTime.Now.Ticks + ".glb");
        File.WriteAllBytes(caminho, glb);
        if (origem == "descricao") GuardarNoCache(nome, caminho);
        float tCarga = Time.time;
        var raiz = CriarRaizGlb(pos, rot, caminho, nome, tamanhoFinal, true);
        Debug.Log("GeradorModelo3D: [tempo] total até salvar " + (tCarga - inicio).ToString("0.0") + " s; carregamento glTFast começa agora");
        ocupado = false;
        voz?.Falar("o modelo 3D de " + nome + " ficou pronto depois de " + Mathf.RoundToInt(Time.time - inicio) + " segundos. Avise o usuário em uma frase curta e animada.");
    }

    // ------------------------------------------------------------------ cache de modelos gerados (só por descrição)
    // Índice persistente "descrição normalizada → arquivo .glb" em Modelos3D/cache.json: pedir o mesmo objeto de novo reaproveita o arquivo.
    static string ArquivoIndice => Path.Combine(Application.persistentDataPath, "Modelos3D", "cache.json");

    // minúsculas, sem acentos nem pontuação, espaços colapsados: "Um Gato!" e "um  gato" dão a mesma chave
    public static string ChaveCache(string descricao)
    {
        var d = (descricao ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        bool espaco = false;
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); espaco = false; }
            else if (!espaco && sb.Length > 0) { sb.Append(' '); espaco = true; }
        }
        return sb.ToString().Trim();
    }

    static Dictionary<string, string> LerIndice()
    {
        try
        {
            if (File.Exists(ArquivoIndice))
                return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(ArquivoIndice)) ?? new Dictionary<string, string>();
        }
        catch (Exception e) { Debug.LogWarning("GeradorModelo3D: índice do cache ilegível, recomeçando: " + e.Message); }
        return new Dictionary<string, string>();
    }

    public static bool TentarCache(string descricao, out string caminho)
    {
        caminho = null;
        string chave = ChaveCache(descricao);
        if (chave.Length == 0) return false;
        var idx = LerIndice();
        if (!idx.TryGetValue(chave, out var c)) return false;
        if (!File.Exists(c)) { idx.Remove(chave); EscreverIndice(idx); return false; }   // arquivo sumiu: limpa a entrada
        caminho = c;
        return true;
    }

    public static void GuardarNoCache(string descricao, string caminho)
    {
        string chave = ChaveCache(descricao);
        if (chave.Length == 0) return;
        var idx = LerIndice();
        idx[chave] = caminho;
        EscreverIndice(idx);
    }

    static void EscreverIndice(Dictionary<string, string> idx)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArquivoIndice));
            File.WriteAllText(ArquivoIndice, Newtonsoft.Json.JsonConvert.SerializeObject(idx));
        }
        catch (Exception e) { Debug.LogWarning("GeradorModelo3D: não consegui gravar o cache: " + e.Message); }
    }

    // ------------------------------------------------------------------ falhas explicadas
    // Mostra a etapa e o erro (painel + rótulo do anel + log) e faz a Zetta explicar por voz em português.
    void ReportarFalha(string nome, string etapa, string erro, bool comToken)
    {
        string baixo = (erro ?? "").ToLowerInvariant();
        string causa, dica;
        if (baixo.Contains("zerogpu") || baixo.Contains("quota") || baixo.Contains("exceeded"))
        {
            causa = "a cota gratuita de GPU do Hugging Face acabou";
            dica = comToken ? "Mesmo com o token a cota diária esgotou; sugira tentar mais tarde ou amanhã."
                            : "Sem token a cota anônima é pequena; diga que o usuário pode criar uma conta grátis no Hugging Face e colocar o token no ZettaSegredos para ter mais cota. Também pode tentar mais tarde.";
        }
        else if (baixo.Contains("endpoint não existe") || baixo.Contains("http 404"))
        {
            causa = "o endereço ou a interface do serviço mudou (endpoint não encontrado)";
            dica = "Diga que o serviço gratuito foi alterado e precisa de uma atualização do app.";
        }
        else if (baixo.Contains("download"))
        {
            causa = "o modelo foi criado mas o download falhou";
            dica = "Sugira tentar de novo e conferir a internet.";
        }
        else if (baixo.Contains("http 503") || baixo.Contains("http 502") || baixo.Contains("http 429") || baixo.Contains("sleeping") || baixo.Contains("building") || baixo.Contains("queue") || baixo.Contains("capacity"))
        {
            causa = "o serviço gratuito está lotado ou fora do ar agora";
            dica = "Sugira tentar de novo daqui a alguns minutos.";
        }
        else if (etapa == "foto da câmera")
        {
            causa = "não consegui usar a câmera do óculos";
            dica = "Peça para conferir a permissão de câmeras do app nas configurações do Quest.";
        }
        else
        {
            causa = "deu um erro inesperado";
            dica = "Diga que o detalhe técnico aparece no painel e sugira tentar de novo.";
        }

        string curto = erro.Length > 140 ? erro.Substring(0, 140) : erro;
        ultimaFalha = etapa + ": " + curto;
        Debug.LogWarning("GeradorModelo3D: falhou em [" + etapa + "]: " + curto);
        voz?.MostrarAviso("3D falhou: " + etapa, 8f);
        var painel = FindAnyObjectByType<PainelInfo>();
        if (painel != null)
            painel.Mostrar(new JObject
            {
                ["titulo"] = "Falha no modelo 3D",
                ["linhas"] = new JArray("Etapa: " + etapa, curto),
                ["duracao"] = 30
            });
        voz?.Falar("a geração do modelo 3D de " + nome + " falhou na etapa " + etapa + ": " + causa + ". " + dica + " Explique em até duas frases simples, sem termos técnicos.");
    }

    // ------------------------------------------------------------------ rede
    async Task<byte[]> GerarImagem(GradioClient c, string desc)
    {
        string prompt = desc + ", single object centered, full object visible, plain neutral light gray background, soft studio lighting, product photo, high detail";
        var r = await c.Chamar("infer", new JArray(prompt, 0, true, 1024, 1024, 4));
        return await c.Baixar(r[0]);
    }

    async Task<byte[]> TrellisParaGlb(GradioClient c, byte[] imagem)
    {
        string sub = "sessão";
        try
        {
            try { await c.Chamar("start_session", new JArray()); } catch (Exception e) { Debug.Log("start_session: " + e.Message); }
            sub = "envio da imagem";
            c.estado = "enviando a imagem";
            var arq = await c.Enviar(imagem, "entrada.jpg", "image/jpeg");
            sub = "preprocess_image";
            var prep = await c.Chamar("preprocess_image", new JArray(arq));
            sub = "image_to_3d";
            int seed = UnityEngine.Random.Range(0, int.MaxValue - 1);
            await c.Chamar("image_to_3d", new JArray(
                prep[0], seed, resolucao,
                7.5, 0.7, 12, 5.0,    // estrutura esparsa
                7.5, 0.5, 12, 3.0,    // forma
                1.0, 0.0, 12, 3.0));  // textura
            sub = "extract_glb";
            c.estado = "exportando o GLB";
            var glb = await c.Chamar("extract_glb", new JArray(null, decimacao, textura));
            sub = "download do GLB";
            c.estado = "baixando o GLB";
            return await c.Baixar(glb[0]);
        }
        catch (Exception e) { throw new Exception("[" + sub + "] " + e.Message, e); }
    }

    async Task<byte[]> ShapEParaGlb(GradioClient c, string desc)
    {
        var r = await c.Chamar("text-to-3d", new JArray(desc, UnityEngine.Random.Range(0, 100000), 15, 64));
        return await c.Baixar(r[0]);
    }

    // cor média do objeto na imagem de entrada: ignora pixels parecidos com o fundo (média dos cantos)
    static Color CorDominante(byte[] jpeg)
    {
        var padrao = new Color(0.78f, 0.78f, 0.8f);
        if (jpeg == null) return padrao;
        var tex = new Texture2D(2, 2);
        try
        {
            if (!tex.LoadImage(jpeg)) return padrao;
            int w = tex.width, h = tex.height;
            Color fundo = (tex.GetPixel(4, 4) + tex.GetPixel(w - 5, 4) + tex.GetPixel(4, h - 5) + tex.GetPixel(w - 5, h - 5)) * 0.25f;
            float r = 0, g = 0, b = 0; int n = 0;
            for (int y = h / 5; y < h * 4 / 5; y += 4)
                for (int x = w / 5; x < w * 4 / 5; x += 4)
                {
                    var c = tex.GetPixel(x, y);
                    var d = c - fundo;
                    if (d.r * d.r + d.g * d.g + d.b * d.b < 0.03f) continue;
                    r += c.r; g += c.g; b += c.b; n++;
                }
            return n > 20 ? new Color(r / n, g / n, b / n) : padrao;
        }
        catch { return padrao; }
        finally { Destroy(tex); }
    }

    // Reescreve o GLB: remove texturas WebP e a extensão exigida, usa 'cor' como cor base do material.
    // Estrutura GLB: cabeçalho 12 bytes + bloco JSON (tamanho, tipo, dados com padding de espaços) + bloco BIN.
    public static byte[] AdaptarGlbSemWebP(byte[] glb, Color cor)
    {
        try
        {
            if (glb.Length < 28 || BitConverter.ToUInt32(glb, 0) != 0x46546C67) return glb;
            int jl = (int)BitConverter.ToUInt32(glb, 12);
            var json = JObject.Parse(System.Text.Encoding.UTF8.GetString(glb, 20, jl));
            string usadas = json["extensionsUsed"]?.ToString() ?? "";
            if (!usadas.Contains("EXT_texture_webp")) return glb;

            json.Remove("textures");
            json.Remove("images");
            json.Remove("extensionsRequired");
            json.Remove("extensionsUsed");
            if (json["materials"] is JArray mats)
                foreach (var m in mats)
                {
                    var pbr = m["pbrMetallicRoughness"] as JObject ?? new JObject();
                    pbr.Remove("baseColorTexture");
                    pbr.Remove("metallicRoughnessTexture");
                    pbr["baseColorFactor"] = new JArray(Mathf.Pow(cor.r, 2.2f), Mathf.Pow(cor.g, 2.2f), Mathf.Pow(cor.b, 2.2f), 1f);   // fator é linear
                    pbr["metallicFactor"] = 0.0;
                    pbr["roughnessFactor"] = 0.75;
                    m["pbrMetallicRoughness"] = pbr;
                    (m as JObject)?.Remove("normalTexture");
                    (m as JObject)?.Remove("occlusionTexture");
                    (m as JObject)?.Remove("emissiveTexture");
                }
            var novo = System.Text.Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            int pad = (4 - novo.Length % 4) % 4;
            int njl = novo.Length + pad;
            int restoIni = 20 + jl;                       // o que vem depois do bloco JSON (bloco BIN)
            int total = 12 + 8 + njl + (glb.Length - restoIni);
            var saida = new byte[total];
            Buffer.BlockCopy(glb, 0, saida, 0, 12);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)total), 0, saida, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)njl), 0, saida, 12, 4);
            Buffer.BlockCopy(glb, 16, saida, 16, 4);       // tipo "JSON"
            Buffer.BlockCopy(novo, 0, saida, 20, novo.Length);
            for (int i = 0; i < pad; i++) saida[20 + novo.Length + i] = 0x20;
            Buffer.BlockCopy(glb, restoIni, saida, 20 + njl, glb.Length - restoIni);
            Debug.Log("GeradorModelo3D: texturas WebP removidas do GLB (cor base " + ColorUtility.ToHtmlStringRGB(cor) + ")");
            return saida;
        }
        catch (Exception e)
        {
            Debug.LogWarning("GeradorModelo3D: não consegui adaptar o GLB: " + e.Message);
            return glb;
        }
    }

    // nome do arquivo = descrição sem caracteres inválidos (a biblioteca de modelos lê o nome daqui)
    static string NomeArquivo(string nome)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in nome.Trim())
            if (char.IsLetterOrDigit(c) || c == ' ') sb.Append(c == ' ' ? '-' : c);
        return sb.Length == 0 ? "objeto" : sb.ToString(0, Mathf.Min(sb.Length, 40));
    }

    // ------------------------------------------------------------------ foto (Passthrough Camera API, em CameraOlhos)
    public IEnumerator CapturarFoto(Action<byte[]> ok, Action<string> erro)
    {
        var cam = FindAnyObjectByType<CameraOlhos>();
        if (cam == null) { erro("câmera indisponível"); yield break; }
        yield return cam.Capturar(ok, erro, ladoFoto, true);
    }

    // ------------------------------------------------------------------ marcador "gerando..."
    GameObject CriarMarcador()
    {
        Vector3 pos;
        var mesa = FindAnyObjectByType<MesaHolografica>();
        pos = (mesa != null && mesa.ativa) ? mesa.Centro + Vector3.up * 0.22f : HoloUtil.PontoNaVista(new Vector3(0f, -0.05f, 0.8f));
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Gerando3D";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, true);
        go.transform.position = pos;
        go.transform.rotation = HoloUtil.OlharParaUsuario(pos);
        go.transform.localScale = Vector3.one * 0.22f;
        CriadorHolograma.AplicarVisual(go, HoloUtil.Ciano, new Vector2(4, 4), 1f);
        var texto = HoloUtil.CriarTexto(go.transform, "Info", "gerando...", new Vector3(0, 0.9f, 0), new Vector2(3.2f, 1.2f), 1.1f, Color.white);
        texto.name = "Info";
        return go;
    }

    IEnumerator AtualizarMarcador(GameObject go, float inicio, Func<string> detalhe)
    {
        float proxRepeticao = Time.time + 2.2f;
        while (go != null)
        {
            var info = go.transform.Find("Info");
            if (info != null)
            {
                var t = info.GetComponent<TextMeshPro>();
                int s = Mathf.FloorToInt(Time.time - inicio);
                t.text = "gerando...  " + (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + "\n<size=60%>" + detalhe() + "</size>";
                // texto sempre legível: de frente para o usuário, sem herdar a escala do cubo girando
                if (Camera.main != null)
                    info.rotation = Quaternion.LookRotation(info.position - Camera.main.transform.position, Vector3.up);
            }
            // repete o efeito de montagem enquanto espera
            if (Time.time >= proxRepeticao)
            {
                var m = go.GetComponent<HologramaMontagem>();
                if (m != null) m.Reiniciar();
                proxRepeticao = Time.time + 2.2f;
            }
            go.transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
            yield return null;
        }
    }

    void ApagarMarcador(GameObject go)
    {
        var m = go.GetComponent<HologramaMontagem>();
        if (m != null) m.Desfazer(0.4f);
        Destroy(go, 0.5f);
    }

    // ------------------------------------------------------------------ GLB em tempo de execução
    // Cria a raiz já registrada (para âncoras/apagar) e carrega o conteúdo do arquivo de forma assíncrona.
    public GameObject CriarRaizGlb(Vector3 pos, Quaternion rot, string caminho, string nome, float tamanho, bool entradaEspecial = true)
    {
        var raiz = new GameObject("Modelo3D_" + nome.Replace(' ', '_'));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rot.eulerAngles.y, 0f));
        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        HoloRegistro.Adicionar(raiz, nome, "", "modelo 3d " + nome, "glb", tamanho, caminho);
        StartCoroutine(CarregarConteudo(raiz, caminho, tamanho > 0.01f ? tamanho : tamanhoFinal));
        return raiz;
    }

    void FalhaCarregamento(GameObject raiz, string motivo)
    {
        Debug.LogWarning("GeradorModelo3D: carregamento do GLB falhou: " + motivo);
        ultimaFalha = "carregamento do GLB (glTFast): " + motivo;
        voz?.MostrarAviso("3D falhou: carregamento do GLB", 8f);
        var painel = FindAnyObjectByType<PainelInfo>();
        if (painel != null)
            painel.Mostrar(new JObject { ["titulo"] = "Falha no modelo 3D", ["linhas"] = new JArray("Etapa: carregamento do GLB", motivo), ["duracao"] = 30 });
        voz?.Falar("o modelo 3D foi baixado mas não consegui abri-lo na sala: " + motivo + ". Explique em uma frase simples e sugira gerar de novo.");
        if (raiz != null) { HoloRegistro.Remover(raiz); Destroy(raiz); }
    }

    IEnumerator CarregarConteudo(GameObject raiz, string caminho, float tamanho)
    {
        byte[] dados = null;
        try { dados = File.ReadAllBytes(caminho); }
        catch (Exception e) { Debug.LogWarning("GeradorModelo3D: arquivo ausente: " + e.Message); }
        if (dados == null) { FalhaCarregamento(raiz, "arquivo do modelo não encontrado"); yield break; }

        var conteudo = new GameObject("Conteudo").transform;
        conteudo.SetParent(raiz.transform, false);
        float tLeitura = Time.realtimeSinceStartup;
        var gltf = new GltfImport();
        var carregar = gltf.LoadGltfBinary(dados);
        while (!carregar.IsCompleted) yield return null;
        if (!carregar.Result) { FalhaCarregamento(raiz, "o glTFast não conseguiu ler o arquivo GLB"); yield break; }
        var inst = gltf.InstantiateMainSceneAsync(conteudo);
        while (!inst.IsCompleted) yield return null;
        if (raiz == null) yield break;
        Debug.Log("GeradorModelo3D: [tempo] glTFast leu e instanciou o GLB em " + (Time.realtimeSinceStartup - tLeitura).ToString("0.0") + " s (" + dados.Length / 1024 + " KB)");

        // normaliza: escala para 'tamanho' no maior eixo e centraliza na raiz
        var b = LimitesMundo(conteudo);
        float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maior > 0.0001f) conteudo.localScale = Vector3.one * (tamanho / maior);
        b = LimitesMundo(conteudo);
        conteudo.position += raiz.transform.position - b.center;
        b = LimitesMundo(conteudo);

        raiz.AddComponent<ColisorPegada>();   // caixa pelos bounds visíveis (antes do XRGrabInteractable)
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);
        ObjetoInterativo.Garantir(raiz);   // nós/submalhas do .glb viram partes (malha única é dividida em regiões)

        // entrada: uma caixa de grade se desfaz em volta do modelo enquanto ele "estala" para o tamanho final
        var alvoEscala = conteudo.localScale;
        conteudo.localScale = alvoEscala * 0.85f;
        var casca = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(casca.GetComponent<Collider>());
        casca.name = "EntradaGrade";
        casca.transform.SetParent(raiz.transform, false);
        casca.transform.localPosition = raiz.transform.InverseTransformPoint(b.center);
        casca.transform.localScale = b.size * 1.05f;
        CriadorHolograma.AplicarVisual(casca, HoloUtil.Ciano, new Vector2(4, 4), 0.6f, null, 0.15f);
        yield return new WaitForSeconds(0.7f);
        var mont = casca.GetComponent<HologramaMontagem>();
        if (mont != null) mont.Desfazer(0.5f);
        for (float t = 0; t < 0.5f && conteudo != null; t += Time.deltaTime)
        {
            conteudo.localScale = Vector3.Lerp(alvoEscala * 0.85f, alvoEscala, Mathf.SmoothStep(0f, 1f, t / 0.5f));
            yield return null;
        }
        if (conteudo != null) conteudo.localScale = alvoEscala;
        Destroy(casca, 0.1f);
    }

    static Bounds LimitesMundo(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.01f);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
