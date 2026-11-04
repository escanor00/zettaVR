using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Testes automáticos da Zetta. Ficam como harness de Editor (menu Zetta > Rodar testes) porque os assemblies de teste da Unity
// não enxergam o Assembly-CSharp. Cobrem registro de objetos, notas, formas matemáticas, diário, estado e (em Play) as ferramentas de voz.
// O resultado vai para o Console e para Docs/TESTES-ZETTA.md.
public static class TestesZetta
{
    static readonly List<(string nome, bool ok, string detalhe)> res = new List<(string, bool, string)>();

    static void T(string nome, Func<string> corpo)
    {
        try { var r = corpo(); res.Add((nome, r == null, r ?? "")); }
        catch (Exception e) { res.Add((nome, false, e.GetType().Name + ": " + e.Message)); }
    }

    [MenuItem("Zetta/Rodar testes")]
    public static void Rodar()
    {
        res.Clear();

        T("HoloRegistro: adicionar, buscar e remover", () =>
        {
            var go = new GameObject("t_holo");
            try
            {
                int antes = HoloRegistro.Itens.Count;
                var it = HoloRegistro.Adicionar(go, "cubo", "azul", "teste");
                if (HoloRegistro.Itens.Count != antes + 1) return "contagem não subiu";
                if (HoloRegistro.Buscar(go) != it) return "Buscar falhou";
                if (HoloRegistro.BuscarPorId(it.id) != it) return "BuscarPorId falhou";
                HoloRegistro.Remover(go);
                return HoloRegistro.Itens.Count == antes ? null : "Remover não retirou";
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        });

        T("HoloRegistro: ids crescentes e eventos", () =>
        {
            var a = new GameObject("t_a"); var b = new GameObject("t_b");
            int ev = 0; Action<GameObject> add = g => ev++, rem = g => ev += 10;
            HoloRegistro.aoAdicionar += add; HoloRegistro.aoRemover += rem;
            try
            {
                var ia = HoloRegistro.Adicionar(a, "x", "", ""); var ib = HoloRegistro.Adicionar(b, "x", "", "");
                HoloRegistro.Remover(a); HoloRegistro.Remover(b);
                if (ib.id <= ia.id) return "ids não crescem";
                return ev == 22 ? null : "eventos = " + ev;
            }
            finally { HoloRegistro.aoAdicionar -= add; HoloRegistro.aoRemover -= rem; UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        });

        T("HoloRegistro: Resumo lista os itens", () =>
        {
            var go = new GameObject("t_r");
            try
            {
                HoloRegistro.Adicionar(go, "esfera", "vermelha", "bola de teste");
                bool achou = HoloRegistro.Resumo().ToString().Contains("bola de teste");
                HoloRegistro.Remover(go);
                return achou ? null : "descrição ausente do resumo";
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        });

        T("Notas: LerDados com JSON", () =>
        {
            var it = new HoloItem { tipo = "nota", extra = "{\"t\":\"comprar pão\",\"f\":1.5,\"c\":\"dourado\"}" };
            var d = NotasHolo.LerDados(it);
            if (d.texto != "comprar pão") return "texto = " + d.texto;
            return Mathf.Approximately(d.letra, 1.5f) ? null : "letra = " + d.letra;
        });

        T("Notas: LerDados com extra vazio ou quebrado", () =>
        {
            var a = NotasHolo.LerDados(new HoloItem { tipo = "nota", extra = "" });
            var b = NotasHolo.LerDados(new HoloItem { tipo = "nota", extra = "{quebrado" });
            return a != null && b != null ? null : "retornou nulo";
        });

        foreach (var forma in new[] { "mobius", "klein", "no_toroidal", "dna", "toro", "fractal" })
        {
            string f = forma;
            T("FormasMatematicas: malha " + f, () =>
            {
                var m = FormasMatematicas.Gerar(f);
                if (m == null || m.vertexCount < 50) return "malha vazia";
                var tam = m.bounds.size;
                float maior = Mathf.Max(tam.x, Mathf.Max(tam.y, tam.z));
                if (float.IsNaN(maior) || maior < 0.5f || maior > 1.2f) return "tamanho fora da caixa de 1 m: " + maior;
                return m.bounds.center.magnitude < 0.2f ? null : "não centrada: " + m.bounds.center;
            });
        }

        T("DiarioZetta: registrar e ler", () =>
        {
            string chave = "zetta_diario_" + DateTime.Now.ToString("yyyyMMdd");
            string salvo = PlayerPrefs.GetString(chave, "");
            DiarioZetta.Registrar("teste", "entrada de teste");
            var lista = DiarioZetta.Hoje();
            bool ok = lista.Count > 0 && lista[lista.Count - 1].texto == "entrada de teste";
            if (lista.Count > 0) lista.RemoveAt(lista.Count - 1);
            if (salvo.Length == 0) PlayerPrefs.DeleteKey(chave); else PlayerPrefs.SetString(chave, salvo);
            return ok ? null : "entrada não encontrada";
        });

        T("EstadoZetta: transições disparam o evento", () =>
        {
            var ev = new List<string>(); Action<EstadoVoz, EstadoVoz> h = (a, b) => ev.Add(a + ">" + b);
            var inicial = EstadoZetta.Atual;
            EstadoZetta.aoMudar += h;
            try
            {
                EstadoZetta.ForcarParaTeste(EstadoVoz.Ouvindo); EstadoZetta.ForcarParaTeste(EstadoVoz.Pensando); EstadoZetta.ForcarParaTeste(EstadoVoz.Pensando);
                return ev.Count >= 1 && EstadoZetta.Atual == EstadoVoz.Pensando ? null : string.Join(",", ev);
            }
            finally { EstadoZetta.aoMudar -= h; EstadoZetta.ForcarParaTeste(inicial); }
        });

        T("Cache 3D: chave normalizada e ida e volta", () =>
        {
            if (GeradorModelo3D.ChaveCache("Um  GATO, Sentado!") != GeradorModelo3D.ChaveCache("um gato sentado")) return "chaves diferentes";
            if (GeradorModelo3D.ChaveCache("Cão") != "cao") return "acento não removido";
            string pasta = System.IO.Path.Combine(Application.persistentDataPath, "Modelos3D");
            System.IO.Directory.CreateDirectory(pasta);
            string arq = System.IO.Path.Combine(pasta, "teste_cache.glb");
            System.IO.File.WriteAllBytes(arq, new byte[] { 1 });
            string desc = "objeto de teste do cache zzz";
            try
            {
                if (GeradorModelo3D.TentarCache(desc, out _)) return "achou antes de guardar";
                GeradorModelo3D.GuardarNoCache(desc, arq);
                if (!GeradorModelo3D.TentarCache("Objeto de TESTE do cache zzz!", out var c) || c != arq) return "não achou depois de guardar";
                System.IO.File.Delete(arq);
                return GeradorModelo3D.TentarCache(desc, out _) ? "entrada de arquivo sumido não foi limpa" : null;
            }
            finally { if (System.IO.File.Exists(arq)) System.IO.File.Delete(arq); }
        });

        T("Ajustes: limites, gravação e restaurar", () =>
        {
            AjustesZetta.Definir("velocidade", 99f);
            if (!Mathf.Approximately(AjustesZetta.Velocidade, 2.5f)) return "não limitou o máximo: " + AjustesZetta.Velocidade;
            AjustesZetta.Definir("tamanho", 0.01f);
            if (!Mathf.Approximately(AjustesZetta.TamanhoEsfera, 0.6f)) return "não limitou o mínimo";
            if (!Mathf.Approximately(PlayerPrefs.GetFloat("zetta_aj_velocidade"), 2.5f)) return "não gravou no PlayerPrefs";
            AjustesZetta.Restaurar();
            return Mathf.Approximately(AjustesZetta.Velocidade, 1f) && Mathf.Approximately(AjustesZetta.TamanhoEsfera, 1f) ? null : "restaurar falhou";
        });

        T("Regras: gatilho, validação e armazenamento", () =>
        {
            if (!RegrasHolo.Casa("Zetta, boa noite!", "Boa noite")) return "não casou frase com acento/pontuação";
            if (RegrasHolo.Casa("boa noitada", "boa noite")) return "casou palavra parcial";
            var ok = new List<RegrasHolo.Passo> { new RegrasHolo.Passo { ferramenta = "modo_foco", args = "{\"acao\":\"entrar\"}" } };
            Func<string, bool> existe = n => n == "modo_foco";
            if (RegrasHolo.Validar("boa noite", ok, existe) != null) return "regra válida rejeitada";
            if (RegrasHolo.Validar("boa noite", new List<RegrasHolo.Passo> { new RegrasHolo.Passo { ferramenta = "criar_regra" } }, n => true) == null) return "aceitou ferramenta proibida";
            if (RegrasHolo.Validar("boa noite", new List<RegrasHolo.Passo> { new RegrasHolo.Passo { ferramenta = "x" } }, existe) == null) return "aceitou ferramenta inexistente";
            if (RegrasHolo.Validar("boa noite", new List<RegrasHolo.Passo> { new RegrasHolo.Passo { ferramenta = "modo_foco", args = "{quebrado" } }, existe) == null) return "aceitou JSON inválido";
            string salvo = PlayerPrefs.GetString("zetta_regras", "[]");
            try
            {
                RegrasHolo.Regras.Clear();
                RegrasHolo.Adicionar(new RegrasHolo.Regra { quando = "Teste Zz", passos = ok });
                RegrasHolo.Adicionar(new RegrasHolo.Regra { quando = "teste zz", passos = ok });   // substitui, não duplica
                return RegrasHolo.Regras.Count == 1 && PlayerPrefs.GetString("zetta_regras").Contains("modo_foco") ? null : "contagem = " + RegrasHolo.Regras.Count;
            }
            finally
            {
                PlayerPrefs.SetString("zetta_regras", salvo);
                RegrasHolo.Regras.Clear();
                RegrasHolo.Regras.AddRange(Newtonsoft.Json.JsonConvert.DeserializeObject<List<RegrasHolo.Regra>>(salvo));
            }
        });

        T("Alertas: sistema pelo script da pilha", () =>
        {
            string a;
            if (AlertasErros.Sistema("x", "AssistenteVoz.Conexao.TratarMensagem () (at Assets/AssistenteVoz.Conexao.cs:200)", out a) != "voz") return "voz: " + a;
            if (AlertasErros.Sistema("x", "(at Assets/GeradorModelo3D.cs:120)", out a) != "modelos 3D") return "3D";
            if (AlertasErros.Sistema("x", "(at Assets/EsferaZetta.cs:77)", out a) != "esfera") return "esfera";
            if (AlertasErros.Sistema("x", "(at Assets/AlertasErros.cs:1)", out a) != "motor Unity") return "deveria ignorar o próprio arquivo";
            return AlertasErros.Sistema("sem pilha", "", out a) == "motor Unity" ? null : "sem pilha";
        });

        T("AssistenteVoz: classes parciais mantêm os métodos", () =>
        {
            var t = typeof(AssistenteVoz);
            foreach (var n in new[] { "RegistrarFerramenta", "Falar", "Interromper", "EnviarImagem" })
                if (t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public) == null) return "falta " + n;
            return null;
        });

        T("Partes: malha dividida em 6 faces e cores relativas", () =>
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var malha = tmp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(tmp);
            var p = MalhaPartes.Dividir(malha, new[] { "a", "b", "c", "d", "e", "f" }, (c, n, v) =>
            {
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                if (az >= ax && az >= ay) return n.z > 0 ? 0 : 1;
                return ax >= ay ? (n.x > 0 ? 2 : 3) : (n.y > 0 ? 4 : 5);
            });
            if (p.Count != 6) return "faces = " + p.Count;
            foreach (var x in p) if (x.malha.triangles.Length != 6) return "face sem 2 triângulos";
            var claro = CriadorHolograma.CorDeEdicao("mais claro", new Color(0.2f, 0.2f, 0.8f));
            var escuro = CriadorHolograma.CorDeEdicao("azul mais escuro", Color.red);
            if (claro.r <= 0.2f) return "mais claro não clareou";
            if (escuro.b >= 0.9f) return "azul mais escuro não escureceu";
            return CriadorHolograma.TentarCor("vermelhas", out _) ? null : "plural/feminino de cor";
        });

        T("Matemática: parser seguro (precedência, implícito, erros)", () =>
        {
            Func<string, double> av = s => { if (!ExpressaoMatematica.Tentar(s, out var e, out var er)) throw new Exception(s + ": " + er); return e.Avaliar(3, 4, 0); };
            if (av("-2^2") != -4) return "-2^2";
            if (av("2^3^2") != 512) return "2^3^2";
            if (av("2x+1") != 7) return "2x+1";
            if (Math.Abs(av("raiz(x^2+y^2)") - 5) > 1e-9) return "raiz";
            if (Math.Abs(av("sen(pi/2)") - 1) > 1e-9) return "sen";
            foreach (var ruim in new[] { "foo(1)", "1+", "(", "x$", "" , "System.Exit(0)" })
                if (ExpressaoMatematica.Tentar(ruim, out _, out _)) return "aceitou '" + ruim + "'";
            return null;
        });

        if (EditorApplication.isPlaying)
        {
            T("Ferramentas: todas registradas", () =>
            {
                var voz = UnityEngine.Object.FindAnyObjectByType<AssistenteVoz>();
                if (voz == null) return "AssistenteVoz ausente";
                var campo = typeof(AssistenteVoz).GetField("ferramentas", BindingFlags.Instance | BindingFlags.NonPublic);
                var dic = campo != null ? campo.GetValue(voz) as System.Collections.IDictionary : null;
                if (dic == null) return "dicionário de ferramentas não encontrado";
                foreach (var n in new[] { "criar_forma_matematica", "vista_explodida", "criar_grafico", "bom_dia", "modo_foco", "linha_do_tempo", "etiquetas_sala", "portal_clima", "apresentar_modelo", "abrir_central", "fechar_central", "pintar_parte", "partes_objeto", "visibilidade_parte", "girar_objeto", "acao_objeto", "mostrar_dna", "desenhar_superficie", "grafico_funcoes", "resolver_passo_a_passo", "calcular", "calculadora_holografica", "geometria_interativa", "modo_estrutura" })
                    if (!dic.Contains(n)) return "não registrada: " + n;
                return dic.Count >= 30 ? null : "só " + dic.Count + " ferramentas";
            });
        }
        else res.Add(("Ferramentas registradas (requer Play)", true, "ignorado fora do Play Mode"));

        Relatar();
    }

    static void Relatar()
    {
        var sb = new StringBuilder();
        int falhas = 0;
        sb.AppendLine("# Testes automáticos da Zetta");
        sb.AppendLine();
        sb.AppendLine("Gerado por `Zetta > Rodar testes` em " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ".");
        sb.AppendLine();
        foreach (var r in res)
        {
            if (!r.ok) falhas++;
            sb.AppendLine("- " + (r.ok ? "OK   " : "FALHA") + " — " + r.nome + (r.detalhe.Length > 0 ? " (" + r.detalhe + ")" : ""));
        }
        sb.AppendLine();
        sb.AppendLine((res.Count - falhas) + "/" + res.Count + " passaram.");
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Docs", "TESTES-ZETTA.md"), sb.ToString(), new UTF8Encoding(false));
        if (falhas == 0) Debug.Log("[TestesZetta] " + res.Count + "/" + res.Count + " testes passaram.");
        else Debug.LogError("[TestesZetta] " + falhas + " falha(s). Veja Docs/TESTES-ZETTA.md");
    }
}
