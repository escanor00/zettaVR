using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Zetta > Ponte PC > Gerar senha: cria uma senha aleatória para a ponte do PC e a grava só em dois lugares fora do git:
// o asset Assets/Segredos/ZettaSegredos.asset e Docs/ponte-pc/.senha (que o servidor lê). A senha NUNCA vai para o Console.
public static class PontePcEditor
{
    const string CaminhoAsset = "Assets/Segredos/ZettaSegredos.asset";
    const int Porta = 8765;

    [MenuItem("Zetta/Ponte PC/Gerar senha")]
    public static void GerarSenha()
    {
        var seg = AssetDatabase.LoadAssetAtPath<ZettaSegredos>(CaminhoAsset);
        if (seg == null) { Debug.LogError("[PontePc] não achei " + CaminhoAsset + " (Assets > Create > Zetta > Segredos)."); return; }

        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(alfabeto[b % alfabeto.Length]);
        string senha = sb.ToString();

        string ip = EnderecoLocal();
        seg.senhaPonte = senha;
        seg.enderecoPonte = ip != null ? "http://" + ip + ":" + Porta : "";
        EditorUtility.SetDirty(seg);
        AssetDatabase.SaveAssets();

        string pasta = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "ponte-pc");
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, ".senha"), senha, new UTF8Encoding(false));

        Debug.Log("[PontePc] senha gerada (" + senha.Length + " caracteres, não exibida). Endereço: " +
                  (ip != null ? seg.enderecoPonte : "não achei o IP; preencha enderecoPonte no ZettaSegredos") +
                  ". Gere um novo APK; inicie o servidor com: python Docs/ponte-pc/servidor.py");
    }

    // primeiro IPv4 privado de uma placa ativa (prefere 192.168.x.x)
    static string EnderecoLocal()
    {
        string melhor = null;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            string nome = (ni.Name + " " + ni.Description).ToLowerInvariant();
            if (nome.Contains("virtual") || nome.Contains("vethernet") || nome.Contains("vmware") || nome.Contains("wsl") || nome.Contains("hyper-v")) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                string s = ua.Address.ToString();
                if (s.StartsWith("192.168.")) return s;
                if (melhor == null && (s.StartsWith("10.") || s.StartsWith("172."))) melhor = s;
            }
        }
        return melhor;
    }
}
