using System.IO;
using UnityEngine;

public class TextureAssetUrl
{

    public DataLoader.DataPathIdentifier Path;
    public string[] Assets;

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

    public Texture2D LoadTexture2D()
    {
        if (Path.IsBundle)
        {
            return OCB.TextureUtils.LoadTexture(Path);
        }
        else
        {
            var data = File.ReadAllBytes(Assets[0]);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(data);
            return tex;
        }
    }

}
