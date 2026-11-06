using UnityEditor;
using UnityEngine;

// Keep the imported CC0 environment textures consistent between editor and player builds.
public sealed class FoundryTextureImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath.Replace('\\', '/');
        if (path.Contains("/Resources/Foundry/Sky/") && path.EndsWith(".hdr"))
        {
            var hdrImporter = (TextureImporter)assetImporter;
            hdrImporter.sRGBTexture = false;
            hdrImporter.maxTextureSize = 2048;
            return;
        }
        if (!path.Contains("/Resources/Foundry/Textures/")) return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        if (path.Contains("_nor_gl") || path.Contains("_normal"))
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
        }
        else
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = path.Contains("_diff");
        }
    }
}
