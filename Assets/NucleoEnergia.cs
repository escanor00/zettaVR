using UnityEngine;

public class NucleoEnergia : MonoBehaviour
{
    [Range(0f, 1f)] public float energia = 0.5f;
    public Color corNucleo = new Color(0.6f, 1f, 1f, 1f);
    public Color corAura = new Color(0.3f, 0.95f, 1f, 1f);
    public float diametro = 0.06f;

    [Header("Voz")]
    public AssistenteVoz voz;
    [Range(0f, 1f)] public float energiaRepouso = 0.15f;
    public float ganhoVoz = 2f;

    public Color corEspera = new Color(0.15f, 0.35f, 1f, 0.45f);
    [Range(0f, 1f)] public float brilhoEspera = 0.3f;
    float brilho;
    Material matNucleo;
    Transform nucleo;
    Transform[] auras;
    Material[] matAuras;
    float[] tamanhos = { 0.14f, 0.26f };
    float[] opacidades = { 0.55f, 0.3f };
    Texture2D texturaRadial;
    float fase;

    void Start()
    {
        var esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "Nucleo";
        Destroy(esfera.GetComponent<Collider>());
        nucleo = esfera.transform;
        nucleo.SetParent(transform, false);
        nucleo.localPosition = Vector3.zero;
        nucleo.localScale = Vector3.one * diametro;
        matNucleo = new Material(Shader.Find("Sprites/Default"));
        matNucleo.color = corNucleo;
        esfera.GetComponent<Renderer>().material = matNucleo;

        texturaRadial = CriarTexturaRadial(128);
        auras = new Transform[tamanhos.Length];
        matAuras = new Material[tamanhos.Length];
        for (int i = 0; i < auras.Length; i++)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Aura" + i;
            Destroy(quad.GetComponent<Collider>());
            auras[i] = quad.transform;
            auras[i].SetParent(transform, false);
            auras[i].localPosition = Vector3.zero;
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = texturaRadial;
            quad.GetComponent<Renderer>().material = mat;
            matAuras[i] = mat;
        }
    }

    Texture2D CriarTexturaRadial(int tamanho)
    {
        var tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float centro = (tamanho - 1) * 0.5f;
        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(centro, centro)) / centro);
                float a = Mathf.Pow(1f - d, 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }

    void Update()
    {
        if (nucleo == null) return;

        // o volume da voz do assistente controla a energia
        if (voz == null) voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null)
            energia = Mathf.Lerp(energia, Mathf.Clamp01(energiaRepouso + voz.volumeSaida * ganhoVoz), Time.deltaTime * 10f);

        // espera: núcleo azul fraco; acordada: mais brilhante
        float alvoBrilho = (voz != null && !voz.acordada) ? brilhoEspera : 1f;
        brilho = Mathf.Lerp(brilho, alvoBrilho, Time.deltaTime * 4f);
        float t = Mathf.InverseLerp(brilhoEspera, 1f, brilho);
        matNucleo.color = Color.Lerp(corEspera, corNucleo, t);
        if (voz != null && !voz.acordada) energia = Mathf.Min(energia, 0.2f);

        // energia controla velocidade e força do pulso
        float velocidade = Mathf.Lerp(1f, 6f, energia);
        float forca = Mathf.Lerp(0.03f, 0.35f, energia);
        fase += Time.deltaTime * velocidade * Mathf.PI * 2f;

        float pulso = 1f + Mathf.Sin(fase) * forca;
        nucleo.localScale = Vector3.one * diametro * pulso;

        var cam = Camera.main;
        for (int i = 0; i < auras.Length; i++)
        {
            float defasagem = Mathf.Sin(fase - i * 0.8f);
            auras[i].localScale = Vector3.one * tamanhos[i] * (1f + defasagem * forca);

            var c = corAura;
            c.a = Mathf.Clamp01(opacidades[i] * Mathf.Lerp(0.35f, 1f, t) * Mathf.Lerp(0.5f, 1f, energia) * (1f + defasagem * forca));
            matAuras[i].color = c;

            // sempre virado para a câmera
            if (cam != null)
                auras[i].rotation = Quaternion.LookRotation(auras[i].position - cam.transform.position);
        }
    }

    void OnDestroy()
    {
        if (texturaRadial != null) Destroy(texturaRadial);
    }
}
