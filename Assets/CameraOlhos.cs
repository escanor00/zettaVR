using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// "Olhos" da Zetta: captura quadros das câmeras do passthrough do Quest (Passthrough Camera API, que no Unity chega
// como WebCamTexture) depois de pedir em tempo de execução as permissões horizonos.permission.HEADSET_CAMERA e CAMERA.
// Usada por "olhar" (visão) e por "gerar_modelo_3d" (foto do objeto). Sem câmera (editor) devolve uma imagem de teste.
public class CameraOlhos : MonoBehaviour
{
    public string ultimoErro = "";     // última falha (também mostrada no rótulo do anel por quem chama)
    public bool permissaoNegada;
    public bool mostrarMiniatura = true;   // miniatura holográfica da foto por 5 s (diagnóstico); desligue quando não precisar
    public float limiarBrilho = 0.03f;     // abaixo disso a imagem é considerada preta

    // lado: maior dimensão do JPEG em pixels. recorteCentral: true = quadrado central (objeto olhado), false = quadro inteiro.
    public IEnumerator Capturar(Action<byte[]> ok, Action<string> erro, int lado = 1024, bool recorteCentral = true)
    {
        ultimoErro = "";
#if UNITY_ANDROID && !UNITY_EDITOR
        string[] perms = { "horizonos.permission.HEADSET_CAMERA", Permission.Camera };
        if (!(Permission.HasUserAuthorizedPermission(perms[0]) && Permission.HasUserAuthorizedPermission(perms[1])))
        {
            bool negou = false;
            var cb = new PermissionCallbacks();
            cb.PermissionDenied += _ => negou = true;
            cb.PermissionDeniedAndDontAskAgain += _ => negou = true;
            Permission.RequestUserPermissions(perms, cb);
            float limite = Time.realtimeSinceStartup + 60f;
            while (!(Permission.HasUserAuthorizedPermission(perms[0]) && Permission.HasUserAuthorizedPermission(perms[1])))
            {
                if (negou || Time.realtimeSinceStartup > limite)
                {
                    permissaoNegada = true;
                    ultimoErro = "câmera negada";
                    erro(ultimoErro);
                    yield break;
                }
                yield return new WaitForSeconds(0.3f);
            }
        }
        permissaoNegada = false;
        // a lista de câmeras só aparece depois da permissão: espera um pouco
        for (float t = 0; WebCamTexture.devices.Length == 0 && t < 5f; t += 0.5f) yield return new WaitForSeconds(0.5f);
#endif
        var dispositivos = WebCamTexture.devices;
        var nomes = new System.Text.StringBuilder();
        foreach (var d in dispositivos) nomes.Append("[" + d.name + (d.isFrontFacing ? " frontal" : "") + "] ");
        Debug.Log("CameraOlhos: " + dispositivos.Length + " câmeras: " + nomes);
        if (dispositivos.Length == 0)
        {
            if (Application.isEditor) { ok(ImagemDeTeste()); yield break; }
            ultimoErro = "nenhuma câmera encontrada";
            erro(ultimoErro);
            yield break;
        }

        // tenta cada câmera; a primeira que entregar imagem com brilho acima do limiar vale
        Texture2D origem = null;
        float brilhoFinal = 0f;
        for (int di = 0; di < dispositivos.Length && origem == null; di++)
        {
            var dev = dispositivos[di];
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
            float lim = Time.realtimeSinceStartup + 6f;
            while (cam.width < 32 && Time.realtimeSinceStartup < lim) yield return null;
            if (cam.width < 32)
            {
                Debug.LogWarning("CameraOlhos: '" + dev.name + "' não entregou imagem");
                cam.Stop(); Destroy(cam);
                continue;
            }

            // aquecimento: descarta os primeiros quadros e espera até ~2 s; até 3 tentativas de brilho
            int quadros = 0;
            float fimAquecimento = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < fimAquecimento)
            {
                if (cam.didUpdateThisFrame) quadros++;
                if (quadros >= 12) break;
                yield return null;
            }
            for (int t = 0; t < 3 && origem == null; t++)
            {
                if (t > 0) yield return new WaitForSeconds(0.5f);
                var copia = CopiarParaTextura(cam);
                float b = BrilhoMedio(copia);
                Debug.Log("CameraOlhos: '" + dev.name + "' " + cam.width + "x" + cam.height + " tentativa " + (t + 1) + " brilho médio " + b.ToString("0.000"));
                if (b >= limiarBrilho) { origem = copia; brilhoFinal = b; }
                else Destroy(copia);
            }
            cam.Stop();
            Destroy(cam);
        }

        if (origem == null)
        {
            if (Application.isEditor) { ok(ImagemDeTeste()); yield break; }   // o simulador XR lista câmeras falsas
            ultimoErro = "imagem da câmera preta";
            erro(ultimoErro);
            yield break;
        }

        if (recorteCentral)
        {
            int q = Mathf.RoundToInt(Mathf.Min(origem.width, origem.height) * 0.7f);
            var recorte = new Texture2D(q, q, TextureFormat.RGB24, false);
            recorte.SetPixels(origem.GetPixels((origem.width - q) / 2, (origem.height - q) / 2, q, q));
            recorte.Apply();
            Destroy(origem);
            origem = recorte;
        }
        var jpeg = ReduzirParaJpeg(origem, lado);
        SalvarDiagnostico(jpeg, brilhoFinal);
        ok(jpeg);
    }

    // Blit da WebCamTexture para uma RenderTexture + ReadPixels (mais confiável que GetPixels32 no Quest)
    static Texture2D CopiarParaTextura(WebCamTexture cam)
    {
        var rt = RenderTexture.GetTemporary(cam.width, cam.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(cam, rt);
        var ant = RenderTexture.active;
        RenderTexture.active = rt;
        var t = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, cam.width, cam.height), 0, 0);
        t.Apply();
        RenderTexture.active = ant;
        RenderTexture.ReleaseTemporary(rt);
        return t;
    }

    // média da luminância (0–1) numa grade de ~48x48 amostras
    static float BrilhoMedio(Texture2D t)
    {
        float soma = 0; int n = 0;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                var c = t.GetPixel(x * (t.width - 1) / 47, y * (t.height - 1) / 47);
                soma += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                n++;
            }
        return soma / n;
    }

    // guarda a última foto, registra o brilho e mostra uma miniatura holográfica por 5 s ao lado do anel
    void SalvarDiagnostico(byte[] jpeg, float brilho)
    {
        try { System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.persistentDataPath, "ultima_foto.jpg"), jpeg); }
        catch (Exception e) { Debug.LogWarning("CameraOlhos: não salvou a foto: " + e.Message); }
        Debug.Log("CameraOlhos: foto " + jpeg.Length / 1024 + " KB, brilho médio " + brilho.ToString("0.000") + " → " + Application.persistentDataPath + "/ultima_foto.jpg");
        if (mostrarMiniatura) StartCoroutine(Miniatura(jpeg));
    }

    IEnumerator Miniatura(byte[] jpeg)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(jpeg)) { Destroy(tex); yield break; }
        var anel = FindAnyObjectByType<AnelHolografico>();
        var cam = Camera.main != null ? Camera.main.transform : null;
        var ancora = anel != null ? anel.transform.position : (cam != null ? cam.position + cam.forward * 0.8f : Vector3.zero);
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "MiniaturaFoto";
        Destroy(go.GetComponent<Collider>());
        float asp = (float)tex.width / tex.height;
        go.transform.localScale = new Vector3(0.14f * asp, 0.14f, 1f);
        go.transform.position = ancora + (cam != null ? cam.right : Vector3.right) * 0.2f;
        go.transform.rotation = HoloUtil.OlharParaUsuario(go.transform.position);
        var m = HoloUtil.NovoMaterialSprite(Color.white);
        m.mainTexture = tex;
        go.GetComponent<Renderer>().material = m;
        yield return new WaitForSeconds(5f);
        Destroy(go);
        Destroy(tex);
    }

    // reduz mantendo a proporção (maior dimensão = lado) e codifica em JPEG
    public static byte[] ReduzirParaJpeg(Texture2D t, int lado, int qualidade = 88)
    {
        int maior = Mathf.Max(t.width, t.height);
        if (maior > lado)
        {
            float k = (float)lado / maior;
            int w = Mathf.Max(2, Mathf.RoundToInt(t.width * k)), h = Mathf.Max(2, Mathf.RoundToInt(t.height * k));
            var rt = RenderTexture.GetTemporary(w, h);
            Graphics.Blit(t, rt);
            var ant = RenderTexture.active;
            RenderTexture.active = rt;
            var r = new Texture2D(w, h, TextureFormat.RGB24, false);
            r.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            r.Apply();
            RenderTexture.active = ant;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(t);
            t = r;
        }
        var bytes = t.EncodeToJPG(qualidade);
        Destroy(t);
        return bytes;
    }

    // imagem sintética para testes sem câmera: esfera vermelha com brilho num fundo claro
    public static byte[] ImagemDeTeste()
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
        return ReduzirParaJpeg(t, 512);
    }
}
