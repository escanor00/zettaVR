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

        T("AssistenteVoz: classes parciais mantêm os métodos", () =>
        {
            var t = typeof(AssistenteVoz);
            foreach (var n in new[] { "RegistrarFerramenta", "Falar", "Interromper", "EnviarImagem" })
                if (t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public) == null) return "falta " + n;
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
                foreach (var n in new[] { "criar_forma_matematica", "vista_explodida", "criar_grafico", "bom_dia", "modo_foco", "linha_do_tempo", "etiquetas_sala", "portal_clima", "apresentar_modelo", "abrir_central", "fechar_central" })
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
