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
        if (dispositivos.Length == 0)
        {
            if (Application.isEditor) { ok(ImagemDeTeste()); yield break; }
            ultimoErro = "nenhuma câmera encontrada";
            erro(ultimoErro);
            yield break;
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
        if (cam.width < 32)
        {
            cam.Stop();
            Destroy(cam);
            if (Application.isEditor) { ok(ImagemDeTeste()); yield break; }   // o simulador XR lista câmeras falsas
            ultimoErro = "a câmera não entregou imagem";
            erro(ultimoErro);
            yield break;
        }
        yield return new WaitForSeconds(0.6f);   // exposição estabiliza

        var cheia = new Texture2D(cam.width, cam.height, TextureFormat.RGBA32, false);
        cheia.SetPixels32(cam.GetPixels32());
        cheia.Apply();
        cam.Stop();
        Destroy(cam);

        Texture2D origem = cheia;
        if (recorteCentral)
        {
            int q = Mathf.RoundToInt(Mathf.Min(cheia.width, cheia.height) * 0.7f);
            var recorte = new Texture2D(q, q, TextureFormat.RGB24, false);
            recorte.SetPixels(cheia.GetPixels((cheia.width - q) / 2, (cheia.height - q) / 2, q, q));
            recorte.Apply();
            Destroy(cheia);
            origem = recorte;
        }
        ok(ReduzirParaJpeg(origem, lado));
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
