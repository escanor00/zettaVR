using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// "Zetta, abra esse modelo": vista explodida do holograma olhado (ou do id dado). As peças se afastam do centro
// em ~0,6 s; "feche" junta de novo. Funciona com modelos prontos, formas e .glb que tenham mais de uma peça.
public class VistaExplodida : MonoBehaviour
{
    public float fator = 0.7f;      // afastamento = fator x maior dimensão do objeto
    public float duracao = 0.6f;

    const string Decl = @"{
  ""name"": ""vista_explodida"",
  ""description"": ""Abre (separa as peças, vista explodida) ou fecha (junta de novo) o holograma que o usuário está olhando ou apontando, ou o de um id informado."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""abrir"", ""fechar""] },
      ""id"": { ""type"": ""INTEGER"", ""description"": ""Id do objeto (opcional; sem ele usa o que o usuário olha)."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao = "Para 'abra esse modelo', 'separe as peças' ou 'vista explodida' use vista_explodida com acao=abrir; para 'feche', 'junte' ou 'monte de novo' use acao=fechar.";

    class Estado
    {
        public bool explodido;
        public readonly Dictionary<Transform, Vector3> original = new Dictionary<Transform, Vector3>();
        public readonly List<Transform> partes = new List<Transform>();
        public Coroutine anim;
    }
    readonly Dictionary<GameObject, Estado> estados = new Dictionary<GameObject, Estado>();

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    public JObject Executar(JObject args)
    {
        string acao = (args.Value<string>("acao") ?? "abrir").ToLowerInvariant();
        HoloItem item = null;
        int? id = args.Value<int?>("id");
        if (id.HasValue) item = HoloRegistro.BuscarPorId(id.Value);
        if (item == null) item = HoloAlvo.ObjetoOlhado();
        if (item == null || item.go == null) return new JObject { ["erro"] = "não sei qual objeto; peça para o usuário olhar ou apontar para ele" };

        if (!estados.TryGetValue(item.go, out var e)) { e = new Estado(); estados[item.go] = e; }
        bool abrir = acao.StartsWith("a") || acao == "explodir";
        if (abrir == e.explodido) return new JObject { ["result"] = abrir ? "já está aberto" : "já está montado" };

        if (abrir)
        {
            e.partes.Clear(); e.original.Clear();
            Partes(item.go.transform, e.partes);
            if (e.partes.Count < 2) return new JObject { ["erro"] = "esse objeto não tem peças separadas para abrir" };
            foreach (var p in e.partes) e.original[p] = p.localPosition;
        }
        e.explodido = abrir;
        if (e.anim != null) StopCoroutine(e.anim);
        e.anim = StartCoroutine(Animar(item.go, e, abrir));
        SonsZetta.Tocar(abrir ? SonsZetta.Abrir : SonsZetta.Fechar);
        return new JObject { ["result"] = abrir ? "vista explodida aberta" : "modelo montado de novo" };
    }

    // filhos com algo para ver; se houver só um filho (embrulho de .glb), desce um nível
    static void Partes(Transform raiz, List<Transform> saida)
    {
        var filhos = new List<Transform>();
        foreach (Transform c in raiz)
        {
            if (!c.gameObject.activeSelf) continue;
            bool visivel = false;
            foreach (var r in c.GetComponentsInChildren<Renderer>()) if (!(r is LineRenderer)) { visivel = true; break; }
            if (visivel) filhos.Add(c);
        }
        if (filhos.Count == 1) { Partes(filhos[0], saida); return; }
        saida.AddRange(filhos);
    }

    IEnumerator Animar(GameObject go, Estado e, bool abrir)
    {
        var centro = HoloAlvo.CentroDoObjeto(go);
        float maior = InteracaoMaos.MaiorDimensao(go);
        var alvo = new Dictionary<Transform, Vector3>();
        int i = 0;
        foreach (var p in e.partes)
        {
            if (p == null) continue;
            var dir = HoloAlvo.CentroDoObjeto(p.gameObject) - centro;
            if (dir.sqrMagnitude < 0.0001f)
            {
                // peça no centro: direção espalhada pela sequência áurea
                float a = i * 2.39996f;
                dir = new Vector3(Mathf.Cos(a), 0.4f, Mathf.Sin(a));
            }
            dir.Normalize();
            var destinoMundo = p.parent.TransformPoint(e.original[p]) + dir * maior * fator;
            alvo[p] = abrir ? p.parent.InverseTransformPoint(destinoMundo) : e.original[p];
            i++;
        }
        var inicio = new Dictionary<Transform, Vector3>();
        foreach (var kv in alvo) if (kv.Key != null) inicio[kv.Key] = kv.Key.localPosition;
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = Mathf.SmoothStep(0f, 1f, t / duracao);
            foreach (var kv in alvo) if (kv.Key != null) kv.Key.localPosition = Vector3.Lerp(inicio[kv.Key], kv.Value, x);
            yield return null;
        }
        foreach (var kv in alvo) if (kv.Key != null) kv.Key.localPosition = kv.Value;
        e.anim = null;
    }
}
