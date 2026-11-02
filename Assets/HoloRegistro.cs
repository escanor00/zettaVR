using System;
using System.Collections.Generic;
using UnityEngine;

public class HoloItem
{
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
    // true enquanto objetos salvos são recriados (a mesa holográfica não os faz subir)
    public static bool Carregando;
    public static event Action<GameObject> aoAdicionar;
    public static event Action<GameObject> aoRemover;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        Itens.Clear();
        Carregando = false;
        aoAdicionar = null;
        aoRemover = null;
    }

    public static HoloItem Adicionar(GameObject go, string forma, string cor, string descricao,
        string tipo = "primitiva", float tamanho = 0.15f, string extra = "")
    {
        Itens.RemoveAll(i => i.go == null);
        var item = new HoloItem { go = go, forma = forma ?? "", cor = cor ?? "", descricao = (descricao ?? "").Trim(), tipo = tipo, tamanho = tamanho, extra = extra ?? "" };
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
}
