using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Mantém o desempenho no Quest: se houver hologramas demais ao mesmo tempo, o mais antigo (que ninguém está segurando) é apagado.
public class LimiteHologramas : MonoBehaviour
{
    public int maximo = 24;

    ApagadorHolo apagador;

    void OnEnable() { HoloRegistro.aoAdicionar += AoAdicionar; }
    void OnDisable() { HoloRegistro.aoAdicionar -= AoAdicionar; }

    void AoAdicionar(GameObject novo)
    {
        if (HoloRegistro.Carregando) return;   // recriação das âncoras salvas: não apaga nada
        if (apagador == null) apagador = FindAnyObjectByType<ApagadorHolo>();
        if (apagador == null) return;
        var itens = HoloRegistro.Itens;
        for (int i = 0; i < itens.Count && itens.Count > maximo; i++)
        {
            var go = itens[i].go;
            if (go == null || go == novo) continue;
            var g = go.GetComponent<XRGrabInteractable>();
            if (g != null && g.isSelected) continue;
            Debug.Log("LimiteHologramas: mais de " + maximo + " hologramas; apagando o mais antigo (" + itens[i].descricao + ").");
            apagador.Apagar(go);
            break;
        }
    }
}
