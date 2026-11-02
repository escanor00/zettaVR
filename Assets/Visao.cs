using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Ferramenta "olhar": tira uma foto do que o usuário está vendo (câmera do passthrough) e envia à Gemini Live,
// que responde sobre ela ("Zetta, o que é isso?").
public class Visao : MonoBehaviour
{
    const string Decl = @"{
  ""name"": ""olhar"",
  ""description"": ""Olha pela câmera do óculos para ver o que o usuário está vendo agora e envia a imagem para você. Use quando perguntarem 'o que é isso?', 'o que você está vendo?', 'leia isso', 'que cor é isso' etc."",
  ""parameters"": {
    ""type"": ""OBJECT"",
    ""properties"": {
      ""pergunta"": { ""type"": ""STRING"", ""description"": ""O que o usuário quer saber sobre a imagem, em português."" }
    }
  }
}";
    const string Instrucao =
        "Você enxerga pela ferramenta olhar. Para 'o que é isso?', 'o que você vê?', 'leia isso' ou qualquer pergunta sobre o que está diante do usuário, " +
        "chame olhar e diga em uma frase curta que vai dar uma olhada; a imagem chegará logo em seguida e então você responde em português, de forma breve.";

    public int ladoFoto = 768;
    AssistenteVoz voz;
    GeradorModelo3D camera3D;
    bool ocupado;

    void Awake()
    {
        voz = FindAnyObjectByType<AssistenteVoz>();
        camera3D = FindAnyObjectByType<GeradorModelo3D>();
        if (voz != null) voz.RegistrarFerramenta(Decl, Olhar, Instrucao);
    }

    JObject Olhar(JObject args)
    {
        if (camera3D == null) return new JObject { ["erro"] = "câmera indisponível" };
        if (ocupado) return new JObject { ["erro"] = "já estou olhando; espere um instante" };
        StartCoroutine(Fluxo(args.Value<string>("pergunta") ?? "o que é isso?"));
        return new JObject { ["result"] = "olhando; a imagem chegará em instantes" };
    }

    IEnumerator Fluxo(string pergunta)
    {
        ocupado = true;
        byte[] foto = null;
        string erro = null;
        yield return camera3D.CapturarFoto(b => foto = b, m => erro = m);
        ocupado = false;
        if (foto == null)
        {
            Debug.LogWarning("Visao: sem imagem: " + erro);
            voz?.Falar("não consegui usar a câmera (" + (erro ?? "erro") + "). Avise o usuário em uma frase; se foi permissão, peça para aceitar o acesso às câmeras do headset.");
            yield break;
        }
        voz?.EnviarImagem(foto);
        voz?.Falar("esta é a imagem do que o usuário está vendo agora. Responda em português, em até duas frases, a pergunta: " + pergunta);
    }
}
