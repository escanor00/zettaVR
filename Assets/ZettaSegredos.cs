using UnityEngine;

// Guarda chaves e tokens fora das cenas e dos scripts. O arquivo do asset (Assets/Segredos/) fica fora do git.
[CreateAssetMenu(fileName = "ZettaSegredos", menuName = "Zetta/Segredos")]
public class ZettaSegredos : ScriptableObject
{
    public string chaveGemini;
    public string tokenGitHub;     // Fase 7: só permissão no repositório zetta-sites
    public string usuarioGitHub;
    public string tokenHuggingFace; // vazio por enquanto
}
