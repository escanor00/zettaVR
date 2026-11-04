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
}
