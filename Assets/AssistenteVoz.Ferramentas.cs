using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Assistente de voz em tempo real via Gemini Live API (WebSocket direto).
// AssistenteVoz — registro de ferramentas (function calling) por outros módulos.
public partial class AssistenteVoz
{
    // Registra uma ferramenta (function calling): declaração JSON + tratador chamado na thread principal.
    public void RegistrarFerramenta(string declaracaoJson, Func<JObject, JObject> tratador, string instrucao = null)
    {
        var decl = JObject.Parse(declaracaoJson);
        declaracoesFuncoes.Add(decl);
        ferramentas[decl.Value<string>("name")] = tratador;
        if (!string.IsNullOrEmpty(instrucao)) instrucoesRuntime.Add(instrucao);
    }

    // Fim do turno de fala do usuário, com a transcrição completa (usado pelas regras "quando eu disser X, faça Y").
    public event Action<string> aoFraseCompleta;
    void FraseCompleta(string frase) { aoFraseCompleta?.Invoke(frase); }

    public bool TemFerramenta(string nome) { return ferramentas.ContainsKey(nome); }

    // Chama uma ferramenta já registrada, como se a Gemini a tivesse chamado (thread principal).
    public JObject ChamarFerramenta(string nome, JObject args)
    {
        if (!ferramentas.TryGetValue(nome, out var tratador)) return new JObject { ["erro"] = "ferramenta desconhecida: " + nome };
        try { return tratador(args ?? new JObject()) ?? new JObject { ["result"] = "ok" }; }
        catch (Exception e) { Debug.LogWarning("AssistenteVoz: ferramenta " + nome + " falhou: " + Sanitizar(e.Message)); return new JObject { ["erro"] = "falhou" }; }
    }
}
