using System.Collections;
using TMPro;
using UnityEngine;

// Helpers de visual holográfico compartilhados (painéis, texto, animações).
public static class HoloUtil
{
    // as cores vêm do tema (TemaZetta); estes nomes ficam para os scripts que já os usam
    public static readonly Color Ciano = TemaZetta.AzulTec;

    static Material matSprite;
    public static Material NovoMaterialSprite(Color cor)
    {
        if (matSprite == null) matSprite = new Material(Shader.Find("Sprites/Default"));
        var m = new Material(matSprite);
        m.color = cor;
        return m;
    }

    static Shader shaderAditivo;
    // Material aditivo com fresnel (shader Zetta/HoloAditivo, em Resources); cai no Sprites/Default se faltar.
    public static Material NovoMaterialAditivo(Color cor, float baseFixa = 0.5f, float borda = 1f)
    {
        if (shaderAditivo == null) shaderAditivo = Shader.Find("Zetta/HoloAditivo");
        if (shaderAditivo == null) return NovoMaterialSprite(cor);
        var m = new Material(shaderAditivo) { color = cor };
        m.SetFloat("_Base", baseFixa);
        m.SetFloat("_Rim", borda);
        return m;
    }

    public static TextMeshPro CriarTexto(Transform pai, string nome, string conteudo, Vector3 posLocal,
        Vector2 caixa, float tamanhoMax, Color cor, TextAlignmentOptions alinhamento = TextAlignmentOptions.Center)
    {
        var go = new GameObject(nome);
        go.transform.SetParent(pai, false);
        go.transform.localPosition = posLocal;
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = caixa;
        t.alignment = alinhamento;
        t.color = cor;
        if (TemaZetta.Fonte != null) t.font = TemaZetta.Fonte;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Truncate;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.03f;
        t.fontSizeMax = tamanhoMax;
        t.text = conteudo;
        t.sortingOrder = TemaZetta.OrdemTexto;
        return t;
    }

    // Painel de vidro holográfico (translucidez + borda + gradiente, tudo no shader). Origem no centro, olhando para +Z.
    public static Transform CriarPainel(Transform pai, string nome, Vector2 tamanho, int ordem = 0)
    {
        var raiz = new GameObject(nome).transform;
        raiz.SetParent(pai, false);

        var rend = NovaQuad(raiz, "Fundo", tamanho, TemaZetta.NovoVidro(tamanho), 0f, ordem);

        // espessura: segunda lâmina atrás do vidro (só em painéis grandes), mais fraca e sem aura
        if (Mathf.Min(tamanho.x, tamanho.y) > 0.08f)
        {
            var mt = TemaZetta.NovoVidro(tamanho);
            mt.SetColor("_CorTopo", TemaZetta.ComAlfa(TemaZetta.AzulTec, 0.10f));
            mt.SetColor("_CorBase", TemaZetta.ComAlfa(TemaZetta.AzulProfundo, 0.25f));
            mt.SetFloat("_Aura", 0f);
            mt.SetFloat("_Brilho", 0.45f);
            NovaQuad(raiz, "Espessura", tamanho, mt, 0.009f, ordem - 1);
        }

        var vidro = raiz.gameObject.AddComponent<PainelVidro>();
        vidro.material = rend.material;
        vidro.tamanho = tamanho;
        return raiz;
    }

    static Renderer NovaQuad(Transform pai, string nome, Vector2 tamanho, Material mat, float z, int ordem)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = nome;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(pai, false);
        q.transform.localPosition = new Vector3(0, 0, z);
        float m = TemaZetta.MargemAura * 2f;
        q.transform.localScale = new Vector3(tamanho.x + m, tamanho.y + m, 1f);   // sobra para a aura
        var r = q.GetComponent<Renderer>();
        r.material = mat;
        r.sortingOrder = ordem;   // vidro < botões < textos e ícones, independente da distância ao centro
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    // Onde nasce um objeto novo: ~0,8 m à frente, na altura dos olhos (2 cm abaixo), dentro da visão; com a mesa holográfica
    // ativa, sobre ela. Se já há objetos por perto tenta outros lugares dentro da visão (lados, depois acima/abaixo).
    public static Vector3 PontoCriacao(float tamanho = 0.3f, float distancia = 0.8f)
    {
        var cam = Camera.main.transform;
        var mesa = Object.FindAnyObjectByType<MesaHolografica>();
        bool naMesa = mesa != null && mesa.ativa;
        var yaw = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        var direita = yaw * Vector3.right;
        Vector3 Candidato(float graus, float subir)
        {
            if (naMesa) return mesa.Centro + Vector3.up * (tamanho * 0.5f + 0.04f) + direita * (graus / 20f * 0.3f);
            // segue o olhar na horizontal; a inclinação da cabeça só conta de 5° para cima a 12° para baixo (um pouco abaixo dos olhos)
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, cam.eulerAngles.x), -5f, 12f);   // eulerAngles.x > 0 = olhando para baixo
            return cam.position + Quaternion.Euler(pitch, cam.eulerAngles.y + graus, 0f) * Vector3.forward * distancia + Vector3.up * (-0.02f + subir);
        }
        float[,] tentativas = { { 0, 0 }, { 20, 0 }, { -20, 0 }, { 0, 0.3f }, { 35, 0 }, { -35, 0 }, { 20, 0.3f }, { -20, 0.3f } };
        Vector3 melhor = Candidato(0, 0); float melhorFolga = -1f;
        for (int t = 0; t < tentativas.GetLength(0); t++)
        {
            var c = Candidato(tentativas[t, 0], tentativas[t, 1]);
            // o objeto (com folga para rótulos) tem de caber inteiro na visão
            var cv = Camera.main.WorldToViewportPoint(c);
            var ev = Camera.main.WorldToViewportPoint(c + Camera.main.transform.right * tamanho * 0.6f + Camera.main.transform.up * tamanho * 0.6f);
            var meiaV = new Vector2(Mathf.Abs(ev.x - cv.x), Mathf.Abs(ev.y - cv.y));
            if (cv.z <= 0f || cv.x - meiaV.x < 0.08f || cv.x + meiaV.x > 0.92f || cv.y - meiaV.y < 0.08f || cv.y + meiaV.y > 0.92f) continue;
            float folga = float.MaxValue;
            foreach (var it in HoloRegistro.Itens)
            {
                if (it.go == null) continue;
                float outro = Mathf.Min(1.2f, InteracaoMaos.MaiorDimensao(it.go));
                folga = Mathf.Min(folga, Vector3.Distance(c, it.go.transform.position) - (tamanho + outro) * 0.55f);
            }
            if (folga >= 0f) return c;
            if (folga > melhorFolga) { melhorFolga = folga; melhor = c; }
        }
        return melhor;
    }

    // Ponto à frente da câmera, em coordenadas locais da vista (x direita, y cima, z frente), com yaw apenas.
    public static Vector3 PontoNaVista(Vector3 local)
    {
        var cam = Camera.main.transform;
        return cam.position + Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * local;
    }

    public static Quaternion OlharParaUsuario(Vector3 pos)
    {
        var cam = Camera.main.transform;
        var d = Vector3.ProjectOnPlane(pos - cam.position, Vector3.up);
        if (d.sqrMagnitude < 0.0001f) d = cam.forward;
        return Quaternion.LookRotation(d, Vector3.up);
    }

    // multiplica o alfa de todos os renderers e textos sob 'raiz'
    public static void DefinirAlfa(Transform raiz, float a)
    {
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r is TextMeshPro || r.GetComponent<TextMeshPro>() != null) continue;
            var m = r.material;
            if (m.HasProperty("_Color")) { var c = m.color; if (!baseAlfa.ContainsKey(m)) baseAlfa[m] = c.a; c.a = baseAlfa[m] * a; m.color = c; }
        }
        foreach (var t in raiz.GetComponentsInChildren<TextMeshPro>(true))
        {
            var c = t.color; c.a = a; t.color = c;
        }
    }
    static readonly System.Collections.Generic.Dictionary<Material, float> baseAlfa = new System.Collections.Generic.Dictionary<Material, float>();

    public static IEnumerator Surgir(Transform alvo, float atraso = 0f, float duracao = TemaZetta.TempoEntrada)
    {
        alvo.localScale = Vector3.zero;
        if (atraso > 0f) yield return new WaitForSeconds(atraso);
        for (float t = 0; t < duracao; t += Time.deltaTime)
        {
            float x = t / duracao;
            float e = TemaZetta.EaseOutBack(x);
            alvo.localScale = new Vector3(e, Mathf.Clamp01(x * 2f) * e, 1f);
            yield return null;
        }
        alvo.localScale = Vector3.one;
    }
}

// Evita que rótulos (nome, dados, % de cálculo, painéis) se sobreponham entre si, aos objetos, à esfera/balão da Zetta
// ou saiam da visão. Cada rótulo pede um lugar a cada quadro; os que chegam depois desviam dos retângulos já ocupados na tela.
public static class RotulosLivres
{
    static readonly Vector2[] passos = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0), new Vector2(1, 1), new Vector2(-1, 1),
                                         new Vector2(1, -1), new Vector2(-1, -1), new Vector2(0, 2), new Vector2(0, -2), new Vector2(2, 0), new Vector2(-2, 0), new Vector2(0, 3), new Vector2(0, -3), new Vector2(3, 0), new Vector2(-3, 0) };
    static int quadro = -1;
    static readonly System.Collections.Generic.List<Rect> usados = new System.Collections.Generic.List<Rect>();
    static readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameObject, Rect>> corpos = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameObject, Rect>>();

    static void Iniciar(Camera cam)
    {
        if (quadro == Time.frameCount) return;
        quadro = Time.frameCount;
        usados.Clear();
        corpos.Clear();
        // reserva: esfera da Zetta (canto inferior direito) e o balão de resposta à esquerda dela
        var anel = Object.FindAnyObjectByType<AnelHolografico>();
        if (anel != null)
        {
            var v = cam.WorldToViewportPoint(anel.AlvoPosicao(cam.transform));
            if (v.z > 0f) usados.Add(new Rect(v.x - 0.34f, v.y - 0.12f, 0.44f, 0.24f));
        }
        // o corpo de cada objeto também ocupa a tela (reduzido a 90%)
        foreach (var it in HoloRegistro.Itens)
        {
            if (it.go == null || !InteracaoMaos.Limites(it.go, out var b)) continue;
            float x0 = 2f, y0 = 2f, x1 = -1f, y1 = -1f; bool ok = true;
            for (int k = 0; k < 8; k++)
            {
                var p = cam.WorldToViewportPoint(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                if (p.z <= 0.05f) { ok = false; break; }
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y);
            }
            if (!ok) continue;
            var r = new Rect(x0, y0, x1 - x0, y1 - y0);
            r.center = r.center; r.size *= 0.9f;
            corpos.Add(new System.Collections.Generic.KeyValuePair<GameObject, Rect>(it.go, r));
        }
    }

    // 'tamanho' em unidades de viewport (0..1); 'dono' = objeto a que o rótulo pertence (o corpo dele conta menos).
    // Devolve a posição de mundo ajustada, na mesma profundidade.
    public static Vector3 Resolver(Camera cam, Vector3 mundo, Vector2 tamanho, GameObject dono = null)
    {
        Iniciar(cam);
        var v = cam.WorldToViewportPoint(mundo);
        if (v.z <= 0.05f) return mundo;
        var meia = tamanho * 0.5f;
        Rect melhor = default; float menorBatida = float.MaxValue;
        foreach (var p in passos)
        {
            float x = Mathf.Clamp(v.x + p.x * (tamanho.x + 0.01f), 0.05f + meia.x, 0.95f - meia.x);
            float y = Mathf.Clamp(v.y + p.y * (tamanho.y + 0.012f), 0.08f + meia.y, 0.92f - meia.y);
            var r = new Rect(x - meia.x, y - meia.y, tamanho.x, tamanho.y);
            float batida = 0f;
            foreach (var u in usados) if (r.Overlaps(u)) batida += 1f;
            foreach (var c in corpos) if (c.Key != dono && r.Overlaps(c.Value)) batida += 1f;
            if (dono != null) foreach (var c in corpos) if (c.Key == dono && r.Overlaps(c.Value)) batida += 0.7f;
            if (batida < menorBatida) { menorBatida = batida; melhor = r; }
            if (batida == 0f) break;
        }
        usados.Add(melhor);
        return cam.ViewportToWorldPoint(new Vector3(melhor.x + meia.x, melhor.y + meia.y, v.z));
    }

    // tamanho aproximado de um texto na tela, em viewport
    public static Vector2 TamanhoNaTela(Camera cam, TMPro.TMP_Text t)
    {
        var b = t.textBounds;   // local ao texto
        var a = cam.WorldToViewportPoint(t.transform.TransformPoint(b.min));
        var c = cam.WorldToViewportPoint(t.transform.TransformPoint(b.max));
        return new Vector2(Mathf.Clamp(Mathf.Abs(c.x - a.x), 0.03f, 0.5f), Mathf.Clamp(Mathf.Abs(c.y - a.y), 0.02f, 0.3f));
    }
}
