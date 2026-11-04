using System;
using UnityEngine;

public enum EstadoVoz { Espera, Ouvindo, Pensando, Falando, Erro }

// Máquina de estados única da Zetta: esfera, central e rótulos só leem daqui (calculado uma vez por quadro).
// Espera → Ouvindo (voz do usuário) → Pensando (aguardando a resposta) → Falando → Espera; Erro quando há falha ativa.
[DefaultExecutionOrder(-50)]
public class EstadoZetta : MonoBehaviour
{
    public static EstadoVoz Atual { get; private set; } = EstadoVoz.Espera;
    public static float DesdeQue { get; private set; }
    public static event Action<EstadoVoz, EstadoVoz> aoMudar;   // (anterior, novo)

    AssistenteVoz voz;
    float ultimaVozUsuario = -100f;

    void Update()
    {
        if (voz == null) { voz = FindAnyObjectByType<AssistenteVoz>(); if (voz == null) return; }
        Definir(Calcular(voz, Time.time));
    }

    EstadoVoz Calcular(AssistenteVoz v, float tempo)
    {
        bool falando = v.Falando || v.volumeSaida > 0.03f;
        if (v.nivelMic > 0.04f && !falando) ultimaVozUsuario = tempo;
        if (falando) return EstadoVoz.Falando;
        if (!string.IsNullOrEmpty(v.ultimoErro) && v.estadoMic == "microfone negado") return EstadoVoz.Erro;
        if (!v.acordada) return EstadoVoz.Espera;
        if (tempo - ultimaVozUsuario < 0.6f) return EstadoVoz.Ouvindo;
        if (v.Processando || tempo - ultimaVozUsuario < 6f) return EstadoVoz.Pensando;
        return EstadoVoz.Espera;
    }

    static void Definir(EstadoVoz novo)
    {
        if (novo == Atual) return;
        var antes = Atual;
        Atual = novo;
        DesdeQue = Time.time;
        aoMudar?.Invoke(antes, novo);
    }

    // para testes: força um estado sem passar pela voz
    public static void ForcarParaTeste(EstadoVoz e) { Definir(e); }
}
