public class TextureAssetUrl
{

    public DataLoader.DataPathIdentifier Path;
    public string[] Assets;

    public TextureAssetUrl(string url)
    {
        Path = DataLoader.ParseDataPathIdentifier(url);
        // Try to load the (cached) asset bundle resource (once)
        AssetBundleManager.Instance.LoadAssetBundle(Path.BundlePath);
        // Support different face textures
        Assets = Path.AssetName.Split(',');
    }

}
