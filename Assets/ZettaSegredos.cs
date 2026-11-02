using UnityEngine;
using UnityEngine.Serialization;

// Guarda chaves e tokens fora das cenas e dos scripts. O arquivo do asset (Assets/Segredos/) fica fora do git.
[CreateAssetMenu(fileName = "ZettaSegredos", menuName = "Zetta/Segredos")]
public class ZettaSegredos : ScriptableObject
{
    public string chaveGemini;
    [FormerlySerializedAs("tokenGitHub")]
    public string githubToken;                                  // Estúdio de Sites: permissão só no repositório abaixo (Contents: Read and write)
    public string githubRepositorio = "escanor00/zetta-sites";  // dono/repositório (GitHub Pages ligado na branch main, raiz)
    public string tokenHuggingFace;                             // opcional: mais cota para o modelo 3D
}
