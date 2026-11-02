using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Painéis holográficos finos em arco à frente do usuário: relógio, data, conexão e assistente.
public class PaineisHolograficos : MonoBehaviour
{
    public float distancia = 1.2f;
    public float[] angulos = { -58f, -30f, 30f, 58f };
    public Vector2 tamanho = new Vector2(0.3f, 0.17f);
    public Color corContorno = new Color(0.2f, 0.95f, 1f, 1f);
    public Color corFundo = new Color(0.02f, 0.12f, 0.16f, 0.12f);
    public float alturaExtra = 0.22f;   // acima da linha do olhar, para não atrapalhar a visão da sala
    public float atrasoInicial = 1.5f;
    public float intervaloEntrada = 0.25f;

    class Painel
    {
        public Transform raiz;
        public TextMeshPro valor;
        public Func<string> conteudo;
    }

    Painel[] paineis;
    AssistenteVoz voz;
    PassthroughMR pass;
    GeradorModelo3D gerador3d;
    float proximaAtualizacao;

    // barrinhas do volume que o microfone está captando (0..1)
    static string Barras(float nivel)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(nivel * 8f), 0, 8);
        return new string('|', n) + new string('.', 8 - n);
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(atrasoInicial);
        voz = FindAnyObjectByType<AssistenteVoz>();
        pass = FindAnyObjectByType<PassthroughMR>();
        gerador3d = FindAnyObjectByType<GeradorModelo3D>();

        var cam = Camera.main.transform;
        // direção horizontal em que o usuário olha agora
        var frente = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;

        var definicoes = new (string titulo, Func<string> texto)[]
        {
            ("HORA", () => DateTime.Now.ToString("HH:mm:ss")),
            ("DATA", () => DateTime.Now.ToString("dd/MM/yyyy") + "\n" + DateTime.Now.ToString("dddd")),
            ("CONEXÃO", () => Application.internetReachability == NetworkReachability.NotReachable
                ? "SEM REDE" : "REDE OK\nGemini: " + (voz != null ? voz.status : "-")
                    + (voz != null && !string.IsNullOrEmpty(voz.ultimoErro) ? "\n" + voz.ultimoErro : "")
                    + (pass != null ? "\n" + pass.estado : "")
                    + (gerador3d != null && !string.IsNullOrEmpty(gerador3d.ultimaFalha) ? "\n3D: " + gerador3d.ultimaFalha : "")),
            ("ASSISTENTE", () => voz != null
                ? "Mic: " + voz.estadoMic + "\n" + Barras(voz.nivelMic)
                    + (string.IsNullOrEmpty(voz.ultimoOuvido) ? "" : "\nOuvi: " + voz.ultimoOuvido) : "-")
        };

        paineis = new Painel[Mathf.Min(angulos.Length, definicoes.Length)];
        for (int i = 0; i < paineis.Length; i++)
        {
            var dir = Quaternion.AngleAxis(angulos[i], Vector3.up) * frente;
            var pos = cam.position + dir * distancia + Vector3.up * alturaExtra;
            paineis[i] = CriarPainel(definicoes[i].titulo, pos, cam.position);
            paineis[i].conteudo = definicoes[i].texto;
        }

        for (int i = 0; i < paineis.Length; i++)
        {
            StartCoroutine(Surgir(paineis[i].raiz, i * intervaloEntrada));
        }
    }

    Painel CriarPainel(string titulo, Vector3 pos, Vector3 olhandoPara)
    {
        var raiz = new GameObject("Painel_" + titulo).transform;
        raiz.SetParent(transform, false);
        raiz.position = pos;
        var dirOlhar = Vector3.ProjectOnPlane(pos - olhandoPara, Vector3.up);
        raiz.rotation = Quaternion.LookRotation(dirOlhar, Vector3.up);
        raiz.localScale = Vector3.zero;

        var mat = new Material(Shader.Find("Sprites/Default"));

        // fundo quase transparente
        var fundo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fundo.name = "Fundo";
        Destroy(fundo.GetComponent<Collider>());
        fundo.transform.SetParent(raiz, false);
        fundo.transform.localScale = new Vector3(tamanho.x, tamanho.y, 1f);
        var matFundo = new Material(mat);
        matFundo.color = corFundo;
        fundo.GetComponent<Renderer>().material = matFundo;

        // contorno ciano brilhante
        var contorno = new GameObject("Contorno");
        contorno.transform.SetParent(raiz, false);
        contorno.transform.localPosition = new Vector3(0, 0, -0.001f);
        var lr = contorno.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.material = mat;
        lr.startColor = lr.endColor = corContorno;
        lr.widthMultiplier = 0.003f;
        float hx = tamanho.x * 0.5f, hy = tamanho.y * 0.5f;
        lr.positionCount = 4;
        lr.SetPositions(new[]
        {
            new Vector3(-hx, -hy, 0), new Vector3(-hx, hy, 0),
            new Vector3(hx, hy, 0), new Vector3(hx, -hy, 0)
        });

        // título e valor
        CriarTexto(raiz, "Titulo", titulo, new Vector3(0, hy - 0.025f, -0.002f),
            new Vector2(tamanho.x - 0.02f, 0.035f), 0.08f, corContorno);
        var valor = CriarTexto(raiz, "Valor", "", new Vector3(0, -0.01f, -0.002f),
            new Vector2(tamanho.x - 0.03f, tamanho.y - 0.07f), 0.2f, Color.white);

        return new Painel { raiz = raiz, valor = valor };
    }

    TextMeshPro CriarTexto(Transform pai, string nome, string conteudo, Vector3 posLocal,
        Vector2 caixa, float tamanhoMax, Color cor)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = caixa;
        t.alignment = TextAlignmentOptions.Center;
        t.color = cor;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.03f;
        t.fontSizeMax = tamanhoMax;
        t.text = conteudo;
        return t;
    }

    IEnumerator Surgir(Transform alvo, float atraso)
    {
        yield return new WaitForSeconds(atraso);
        const float duracao = 0.6f;
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = t / duracao;
            // ease out back (leve overshoot)
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            // abre primeiro na horizontal, depois na vertical
            alvo.localScale = new Vector3(e, Mathf.Clamp01(x * 2f) * e, 1f);
            yield return null;
        }
        alvo.localScale = Vector3.one;
    }

    void Update()
    {
        if (paineis == null || Time.time < proximaAtualizacao) return;
        proximaAtualizacao = Time.time + 0.25f;
        foreach (var p in paineis)
            if (p != null && p.valor != null) p.valor.text = p.conteudo();
    }
}
