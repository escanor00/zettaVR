using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

// Infográfico holográfico do DNA (visual original): hélice vertical ciano-azul sobre uma base de anéis, fitas com nós
// luminosos, pares de bases como degraus de luz (A-T e C-G em cores diferentes ao "mostrar as bases"), hexágonos
// translúcidos ao fundo, 3 chamadas ligando a hélice a painéis com informação real e um gráfico ilustrativo.
// Tudo é feito de partes (ObjetoInterativo): fita A, fita B, pares A-T, pares C-G, base, hexagonos, chamadas.
public class DnaHolo : MonoBehaviour
{
    public float tamanhoPadrao = 0.45f;

    const float Altura = 1.0f, Raio = 0.17f, Voltas = 2.5f;
    const int Pares = 26;                       // ~10,5 pares por volta
    static readonly Color CorFitaA = new Color(0.25f, 0.88f, 1f);
    static readonly Color CorFitaB = new Color(0.28f, 0.52f, 1f);
    static readonly Color CorAT = new Color(1f, 0.78f, 0.28f);
    static readonly Color CorCG = new Color(0.42f, 1f, 0.69f);
    static readonly Color CorA = new Color(1f, 0.8f, 0.3f), CorT = new Color(1f, 0.55f, 0.25f);
    static readonly Color CorC = new Color(0.42f, 1f, 0.69f), CorG = new Color(1f, 0.45f, 0.75f);
    static readonly Color CorPar = new Color(0.65f, 0.95f, 1f);

    const string Decl = @"{
  ""name"": ""mostrar_dna"",
  ""description"": ""Infográfico holográfico do DNA: dupla hélice com pares de bases, 3 chamadas (fita, par de bases, gene) e um gráfico de exemplo. acao: mostrar (cria), bases (colore A-T e C-G, ou desliga com ligar=false), explicar (percorre as chamadas e devolve os fatos para você explicar), fechar."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""description"": ""mostrar, bases, explicar ou fechar. Padrão: mostrar."" },
      ""ligar"": { ""type"": ""BOOLEAN"", ""description"": ""Só para bases: true mostra as cores das bases, false volta ao normal."" }
    }
  }
}";
    const string Instrucao = "Para 'mostre o DNA', 'infográfico do DNA' use mostrar_dna (acao mostrar). 'Mostre as bases' = acao bases. 'Explique o DNA' = acao explicar e depois fale os fatos devolvidos em poucas frases, sem inventar números.";

    public static readonly string[] Fatos =
    {
        "A fita é o esqueleto de açúcar e fosfato; as duas fitas correm em sentidos opostos (5' para 3' e 3' para 5').",
        "Pares de bases: A liga com T por 2 pontes de hidrogênio e C com G por 3. Uma volta da hélice tem cerca de 10,5 pares (3,4 nm) e a largura é ~2 nm.",
        "Um gene é um trecho de DNA que codifica uma proteína. O genoma humano tem cerca de 3,1 bilhões de pares de bases e ~20 mil genes."
    };

    static readonly string[] Resumo =
    {
        "Esqueleto de açúcar e fosfato. As duas fitas correm em sentidos opostos.",
        "A–T: 2 pontes de hidrogênio. C–G: 3. Cerca de 10,5 pares por volta.",
        "Trecho que codifica uma proteína. Humano: ~20 mil genes."
    };

    class Chamada { public string titulo, texto; public Transform painel; public Transform marcador; }

    GameObject atual;
    DnaInstancia inst;
    int contador;

    // dados de uma instância criada
    class DnaInstancia
    {
        public Mesh paresAT, paresCG;
        public List<int> tipoAT = new List<int>(), tipoCG = new List<int>();   // 0 A-T, 1 T-A (AT) | 2 C-G, 3 G-C (CG)
        public bool bases;
        public List<Chamada> chamadas = new List<Chamada>();
        public GameObject raiz;
    }

    void Awake()
    {
        AncorasHolo.Fabricas["dna_info"] = (item, pos, rot) =>
        {
            try { var g = Montar(pos, rot); return g; } catch { return null; }
        };
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Tratar, Instrucao);
    }

    JObject Tratar(JObject a)
    {
        string acao = (a.Value<string>("acao") ?? "mostrar").ToLowerInvariant();
        if (acao.StartsWith("fech") || acao.StartsWith("apag") || acao.StartsWith("tir"))
        {
            if (atual == null) return new JObject { ["result"] = "não há infográfico aberto" };
            HoloRegistro.Remover(atual); Destroy(atual); atual = null; inst = null;
            return new JObject { ["result"] = "infográfico fechado" };
        }
        if (atual == null) Montar(null, null);
        if (acao.StartsWith("base"))
        {
            bool lig = a["ligar"] == null || a.Value<bool>("ligar");
            MostrarBases(lig);
            return new JObject { ["result"] = lig ? "bases coloridas: A dourado, T laranja, C verde, G rosa (pares A-T e C-G em cores diferentes)" : "bases normais" };
        }
        if (acao.StartsWith("expl"))
        {
            StartCoroutine(Percorrer());
            return new JObject { ["result"] = "chamadas acesas em sequência", ["fatos"] = new JArray(Fatos), ["gráfico"] = "o gráfico no canto é só um exemplo ilustrativo de % de G+C (valores aproximados)" };
        }
        return new JObject { ["result"] = "infográfico do DNA aberto; dá para pegar, girar, escalar, abrir em partes e pintar cada parte" };
    }

    IEnumerator Percorrer()
    {
        if (inst == null) yield break;
        foreach (var c in inst.chamadas)
        {
            if (c.painel == null) yield break;
            var rs = c.painel.GetComponentsInChildren<Renderer>(true);
            var escalaIni = c.painel.localScale;
            for (float t = 0; t < 1.6f && c.painel != null; t += Time.deltaTime)
            {
                c.painel.localScale = escalaIni * (1f + 0.08f * Mathf.Sin(t / 1.6f * Mathf.PI));
                if (c.marcador != null) c.marcador.localScale = Vector3.one * (1f + 0.8f * Mathf.Sin(t / 1.6f * Mathf.PI));
                yield return null;
            }
            if (c.painel != null) c.painel.localScale = escalaIni;
            if (c.marcador != null) c.marcador.localScale = Vector3.one;
        }
    }

    // ---------- geometria ----------
    static Vector3 PontoFita(float u, int fita)
    {
        float ang = u * Voltas * Mathf.PI * 2f + (fita == 0 ? 0f : 2.4f);
        return new Vector3(Mathf.Cos(ang) * Raio, (u - 0.5f) * Altura, Mathf.Sin(ang) * Raio);
    }

    static void Octaedro(List<Vector3> v, List<int> t, List<Color> c, Vector3 p, float r, Color cor)
    {
        int b = v.Count;
        v.Add(p + Vector3.right * r); v.Add(p - Vector3.right * r);
        v.Add(p + Vector3.up * r * 1.3f); v.Add(p - Vector3.up * r * 1.3f);
        v.Add(p + Vector3.forward * r); v.Add(p - Vector3.forward * r);
        for (int i = 0; i < 6; i++) c.Add(cor);
        int[] f = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 0, 4, 3, 4, 1, 3, 1, 5, 3, 5, 0, 3 };
        foreach (int i in f) t.Add(b + i);
    }

    static void Caixa(List<Vector3> v, List<int> t, List<Color> c, Vector3 a, Vector3 b, float meia, Color cor)
    {
        var d = (b - a);
        var lado = Vector3.Cross(d, Vector3.up);
        if (lado.sqrMagnitude < 1e-8f) lado = Vector3.right;
        lado = lado.normalized * meia;
        var topo = Vector3.Cross(d, lado).normalized * meia;
        int i0 = v.Count;
        for (int k = 0; k < 2; k++)
        {
            var p = k == 0 ? a : b;
            v.Add(p - lado - topo); v.Add(p + lado - topo); v.Add(p + lado + topo); v.Add(p - lado + topo);
            for (int q = 0; q < 4; q++) c.Add(cor);
        }
        for (int q = 0; q < 4; q++)
        {
            int n = (q + 1) % 4;
            t.Add(i0 + q); t.Add(i0 + n); t.Add(i0 + 4 + n);
            t.Add(i0 + q); t.Add(i0 + 4 + n); t.Add(i0 + 4 + q);
        }
    }

    static Mesh Fazer(string nome, List<Vector3> v, List<int> t, List<Color> c)
    {
        var m = new Mesh { name = nome };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    static void Pintar(Mesh m, Color cor, float alfa)
    {
        var cs = new Color[m.vertexCount];
        cor.a = alfa;
        for (int i = 0; i < cs.Length; i++) cs[i] = cor;
        m.colors = cs;
    }

    // aditivo (luz), sem cor chapada; MatF tem fresnel (borda mais viva) para os volumes pequenos (nós, degraus)
    Material Mat() => HoloUtil.NovoMaterialAditivo(new Color(1f, 1f, 1f, 0.75f), 1f, 0f);
    Material MatF() => HoloUtil.NovoMaterialAditivo(new Color(0.8f, 0.9f, 1f, 0.8f), 0.45f, 1.4f);

    // ---------- montagem ----------
    public GameObject Montar(Vector3? posMundo, Quaternion? rotMundo)
    {
        var cam = Camera.main.transform;
        var mesa = FindAnyObjectByType<MesaHolografica>();
        var pos = posMundo ?? (mesa != null && mesa.ativa ? mesa.Centro + Vector3.up * 0.3f : HoloUtil.PontoNaVista(new Vector3(0f, 0f, 0.85f)));
        if (atual != null && posMundo == null) { HoloRegistro.Remover(atual); Destroy(atual); atual = null; }

        var raiz = new GameObject("DnaInfo_" + (++contador));
        raiz.transform.SetParent(transform, true);
        raiz.transform.SetPositionAndRotation(pos, rotMundo ?? Quaternion.Euler(0f, cam.eulerAngles.y, 0f));
        raiz.transform.localScale = Vector3.one * tamanhoPadrao;
        var oi = raiz.AddComponent<ObjetoInterativo>();
        var d = new DnaInstancia { raiz = raiz };
        inst = d;

        // fitas: linhas paralelas + nós luminosos
        for (int f = 0; f < 2; f++)
        {
            var cor = f == 0 ? CorFitaA : CorFitaB;
            var pts = new List<Vector3>(); var cs = new List<Color>();
            const int seg = 140;
            for (int k = 0; k < seg; k++)
            {
                // fita fina: fio central forte e dois fios de aura bem fracos
                foreach (var off in new[] { Vector3.zero, new Vector3(0, 0.004f, 0), new Vector3(0, -0.004f, 0) })
                {
                    pts.Add(PontoFita(k / (float)seg, f) + off); pts.Add(PontoFita((k + 1) / (float)seg, f) + off);
                    var cc = cor; cc.a = off == Vector3.zero ? 0.85f : 0.22f; cs.Add(cc); cs.Add(cc);
                }
            }
            var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
            for (int p = 0; p <= Pares; p++) Octaedro(v, t, c, PontoFita(p / (float)Pares, f), 0.010f, Color.Lerp(cor, Color.white, 0.55f));
            var malhaNos = Fazer(f == 0 ? "FitaA_nos" : "FitaB_nos", v, t, c);
            malhaNos.RecalculateNormals();
            var go = HoloMat.NovoMeshGO(raiz.transform, f == 0 ? "fita A" : "fita B", malhaNos, MatF());
            var malhaLinhas = HoloMat.MalhaDeLinhas(pts, cs, "FitaLinhas");
            HoloMat.NovoMeshGO(go.transform, "Linhas", malhaLinhas, Mat());
            var ph = go.AddComponent<ParteHolo>();
            ph.nome = f == 0 ? "fita A" : "fita B";
            ph.apelidos = f == 0 ? new[] { "fita a", "primeira fita", "esqueleto", "fita um" } : new[] { "fita b", "segunda fita", "fita dois" };
            ph.pintorPersonalizado = col => { Pintar(malhaNos, Color.Lerp(col, Color.white, 0.55f), 1f); Pintar(malhaLinhas, col, 0.9f); };
            oi.AdicionarParte(ph);
        }

        // pares de bases: degraus de luz; A-T e C-G em partes separadas
        {
            uint sem = 12345;
            Func<int> rnd = () => { sem = sem * 1664525u + 1013904223u; return (int)((sem >> 16) & 3); };
            var vA = new List<Vector3>(); var tA = new List<int>(); var cA = new List<Color>();
            var vC = new List<Vector3>(); var tC = new List<int>(); var cC = new List<Color>();
            for (int p = 1; p < Pares; p++)
            {
                float u = p / (float)Pares;
                var a = PontoFita(u, 0); var b = PontoFita(u, 1);
                var mid = (a + b) * 0.5f;
                int tipo = rnd();   // 0 A-T, 1 T-A, 2 C-G, 3 G-C
                // degraus SEPARADOS das fitas (fora dos nós) e com uma fresta no meio entre as duas bases
                var ini = Vector3.Lerp(a, b, 0.09f); var fim = Vector3.Lerp(a, b, 0.91f);
                var m1 = Vector3.Lerp(a, b, 0.485f); var m2 = Vector3.Lerp(a, b, 0.515f);
                if (tipo < 2) { d.tipoAT.Add(tipo); Caixa(vA, tA, cA, ini, m1, 0.0022f, CorPar); Caixa(vA, tA, cA, m2, fim, 0.0022f, CorPar); }
                else { d.tipoCG.Add(tipo); Caixa(vC, tC, cC, ini, m1, 0.0022f, CorPar); Caixa(vC, tC, cC, m2, fim, 0.0022f, CorPar); }
            }
            d.paresAT = Fazer("ParesAT", vA, tA, cA);
            d.paresCG = Fazer("ParesCG", vC, tC, cC);
            d.paresAT.RecalculateNormals(); d.paresCG.RecalculateNormals();
            var gAT = HoloMat.NovoMeshGO(raiz.transform, "pares A-T", d.paresAT, MatF());
            var gCG = HoloMat.NovoMeshGO(raiz.transform, "pares C-G", d.paresCG, MatF());
            var pAT = gAT.AddComponent<ParteHolo>(); pAT.nome = "pares A-T"; pAT.apelidos = new[] { "pares at", "degraus a t", "adenina timina", "pares a-t" };
            pAT.pintorPersonalizado = col => { d.bases = false; Pintar(d.paresAT, col, 0.9f); };
            var pCG = gCG.AddComponent<ParteHolo>(); pCG.nome = "pares C-G"; pCG.apelidos = new[] { "pares cg", "degraus c g", "citosina guanina", "pares c-g" };
            pCG.pintorPersonalizado = col => { d.bases = false; Pintar(d.paresCG, col, 0.9f); };
            oi.AdicionarParte(pAT); oi.AdicionarParte(pCG);
            Pintar(d.paresAT, CorPar, 0.9f); Pintar(d.paresCG, CorPar, 0.9f);
        }

        // base de anéis
        {
            var pts = new List<Vector3>(); var cs = new List<Color>();
            float y = -Altura * 0.5f - 0.07f;
            float[] raios = { 0.26f, 0.37f, 0.5f }; float[] alfas = { 0.95f, 0.65f, 0.4f };
            for (int r = 0; r < 3; r++)
                for (int k = 0; k < 72; k++)
                {
                    float a0 = k / 72f * Mathf.PI * 2f, a1 = (k + 1) / 72f * Mathf.PI * 2f;
                    pts.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * raios[r] + Vector3.up * y);
                    pts.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * raios[r] + Vector3.up * y);
                    var cc = CorFitaA; cc.a = alfas[r]; cs.Add(cc); cs.Add(cc);
                }
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                pts.Add(dir * 0.26f + Vector3.up * y); pts.Add(dir * 0.5f + Vector3.up * y);
                var cc = CorFitaB; cc.a = 0.35f; cs.Add(cc); cs.Add(cc);
            }
            // anel de cima (como na referencia): dois aneis finos acima da helice
            float yt = Altura * 0.5f + 0.07f;
            float[] raiosT = { 0.2f, 0.3f };
            for (int r = 0; r < 2; r++)
                for (int k = 0; k < 64; k++)
                {
                    float a0 = k / 64f * Mathf.PI * 2f, a1 = (k + 1) / 64f * Mathf.PI * 2f;
                    pts.Add(new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * raiosT[r] + Vector3.up * yt);
                    pts.Add(new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * raiosT[r] + Vector3.up * yt);
                    var cc = CorFitaA; cc.a = r == 0 ? 0.7f : 0.35f; cs.Add(cc); cs.Add(cc);
                }
            pts.Add(new Vector3(0, y, 0)); pts.Add(new Vector3(0, y + 0.07f, 0)); var cx = CorFitaA; cx.a = 0.8f; cs.Add(cx); cs.Add(cx);
            var m = HoloMat.MalhaDeLinhas(pts, cs, "BaseAneis");
            var go = HoloMat.NovoMeshGO(raiz.transform, "base", m, Mat());
            var ph = go.AddComponent<ParteHolo>(); ph.nome = "base"; ph.apelidos = new[] { "anéis", "aneis", "base de anéis", "pedestal" };
            ph.pintorPersonalizado = col => { var arr = new Color[m.vertexCount]; var cl = m.colors; for (int i = 0; i < arr.Length; i++) { arr[i] = col; arr[i].a = cl[i].a; } m.colors = arr; };
            oi.AdicionarParte(ph);
        }

        // hexágonos translúcidos ao fundo
        {
            var vF = new List<Vector3>(); var tF = new List<int>(); var cF = new List<Color>();
            var pts = new List<Vector3>(); var cs = new List<Color>();
            uint sem = 777;
            Func<float> r01 = () => { sem = sem * 1664525u + 1013904223u; return ((sem >> 8) & 0xFFFF) / 65535f; };
            for (int n = 0; n < 16; n++)
            {
                var ctr = new Vector3(Mathf.Lerp(-0.85f, 0.85f, r01()), Mathf.Lerp(-0.5f, 0.55f, r01()), 0.42f + r01() * 0.1f);
                float tam = Mathf.Lerp(0.07f, 0.17f, r01());
                bool cheio = r01() > 0.45f;
                int b = vF.Count;
                for (int k = 0; k < 6; k++)
                {
                    float a0 = k * Mathf.PI / 3f, a1 = (k + 1) * Mathf.PI / 3f;
                    var p0 = ctr + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * tam; var p1 = ctr + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * tam;
                    pts.Add(p0); pts.Add(p1); var cc = CorFitaA; cc.a = 0.35f; cs.Add(cc); cs.Add(cc);
                    vF.Add(ctr); vF.Add(p0); vF.Add(p1);
                    var cf = CorFitaB; cf.a = cheio ? 0.16f : 0.04f; cF.Add(cf); cF.Add(cf); cF.Add(cf);
                    tF.Add(vF.Count - 3); tF.Add(vF.Count - 2); tF.Add(vF.Count - 1);
                }
            }
            var mf = Fazer("HexFundo", vF, tF, cF);
            var go = HoloMat.NovoMeshGO(raiz.transform, "hexagonos", mf, Mat());
            var ml = HoloMat.MalhaDeLinhas(pts, cs, "HexLinhas");
            HoloMat.NovoMeshGO(go.transform, "Linhas", ml, Mat());
            var ph = go.AddComponent<ParteHolo>(); ph.nome = "hexagonos"; ph.apelidos = new[] { "hexágonos", "fundo", "hexagono" };
            ph.pintorPersonalizado = col => { Pintar(mf, col, 0.1f); Pintar(ml, col, 0.35f); };
            oi.AdicionarParte(ph);
        }

        MontarChamadas(raiz, oi, d);

        HoloMat.TornarPegavel(raiz);
        HoloRegistro.Adicionar(raiz, "dna_info", "", "infográfico do DNA", "dna_info", tamanhoPadrao, "{}");
        StartCoroutine(Surgir(raiz.transform));
        StartCoroutine(RevelarPorAltura(raiz));
        CriarTracos(raiz);
        atual = raiz;
        return raiz;
    }

    void MontarChamadas(GameObject raiz, ObjetoInterativo oi, DnaInstancia d)
    {
        var grupo = new GameObject("chamadas");
        grupo.transform.SetParent(raiz.transform, false);
        var pts = new List<Vector3>(); var cs = new List<Color>();
        var vM = new List<Vector3>(); var tM = new List<int>(); var cM = new List<Color>();
        var verde = new Color(0.42f, 1f, 0.69f);

        // (fita, u, lado, y do painel, título)
        var def = new[]
        {
            new { fita = 0, u = 0.80f, lado = 1f, y = 0.32f, tit = "FITA" },
            new { fita = 1, u = 0.52f, lado = -1f, y = 0.02f, tit = "PAR DE BASES" },
            new { fita = 0, u = 0.22f, lado = 1f, y = -0.30f, tit = "GENE" }
        };
        for (int i = 0; i < 3; i++)
        {
            var c = def[i];
            var ancora = PontoFita(c.u, c.fita);
            if (i == 1) ancora = (PontoFita(c.u, 0) + PontoFita(c.u, 1)) * 0.5f;
            var tamP = new Vector2(0.74f, 0.36f);
            var centro = new Vector3(c.lado * 0.98f, c.y, -0.05f);
            var painel = HoloUtil.CriarPainel(grupo.transform, "Chamada" + (i + 1), tamP);
            painel.localPosition = centro;
            HoloUtil.CriarTexto(painel, "Titulo", c.tit, new Vector3(0, 0.125f, TemaZetta.ProfundidadeTexto), new Vector2(0.68f, 0.07f), 0.7f, verde);
            HoloUtil.CriarTexto(painel, "Texto", Resumo[i], new Vector3(0, -0.04f, TemaZetta.ProfundidadeTexto), new Vector2(0.66f, 0.22f), 0.45f, TemaZetta.Branco);

            // linha em cotovelo da hélice até a borda do painel
            var borda = centro + new Vector3(-c.lado * tamP.x * 0.5f, 0, 0);
            var cotovelo = new Vector3(c.lado * 0.42f, centro.y, ancora.z);
            var cc = verde; cc.a = 0.9f;
            pts.Add(ancora); pts.Add(new Vector3(cotovelo.x, ancora.y, ancora.z)); cs.Add(cc); cs.Add(cc);
            pts.Add(new Vector3(cotovelo.x, ancora.y, ancora.z)); pts.Add(new Vector3(borda.x, borda.y, borda.z)); cs.Add(cc); cs.Add(cc);
            Octaedro(vM, tM, cM, ancora, 0.022f, verde);

            d.chamadas.Add(new Chamada { titulo = c.tit, texto = Fatos[i], painel = painel });
        }
        // gráfico ilustrativo (marcado como exemplo)
        {
            var tam = new Vector2(0.74f, 0.42f);
            var painel = HoloUtil.CriarPainel(grupo.transform, "GraficoExemplo", tam);
            painel.localPosition = new Vector3(-0.98f, -0.40f, -0.05f);
            HoloUtil.CriarTexto(painel, "Titulo", "EXEMPLO ILUSTRATIVO", new Vector3(0, 0.165f, TemaZetta.ProfundidadeTexto), new Vector2(0.68f, 0.06f), 0.6f, TemaZetta.Dourado);
            string[] nomes = { "Humano", "Levedura", "E. coli", "Malária" };
            float[] vals = { 41f, 38f, 51f, 19f };
            Color[] cores = { CorFitaA, CorFitaB, verde, CorAT };
            for (int k = 0; k < 4; k++)
            {
                float x = -0.21f + k * 0.14f;
                float h = vals[k] / 60f * 0.17f;
                var barra = GameObject.CreatePrimitive(PrimitiveType.Quad);
                barra.name = "Barra" + k;
                DestroyImmediate(barra.GetComponent<Collider>());
                barra.transform.SetParent(painel, false);
                barra.transform.localPosition = new Vector3(x, -0.11f + h * 0.5f, TemaZetta.ProfundidadeTexto * 0.5f);
                barra.transform.localScale = new Vector3(0.095f, h, 1f);
                var cb = cores[k]; cb.a = 0.85f;
                barra.GetComponent<Renderer>().sharedMaterial = HoloUtil.NovoMaterialSprite(cb);
                HoloUtil.CriarTexto(painel, "Nome" + k, nomes[k], new Vector3(x, -0.15f, TemaZetta.ProfundidadeTexto), new Vector2(0.16f, 0.04f), 0.35f, TemaZetta.Ciano);
                HoloUtil.CriarTexto(painel, "Valor" + k, vals[k] + "%", new Vector3(x, -0.11f + h + 0.03f, TemaZetta.ProfundidadeTexto), new Vector2(0.14f, 0.04f), 0.4f, TemaZetta.Branco);
            }
            HoloUtil.CriarTexto(painel, "Aviso", "% de G+C, valores aproximados, só para ilustrar", new Vector3(0, -0.185f, TemaZetta.ProfundidadeTexto), new Vector2(0.7f, 0.035f), 0.28f, TemaZetta.ComAlfa(TemaZetta.Branco, 0.7f));
        }

        var mLin = HoloMat.MalhaDeLinhas(pts, cs, "ChamadasLinhas");
        HoloMat.NovoMeshGO(grupo.transform, "Linhas", mLin, Mat());
        var mMar = Fazer("Marcadores", vM, tM, cM);
        var gm = HoloMat.NovoMeshGO(grupo.transform, "Marcadores", mMar, Mat());
        var ph = grupo.AddComponent<ParteHolo>();
        ph.nome = "chamadas"; ph.apelidos = new[] { "painéis", "paineis", "informações", "informacoes", "legendas", "gráfico", "grafico" };
        ph.pintorPersonalizado = col => { Pintar(mLin, col, 0.9f); Pintar(mMar, col, 1f); };
        oi.AdicionarParte(ph);
    }

    public void MostrarBases(bool lig)
    {
        if (inst == null) return;
        inst.bases = lig;
        Recolorir(inst.paresAT, inst.tipoAT, lig, CorAT, CorA, CorT);
        Recolorir(inst.paresCG, inst.tipoCG, lig, CorCG, CorC, CorG);
    }

    // cada degrau tem 2 caixas de 8 vértices (metade de cada base); sem bases, uma cor única
    static void Recolorir(Mesh m, List<int> tipos, bool lig, Color par, Color c1, Color c2)
    {
        if (m == null) return;
        var cs = new Color[m.vertexCount];
        for (int r = 0; r < tipos.Count; r++)
        {
            bool inverso = (tipos[r] & 1) == 1;
            var ca = lig ? (inverso ? c2 : c1) : CorPar; var cb = lig ? (inverso ? c1 : c2) : CorPar;
            if (lig) { ca = Color.Lerp(ca, Color.white, 0.1f); }
            ca.a = 1f; cb.a = 1f;
            for (int k = 0; k < 8; k++) { cs[r * 16 + k] = ca; cs[r * 16 + 8 + k] = cb; }
        }
        m.colors = cs;
    }

    // montagem progressiva: cada malha da helice aparece de baixo para cima (alfa por vertice)
    IEnumerator RevelarPorAltura(GameObject raiz)
    {
        var malhas = new List<Mesh>(); var base0 = new List<Color[]>(); var verts = new List<Vector3[]>();
        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh;
            if (m == null || m.colors.Length != m.vertexCount || mf.transform.IsChildOf(raiz.transform.Find("chamadas"))) continue;
            malhas.Add(m); base0.Add(m.colors); verts.Add(m.vertices);
        }
        const float dur = 2.4f;
        for (float t = 0; t < dur && raiz != null; t += Time.deltaTime)
        {
            float frente = Mathf.SmoothStep(0f, 1f, t / dur) * 1.25f;
            for (int i = 0; i < malhas.Count; i++)
            {
                if (malhas[i] == null) continue;
                var cs = new Color[base0[i].Length];
                for (int k = 0; k < cs.Length; k++)
                {
                    float h = verts[i][k].y / Altura + 0.6f;   // ~0 na base, ~1,2 no topo
                    cs[k] = base0[i][k]; cs[k].a *= Mathf.Clamp01((frente - h * 0.85f) / 0.15f);
                }
                malhas[i].colors = cs;
            }
            yield return null;
        }
        for (int i = 0; i < malhas.Count; i++) if (malhas[i] != null) malhas[i].colors = base0[i];
    }

    // 3 traços de luz com rastro percorrendo as fitas
    void CriarTracos(GameObject raiz)
    {
        var mat = HoloUtil.NovoMaterialAditivo(Color.white, 1f, 0f);
        for (int k = 0; k < 3; k++)
        {
            var go = new GameObject("Traco" + k);
            go.transform.SetParent(raiz.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = mat;
            lr.positionCount = 16;
            lr.widthMultiplier = 0.006f;
            lr.numCapVertices = 2;
            var cor = k == 1 ? new Color(0.42f, 1f, 0.69f) : CorFitaA;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(cor, 0f), new GradientColorKey(cor, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 1f) });
            lr.colorGradient = grad;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(1f, 1f));
            lr.enabled = false;
            StartCoroutine(CorrerTraco(lr, k % 2, k * 0.33f, k == 1 ? -0.17f : 0.2f));
        }
    }

    IEnumerator CorrerTraco(LineRenderer lr, int fita, float fase, float velocidade)
    {
        yield return new WaitForSeconds(2.4f);
        if (lr != null) lr.enabled = true;
        float u = fase;
        while (lr != null)
        {
            u = Mathf.Repeat(u + Time.deltaTime * velocidade, 1f);
            for (int i = 0; i < lr.positionCount; i++)
                lr.SetPosition(i, PontoFita(Mathf.Repeat(u - (lr.positionCount - 1 - i) * 0.006f * Mathf.Sign(velocidade), 1f), fita));
            yield return null;
        }
    }

    // surge de baixo para cima
    IEnumerator Surgir(Transform raiz)
    {
        var fim = raiz.localScale;
        for (float t = 0; t < 1.0f && raiz != null; t += Time.deltaTime)
        {
            float x = Mathf.SmoothStep(0f, 1f, t / 1.0f);
            raiz.localScale = new Vector3(fim.x * (0.7f + 0.3f * x), fim.y * Mathf.Max(0.05f, x), fim.z * (0.7f + 0.3f * x));
            yield return null;
        }
        if (raiz != null) raiz.localScale = fim;
    }
}
