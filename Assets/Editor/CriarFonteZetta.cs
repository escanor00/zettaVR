using TMPro;
using UnityEditor;
using UnityEngine;

// Gera o TMP Font Asset da Zetta (Rajdhani SemiBold, licença OFL) com os acentos do português.
public static class CriarFonteZetta
{
    [MenuItem("Zetta/Gerar fonte")]
    public static void Gerar()
    {
        var ttf = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Rajdhani-SemiBold.ttf");
        if (ttf == null) { Debug.LogError("CriarFonteZetta: fonte não encontrada"); return; }
        var fa = TMP_FontAsset.CreateFontAsset(ttf, 72, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        string chars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                       "ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑáàâãäéèêëíìîïóòôõöúùûüçñ°ºª•…–—“”‘’«»€×÷";
        System.IO.Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.CreateAsset(fa, "Assets/Resources/ZettaFonte SDF.asset");
        fa.TryAddCharacters(chars, out string faltando);
        fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        if (fa.material != null) { fa.material.name = "ZettaFonte SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        if (fa.atlasTextures != null && fa.atlasTextures.Length > 0) { fa.atlasTextures[0].name = "ZettaFonte SDF Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa); }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("CriarFonteZetta: gerada. Caracteres que faltaram: [" + faltando + "]");
    }
}
