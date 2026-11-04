using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class Girar : MonoBehaviour
{
    public Vector3 eixo = Vector3.up;
    public float grausPorSegundo = 20f;
    public float inicioEm;
    void Update()
    {
        if (Time.time < inicioEm) return;
        transform.Rotate(eixo, grausPorSegundo * Time.deltaTime, Space.Self);
    }
}

// Faz a peça voar do ponto de partida (local) até o lugar final, depois de um atraso.
public class VooParaLugar : MonoBehaviour
{
    public IEnumerator Voar(Vector3 inicio, Vector3 fim, float atraso, float duracao)
    {
        transform.localPosition = inicio;
        var m = GetComponent<HologramaMontagem>();
        if (atraso > 0f) yield return new WaitForSeconds(atraso);
        if (m != null) m.Reiniciar(); // a grade/preenchimento só começam quando a peça parte
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = t / duracao;
            float e = 1f - Mathf.Pow(1f - x, 3f); // ease-out
            transform.localPosition = Vector3.Lerp(inicio, fim, e);
            yield return null;
        }
        transform.localPosition = fim;
    }
}

// Ferramenta "mostrar_modelo": sistema solar, molécula de água e globo terrestre, com formas básicas.
public class ModelosProntos : MonoBehaviour
{
    public Vector3 posicaoVista = new Vector3(0f, 0.05f, 0.9f);
    public int maxModelos = 3;

    const string Decl = @"{
  ""name"": ""mostrar_modelo"",
  ""description"": ""Mostra um modelo holográfico pronto que se monta peça por peça: sistema solar com planetas orbitando, molécula de água ou globo terrestre girando."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""modelo"": { ""type"": ""STRING"", ""enum"": [""sistema_solar"", ""molecula_agua"", ""globo_terrestre""] }
    },
    ""required"": [""modelo""]
  }
}";
    const string Instrucao = "Para mostrar sistema solar, molécula de água ou globo terrestre use mostrar_modelo e comente em uma frase curta.";

    readonly System.Collections.Generic.List<GameObject> modelos = new System.Collections.Generic.List<GameObject>();

    void Awake()
    {
        AncorasHolo.Fabricas["modelo"] = (item, pos, rot) =>
        {
            Mostrar(new JObject { ["modelo"] = item.extra }, pos, rot, item.estado);
            return HoloRegistro.Itens.Count > 0 ? HoloRegistro.Itens[HoloRegistro.Itens.Count - 1].go : null;
        };
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, a => Mostrar(a), Instrucao);
    }

    public JObject Mostrar(JObject args, Vector3? posMundo = null, Quaternion? rotMundo = null, string estadoSalvo = null)
    {
        string nome = (args.Value<string>("modelo") ?? "").ToLowerInvariant();
        var raiz = new GameObject("Modelo_" + nome);
        raiz.transform.SetParent(transform, true);
        var pos = posMundo ?? HoloUtil.PontoNaVista(posicaoVista);
        raiz.transform.position = pos;
        raiz.transform.rotation = rotMundo ?? Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);

        switch (nome)
        {
            case "sistema_solar": SistemaSolar(raiz.transform); break;
            case "molecula_agua": MoleculaAgua(raiz.transform); break;
            case "globo_terrestre": Globo(raiz.transform); break;
            default:
                Destroy(raiz);
                return new JObject { ["erro"] = "modelo inválido; use sistema_solar, molecula_agua ou globo_terrestre" };
        }

        // pegar com as mãos
        var rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 3f;
        rb.angularDamping = 3f;
        raiz.AddComponent<ColisorPegada>();   // caixa pelos bounds visíveis (antes do XRGrabInteractable)
        raiz.AddComponent<XRGrabInteractable>();
        InteracaoMaos.Aplicar(raiz);

        HoloRegistro.Adicionar(raiz, nome, "", nome.Replace('_', ' '), "modelo", 0.5f, nome);
        raiz.AddComponent<ObjetoInterativo>().Descobrir();   // cada peça nomeada (Sol, planetas, Oxigenio...) vira uma parte
        if (!string.IsNullOrEmpty(estadoSalvo)) raiz.GetComponent<ObjetoInterativo>().AplicarEstado(estadoSalvo);
        modelos.Add(raiz);
        while (modelos.Count > maxModelos)
        {
            if (modelos[0] != null) { HoloRegistro.Remover(modelos[0]); Destroy(modelos[0]); }
            modelos.RemoveAt(0);
        }
        return new JObject { ["result"] = "modelo " + nome + " criado" };
    }

    // cria uma parte holográfica filha de 'pai', que voa de 'inicio' até 'fim'
    GameObject Parte(Transform pai, PrimitiveType tipo, string nome, Vector3 fim, Vector3 escala, Color cor,
        Vector2 tiling, float atraso, float voo = 0.5f, Texture2D tex = null, float alfa = 0.3f)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        go.transform.SetParent(pai, false);
        go.transform.localScale = escala;
        go.transform.localPosition = fim;
        CriadorHolograma.AplicarVisual(go, cor, tiling, 0.6f, tex, alfa);
        // parte em cima do pai que já gira? o voo é no espaço local do pai
        var dir = Random.onUnitSphere;
        var inicio = fim + dir * 0.45f;
        StartCoroutine(go.AddComponent<VooParaLugar>().Voar(inicio, fim, atraso, voo));
        return go;
    }

    GameObject Anel(Transform pai, float raio, Color cor, float atraso, float largura = 0.0015f)
    {
        var go = new GameObject("Orbita");
        go.transform.SetParent(pai, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = largura;
        lr.material = HoloUtil.NovoMaterialSprite(new Color(cor.r, cor.g, cor.b, 0f));
        lr.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * raio);
        }
        StartCoroutine(AparecerLinha(lr, cor, atraso));
        return go;
    }

    IEnumerator AparecerLinha(LineRenderer lr, Color cor, float atraso)
    {
        yield return new WaitForSeconds(atraso);
        for (float t = 0; t < 0.6f && lr != null; t += Time.deltaTime)
        {
            var c = new Color(cor.r, cor.g, cor.b, t / 0.6f * 0.55f);
            lr.startColor = lr.endColor = c;
            lr.material.color = Color.white;
            yield return null;
        }
        if (lr != null) lr.startColor = lr.endColor = new Color(cor.r, cor.g, cor.b, 0.55f);
    }

    void SistemaSolar(Transform raiz)
    {
        // nome, raio da órbita, tamanho, cor, período (s para volta completa)
        var planetas = new (string n, float r, float d, Color c, float p)[]
        {
            ("Mercurio", 0.060f, 0.010f, new Color(0.7f, 0.65f, 0.6f), 8f),
            ("Venus",    0.085f, 0.016f, new Color(1f, 0.8f, 0.4f), 12f),
            ("Terra",    0.110f, 0.017f, new Color(0.2f, 0.5f, 1f), 16f),
            ("Marte",    0.135f, 0.013f, new Color(1f, 0.35f, 0.2f), 22f),
            ("Jupiter",  0.175f, 0.044f, new Color(1f, 0.7f, 0.45f), 34f),
            ("Saturno",  0.215f, 0.038f, new Color(1f, 0.9f, 0.55f), 46f),
            ("Urano",    0.250f, 0.024f, new Color(0.5f, 0.95f, 1f), 60f),
            ("Netuno",   0.280f, 0.023f, new Color(0.3f, 0.4f, 1f), 74f)
        };
        float passo = 0.15f; // 9 peças x 0,15 s + 0,5 s de voo ≈ 1,9 s
        Parte(raiz, PrimitiveType.Sphere, "Sol", Vector3.zero, Vector3.one * 0.05f, new Color(1f, 0.85f, 0.2f), new Vector2(16, 8), 0f);

        for (int i = 0; i < planetas.Length; i++)
        {
            var p = planetas[i];
            float atraso = (i + 1) * passo;
            Anel(raiz, p.r, HoloUtil.Ciano, atraso);
            var pivo = new GameObject("Pivo_" + p.n).transform;
            pivo.SetParent(raiz, false);
            var g = pivo.gameObject.AddComponent<Girar>();
            g.grausPorSegundo = 360f / p.p;
            g.inicioEm = Time.time + 2f; // só orbita depois de montado
            var planeta = Parte(pivo, PrimitiveType.Sphere, p.n, new Vector3(p.r, 0, 0), Vector3.one * p.d, p.c, new Vector2(12, 6), atraso);
            if (p.n == "Saturno")
            {
                var aneis = Parte(planeta.transform, PrimitiveType.Cylinder, "AnelSaturno", Vector3.zero, new Vector3(2.1f, 0.03f, 2.1f), new Color(1f, 0.9f, 0.6f), new Vector2(16, 1), atraso, 0.5f, null, 0.18f);
                aneis.GetComponent<Collider>().enabled = false;
            }
            // as peças dentro de pivôs não precisam de colliders individuais
            var col = planeta.GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    void MoleculaAgua(Transform raiz)
    {
        float ang = 104.5f * 0.5f * Mathf.Deg2Rad;
        float L = 0.09f;
        var h1 = new Vector3(Mathf.Sin(ang), -Mathf.Cos(ang), 0f) * L;
        var h2 = new Vector3(-Mathf.Sin(ang), -Mathf.Cos(ang), 0f) * L;

        Parte(raiz, PrimitiveType.Sphere, "Oxigenio", Vector3.zero, Vector3.one * 0.085f, new Color(1f, 0.2f, 0.2f), new Vector2(12, 6), 0f);
        Parte(raiz, PrimitiveType.Sphere, "Hidrogenio1", h1, Vector3.one * 0.05f, Color.white, new Vector2(10, 5), 0.4f);
        Parte(raiz, PrimitiveType.Sphere, "Hidrogenio2", h2, Vector3.one * 0.05f, Color.white, new Vector2(10, 5), 0.8f);
        Ligacao(raiz, h1, 1.2f);
        Ligacao(raiz, h2, 1.5f);

        var g = raiz.gameObject.AddComponent<Girar>();
        g.grausPorSegundo = 25f;
        g.inicioEm = Time.time + 2f;
    }

    void Ligacao(Transform raiz, Vector3 ate, float atraso)
    {
        // cilindro do centro até o hidrogênio (cilindro tem altura 2, então escala Y = comprimento/2)
        var meio = ate * 0.5f;
        var go = Parte(raiz, PrimitiveType.Cylinder, "Ligacao", meio, new Vector3(0.012f, ate.magnitude * 0.5f, 0.012f),
            HoloUtil.Ciano, new Vector2(8, 3), atraso);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, ate.normalized);
        go.GetComponent<Collider>().enabled = false;
    }

    void Globo(Transform raiz)
    {
        var inclinado = new GameObject("EixoInclinado").transform;
        inclinado.SetParent(raiz, false);
        inclinado.localRotation = Quaternion.Euler(0f, 0f, 23.5f);

        // haste do eixo e suporte
        var haste = Parte(inclinado, PrimitiveType.Cylinder, "Eixo", Vector3.zero, new Vector3(0.006f, 0.16f, 0.006f), HoloUtil.Ciano, new Vector2(4, 8), 0f);
        haste.GetComponent<Collider>().enabled = false;
        Anel(inclinado, 0.15f, HoloUtil.Ciano, 0.5f, 0.002f);

        // globo: grade de linhas + preenchimento com continentes gerados por código
        var globo = Parte(inclinado, PrimitiveType.Sphere, "Globo", Vector3.zero, Vector3.one * 0.22f, new Color(0.3f, 0.7f, 1f),
            new Vector2(24, 12), 1f, 0.5f, TexturaTerra(), 0.85f);
        var g = globo.AddComponent<Girar>();
        g.eixo = Vector3.up;
        g.grausPorSegundo = 30f;
        g.inicioEm = Time.time + 2f;
        globo.GetComponent<Collider>().enabled = false;
    }

    Texture2D TexturaTerra()
    {
        const int w = 256, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float lon = x / (float)w * Mathf.PI * 2f, lat = (y / (float)h - 0.5f) * Mathf.PI;
                var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                float n = (Mathf.PerlinNoise(p.x * 1.6f + 10f, p.y * 1.6f) + Mathf.PerlinNoise(p.y * 1.6f + 20f, p.z * 1.6f)
                    + Mathf.PerlinNoise(p.z * 1.6f + 30f, p.x * 1.6f)) / 3f;
                bool terra = n > 0.52f;
                bool gelo = Mathf.Abs(lat) > 1.25f;
                tex.SetPixel(x, y, gelo ? new Color(0.9f, 0.97f, 1f, 1f)
                    : terra ? new Color(0.25f, 0.9f, 0.4f, 1f) : new Color(0.1f, 0.4f, 0.95f, 0.75f));
            }
        tex.Apply(true);
        return tex;
    }
}
