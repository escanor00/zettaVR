using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class HoloItem
{
    public int id;                      // número curto para a Zetta se referir ao objeto
    public GameObject go;
    public string forma;
    public string cor;
    public string descricao;
    public string tipo = "primitiva"; // primitiva | modelo | glb
    public float tamanho = 0.15f;
    public string extra = "";          // nome do modelo ou caminho do .glb
}

// Registro central dos hologramas criados (usado por apagar, âncoras, física, mesa, etc.).
public static class HoloRegistro
{
    public static readonly List<HoloItem> Itens = new List<HoloItem>();
    static int proximoId;
    // true enquanto objetos salvos são recriados (a mesa holográfica não os faz subir)
    public static bool Carregando;
    public static event Action<GameObject> aoAdicionar;
    public static event Action<GameObject> aoRemover;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Itens.Clear();
        proximoId = 0;
        Carregando = false;
        aoAdicionar = null;
        aoRemover = null;
    }

    public static HoloItem Adicionar(GameObject go, string forma, string cor, string descricao,
        string tipo = "primitiva", float tamanho = 0.15f, string extra = "")
    {
        Itens.RemoveAll(i => i.go == null);
        var item = new HoloItem { id = ++proximoId, go = go, forma = forma ?? "", cor = cor ?? "", descricao = (descricao ?? "").Trim(), tipo = tipo, tamanho = tamanho, extra = extra ?? "" };
        Itens.Add(item);
        aoAdicionar?.Invoke(go);
        return item;
    }

    public static void Remover(GameObject go)
    {
        Itens.RemoveAll(i => i.go == null || i.go == go);
        aoRemover?.Invoke(go);
    }

    public static HoloItem Buscar(GameObject go) => Itens.Find(i => i.go == go);
    public static HoloItem BuscarPorId(int id) => Itens.Find(i => i.go != null && i.id == id);

    // Resumo de tudo que existe (id, tipo, cor, tamanho, nome curto): é o que a Gemini recebe para achar o objeto pela descrição.
    public static JArray Resumo()
    {
        var arr = new JArray();
        foreach (var i in Itens)
        {
            if (i.go == null) continue;
            arr.Add(new JObject
            {
                ["id"] = i.id,
                ["tipo"] = i.tipo,
                ["nome"] = string.IsNullOrWhiteSpace(i.descricao) ? i.forma : i.descricao,
                ["cor"] = i.cor,
                ["tamanho_cm"] = Mathf.RoundToInt(i.tamanho * 100f)
            });
        }
        return arr;
    }
}
