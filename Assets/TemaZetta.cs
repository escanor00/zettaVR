using TMPro;
using UnityEngine;

// Identidade visual da Zetta (redesign Direção B): cores, materiais e medidas num lugar só.
// Os scripts de painéis pedem tudo daqui; nenhum valor visual fica espalhado por eles.
public static class TemaZetta
{
    // paleta oficial
    public static readonly Color AzulTec = Hex(0x42C8FF);
    public static readonly Color AzulProfundo = Hex(0x102C46);
    public static readonly Color Ciano = Hex(0x8BEAFF);
    public static readonly Color Dourado = Hex(0xE8B95E);
    public static readonly Color Branco = Hex(0xEAF6FF);
    public static readonly Color Roxo = Hex(0x947BFF);
    public static readonly Color Erro = new Color(1f, 0.42f, 0.36f, 1f);
    public static readonly Color Ok = new Color(0.4f, 1f, 0.6f, 1f);

    // vidro: translucidez + gradiente (topo mais claro)
    public static readonly Color VidroTopo = new Color(0.10f, 0.30f, 0.45f, 0.30f);
    public static readonly Color VidroBase = new Color(0.04f, 0.14f, 0.26f, 0.16f);

    // medidas
    public const float Raio = 0.012f;
    public const float EspessuraBorda = 0.0018f;
    public const float ProfundidadeTexto = -0.002f;

    // tempos de animação (s)
    public const float TempoEntrada = 0.45f;
    public const float TempoSaida = 0.3f;
    public const float RecolherCentral = 15f;

    static Shader shaderVidro;
    static TMP_FontAsset fonte;
    static bool fonteProcurada;

    public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    public static Color ComAlfa(Color c, float a) { c.a = a; return c; }

    // fonte importada (Rajdhani, OFL); null = usa a padrão do TMP
    public static TMP_FontAsset Fonte
    {
        get
        {
            if (!fonteProcurada) { fonte = Resources.Load<TMP_FontAsset>("ZettaFonte SDF"); fonteProcurada = true; }
            return fonte;
        }
    }

    public static Material NovoVidro(Vector2 tamanho, float raio = Raio)
    {
        if (shaderVidro == null) shaderVidro = Resources.Load<Shader>("VidroHolo");
        if (shaderVidro == null) shaderVidro = Shader.Find("Sprites/Default");
        var m = new Material(shaderVidro);
        m.SetColor("_CorTopo", VidroTopo);
        m.SetColor("_CorBase", VidroBase);
        m.SetColor("_CorBorda", AzulTec);
        m.SetVector("_Tamanho", new Vector4(tamanho.x, tamanho.y, 0, 0));
        m.SetFloat("_Raio", Mathf.Min(raio, Mathf.Min(tamanho.x, tamanho.y) * 0.45f));
        m.SetFloat("_Espessura", EspessuraBorda);
        return m;
    }

    // ease out back (leve overshoot)
    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}

// Controle visual de um painel de vidro: cor da borda, brilho e realce, sem recriar materiais.
public class PainelVidro : MonoBehaviour
{
    public Material material;
    public Vector2 tamanho;

    public void DefinirBorda(Color cor, float brilho = 1f)
    {
        if (material == null) return;
        material.SetColor("_CorBorda", cor);
        material.SetFloat("_Brilho", brilho);
    }

    public void DefinirRealce(float r) { if (material != null) material.SetFloat("_Realce", r); }
}
