using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

// Ferramentas de voz e gesto do sistema de partes (ObjetoInterativo): pintar, listar/destacar, tirar/mostrar, girar e ações extras.
// "Abra/separe" e "feche/junte" ficam em vista_explodida. Pinçar uma parte destaca e mostra o nome dela.
public class FerramentasPartes : MonoBehaviour
{
    const string DeclPintar = @"{
  ""name"": ""pintar_parte"",
  ""description"": ""Pinta uma parte de um holograma (ex.: a base do cubo, o topo do cilindro, Marte, a fita A do DNA, uma região do modelo 3D). parte='tudo' pinta o objeto inteiro. Também aceita várias partes em 'pares'."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""objeto"": { ""type"": ""STRING"", ""description"": ""Descrição do objeto (ex.: cubo azul). Vazio ou 'isso' = o que o usuário olha/aponta, ou o último criado."" },
      ""parte"": { ""type"": ""STRING"", ""description"": ""Nome da parte: frente, tras, direita, esquerda, topo, base, lateral, lados, fora, dentro, norte, sul, faixa 1..4, nome do planeta, etc."" },
      ""cor"": { ""type"": ""STRING"", ""description"": ""Cor em português, hexadecimal, ou relativa (mais claro, mais escuro)."" },
      ""pares"": { ""type"": ""ARRAY"", ""description"": ""Opcional: várias partes de uma vez, [{parte, cor}]. 'resto' pinta as não citadas."", ""items"": { ""type"": ""OBJECT"", ""properties"": { ""parte"": { ""type"": ""STRING"" }, ""cor"": { ""type"": ""STRING"" } } } }
    }
  }
}";
    const string DeclPartes = @"{
  ""name"": ""partes_objeto"",
  ""description"": ""Lista as partes de um holograma e as faz piscar com o nome flutuando. Com 'parte', destaca só essa parte."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""objeto"": { ""type"": ""STRING"", ""description"": ""Descrição do objeto; vazio = o que o usuário olha."" },
      ""parte"": { ""type"": ""STRING"", ""description"": ""Opcional: parte a destacar."" }
    }
  }
}";
    const string DeclVisib = @"{
  ""name"": ""visibilidade_parte"",
  ""description"": ""Tira (esconde) ou mostra de volta uma parte de um holograma. parte='tudo' mostra todas as partes escondidas."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""objeto"": { ""type"": ""STRING"" },
      ""parte"": { ""type"": ""STRING"" },
      ""acao"": { ""type"": ""STRING"", ""enum"": [""tirar"", ""mostrar""] }
    },
    ""required"": [""parte"", ""acao""]
  }
}";
    const string DeclGirar = @"{
  ""name"": ""girar_objeto"",
  ""description"": ""Faz um holograma girar sozinho devagar, ou para de girar."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""objeto"": { ""type"": ""STRING"" },
      ""acao"": { ""type"": ""STRING"", ""enum"": [""girar"", ""parar""] },
      ""velocidade"": { ""type"": ""NUMBER"", ""description"": ""Graus por segundo (padrão 30)."" }
    },
    ""required"": [""acao""]
  }
}";
    const string DeclAcao = @"{
  ""name"": ""acao_objeto"",
  ""description"": ""Ação extra própria do tipo de objeto. DNA (hélice): 'desenrolar', 'enrolar' e 'voltas' (valor = número de voltas, 0.5 a 8)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""objeto"": { ""type"": ""STRING"" },
      ""acao"": { ""type"": ""STRING"", ""enum"": [""desenrolar"", ""enrolar"", ""voltas""] },
      ""valor"": { ""type"": ""NUMBER"" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao =
        "Todo holograma é feito de partes. 'Pinte a base do cubo de dourado' → pintar_parte; 'quais são as partes disso' / 'mostre a parte X' → partes_objeto; " +
        "'tire a tampa' / 'mostre de volta' → visibilidade_parte; 'gire isso' / 'pare de girar' → girar_objeto; 'abra/separe' e 'feche/junte' → vista_explodida; " +
        "'desenrole o DNA' / 'DNA com 4 voltas' → acao_objeto. Use objeto vazio quando disserem 'isso'. Confirme em poucas palavras.";

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz == null) return;
        voz.RegistrarFerramenta(DeclPintar, Pintar, Instrucao);
        voz.RegistrarFerramenta(DeclPartes, Partes);
        voz.RegistrarFerramenta(DeclVisib, Visibilidade);
        voz.RegistrarFerramenta(DeclGirar, Girar);
        voz.RegistrarFerramenta(DeclAcao, Acao);
    }

    static ObjetoInterativo Obter(JObject a, out HoloItem item, out JObject erro)
    {
        erro = null;
        item = ObjetoInterativo.Alvo(a, out string e);
        if (item == null || item.go == null) { erro = new JObject { ["erro"] = e ?? "objeto não encontrado" }; return null; }
        var oi = ObjetoInterativo.Garantir(item.go);
        if (oi.partes.Count == 0) { erro = new JObject { ["erro"] = "esse objeto ainda não tem partes (talvez ainda esteja carregando)" }; return null; }
        return oi;
    }

    static void Avisar(HoloItem item)
    {
        var anc = FindAnyObjectByType<AncorasHolo>();
        if (anc != null && item != null && item.go != null) anc.MarcarAlterado(item.go);
    }

    public JObject Pintar(JObject a)
    {
        var oi = Obter(a, out var item, out var erro);
        if (oi == null) return erro;
        int n = 0;
        if (a["pares"] is JArray pares && pares.Count > 0)
        {
            var feitas = new HashSet<ParteHolo>();
            string resto = null;
            foreach (var e in pares)
            {
                string parte = e.Value<string>("parte") ?? "";
                string np = ApagadorHolo.Normalizar(parte).Trim();
                string txt = e.Value<string>("cor");
                if (np == "resto" || np == "demais" || np == "outras" || np == "restante") { resto = txt; continue; }
                foreach (var p in oi.Resolver(parte)) { p.Pintar(CriadorHolograma.CorDeEdicao(txt, p.CorVisual())); feitas.Add(p); n++; }
            }
            if (resto != null) foreach (var p in oi.partes) if (!feitas.Contains(p)) { p.Pintar(CriadorHolograma.CorDeEdicao(resto, p.CorVisual())); n++; }
        }
        else
        {
            string parte = a.Value<string>("parte") ?? "tudo";
            n = oi.PintarTexto(parte, a.Value<string>("cor"));
            if (n == 0) return new JObject { ["erro"] = "não achei a parte '" + parte + "'. As partes são: " + string.Join(", ", oi.Nomes()) };
        }
        if (n == 0) return new JObject { ["erro"] = "nenhuma parte pintada; as partes são: " + string.Join(", ", oi.Nomes()) };
        Avisar(item);
        return new JObject { ["result"] = n + " parte(s) pintada(s)" };
    }

    public JObject Partes(JObject a)
    {
        var oi = Obter(a, out var item, out var erro);
        if (oi == null) return erro;
        string parte = a.Value<string>("parte");
        if (!string.IsNullOrWhiteSpace(parte))
        {
            var l = oi.Resolver(parte);
            if (l.Count == 0) return new JObject { ["erro"] = "não achei a parte '" + parte + "'. As partes são: " + string.Join(", ", oi.Nomes()) };
            foreach (var p in l) oi.Destacar(p, 4f);
            return new JObject { ["result"] = "destacando " + l[0].nome };
        }
        foreach (var p in oi.partes) if (!p.oculta) oi.Destacar(p, 3f);
        return new JObject { ["partes"] = new JArray(oi.Nomes()), ["extras"] = new JArray(oi.acoesExtra) };
    }

    public JObject Visibilidade(JObject a)
    {
        var oi = Obter(a, out var item, out var erro);
        if (oi == null) return erro;
        bool tirar = (a.Value<string>("acao") ?? "").ToLowerInvariant().StartsWith("t");
        string parte = a.Value<string>("parte") ?? "";
        int n;
        if (!tirar && ApagadorHolo.Normalizar(parte).Trim() == "tudo") { oi.MostrarTudo(); n = oi.partes.Count; }
        else
        {
            if (oi.explodido && tirar) { /* a pegada da parte some junto com ela */ }
            n = oi.Ocultar(parte, tirar);
        }
        if (n == 0) return new JObject { ["erro"] = "não achei a parte '" + parte + "'. As partes são: " + string.Join(", ", oi.Nomes()) };
        Avisar(item);
        return new JObject { ["result"] = tirar ? "parte retirada" : "parte de volta" };
    }

    public JObject Girar(JObject a)
    {
        var oi = Obter(a, out var item, out var erro);
        if (oi == null) return erro;
        bool ligar = (a.Value<string>("acao") ?? "girar").ToLowerInvariant().StartsWith("g");
        oi.Girar(ligar, a.Value<float?>("velocidade"));
        Avisar(item);
        return new JObject { ["result"] = ligar ? "girando" : "parou de girar" };
    }

    public JObject Acao(JObject a)
    {
        var oi = Obter(a, out var item, out var erro);
        if (oi == null) return erro;
        if (oi.acaoExtra == null) return new JObject { ["erro"] = "esse objeto não tem ações extras" };
        string msg = oi.acaoExtra(a.Value<string>("acao") ?? "", a.Value<float?>("valor") ?? 0f);
        if (msg == null) return new JObject { ["erro"] = "ação desconhecida; este objeto aceita: " + string.Join(", ", oi.acoesExtra) };
        Avisar(item);
        return new JObject { ["result"] = msg };
    }

    // ---------- pinça numa parte: destaca e mostra o nome ----------
    XRHandSubsystem sub;
    XROrigin origem;
    static readonly List<XRHandSubsystem> subs = new List<XRHandSubsystem>();
    readonly bool[] pinca = new bool[2];

    void Update()
    {
        if (sub == null || !sub.running)
        {
            SubsystemManager.GetSubsystems(subs);
            sub = subs.Count > 0 ? subs[0] : null;
            if (sub == null || !sub.running) return;
        }
        if (origem == null) origem = FindAnyObjectByType<XROrigin>();
        if (origem == null) return;
        for (int i = 0; i < 2; i++)
        {
            var mao = i == 0 ? sub.rightHand : sub.leftHand;
            bool agora = false; Vector3 ponto = default;
            if (mao.isTracked && mao.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var a) && mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var b)
                && Vector3.Distance(a.position, b.position) < 0.025f)
            {
                agora = true;
                ponto = origem.Origin.transform.TransformPoint((a.position + b.position) * 0.5f);
            }
            if (agora && !pinca[i]) PinçarParte(ponto);
            pinca[i] = agora;
        }
    }

    void PinçarParte(Vector3 ponto)
    {
        ObjetoInterativo melhorO = null; ParteHolo melhorP = null; float melhor = 0.05f;
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null) continue;
            var oi = it.go.GetComponent<ObjetoInterativo>();
            if (oi == null) continue;
            foreach (var p in oi.partes)
            {
                if (p == null || p.oculta) continue;
                float d = Mathf.Sqrt(p.LimitesMundo().SqrDistance(ponto));
                if (d < melhor) { melhor = d; melhorO = oi; melhorP = p; }
            }
        }
        if (melhorP != null) melhorO.Destacar(melhorP, 3f);
    }
}
