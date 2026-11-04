using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// Modo desenho: traços de luz ciano saem da ponta do indicador enquanto a mão faz pinça.
public class DesenhoNoAr : MonoBehaviour
{
    public bool ativo;
    public float larguraTraco = 0.007f;
    public float distanciaPontos = 0.006f;
    public float pinçaInicio = 0.022f;   // polegar-indicador para começar
    public float pinçaFim = 0.034f;      // para terminar (histerese)
    public int maxTracos = 200;

    class Mao
    {
        public Handedness lado;
        public bool pinçando;
        public LineRenderer linha, brilho;
        public readonly List<Vector3> pontos = new List<Vector3>();
        public Transform cursor;
    }

    readonly Mao[] maos = { new Mao { lado = Handedness.Right }, new Mao { lado = Handedness.Left } };
    readonly List<GameObject> tracos = new List<GameObject>();
    XRHandSubsystem sub;
    XROrigin origem;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    Material matLinha, matBrilho;

    const string Decl = @"{
  ""name"": ""controlar_desenho"",
  ""description"": ""Controla o modo desenho no ar (traços de luz com o dedo indicador durante a pinça)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""ligar"", ""apagar"", ""sair""], ""description"": ""ligar = entrar no modo desenho; apagar = limpar todos os traços; sair = desligar o modo desenho."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'modo desenho' use controlar_desenho com acao=ligar; 'apagar desenho' acao=apagar; 'sair do modo desenho' acao=sair. Confirme em poucas palavras.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Controlar, Instrucao);
        matLinha = HoloUtil.NovoMaterialSprite(new Color(0.3f, 1f, 1f, 1f));
        matBrilho = HoloUtil.NovoMaterialSprite(new Color(0.1f, 0.8f, 1f, 0.22f));
    }

    public JObject Controlar(JObject args)
    {
        switch ((args.Value<string>("acao") ?? "").ToLowerInvariant())
        {
            case "ligar": Ativar(true); return new JObject { ["result"] = "modo desenho ligado; faça pinça para desenhar" };
            case "sair": Ativar(false); return new JObject { ["result"] = "modo desenho desligado" };
            case "apagar": Apagar(); return new JObject { ["result"] = "desenho apagado" };
        }
        return new JObject { ["erro"] = "acao inválida" };
    }

    public void Ativar(bool v)
    {
        ativo = v;
        if (!v)
            foreach (var m in maos)
            {
                Finalizar(m);
                if (m.cursor != null) m.cursor.gameObject.SetActive(false);
            }
    }

    // editar_objeto em desenhos: cor (todos os traços e os próximos), mover e escalar o conjunto
    public void Tingir(Color c)
    {
        matLinha.color = new Color(c.r, c.g, c.b, 1f);
        matBrilho.color = new Color(c.r, c.g, c.b, 0.22f);
    }

    public Color CorAtual => matLinha != null ? matLinha.color : Color.cyan;

    public void Transformar(Vector3 deslocamento, float fator)
    {
        tracos.RemoveAll(t => t == null);
        var soma = Vector3.zero; int n = 0;
        var linhas = new List<LineRenderer>();
        foreach (var t in tracos)
            foreach (var lr in t.GetComponentsInChildren<LineRenderer>())
            {
                linhas.Add(lr);
                if (lr.name != "Nucleo") continue;
                var pts = new Vector3[lr.positionCount]; lr.GetPositions(pts);
                foreach (var p in pts) { soma += p; n++; }
            }
        if (n == 0) return;
        var centro = soma / n;
        foreach (var lr in linhas)
        {
            var pts = new Vector3[lr.positionCount]; lr.GetPositions(pts);
            for (int i = 0; i < pts.Length; i++) pts[i] = centro + (pts[i] - centro) * fator + deslocamento;
            lr.SetPositions(pts);
            lr.widthMultiplier *= fator;
        }
    }

    public void Apagar()
    {
        foreach (var m in maos) Finalizar(m);
        foreach (var t in tracos) if (t != null) Destroy(t);
        tracos.Clear();
    }

    void Update()
    {
        if (!ativo) return;
        if (!ObterSubsistema()) return;
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        if (origem == null) return;

        foreach (var m in maos)
        {
            var mao = m.lado == Handedness.Right ? sub.rightHand : sub.leftHand;
            if (!mao.isTracked) { Finalizar(m); EsconderCursor(m); continue; }
            if (!Ponta(mao, XRHandJointID.IndexTip, out var indicador) || !Ponta(mao, XRHandJointID.ThumbTip, out var polegar))
            { Finalizar(m); continue; }

            MoverCursor(m, indicador);
            float d = Vector3.Distance(indicador, polegar);
            if (!m.pinçando && d < pinçaInicio) Iniciar(m, indicador);
            else if (m.pinçando && d > pinçaFim) Finalizar(m);
            else if (m.pinçando) Acrescentar(m, indicador);
        }
    }

    bool ObterSubsistema()
    {
        if (sub != null && sub.running) return true;
        SubsystemManager.GetSubsystems(subs);
        sub = subs.Count > 0 ? subs[0] : null;
        return sub != null && sub.running;
    }

    bool Ponta(XRHand mao, XRHandJointID id, out Vector3 mundo)
    {
        mundo = default;
        var j = mao.GetJoint(id);
        if (!j.TryGetPose(out var pose)) return false;
        mundo = origem.Origin.transform.TransformPoint(pose.position);
        return true;
    }

    // ---- traços ----
    void Iniciar(Mao m, Vector3 p)
    {
        m.pinçando = true;
        m.pontos.Clear();
        var go = new GameObject("Traco");
        go.transform.SetParent(transform, true);
        m.linha = NovaLinha(go.transform, "Nucleo", matLinha, larguraTraco);
        m.brilho = NovaLinha(go.transform, "Brilho", matBrilho, larguraTraco * 3.5f);
        tracos.Add(go);
        while (tracos.Count > maxTracos) { if (tracos[0] != null) Destroy(tracos[0]); tracos.RemoveAt(0); }
        Acrescentar(m, p, true);
    }

    LineRenderer NovaLinha(Transform pai, string nome, Material mat, float largura)
    {
        var o = new GameObject(nome);
        o.transform.SetParent(pai, false);
        var lr = o.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.material = mat;
        lr.widthMultiplier = largura;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 3;
        lr.positionCount = 0;
        return lr;
    }

    void Acrescentar(Mao m, Vector3 p, bool forcar = false)
    {
        if (m.linha == null) return;
        if (!forcar && m.pontos.Count > 0 && Vector3.Distance(m.pontos[m.pontos.Count - 1], p) < distanciaPontos) return;
        m.pontos.Add(p);
        m.linha.positionCount = m.brilho.positionCount = m.pontos.Count;
        m.linha.SetPosition(m.pontos.Count - 1, p);
        m.brilho.SetPosition(m.pontos.Count - 1, p);
    }

    void Finalizar(Mao m)
    {
        m.pinçando = false;
        m.linha = m.brilho = null;
        m.pontos.Clear();
    }

    // ---- cursor na ponta do indicador ----
    void MoverCursor(Mao m, Vector3 p)
    {
        if (m.cursor == null)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "CursorDesenho_" + m.lado;
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(transform, false);
            s.transform.localScale = Vector3.one * 0.012f;
            s.GetComponent<Renderer>().material = HoloUtil.NovoMaterialSprite(new Color(0.4f, 1f, 1f, 0.9f));
            m.cursor = s.transform;
        }
        m.cursor.gameObject.SetActive(true);
        m.cursor.position = p;
    }

    void EsconderCursor(Mao m)
    {
        if (m.cursor != null) m.cursor.gameObject.SetActive(false);
    }

    // Para testes sem óculos: desenha um traço por uma lista de pontos.
    public void SimularTraco(Vector3[] pontos)
    {
        var m = maos[0];
        Iniciar(m, pontos[0]);
        for (int i = 1; i < pontos.Length; i++) Acrescentar(m, pontos[i], true);
        Finalizar(m);
    }

    public int QuantidadeTracos { get { tracos.RemoveAll(t => t == null); return tracos.Count; } }
}
