using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Templates.MR;

// Realidade mista de verdade: a sala real sempre visível, hologramas por cima, nunca VR.
// Causa do "fundo azul sem a sala": o tutorial do template (GoalManager) chama TogglePassthrough(false)
// no Start, o que desliga o ARCameraManager (e o passthrough do Meta) até o usuário avançar os cartões.
[DefaultExecutionOrder(-1000)]
public class PassthroughMR : MonoBehaviour
{
    public string estado = "iniciando";

    ARCameraManager camMgr;
    ARCameraBackground camBg;
    ARSession sessao;
    int paradoSeguidos;

    void Awake()
    {
        // desliga o tutorial antes do Start dele rodar
        var gm = FindAnyObjectByType<GoalManager>(FindObjectsInactive.Include);
        if (gm != null) gm.gameObject.SetActive(false);
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Coaching UI" || t.name == "Tap Tooltip" || t.name.StartsWith("Hand Menu Setup"))   // menu "Player Settings" do template t.gameObject.SetActive(false);
        Aplicar();
    }

    void Aplicar()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            // fundo sólido preto com alfa 0 = o passthrough aparece por trás dos hologramas
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            if (camMgr == null) camMgr = cam.GetComponent<ARCameraManager>();
            if (camBg == null) camBg = cam.GetComponent<ARCameraBackground>();
        }
        RenderSettings.skybox = null;
        if (camMgr != null && !camMgr.enabled) camMgr.enabled = true;
        if (camBg != null && !camBg.enabled) camBg.enabled = true;
        if (sessao == null) sessao = FindAnyObjectByType<ARSession>(FindObjectsInactive.Include);
        if (sessao != null && !sessao.enabled) sessao.enabled = true;
        // some o ambiente virtual azul do template
        foreach (var f in FindObjectsByType<FadeMaterial>(FindObjectsInactive.Include))
        {
            var r = f.GetComponent<Renderer>();
            if (r != null && r.enabled) r.enabled = false;
        }
    }

    IEnumerator Start()
    {
        var espera = new WaitForSeconds(0.5f);
        while (true)
        {
            Aplicar();
            var sub = camMgr != null ? camMgr.subsystem : null;
            // subsistema criado mas parado por mais de 1,5 s: reinicia o gerenciador da câmera
            paradoSeguidos = (sub != null && !sub.running) ? paradoSeguidos + 1 : 0;
            if (paradoSeguidos >= 3)
            {
                paradoSeguidos = 0;
                Debug.LogWarning("PassthroughMR: câmera AR parada; reiniciando o ARCameraManager.");
                camMgr.enabled = false;
                yield return null;
                camMgr.enabled = true;
            }
            string novo = Descrever(sub);
            if (novo != estado)
            {
                estado = novo;
                Debug.Log("PassthroughMR: " + estado);
            }
            yield return espera;
        }
    }

    string Descrever(XRCameraSubsystem sub)
    {
        if (camMgr == null) return "sem ARCameraManager";
        if (sub == null) return "aguardando câmera AR";
        return sub.running ? "passthrough ligado" : "passthrough parado";
    }

    // ao voltar de uma pausa (menu do sistema, dois toques no óculos) reaplica tudo na hora
    void OnApplicationPause(bool pausado)
    {
        Debug.Log("PassthroughMR: app " + (pausado ? "pausado" : "retomado"));
        if (!pausado) Aplicar();
    }
}
