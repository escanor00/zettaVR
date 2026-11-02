using System;
using System.Collections;
using System.IO;
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
        clienteTrellis = new GradioClient(espacoTrellis);
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
            if (imagem == null) erro = e1 ?? "não consegui usar a câmera";
        }
        else
        {
            etapa = "criando a imagem";
            var cImg = new GradioClient(espacoImagem);
            clienteAtual = cImg;
            var t = GerarImagem(cImg, nome);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) Debug.LogWarning("GeradorModelo3D: imagem falhou: " + t.Exception.GetBaseException().Message);
            else imagem = t.Result;
            clienteAtual = clienteTrellis;
        }

        // 2) imagem → 3D (TRELLIS.2)
        if (imagem != null && erro == null)
        {
            etapa = "TRELLIS.2: modelando";
            var t = TrellisParaGlb(clienteTrellis, imagem);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) erro = t.Exception.GetBaseException().Message;
            else glb = t.Result;
        }

        // 3) reserva: texto → 3D direto (Shap-E) se a imagem ou o TRELLIS falharam
        if (glb == null && origem == "descricao")
        {
            etapa = "plano B: Shap-E (texto → 3D)";
            var cShap = new GradioClient(espacoShapE);
            clienteAtual = cShap;
            var t = ShapEParaGlb(cShap, nome);
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) erro = (erro != null ? erro + " | " : "") + t.Exception.GetBaseException().Message;
            else { glb = t.Result; erro = null; }
        }

        StopCoroutine(relogio);
        var pos = holo != null ? holo.transform.position : HoloUtil.PontoNaVista(new Vector3(0, 0, 0.8f));
        var rot = holo != null ? holo.transform.rotation : Quaternion.identity;
        if (holo != null) ApagarMarcador(holo);

        if (glb == null)
        {
            ocupado = false;
            Debug.LogWarning("GeradorModelo3D: falhou: " + erro);
            bool fila = erro != null && (erro.Contains("quota") || erro.Contains("GPU") || erro.Contains("queue") || erro.Contains("429") || erro.Contains("503"));
            voz?.Falar("a geração do modelo 3D de " + nome + " falhou" + (fila ? " porque a fila ou a cota do serviço gratuito está cheia" : "")
                + ". Avise o usuário em uma frase e sugira tentar de novo mais tarde.");
            yield break;
        }

        // 4) salva e carrega
        string pasta = Path.Combine(Application.persistentDataPath, "Modelos3D");
        Directory.CreateDirectory(pasta);
        string caminho = Path.Combine(pasta, "modelo_" + DateTime.Now.Ticks + ".glb");
        File.WriteAllBytes(caminho, glb);
        var raiz = CriarRaizGlb(pos, rot, caminho, nome, tamanhoFinal, true);
        ocupado = false;
        voz?.Falar("o modelo 3D de " + nome + " ficou pronto depois de " + Mathf.RoundToInt(Time.time - inicio) + " segundos. Avise o usuário em uma frase curta e animada.");
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
        try { await c.Chamar("start_session", new JArray()); } catch (Exception e) { Debug.Log("start_session: " + e.Message); }
        c.estado = "enviando a imagem";
        var arq = await c.Enviar(imagem, "entrada.jpg", "image/jpeg");
        var prep = await c.Chamar("preprocess_image", new JArray(arq));
        int seed = UnityEngine.Random.Range(0, int.MaxValue - 1);
        await c.Chamar("image_to_3d", new JArray(
            prep[0], seed, resolucao,
            7.5, 0.7, 12, 5.0,    // estrutura esparsa
            7.5, 0.5, 12, 3.0,    // forma
            1.0, 0.0, 12, 3.0));  // textura
        c.estado = "exportando o GLB";
        var glb = await c.Chamar("extract_glb", new JArray(null, decimacao, textura));
        return await c.Baixar(glb[0]);
    }

    async Task<byte[]> ShapEParaGlb(GradioClient c, string desc)
    {
        var r = await c.Chamar("text-to-3d", new JArray(desc, UnityEngine.Random.Range(0, 100000), 15, 64));
        return await c.Baixar(r[0]);
    }

    // ------------------------------------------------------------------ foto (Passthrough Camera API)
    IEnumerator CapturarFoto(Action<byte[]> ok, Action<string> erro)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string[] perms = { "horizonos.permission.HEADSET_CAMERA", Permission.Camera };
        bool tem = Permission.HasUserAuthorizedPermission(perms[0]) && Permission.HasUserAuthorizedPermission(perms[1]);
        if (!tem)
        {
            bool respondeu = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => { };
            cb.PermissionDenied += _ => { };
            cb.PermissionDeniedAndDontAskAgain += _ => { };
            Permission.RequestUserPermissions(perms, cb);
            float limite = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < limite)
            {
                yield return new WaitForSeconds(0.5f);
                if (Permission.HasUserAuthorizedPermission(perms[0]) && Permission.HasUserAuthorizedPermission(perms[1])) { respondeu = true; break; }
            }
            if (!respondeu) { erro("permissão da câmera negada"); yield break; }
        }
#endif
        var dispositivos = WebCamTexture.devices;
        if (dispositivos.Length == 0)
        {
            // sem câmera (ex.: editor): usa uma imagem de teste
            if (Application.isEditor) { ok(ImagemDeTeste()); yield break; }
            erro("nenhuma câmera encontrada"); yield break;
        }
        var dev = dispositivos[0];
        int w = 1280, h = 960;
        if (dev.availableResolutions != null && dev.availableResolutions.Length > 0)
        {
            float melhor = float.MaxValue;
            foreach (var res in dev.availableResolutions)
            {
                float d = Mathf.Abs(res.width - 1280) + Mathf.Abs(res.height - 960);
                if (d < melhor) { melhor = d; w = res.width; h = res.height; }
            }
        }
        var cam = new WebCamTexture(dev.name, w, h, 30);
        cam.Play();
        float lim = Time.realtimeSinceStartup + 10f;
        while ((cam.width < 32 || !cam.didUpdateThisFrame) && Time.realtimeSinceStartup < lim) yield return null;
        if (cam.width < 32) { cam.Stop(); Destroy(cam); erro("a câmera não entregou imagem"); yield break; }
        yield return new WaitForSeconds(0.6f); // exposição estabiliza

        var cheia = new Texture2D(cam.width, cam.height, TextureFormat.RGBA32, false);
        cheia.SetPixels32(cam.GetPixels32());
        cheia.Apply();
        cam.Stop();
        Destroy(cam);

        // recorte quadrado central (o objeto olhado fica no meio)
        int lado = Mathf.RoundToInt(Mathf.Min(cheia.width, cheia.height) * 0.7f);
        var recorte = new Texture2D(lado, lado, TextureFormat.RGB24, false);
        recorte.SetPixels(cheia.GetPixels((cheia.width - lado) / 2, (cheia.height - lado) / 2, lado, lado));
        recorte.Apply();
        Destroy(cheia);
        ok(Reduzir(recorte, ladoFoto));
    }

    byte[] Reduzir(Texture2D t, int lado)
    {
        if (t.width > lado)
        {
            var rt = RenderTexture.GetTemporary(lado, lado);
            Graphics.Blit(t, rt);
            var ant = RenderTexture.active;
            RenderTexture.active = rt;
            var r = new Texture2D(lado, lado, TextureFormat.RGB24, false);
            r.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
            r.Apply();
            RenderTexture.active = ant;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(t);
            t = r;
        }
        var bytes = t.EncodeToJPG(92);
        Destroy(t);
        return bytes;
    }

    // imagem sintética para testes sem câmera: esfera vermelha com brilho num fundo claro
    byte[] ImagemDeTeste()
    {
        const int n = 512;
        var t = new Texture2D(n, n, TextureFormat.RGB24, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n * 0.5f) / (n * 0.3f), dy = (y - n * 0.5f) / (n * 0.3f);
                float d2 = dx * dx + dy * dy;
                Color c = new Color(0.9f, 0.9f, 0.92f);
                if (d2 < 1f)
                {
                    float nz = Mathf.Sqrt(1f - d2);
                    float luz = Mathf.Clamp01(Vector3.Dot(new Vector3(dx, dy, nz), new Vector3(-0.4f, 0.5f, 0.75f).normalized));
                    c = new Color(0.85f, 0.1f, 0.1f) * (0.35f + 0.65f * luz) + Color.white * Mathf.Pow(luz, 24f) * 0.5f;
                }
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return Reduzir(t, 512);
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

    IEnumerator CarregarConteudo(GameObject raiz, string caminho, float tamanho)
    {
        byte[] dados = null;
        try { dados = File.ReadAllBytes(caminho); }
        catch (Exception e) { Debug.LogWarning("GeradorModelo3D: arquivo ausente: " + e.Message); }
        if (dados == null) yield break;

        var conteudo = new GameObject("Conteudo").transform;
        conteudo.SetParent(raiz.transform, false);
        var gltf = new GltfImport();
        var carregar = gltf.LoadGltfBinary(dados);
        while (!carregar.IsCompleted) yield return null;
        if (!carregar.Result) { Debug.LogWarning("GeradorModelo3D: glTFast não conseguiu ler o GLB"); yield break; }
        var inst = gltf.InstantiateMainSceneAsync(conteudo);
        while (!inst.IsCompleted) yield return null;
        if (raiz == null) yield break;

        // normaliza: escala para 'tamanho' no maior eixo e centraliza na raiz
        var b = LimitesMundo(conteudo);
        float maior = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (maior > 0.0001f) conteudo.localScale = Vector3.one * (tamanho / maior);
        b = LimitesMundo(conteudo);
        conteudo.position += raiz.transform.position - b.center;
        b = LimitesMundo(conteudo);

        var box = raiz.AddComponent<BoxCollider>();
        box.center = raiz.transform.InverseTransformPoint(b.center);
        box.size = b.size;
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);

        // entrada: uma caixa de grade se desfaz em volta do modelo enquanto ele "estala" para o tamanho final
        var alvoEscala = conteudo.localScale;
        conteudo.localScale = alvoEscala * 0.85f;
        var casca = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(casca.GetComponent<Collider>());
        casca.name = "EntradaGrade";
        casca.transform.SetParent(raiz.transform, false);
        casca.transform.localPosition = box.center;
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
