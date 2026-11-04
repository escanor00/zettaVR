using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Uma parte nomeada de um holograma (face do cubo, planeta, fita do DNA, nó de um .glb...).
// Tem malha e renderer próprios; pode ser pintada, escondida, separada e destacada.
public class ParteHolo : MonoBehaviour
{
    public string nome;
    public string[] apelidos = new string[0];
    public int[] origem;            // índices dos vértices na malha original (para animar a malha, ex.: DNA)
    public Vector3 posOriginal;     // posição local quando montada
    public bool oculta;
    public bool pintada;
    public Color cor;
    public bool estiloLinhas;                 // desenhada por linhas (modo estrutura): a grade sólida fica desligada
    public Action<Color> pintorPersonalizado; // partes com vértices coloridos ou várias linhas pintam por aqui

    static readonly HashSet<Material> instancias = new HashSet<Material>();

    public IEnumerable<Renderer> Renderers()
    {
        var r = GetComponent<Renderer>();
        if (r != null && !(r is LineRenderer)) yield return r;
        foreach (var nome in new[] { "Preenchimento", "Linhas", "Nervuras" })
        {
            var f = transform.Find(nome);
            if (f != null) { var fr = f.GetComponent<Renderer>(); if (fr != null) yield return fr; }
        }
    }

    public void Pintar(Color c)
    {
        cor = c; pintada = true;
        if (pintorPersonalizado != null) { pintorPersonalizado(c); return; }
        foreach (var r in Renderers()) AplicarCor(r, c, r.gameObject.name == "Preenchimento");
    }

    // materiais holográficos (Sprites/Default) recebem a cor direto; materiais de .glb são copiados antes (não pintam as outras peças)
    public static void AplicarCor(Renderer r, Color c, bool preenchimento)
    {
        var mats = r.sharedMaterials;
        bool mudou = false;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (m == null) continue;
            if (m.shader != null && m.shader.name == "Sprites/Default")
            {
                float a = m.color.a;
                var n = preenchimento ? c : Color.Lerp(c, Color.white, 0.35f);
                n.a = a;
                m.color = n;
            }
            else
            {
                if (!instancias.Contains(m)) { m = new Material(m); instancias.Add(m); mats[i] = m; mudou = true; }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }
        if (mudou) r.sharedMaterials = mats;
    }

    // cor que se vê agora (a pintada, ou a do material original); base para "mais claro/escuro"
    public Color CorVisual()
    {
        if (pintada) return cor;
        if (pintorPersonalizado != null) return TemaZetta.AzulTec;
        foreach (var r in Renderers())
        {
            var m = r.sharedMaterial;
            if (m == null) continue;
            Color c;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            else continue;
            c.a = 1f;
            return c;
        }
        return TemaZetta.AzulTec;
    }

    public void Ocultar(bool o)
    {
        oculta = o;
        if (pintorPersonalizado != null) { foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = !o; }
        else
        {
            var propria = GetComponent<Renderer>();
            foreach (var r in Renderers()) r.enabled = !o && !(estiloLinhas && r == propria);
        }
        var c = GetComponent<Collider>();
        if (c != null) c.enabled = !o;
    }

    public Bounds LimitesMundo()
    {
        Bounds? b = null;
        foreach (var r in Renderers()) { if (b == null) b = r.bounds; else { var x = b.Value; x.Encapsulate(r.bounds); b = x; } }
        return b ?? new Bounds(transform.position, Vector3.one * 0.02f);
    }

    public bool Chama(string consulta)
    {
        string c = ApagadorHolo.Normalizar(consulta).Trim();
        if (c.Length == 0) return false;
        string n = ApagadorHolo.Normalizar(nome);
        if (n == c) return true;
        foreach (var a in apelidos) if (ApagadorHolo.Normalizar(a) == c) return true;
        return false;
    }
}

// Divide malhas em regiões/faces (sem copiar ou deformar: os vértices ficam no mesmo espaço da malha original).
public static class MalhaPartes
{
    public class Pedaco { public string nome; public Mesh malha; public int[] origem; }

    public static List<Pedaco> Dividir(Mesh src, string[] nomes, Func<Vector3, Vector3, int, int> classe)
    {
        var v = src.vertices; var n = src.normals; var uv = src.uv; var tris = src.triangles;
        bool temN = n != null && n.Length == v.Length, temUv = uv != null && uv.Length == v.Length;
        var porClasse = new List<int>[nomes.Length];
        for (int i = 0; i < nomes.Length; i++) porClasse[i] = new List<int>();
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            var centro = (v[a] + v[b] + v[c]) / 3f;
            var normal = temN ? (n[a] + n[b] + n[c]).normalized : Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized;
            int k = Mathf.Clamp(classe(centro, normal, a), 0, nomes.Length - 1);
            porClasse[k].Add(a); porClasse[k].Add(b); porClasse[k].Add(c);
        }
        var saida = new List<Pedaco>();
        for (int k = 0; k < nomes.Length; k++)
        {
            if (porClasse[k].Count == 0) continue;
            var mapa = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<int>(); var orig = new List<int>();
            foreach (int idx in porClasse[k])
            {
                if (!mapa.TryGetValue(idx, out int novo))
                {
                    novo = nv.Count; mapa[idx] = novo;
                    nv.Add(v[idx]); if (temN) nn.Add(n[idx]); if (temUv) nuv.Add(uv[idx]); orig.Add(idx);
                }
                nt.Add(novo);
            }
            var m = new Mesh { name = src.name + "_" + nomes[k], indexFormat = nv.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(nv);
            if (temN) m.SetNormals(nn);
            if (temUv) m.SetUVs(0, nuv);
            m.SetTriangles(nt, 0);
            if (!temN) m.RecalculateNormals();
            m.RecalculateBounds();
            saida.Add(new Pedaco { nome = nomes[k], malha = m, origem = orig.ToArray() });
        }
        return saida;
    }

    // 3 regiões ao longo do maior eixo da malha (para malhas únicas sem partes naturais)
    public static List<Pedaco> Regioes(Mesh src)
    {
        var b = src.bounds;
        int eixo = b.size.y >= b.size.x && b.size.y >= b.size.z ? 1 : (b.size.x >= b.size.z ? 0 : 2);
        string[] nomes = eixo == 1 ? new[] { "base", "meio", "topo" } : eixo == 0 ? new[] { "esquerda", "centro", "direita" } : new[] { "tras", "centro", "frente" };
        float ini = b.min[eixo], tam = Mathf.Max(0.0001f, b.size[eixo]);
        return Dividir(src, nomes, (c, nrm, i) => Mathf.Min(2, (int)((c[eixo] - ini) / tam * 3f)));
    }
}

// Sistema único de interação por partes: TUDO que a Zetta cria é um ObjetoInterativo feito de ParteHolo.
// Pintar partes, esconder/mostrar, vista explodida, girar, destacar com nome, pegar uma parte separada e estado salvo com a âncora.
public class ObjetoInterativo : MonoBehaviour
{
    public readonly List<ParteHolo> partes = new List<ParteHolo>();
    public bool explodido, girando;
    public float grausPorSegundo = 30f;
    public Dictionary<string, string[]> grupos = new Dictionary<string, string[]>();   // "norte" → nomes de partes
    // ações extras do tipo (ex.: DNA "desenrolar", "voltas"): (acao, valor) → mensagem, ou null se não conhece
    public Func<string, float, string> acaoExtra;
    public string[] acoesExtra = new string[0];
    public Dictionary<string, float> extras = new Dictionary<string, float>();

    string estadoPendente;
    float proximaDescoberta;
    Coroutine animacao;
    XRGrabInteractable grabRaiz;
    static TextMeshPro rotulo;

    public static ObjetoInterativo Garantir(GameObject go)
    {
        var o = go.GetComponent<ObjetoInterativo>();
        if (o == null) { o = go.AddComponent<ObjetoInterativo>(); }
        o.Descobrir();
        return o;
    }

    public void AdicionarParte(ParteHolo p)
    {
        if (partes.Contains(p)) return;
        p.posOriginal = p.transform.localPosition;
        string baseNome = string.IsNullOrWhiteSpace(p.nome) ? p.gameObject.name : p.nome;
        string nome = baseNome; int k = 2;
        while (partes.Exists(x => x.nome == nome)) nome = baseNome + " " + k++;
        p.nome = nome;
        partes.Add(p);
    }

    // partes criadas pelo construtor (ParteHolo nos filhos) ou, para o resto (modelos prontos, .glb), descobertas pelos renderers
    public void Descobrir()
    {
        foreach (var p in GetComponentsInChildren<ParteHolo>(true)) AdicionarParte(p);
        if (partes.Count > 0) return;

        var candidatos = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is TextMeshPro || r.GetComponent<TextMeshPro>() != null) continue;
            if (r.gameObject.name == "Preenchimento" || r.gameObject.name == "EntradaGrade" || r.GetComponentInParent<ExcluirDosLimites>() != null) continue;
            if (r.GetComponent<MeshFilter>() == null && !(r is SkinnedMeshRenderer)) continue;
            candidatos.Add(r);
        }
        if (candidatos.Count == 0) return;

        if (candidatos.Count == 1)
        {
            var r = candidatos[0];
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.vertexCount > 24)
            {
                DividirRenderer(r, mf);
                if (partes.Count > 0) return;
            }
        }
        foreach (var r in candidatos)
        {
            var mf = r.GetComponent<MeshFilter>();
            // renderer com vários materiais (submalhas): uma parte por submalha, quando a malha é legível
            if (candidatos.Count <= 4 && mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.subMeshCount > 1)
            {
                DividirSubmalhas(r, mf);
                if (partes.Count > 0) continue;
            }
            var p = r.GetComponent<ParteHolo>() ?? r.gameObject.AddComponent<ParteHolo>();
            p.nome = LimparNome(r.gameObject.name);
            AdicionarParte(p);
        }
    }

    static string LimparNome(string n)
    {
        n = n.Replace("(Clone)", "").Replace('_', ' ').Trim();
        return n.Length == 0 ? "parte" : n;
    }

    void DividirRenderer(Renderer r, MeshFilter mf)
    {
        var pedacos = MalhaPartes.Regioes(mf.sharedMesh);
        if (pedacos.Count < 2) return;
        CriarFilhosDePedacos(r, pedacos);
    }

    void DividirSubmalhas(Renderer r, MeshFilter mf)
    {
        var src = mf.sharedMesh;
        var mats = r.sharedMaterials;
        var pedacos = new List<MalhaPartes.Pedaco>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tris = src.GetTriangles(s);
            if (tris.Length == 0) continue;
            var m = new Mesh { name = src.name + "_sub" + s, indexFormat = src.indexFormat };
            m.vertices = src.vertices; m.normals = src.normals; m.uv = src.uv; m.colors = src.colors;
            m.triangles = tris;
            m.RecalculateBounds();
            string nome = s < mats.Length && mats[s] != null ? LimparNome(mats[s].name) : "material " + (s + 1);
            pedacos.Add(new MalhaPartes.Pedaco { nome = nome, malha = m });
        }
        if (pedacos.Count >= 2) CriarFilhosDePedacos(r, pedacos, true);
    }

    // troca o renderer único por filhos (um por pedaço), com o mesmo material (copiado por parte quando for pintado)
    void CriarFilhosDePedacos(Renderer r, List<MalhaPartes.Pedaco> pedacos, bool porSubmalha = false)
    {
        var mats = r.sharedMaterials;
        for (int i = 0; i < pedacos.Count; i++)
        {
            var go = new GameObject(pedacos[i].nome);
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = pedacos[i].malha;
            var nr = go.AddComponent<MeshRenderer>();
            nr.sharedMaterial = porSubmalha && i < mats.Length ? mats[i] : (mats.Length > 0 ? mats[0] : null);
            var p = go.AddComponent<ParteHolo>();
            p.nome = pedacos[i].nome;
            AdicionarParte(p);
        }
        r.enabled = false;
        var colisorOriginal = r.GetComponent<Collider>();
        if (colisorOriginal != null && !(colisorOriginal is BoxCollider)) colisorOriginal.enabled = false;
    }

    void Update()
    {
        if (estadoPendente != null && Time.time >= proximaDescoberta)
        {
            proximaDescoberta = Time.time + 0.5f;
            if (partes.Count == 0) Descobrir();
            if (partes.Count > 0) { var e = estadoPendente; estadoPendente = null; AplicarEstado(e); }
        }
        if (girando)
        {
            if (grabRaiz == null) grabRaiz = GetComponent<XRGrabInteractable>();
            if (grabRaiz == null || !grabRaiz.isSelected) transform.Rotate(Vector3.up, grausPorSegundo * Time.deltaTime, Space.World);
        }
    }

    // ---------- busca ----------
    public List<ParteHolo> Resolver(string consulta)
    {
        var achadas = new List<ParteHolo>();
        string c = ApagadorHolo.Normalizar(consulta ?? "").Trim();
        if (c.Length == 0) return achadas;
        if (c == "tudo" || c == "todas" || c == "todos" || c == "objeto") { achadas.AddRange(partes); return achadas; }
        foreach (var p in partes) if (p.Chama(c)) achadas.Add(p);
        if (achadas.Count > 0) return achadas;
        // grupos ("norte" → faixas), do apelido mais longo para o mais curto
        var chaves = new List<string>(grupos.Keys);
        chaves.Sort((a, b) => b.Length.CompareTo(a.Length));
        foreach (var k in chaves)
        {
            if (!c.Contains(ApagadorHolo.Normalizar(k))) continue;
            foreach (var nome in grupos[k]) { var p = partes.Find(x => x.nome == nome); if (p != null && !achadas.Contains(p)) achadas.Add(p); }
            if (achadas.Count > 0) return achadas;
        }
        foreach (var p in partes)
        {
            string n = ApagadorHolo.Normalizar(p.nome);
            if (n.Length > 0 && (c.Contains(n) || n.Contains(c))) achadas.Add(p);
        }
        return achadas;
    }

    public string[] Nomes()
    {
        var l = new List<string>();
        foreach (var p in partes) l.Add(p.nome);
        return l.ToArray();
    }

    // ---------- cor ----------
    public int PintarTudo(Color c) { foreach (var p in partes) p.Pintar(c); return partes.Count; }

    public int Pintar(string consulta, Color c)
    {
        var l = Resolver(consulta);
        foreach (var p in l) p.Pintar(c);
        return l.Count;
    }

    // texto de cor (inclui "mais claro/escuro") aplicado a cada parte a partir da cor que ela tem agora
    public int PintarTexto(string consulta, string textoCor)
    {
        var l = Resolver(consulta);
        foreach (var p in l) p.Pintar(CriadorHolograma.CorDeEdicao(textoCor, p.CorVisual()));
        return l.Count;
    }

    // cores na ordem das partes (cicla); degradê interpola entre as cores ao longo das partes
    public void PintarEmOrdem(List<Color> cores, bool degrade)
    {
        if (cores.Count == 0 || partes.Count == 0) return;
        for (int i = 0; i < partes.Count; i++)
        {
            Color c;
            if (degrade && cores.Count > 1)
            {
                float t = partes.Count == 1 ? 0f : i / (float)(partes.Count - 1) * (cores.Count - 1);
                int a = Mathf.Min(cores.Count - 2, (int)t);
                c = Color.Lerp(cores[a], cores[a + 1], t - a);
            }
            else c = cores[i % cores.Count];
            partes[i].Pintar(c);
        }
    }

    // ---------- esconder/mostrar ----------
    public int Ocultar(string consulta, bool o)
    {
        var l = Resolver(consulta);
        foreach (var p in l) p.Ocultar(o);
        return l.Count;
    }

    public void MostrarTudo() { foreach (var p in partes) p.Ocultar(false); }

    // ---------- girar ----------
    public void Girar(bool ligar, float? velocidade = null)
    {
        girando = ligar;
        if (velocidade.HasValue) grausPorSegundo = Mathf.Clamp(velocidade.Value, 5f, 360f);
        var rb = GetComponent<Rigidbody>();
        if (rb != null && ligar) { rb.angularVelocity = Vector3.zero; }
    }

    // ---------- vista explodida ----------
    public bool Explodir(bool abrir, bool animar = true)
    {
        Descobrir();
        var visiveis = partes.FindAll(p => !p.oculta);
        if (visiveis.Count < 2) return false;
        if (abrir == explodido) return true;
        explodido = abrir;
        if (animacao != null) StopCoroutine(animacao);
        if (abrir) foreach (var p in partes) if (!p.GetComponent<Rigidbody>()) p.posOriginal = p.transform.localPosition;
        animacao = StartCoroutine(AnimarExplosao(visiveis, abrir, animar ? 0.6f : 0f));
        return true;
    }

    IEnumerator AnimarExplosao(List<ParteHolo> alvo, bool abrir, float duracao)
    {
        if (!abrir) AtivarPegadaPartes(false);
        var centro = HoloAlvo.CentroDoObjeto(gameObject);
        float maior = InteracaoMaos.MaiorDimensao(gameObject);
        var inicio = new Dictionary<ParteHolo, Vector3>();
        var fim = new Dictionary<ParteHolo, Vector3>();
        int i = 0;
        foreach (var p in alvo)
        {
            inicio[p] = p.transform.localPosition;
            if (abrir)
            {
                var dir = p.LimitesMundo().center - centro;
                if (dir.sqrMagnitude < 0.00005f) { float a = i * 2.39996f; dir = new Vector3(Mathf.Cos(a), 0.4f, Mathf.Sin(a)); }
                dir.Normalize();
                var destino = p.transform.parent.TransformPoint(p.posOriginal) + dir * maior * 0.7f;
                fim[p] = p.transform.parent.InverseTransformPoint(destino);
            }
            else fim[p] = p.posOriginal;
            i++;
        }
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = Mathf.SmoothStep(0f, 1f, t / duracao);
            foreach (var p in alvo) if (p != null) p.transform.localPosition = Vector3.Lerp(inicio[p], fim[p], x);
            yield return null;
        }
        foreach (var p in alvo) if (p != null) p.transform.localPosition = fim[p];
        if (abrir) AtivarPegadaPartes(true);
        animacao = null;
    }

    // ---------- pegar uma parte separada ----------
    // com o objeto aberto, cada parte ganha colisor + pegada própria (e o colisor da raiz sai do caminho)
    void AtivarPegadaPartes(bool ligar)
    {
        var raizCol = GetComponent<BoxCollider>();
        if (raizCol != null) raizCol.enabled = !ligar;
        foreach (var p in partes)
        {
            if (p == null) continue;
            if (ligar && !p.oculta)
            {
                var mf = p.GetComponent<MeshFilter>();
                var caixa = p.GetComponent<BoxCollider>();
                if (caixa == null) caixa = p.gameObject.AddComponent<BoxCollider>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var b = mf.sharedMesh.bounds;
                    var s = p.transform.lossyScale;
                    float minimo = 0.04f / Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
                    caixa.center = b.center;
                    caixa.size = new Vector3(Mathf.Max(b.size.x, minimo), Mathf.Max(b.size.y, minimo), Mathf.Max(b.size.z, minimo));
                }
                var rb = p.GetComponent<Rigidbody>();
                if (rb == null) { rb = p.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; }
                if (p.GetComponent<XRGrabInteractable>() == null)
                {
                    var g = p.gameObject.AddComponent<XRGrabInteractable>();
                    g.movementType = XRBaseInteractable.MovementType.Instantaneous;
                    g.selectMode = InteractableSelectMode.Single;
                    g.throwOnDetach = false;
                    g.useDynamicAttach = true;
                    g.matchAttachPosition = true;
                    g.matchAttachRotation = true;
                }
            }
            else StartCoroutine(RemoverPegada(p));
        }
    }

    IEnumerator RemoverPegada(ParteHolo p)
    {
        if (p == null) yield break;
        var g = p.GetComponent<XRGrabInteractable>();
        if (g != null) Destroy(g);
        yield return null;
        if (p == null) yield break;
        var rb = p.GetComponent<Rigidbody>(); if (rb != null) Destroy(rb);
        var c = p.GetComponent<Collider>(); if (c != null) Destroy(c);
    }

    // ---------- destaque com nome ----------
    public void Destacar(ParteHolo p, float segundos = 3f)
    {
        if (p == null) return;
        StartCoroutine(Pulsar(p, segundos));
        MostrarRotulo(p, segundos);
    }

    IEnumerator Pulsar(ParteHolo p, float segundos)
    {
        var baseEsc = p.transform.localScale;
        for (float t = 0; t < segundos && p != null; t += Time.deltaTime)
        {
            float k = 1f + 0.06f * (0.5f + 0.5f * Mathf.Sin(t * 9f)) * Mathf.Clamp01((segundos - t) * 2f);
            p.transform.localScale = baseEsc * k;
            yield return null;
        }
        if (p != null) p.transform.localScale = baseEsc;
    }

    void MostrarRotulo(ParteHolo p, float segundos)
    {
        if (rotulo == null)
        {
            var go = new GameObject("RotuloParte");
            rotulo = HoloUtil.CriarTexto(go.transform, "Texto", "", Vector3.zero, new Vector2(0.4f, 0.06f), 0.1f, TemaZetta.Branco);
            rotulo.fontStyle = FontStyles.Bold;
            rotulo.transform.parent.gameObject.AddComponent<RotuloSegue>();
        }
        var seg = rotulo.transform.parent.GetComponent<RotuloSegue>();
        rotulo.text = p.nome;
        seg.Mostrar(p, segundos);
    }

    class RotuloSegue : MonoBehaviour
    {
        ParteHolo alvo; float ate;
        public void Mostrar(ParteHolo p, float s) { alvo = p; ate = Time.time + s; gameObject.SetActive(true); }
        void LateUpdate()
        {
            if (alvo == null || Time.time > ate) { gameObject.SetActive(false); return; }
            var b = alvo.LimitesMundo();
            transform.position = b.center + Vector3.up * (b.extents.y + 0.05f);
            if (Camera.main != null) transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Vector3.up);
        }
    }

    // ---------- estado salvo com a âncora ----------
    public string Serializar()
    {
        if (partes.Count == 0) return estadoPendente ?? "";
        var cores = new JObject(); var ocultas = new JArray();
        foreach (var p in partes)
        {
            if (p.pintada) cores[p.nome] = "#" + ColorUtility.ToHtmlStringRGB(p.cor);
            if (p.oculta) ocultas.Add(p.nome);
        }
        var ex = new JObject();
        foreach (var kv in extras) ex[kv.Key] = kv.Value;
        if (cores.Count == 0 && ocultas.Count == 0 && !girando && !explodido && ex.Count == 0) return "";
        var o = new JObject();
        if (cores.Count > 0) o["c"] = cores;
        if (ocultas.Count > 0) o["o"] = ocultas;
        if (girando) { o["g"] = true; o["v"] = grausPorSegundo; }
        if (explodido) o["e"] = true;
        if (ex.Count > 0) o["x"] = ex;
        return o.ToString(Newtonsoft.Json.Formatting.None);
    }

    public void AplicarEstado(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        if (partes.Count == 0) { estadoPendente = json; return; }
        try
        {
            var o = JObject.Parse(json);
            if (o["x"] is JObject ex)
                foreach (var kv in ex) { float v = kv.Value.Value<float>(); extras[kv.Key] = v; acaoExtra?.Invoke("direto:" + kv.Key, v); }
            if (o["c"] is JObject cores)
                foreach (var kv in cores)
                {
                    var p = partes.Find(x => x.nome == kv.Key);
                    if (p != null && ColorUtility.TryParseHtmlString(kv.Value.ToString(), out var c)) p.Pintar(c);
                }
            if (o["o"] is JArray ocultas)
                foreach (var n in ocultas) { var p = partes.Find(x => x.nome == n.ToString()); if (p != null) p.Ocultar(true); }
            if (o.Value<bool?>("g") == true) Girar(true, o.Value<float?>("v"));
            if (o.Value<bool?>("e") == true) Explodir(true, false);
        }
        catch (Exception e) { Debug.LogWarning("ObjetoInterativo: estado salvo inválido: " + e.Message); }
    }

    // ---------- achar o objeto a partir de argumentos de ferramenta ----------
    public static HoloItem Alvo(JObject a, out string erro)
    {
        erro = null;
        string desc = a.Value<string>("objeto") ?? a.Value<string>("descricao");
        HoloItem item = null;
        if (!string.IsNullOrWhiteSpace(desc) && ApagadorHolo.Normalizar(desc) != "isso" && ApagadorHolo.Normalizar(desc) != "esse")
        {
            item = ApagadorHolo.Buscar(desc);
            if (item == null) { erro = "não encontrei esse objeto"; return null; }
            return item;
        }
        item = HoloAlvo.ObjetoOlhado();
        if (item == null)
        {
            // sem olhar nem apontar: o criado mais recentemente
            HoloRegistro.Itens.RemoveAll(i => i.go == null);
            if (HoloRegistro.Itens.Count > 0) item = HoloRegistro.Itens[HoloRegistro.Itens.Count - 1];
        }
        if (item == null) erro = "não sei qual objeto; peça pela descrição";
        return item;
    }
}
