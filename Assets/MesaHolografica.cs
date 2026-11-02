using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Plataforma circular holográfica na mesa real (ou a 1 m à frente). Objetos criados com ela ativa nascem do centro, subindo.
public class MesaHolografica : MonoBehaviour
{
    public float raio = 0.25f;
    public float alturaFlutuar = 0.22f;
    public float duracaoSubida = 1.2f;

    GameObject plataforma;
    public bool ativa => plataforma != null;
    public Vector3 Centro => plataforma != null ? plataforma.transform.position : Vector3.zero;

    const string Decl = @"{
  ""name"": ""controlar_mesa"",
  ""description"": ""Ativa ou desativa a mesa holográfica: uma plataforma circular brilhante com anéis girando, na mesa real detectada ou a 1 metro à frente. Objetos criados com ela ativa nascem do centro dela, subindo."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""ativar"", ""desativar""] }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'ativar mesa' use controlar_mesa com acao=ativar e para 'desativar mesa' acao=desativar; confirme em poucas palavras.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Controlar, Instrucao);
        HoloRegistro.aoAdicionar += AoCriarObjeto;
    }

    void OnDestroy() { HoloRegistro.aoAdicionar -= AoCriarObjeto; }

    public JObject Controlar(JObject args)
    {
        switch ((args.Value<string>("acao") ?? "").ToLowerInvariant())
        {
            case "ativar": return new JObject { ["result"] = Ativar() };
            case "desativar": Desativar(); return new JObject { ["result"] = "mesa holográfica desativada" };
        }
        return new JObject { ["erro"] = "acao inválida" };
    }

    public string Ativar()
    {
        if (plataforma != null) Destroy(plataforma);
        Vector3 pos;
        string onde;
        var sala = FindAnyObjectByType<SalaAR>();
        if (sala != null && sala.TentarMesa(out var centro, out var _))
        {
            pos = centro;
            onde = "na mesa real detectada";
        }
        else
        {
            // 1 m à frente, na altura de uma mesa (cerca de 0,75 m acima do chão, mas nunca acima do peito)
            var cam = Camera.main.transform;
            float chao = sala != null ? sala.AlturaChao() : 0f;
            float y = Mathf.Min(chao + 0.75f, cam.position.y - 0.35f);
            pos = HoloUtil.PontoNaVista(new Vector3(0f, 0f, 1f));
            pos.y = y;
            onde = "a 1 metro à sua frente (mesa não detectada)";
        }
        plataforma = Construir(pos);
        return "mesa holográfica ativada " + onde;
    }

    public void Desativar()
    {
        if (plataforma == null) return;
        StartCoroutine(Sumir(plataforma));
        plataforma = null;
    }

    GameObject Construir(Vector3 pos)
    {
        var raizGo = new GameObject("MesaHolografica");
        raizGo.transform.SetParent(transform, true);
        raizGo.transform.position = pos;
        raizGo.transform.rotation = HoloUtil.OlharParaUsuario(pos);

        // disco brilhante
        var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "Disco";
        Destroy(disco.GetComponent<Collider>());
        disco.transform.SetParent(raizGo.transform, false);
        disco.transform.localScale = new Vector3(raio * 2f, 0.004f, raio * 2f);
        CriadorHolograma.AplicarVisual(disco, HoloUtil.Ciano, new Vector2(24, 1), 1f, null, 0.28f);

        // anéis girando em velocidades diferentes
        Anel(raizGo.transform, "AnelA", raio * 1.04f, 0, 360, 0.0035f, 18f);
        Anel(raizGo.transform, "AnelB", raio * 1.20f, 14, 14f, 0.0025f, -35f);
        Anel(raizGo.transform, "AnelC", raio * 1.36f, 3, 80f, 0.005f, 55f);

        StartCoroutine(HoloUtil.Surgir(raizGo.transform, 0f, 0.7f));
        return raizGo;
    }

    // 'arcos' = 0 → círculo completo; senão N arcos de 'grausArco' graus distribuídos
    void Anel(Transform pai, string nome, float r, int arcos, float grausArco, float largura, float velocidade)
    {
        var anel = new GameObject(nome).transform;
        anel.SetParent(pai, false);
        anel.localPosition = new Vector3(0, 0.004f, 0);
        var g = anel.gameObject.AddComponent<Girar>();
        g.eixo = Vector3.up;
        g.grausPorSegundo = velocidade;

        int n = Mathf.Max(1, arcos);
        for (int a = 0; a < n; a++)
        {
            var seg = new GameObject("Arco");
            seg.transform.SetParent(anel, false);
            var lr = seg.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = largura;
            lr.material = HoloUtil.NovoMaterialSprite(Color.white);
            lr.startColor = lr.endColor = new Color(0.3f, 1f, 1f, 0.9f);
            bool completo = arcos == 0;
            float ini = completo ? 0f : a * 360f / n;
            float fim = completo ? 360f : ini + grausArco;
            int pontos = Mathf.Max(8, Mathf.CeilToInt((fim - ini) / 5f)) + 1;
            lr.positionCount = pontos;
            lr.loop = completo;
            if (completo) lr.positionCount = pontos - 1;
            for (int i = 0; i < lr.positionCount; i++)
            {
                float ang = Mathf.Lerp(ini, fim, i / (float)(pontos - 1)) * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r);
            }
        }
    }

    IEnumerator Sumir(GameObject go)
    {
        for (float t = 0; t < 0.5f && go != null; t += Time.deltaTime)
        {
            float k = 1f - t / 0.5f;
            go.transform.localScale = new Vector3(k, k, k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // objetos criados enquanto a mesa está ativa nascem no centro e sobem
    void AoCriarObjeto(GameObject go)
    {
        if (plataforma == null || go == null || HoloRegistro.Carregando) return;
        StartCoroutine(Subir(go));
    }

    IEnumerator Subir(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        bool eraCinematico = rb != null && rb.isKinematic;
        if (rb != null) { rb.isKinematic = true; }
        Vector3 inicio = plataforma.transform.position + Vector3.up * 0.01f;
        Vector3 fim = plataforma.transform.position + Vector3.up * alturaFlutuar;
        go.transform.position = inicio;
        for (float t = 0; t < duracaoSubida && go != null; t += Time.deltaTime)
        {
            float x = t / duracaoSubida;
            go.transform.position = Vector3.Lerp(inicio, fim, 1f - Mathf.Pow(1f - x, 3f));
            yield return null;
        }
        if (go == null) yield break;
        go.transform.position = fim;
        if (rb != null) rb.isKinematic = eraCinematico;
    }
}
