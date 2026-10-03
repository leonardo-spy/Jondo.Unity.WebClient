using UnityEngine;
using UnityEditor;
using UnityEngine.TextCore.Text;
using System.IO;
using System.Net;

public static class CreateDefaultFontAsset
{
    static readonly string FontDir = "Assets/Resources/Fonts";
    static readonly string TtfPath = "Assets/Resources/Fonts/Lexend-Regular.ttf";
    static readonly string FontAssetPath = "Assets/Resources/DefaultFont.asset";
    static readonly string FontAssetUSSPath = "Assets/UIToolkit-Importing/USS/Lexend-Regular.asset";

    [MenuItem("Build/1. Download Lexend Font")]
    public static void DownloadFont()
    {
        if (!Directory.Exists(FontDir)) Directory.CreateDirectory(FontDir);

        if (File.Exists(TtfPath))
        {
            Debug.Log($"[Font] Already exists: {TtfPath}");
            return;
        }

        // Lexend-Regular from Google Fonts (static TTF)
        var url = "https://raw.githubusercontent.com/googlefonts/lexend/main/fonts/lexend/ttf/Lexend-Regular.ttf";
        Debug.Log($"[Font] Downloading from {url}...");

        try
        {
            using (var client = new WebClient())
            {
                client.DownloadFile(url, TtfPath);
            }
            AssetDatabase.Refresh();

            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (font != null)
                Debug.Log($"[Font] Downloaded OK: {TtfPath} ({new FileInfo(TtfPath).Length / 1024}KB) dynamic={font.dynamic}");
            else
                Debug.LogError("[Font] Download succeeded but Unity can't load the font");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Font] Download failed: {e.Message}");
            Debug.Log("[Font] Manual alternative: download Lexend-Regular.ttf from https://fonts.google.com/specimen/Lexend");
            Debug.Log($"[Font] Place it at: {Path.GetFullPath(TtfPath)}");
        }
    }

    [MenuItem("Build/2. Create FontAsset from Lexend")]
    public static void CreateFontAsset()
    {
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[Font] Font not found at {TtfPath}. Run 'Build > 1. Download Lexend Font' first.");
            return;
        }

        Debug.Log($"[Font] Source: {sourceFont.name} dynamic={sourceFont.dynamic}");

        // Create SDF FontAsset
        var fontAsset = FontAsset.CreateFontAsset(sourceFont, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);

        if (fontAsset == null)
        {
            Debug.LogError("[Font] FontAsset.CreateFontAsset returned null");
            return;
        }

        // Also try a character set to pre-populate
        fontAsset.TryAddCharacters(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" +
            " !@#$%^&*()_+-=[]{}|;':\",./<>?~`" +
            "PLAYCONNECTINGJondoEmuWebGL" +
            "áàãâéèêíìóòõôúùûçÁÀÃÂÉÈÊÍÌÓÒÕÔÚÙÛÇñÑ");

        fontAsset.name = "DefaultFont";
        fontAsset.hideFlags = HideFlags.None;

        // Clean up old
        if (AssetDatabase.LoadAssetAtPath<Object>(FontAssetPath) != null)
            AssetDatabase.DeleteAsset(FontAssetPath);

        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        // Save atlas and material as sub-assets
        if (fontAsset.material != null)
        {
            fontAsset.material.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(fontAsset.material, FontAssetPath);
        }
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex == null) continue;
                tex.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(tex, FontAssetPath);
            }
        }

        // Save atlas as standalone texture (prevents stripping in WebGL build)
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
        {
            var atlas = fontAsset.atlasTextures[0];
            if (atlas != null && atlas.width > 1)
            {
                var atlasPath = "Assets/Resources/DefaultFontAtlas.asset";
                var atlasCopy = Object.Instantiate(atlas);
                atlasCopy.name = "DefaultFontAtlas";
                atlasCopy.hideFlags = HideFlags.None;
                if (AssetDatabase.LoadAssetAtPath<Object>(atlasPath) != null)
                    AssetDatabase.DeleteAsset(atlasPath);
                AssetDatabase.CreateAsset(atlasCopy, atlasPath);
                Debug.Log($"[Font] Atlas saved standalone: {atlasPath} ({atlas.width}x{atlas.height})");
            }
        }

        // Save material as standalone (also prevents stripping)
        if (fontAsset.material != null)
        {
            var matPath = "Assets/Resources/DefaultFontMaterial.mat";
            var matCopy = Object.Instantiate(fontAsset.material);
            matCopy.name = "DefaultFontMaterial";
            if (AssetDatabase.LoadAssetAtPath<Object>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(matCopy, matPath);
            Debug.Log($"[Font] Material saved standalone: {matPath}");
        }

        AssetDatabase.SaveAssets();

        // Also copy to USS directory so url("Lexend-Regular") resolves in reconstructed USS files
        if (AssetDatabase.LoadAssetAtPath<Object>(FontAssetUSSPath) != null)
            AssetDatabase.DeleteAsset(FontAssetUSSPath);
        AssetDatabase.CopyAsset(FontAssetPath, FontAssetUSSPath);

        AssetDatabase.Refresh();

        // Verify
        var loaded = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
        var ussLoaded = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetUSSPath);
        if (loaded != null && loaded.atlasTexture != null && loaded.glyphTable.Count > 0)
        {
            Debug.Log($"[Font] SUCCESS: {FontAssetPath} glyphs={loaded.glyphTable.Count} atlas={loaded.atlasTexture.width}x{loaded.atlasTexture.height}");
            Debug.Log($"[Font] USS copy: {FontAssetUSSPath} → {(ussLoaded != null ? "OK" : "FAILED")}");
        }
        else
            Debug.LogError($"[Font] FAILED: glyphs={loaded?.glyphTable?.Count ?? 0} atlas={loaded?.atlasTexture?.width ?? 0}");
    }
}
