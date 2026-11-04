using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Ferramenta "modelos_salvos": lista os .glb gerados e salvos no óculos e coloca um deles de novo na sala.
public class BibliotecaModelos : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""modelos_salvos"",
  ""description"": ""Biblioteca dos modelos 3D realistas já gerados e salvos no óculos. acao=listar diz quais existem; acao=mostrar coloca um deles na sala (pelo número da lista ou por parte do nome)."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""acao"": { ""type"": ""STRING"", ""enum"": [""listar"", ""mostrar""] },
      ""numero"": { ""type"": ""INTEGER"", ""description"": ""Posição na lista (1 = mais recente). Para acao=mostrar."" },
      ""nome"": { ""type"": ""STRING"", ""description"": ""Parte do nome do modelo. Para acao=mostrar."" }
    },
    ""required"": [""acao""]
  }
}";
    const string Instrucao =
        "'Zetta, mostre meus modelos' → modelos_salvos acao=listar e leia os nomes em poucas palavras. 'Mostre o <nome>' ou 'o segundo' → acao=mostrar.";

    string Pasta => Path.Combine(Application.persistentDataPath, "Modelos3D");

    void Awake()
    {
        var voz = FindAnyObjectByType<AssistenteVoz>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Executar, Instrucao);
    }

    struct Modelo { public string caminho, nome; }

    List<Modelo> Listar()
    {
        var r = new List<Modelo>();
        if (!Directory.Exists(Pasta)) return r;
        foreach (var f in new DirectoryInfo(Pasta).GetFiles("*.glb").OrderByDescending(x => x.LastWriteTimeUtc))
        {
            string n = Path.GetFileNameWithoutExtension(f.Name);
            int i = n.LastIndexOf('_');
            if (i > 0) n = n.Substring(0, i);
            r.Add(new Modelo { caminho = f.FullName, nome = n.Replace('-', ' ') });
        }
        return r;
    }

    JObject Executar(JObject a)
    {
        var lista = Listar();
        if (lista.Count == 0) return new JObject { ["result"] = "ainda não há modelos salvos; peça para gerar um com gerar_modelo_3d" };
        string acao = (a.Value<string>("acao") ?? "").ToLowerInvariant();
        if (acao == "listar")
        {
            var arr = new JArray();
            for (int i = 0; i < Mathf.Min(lista.Count, 12); i++) arr.Add((i + 1) + ". " + lista[i].nome);
            return new JObject { ["modelos"] = arr, ["total"] = lista.Count };
        }
        if (acao != "mostrar") return new JObject { ["erro"] = "acao inválida" };

        Modelo? escolhido = null;
        int? n = a.Value<int?>("numero");
        string parte = (a.Value<string>("nome") ?? "").Trim().ToLowerInvariant();
        if (n.HasValue && n.Value >= 1 && n.Value <= lista.Count) escolhido = lista[n.Value - 1];
        else if (parte.Length > 0) escolhido = lista.Where(m => m.nome.ToLowerInvariant().Contains(parte)).Select(m => (Modelo?)m).FirstOrDefault();
        else escolhido = lista[0];
        if (escolhido == null) return new JObject { ["erro"] = "não achei esse modelo; use listar para ver os nomes" };

        var gerador = FindAnyObjectByType<GeradorModelo3D>();
        if (gerador == null) return new JObject { ["erro"] = "carregador de modelos indisponível" };
        var pos = HoloUtil.PontoCriacao(gerador.tamanhoFinal);
        gerador.CriarRaizGlb(pos, HoloUtil.OlharParaUsuario(pos), escolhido.Value.caminho, escolhido.Value.nome, gerador.tamanhoFinal);
        return new JObject { ["result"] = "mostrando " + escolhido.Value.nome };
    }
}
