using System.IO;
using UnityEngine;

// ####################################################################
// Helper class for loading textures from disk/asset-bundles
// ####################################################################

public class TextureAssetUrl
{

    // ####################################################################
    // ####################################################################

    public DataLoader.DataPathIdentifier Path;
    public string[] Assets;

    // ####################################################################
    // ####################################################################

    public TextureAssetUrl(string url)
    {
        Path = DataLoader.ParseDataPathIdentifier(url);

        if (Path.IsBundle)
        {
            // Try to load the (cached) asset bundle resource (once)
            AssetBundleManager.Instance.LoadAssetBundle(Path.BundlePath);
        }
        // Support different face textures
        Assets = Path.AssetName.Split(',');
    }

    // ####################################################################
    // ####################################################################

    public Texture2D LoadTexture2D()
    {
        if (Path.IsBundle)
        {
            return OcbTextureUtils.LoadTexture(
                Path, out int _) as Texture2D;
        }
        else
        {
            // Load texture from image file directly
            // Not recommended at all to do it this way!
            // May or may not work, so beware!
            var data = File.ReadAllBytes(Assets[0]);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            return tex;
        }
    }

    // ####################################################################
    // ####################################################################

    public void CopyTo(Texture2D dst, int x, int y)
    {
        var texture = LoadTexture2D();
        if (texture == null) return;
        dst.SetPixels(x, y,
            texture.width,
            texture.height,
            texture.GetPixels(0));
    }

    // ####################################################################
    // ####################################################################

}
