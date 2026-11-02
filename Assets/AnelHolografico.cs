using UnityEngine;

public class AnelHolografico : MonoBehaviour
{
    public Color cor = new Color(0.2f, 0.9f, 1f, 0.8f);
    public float distancia = 1.2f;
    // canto inferior direito da visão (direita, cima), em metros a 'distancia'
    public Vector2 deslocamento = new Vector2(0.36f, -0.24f);
    public float escala = 0.4f;
    public float raioEsfera = 0.42f;   // raio da esfera (x escala = ~12 cm)

    Transform[] aneis;
    float[] velocidades = { 20f, -35f, 50f, -15f };

    void Start()
    {
        // Posiciona na frente da cabeça
        var cam = Camera.main.transform;
        transform.position = AlvoPosicao(cam);
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);

        // a esfera (EsferaZetta) substitui os anéis simples e o núcleo antigo (NucleoEnergia fica no projeto, desligado)
        var nucleo = FindAnyObjectByType<NucleoEnergia>();
        if (nucleo != null) { nucleo.enabled = false; foreach (var r in nucleo.GetComponentsInChildren<Renderer>()) r.enabled = false; }
        var esfera = new GameObject("EsferaZetta");
        esfera.transform.SetParent(transform, false);
        esfera.transform.localScale = Vector3.one * raioEsfera;
        esfera.AddComponent<EsferaZetta>();
        aneis = new Transform[0];

        transform.localScale = Vector3.zero; // começa invisível
    }

    // sempre no canto inferior direito do campo de visão, nunca no centro
    public Vector3 AlvoPosicao(Transform cam)
    {
        return cam.position + cam.forward * distancia + cam.right * deslocamento.x + cam.up * deslocamento.y;
    }

    Transform CriarAnel(float raio, int pontos, bool tracejado, float espessura)
    {
        var obj = new GameObject("Anel");
        obj.transform.SetParent(transform, false);
        var mat = new Material(Shader.Find("Sprites/Default"));

        int segmentos = tracejado ? pontos / 2 : 1;
        for (int s = 0; s < segmentos; s++)
        {
            var seg = new GameObject("Seg");
            seg.transform.SetParent(obj.transform, false);
            var lr = seg.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.material = mat;
            lr.startColor = lr.endColor = cor;
            lr.widthMultiplier = espessura;

            int n = tracejado ? 2 : pontos + 1;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = tracejado ? (s * 2 + i) / (float)pontos : i / (float)pontos;
                float ang = t * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0) * raio);
            }
        }
        return obj.transform;
    }

    void Update()
    {
        // Segue o olhar suavemente
        var cam = Camera.main.transform;
        transform.position = Vector3.Lerp(transform.position, AlvoPosicao(cam), Time.deltaTime * 2f);
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(transform.position - cam.position), Time.deltaTime * 4f);

        // Animação de entrada: cresce suavemente
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * escala, Time.deltaTime * 3f);

        // Cada anel gira numa velocidade
        for (int i = 0; i < aneis.Length; i++)
            aneis[i].Rotate(0, 0, velocidades[i] * Time.deltaTime);

        // Pulso de brilho
        float pulso = 0.85f + Mathf.Sin(Time.time * 3f) * 0.15f;
        if (aneis.Length > 0) aneis[0].localScale = Vector3.one * pulso;
    }
}