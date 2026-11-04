using System.Collections.Generic;
using UnityEngine;

// Sons espaciais suaves, gerados por código (sem arquivos): abrir, fechar, criar, apagar e clique.
// Volume baixo para não competir com a voz da Zetta nem entrar forte no microfone.
public static class SonsZetta
{
    public const string Clique = "clique", Abrir = "abrir", Fechar = "fechar", Criar = "criar", Apagar = "apagar";

    public static float volume = 0.25f;
    public static bool ligado = true;

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static AudioSource fonte;
    const int Taxa = 24000;

    public static void Tocar(string nome)
    {
        if (!ligado || !Application.isPlaying) return;
        if (fonte == null)
        {
            var go = new GameObject("SonsZetta");
            Object.DontDestroyOnLoad(go);
            fonte = go.AddComponent<AudioSource>();
            fonte.spatialBlend = 0f;
            fonte.playOnAwake = false;
        }
        if (!clips.TryGetValue(nome, out var clip)) { clip = Gerar(nome); clips[nome] = clip; }
        if (clip != null) fonte.PlayOneShot(clip, volume);
    }

    static AudioClip Gerar(string nome)
    {
        switch (nome)
        {
            case Clique: return Sintetizar(nome, 0.09f, t => Nota(t, 1500f, 18f) * 0.8f + Nota(t, 3000f, 30f) * 0.3f);
            case Abrir: return Sintetizar(nome, 0.5f, t => Varredura(t, 380f, 980f, 0.5f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f)));
            case Fechar: return Sintetizar(nome, 0.4f, t => Varredura(t, 900f, 320f, 0.4f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f)));
            case Criar:
                return Sintetizar(nome, 0.6f, t => Nota(t, 660f, 6f) * 0.5f + Nota(t - 0.1f, 880f, 6f) * 0.5f + Nota(t - 0.2f, 1320f, 5f) * 0.5f);
            case Apagar:
                return Sintetizar(nome, 0.4f, t => Varredura(t, 700f, 140f, 0.4f) * (1f - t / 0.4f) * (0.7f + 0.3f * Mathf.Sin(t * 90f)));
        }
        return null;
    }

    static float Nota(float t, float hz, float decaimento) => t < 0f ? 0f : Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-decaimento * t) * Mathf.Min(1f, t * 400f);

    static float Varredura(float t, float de, float ate, float dur)
    {
        // fase da varredura linear de frequência
        float fase = 2f * Mathf.PI * (de * t + (ate - de) * t * t / (2f * dur));
        return Mathf.Sin(fase) * 0.6f;
    }

    static AudioClip Sintetizar(string nome, float dur, System.Func<float, float> f)
    {
        int n = Mathf.RoundToInt(dur * Taxa);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)Taxa), -1f, 1f);
        var c = AudioClip.Create("zetta_" + nome, n, 1, Taxa, false);
        c.SetData(d, 0);
        return c;
    }
}
