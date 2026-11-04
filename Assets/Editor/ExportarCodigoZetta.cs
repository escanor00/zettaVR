using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Empacota uma cópia SOMENTE LEITURA do código da Zetta no build, para o visualizador de código e o mapa 3D do projeto:
//  - Assets/Resources/Codigo/<Nome>.txt  (cada Assets/*.cs, sem a pasta Editor nem Segredos; chaves/tokens são apagados)
//  - Assets/Resources/Codigo/_mapa.txt   (JSON: arquivos, tamanho e dependências entre eles, gerado aqui no Editor)
// Roda sozinho antes de cada build e no menu Zetta > Exportar código. A pasta é gerada: está no .gitignore.
public class ExportarCodigoZetta : IPreprocessBuildWithReport
{
    const string Pasta = "Assets/Resources/Codigo";
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport r) { Exportar(); }

    // padrões que parecem segredo: tokens do GitHub/HF, chaves do Google, cabeçalhos Bearer e atribuições de "token/key/senha" com literal
    static readonly Regex[] Segredos =
    {
        new Regex(@"gh[pousr]_[A-Za-z0-9]{20,}"),
        new Regex(@"github_pat_[A-Za-z0-9_]{20,}"),
        new Regex(@"hf_[A-Za-z0-9]{20,}"),
        new Regex(@"AIza[A-Za-z0-9_\-]{30,}"),
        new Regex(@"sk-[A-Za-z0-9]{20,}"),
        new Regex(@"Bearer\s+[A-Za-z0-9._\-]{20,}"),
    };
    static readonly Regex AtribuicaoSegredo = new Regex(@"(?i)((token|apikey|api_key|senha|password|secret)\w*\s*=\s*)""[^""\r\n]{12,}""");

    public static string Limpar(string texto)
    {
        foreach (var rx in Segredos) texto = rx.Replace(texto, "[removido]");
        return AtribuicaoSegredo.Replace(texto, "$1\"[removido]\"");
    }

    [MenuItem("Zetta/Exportar código")]
    public static void Exportar()
    {
        Directory.CreateDirectory(Pasta);
        foreach (var antigo in Directory.GetFiles(Pasta, "*.txt")) File.Delete(antigo);

        var arquivos = Directory.GetFiles("Assets", "*.cs", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToList();
        var textos = new Dictionary<string, string>();
        foreach (var f in arquivos)
        {
            string nome = Path.GetFileNameWithoutExtension(f);
            textos[nome] = Limpar(File.ReadAllText(f, Encoding.UTF8));
            File.WriteAllText(Path.Combine(Pasta, nome + ".txt"), textos[nome], new UTF8Encoding(false));
        }

        // tipos declarados por arquivo e dependências: A depende de B se o texto de A cita um tipo declarado em B
        var decl = new Regex(@"\b(?:class|struct|enum|interface)\s+([A-Z]\w+)");
        var tipos = new Dictionary<string, string>();   // tipo → arquivo
        foreach (var kv in textos)
            foreach (Match m in decl.Matches(kv.Value))
                if (!tipos.ContainsKey(m.Groups[1].Value)) tipos[m.Groups[1].Value] = kv.Key;

        var nos = new List<object>();
        var arestas = new List<string[]>();
        foreach (var kv in textos)
        {
            var citados = new HashSet<string>();
            foreach (var t in tipos)
            {
                if (t.Value == kv.Key || t.Key.Length < 5) continue;
                if (Regex.IsMatch(kv.Value, @"\b" + Regex.Escape(t.Key) + @"\b")) citados.Add(t.Value);
            }
            foreach (var alvo in citados) arestas.Add(new[] { kv.Key, alvo });
            nos.Add(new { n = kv.Key, l = kv.Value.Split('\n').Length });
        }
        File.WriteAllText(Path.Combine(Pasta, "_mapa.txt"), JsonConvert.SerializeObject(new { nos, arestas }), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log("[ExportarCodigoZetta] " + textos.Count + " arquivos, " + arestas.Count + " dependências em " + Pasta);
    }
}
