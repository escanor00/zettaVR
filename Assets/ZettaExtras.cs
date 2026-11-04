using System;
using UnityEngine;

// Cria sozinho, ao abrir a cena, os módulos novos da Zetta (cada um registra as próprias ferramentas de voz no Awake).
// Assim a cena não precisa de edição manual e cada módulo pode ser desligado tirando-o da lista.
public static class ZettaExtras
{
    static readonly Type[] Modulos =
    {
        typeof(EstadoZetta),
        typeof(DesempenhoHolo),
        typeof(CodigoHolo),
        typeof(MapaProjeto),
        typeof(AjustesHolo),
        typeof(RegrasHolo),
        typeof(AlertasErros),
        typeof(PonteHolo),
        typeof(FormasMatematicas),
        typeof(VistaExplodida),
        typeof(FerramentasPartes),
        typeof(ModoEstrutura),
        typeof(SuperficieHolo), typeof(GraficoFuncoes), typeof(ResolucaoHolo), typeof(CalculadoraHolo), typeof(GeometriaHolo), typeof(DnaHolo),
        typeof(GraficoHolo),
        typeof(BriefingBomDia),
        typeof(ModoFoco),
        typeof(SonsAmbiente),
        typeof(GestosDuplos),
        typeof(LinhaDoTempo),
        typeof(EtiquetasSala),
        typeof(PortalClima),
        typeof(ApresentacaoModelo),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (UnityEngine.Object.FindAnyObjectByType<AssistenteVoz>() == null) return;
        var go = new GameObject("ZettaExtras");
        foreach (var t in Modulos) go.AddComponent(t);
    }
}
