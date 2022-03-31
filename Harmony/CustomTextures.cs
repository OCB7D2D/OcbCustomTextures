using HarmonyLib;
using UnityEngine;
using System;
using System.Xml;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using OCB;

public class OcbCustomTextures : IModApi
{

    // Last active texture quality
    static int Quality = -1;

    // Enable to dump all textures to disk
    static bool Dump = false;

    // ID automatically set from internal paints
    // The default is just a safe bet hard-coding
    static int PaintID = 183;

    // Flag if texture quality change handler is hooked
    static bool Registered = false;

    static Dictionary<string, int> UvMap = new Dictionary<string, int>();

    // A structure holding all the info for custom textures
    public struct TexInfo
    {
        public UVRectTiling tiling;

        public string ID;
        public string Diffuse;
        public string Normal;
        public string Specular;

        public TexInfo(XmlElement xml, DynamicProperties props)
        {

            tiling = new UVRectTiling();

            if (!xml.HasAttribute("id")) throw new Exception("Mandatory attribute `id` missing");
            // if (!props.Contains("Diffuse")) throw new Exception("Mandatory property `Diffuse` missing");
            // if (!props.Contains("Normal")) throw new Exception("Mandatory property `Normal` missing");
            // if (!props.Contains("Material")) throw new Exception("Mandatory property `Material` missing");

            ID = xml.GetAttribute("id");

            tiling.uv.x = xml.HasAttribute("x") ? float.Parse(xml.GetAttribute("x")) : 0;
            tiling.uv.y = xml.HasAttribute("y") ? float.Parse(xml.GetAttribute("y")) : 0;
            tiling.uv.width = xml.HasAttribute("w") ? float.Parse(xml.GetAttribute("w")) : 1;
            tiling.uv.height = xml.HasAttribute("h") ? float.Parse(xml.GetAttribute("h")) : 1;
            tiling.blockW = xml.HasAttribute("blockw") ? int.Parse(xml.GetAttribute("blockw")) : 1;
            tiling.blockH = xml.HasAttribute("blockh") ? int.Parse(xml.GetAttribute("blockh")) : 1;

            // tiling.textureName = xml.HasAttribute("name") ? xml.GetAttribute("name") : "";

            tiling.color = !props.Contains("Color") ? Color.white :
                 StringParsers.ParseColor(props.GetString("Color"));
            tiling.material = !props.Contains("Material") ? null:
                MaterialBlock.fromString(props.GetString("Material"));
            tiling.bGlobalUV = props.Contains("GlobalUV") ?
                props.GetBool("GlobalUV") : false;
            tiling.bSwitchUV = props.Contains("SwitchUV") ?
                props.GetBool("SwitchUV") : false;

            Diffuse = props.Contains("Diffuse") ?
                props.GetString("Diffuse") : null;
            Normal = props.Contains("Normal") ?
                props.GetString("Normal") : null;
            Specular = props.Contains("Specular") ?
                props.GetString("Specular") : null;

        }
    }

    // A static list of additional texture (used for hot reload)
    static List<TexInfo> Textures = new List<TexInfo>();

    public void InitMod(Mod mod)
    {
        Debug.Log("Loading OCB Texture Atlas Patch: " + GetType().ToString());
        new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());

        ModEvents.GameStartDone.RegisterHandler(GameStartDone);
        ModEvents.GameShutdown.RegisterHandler(GameShutdown);

    }

    void TextureQualityChanged(int quality)
    {
        if (Quality == -1) Quality = quality;
        else if (Quality == 2 && quality == 3) { Quality = 3; return; }
        else if (Quality == 3 && quality == 2) { Quality = 2; return; }
        var mesh = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
        foreach (var texture in Textures)
            PatchBlocksAtlas(mesh, texture);
        if (GameManager.Instance != null && GameManager.Instance.prefabLODManager != null)
            GameManager.Instance.prefabLODManager.UpdateMaterials();
    }

    public void GameStartDone()
    {
        if (Registered) return;
        GameOptionsManager.TextureQualityChanged += TextureQualityChanged;
        Registered = true;
    }

    public void GameShutdown()
    {
        if (!Registered) return;
        GameOptionsManager.TextureQualityChanged -= TextureQualityChanged;
        Registered = false;
    }

    static int DumpedDiffuse = 0;
    static int DumpedNormal = 0;
    static int DumpedSpecular = 0;

    static void DumpAtlas(TextureAtlas _ta, string name)
    {

        System.IO.Directory.CreateDirectory("export");
        System.IO.Directory.CreateDirectory("export/" + name);

        if (_ta.diffuseTexture is Texture2DArray diff)
        {
            for (; DumpedDiffuse < diff.depth; DumpedDiffuse++)
            {
                TexUtils.DumpTexure(diff, DumpedDiffuse, String.Format(
                    "export/{0}/{1}.diffuse.png", name, DumpedDiffuse));
            }
        }
        else if (_ta.diffuseTexture is Texture2D tex2d)
        {
            TexUtils.DumpTexure(tex2d, String.Format(
                "export/{0}/diffuse.png", name));
        }

        if (_ta.normalTexture is Texture2DArray norm)
        {
            for (; DumpedNormal < norm.depth; DumpedNormal++)
            {
                TexUtils.DumpNormalTexure(norm, DumpedNormal, String.Format(
                    "export/{0}/{1}.normal.png", name, DumpedNormal));
            }
        }
        else if (_ta.normalTexture is Texture2D tex2d)
        {
            TexUtils.DumpNormalTexure(tex2d, String.Format(
                "export/{0}/normal.png", name));
        }

        if (_ta.specularTexture is Texture2DArray spec)
        {
            for (; DumpedSpecular < spec.depth; DumpedSpecular++)
            {
                TexUtils.DumpSpecular(spec, DumpedSpecular, String.Format(
                    "export/{0}/{1}.specular.png", name, DumpedSpecular));
            }
        }
        else if (_ta.specularTexture is Texture2D tex2d)
        {
            TexUtils.DumpSpecular(tex2d, String.Format(
                "export/{0}/specular.png", name));
        }
    }

    static void DumpTerrainAtlas(TextureAtlas _ta, string name)
    {
        // The following fails since textures are not readable
        // This is only a flag in unity, but not easy to get by
        if (_ta is TextureAtlasTerrain terrains)
        {
            for (; DumpedDiffuse < terrains.diffuse.Length; DumpedDiffuse++)
            {
                if (terrains.diffuse[DumpedDiffuse] == null) continue;
                var img = terrains.diffuse[DumpedDiffuse];
                TexUtils.DumpTexure(img, String.Format(
                    "export/{0}/arr-{1}.diffuse.png",
                    name, DumpedDiffuse));
            }
            for (; DumpedNormal < terrains.normal.Length; DumpedNormal++)
            {
                if (terrains.normal[DumpedNormal] == null) continue;
                var img = terrains.normal[DumpedNormal];
                TexUtils.DumpNormalTexure(img, String.Format(
                    "export/{0}/arr-{1}.normal.png",
                    name, DumpedNormal));
            }
            for (; DumpedSpecular < terrains.specular.Length; DumpedSpecular++)
            {
                if (terrains.specular[DumpedSpecular] == null) continue;
                var img = terrains.specular[DumpedSpecular];
                Texture2D tex = ResizeTexture(img, img.width, img.height, true);
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(String.Format(
                    "export/{0}/arr-{1}.specular.png",
                    name, DumpedSpecular), bytes);
            }
        
        }
        if (_ta.diffuseTexture is Texture2DArray diff2Darr)
        {
            for (int i = 0; i < diff2Darr.depth; i++)
            {
                Texture2D copy = new Texture2D(diff2Darr.width, diff2Darr.height, diff2Darr.format, true, false);
                Graphics.CopyTexture(diff2Darr, i, copy, 0);
                Texture2D tex = copy.DeCompress();
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(String.Format(
                    "export/{0}/tex2darr-{1}.diffuse.png",
                    name, i), bytes);
            }
        }
        else if (_ta.diffuseTexture is Texture2D diff2D)
        {
            Texture2D tex = ResizeTexture(diff2D, diff2D.width, diff2D.height, true);
            byte[] bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(String.Format(
                "export/{0}/tex2d.diffuse.png",
                name), bytes);
        }

        if (_ta.normalTexture is Texture2DArray norm2Darr)
        {
            for (int i = 0; i < norm2Darr.depth; i++)
            {
                Texture2D copy = new Texture2D(norm2Darr.width, norm2Darr.height, norm2Darr.format, true, true);
                Graphics.CopyTexture(norm2Darr, i, copy, 0);
                Texture2D tex = copy.DeCompress();
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(String.Format(
                    "export/{0}/tex2darr-{1}.normal.png",
                    name, i), bytes);
            }
        }
        else if (_ta.normalTexture is Texture2D spec2D)
        {
            Texture2D tex = ResizeTexture(spec2D, spec2D.width, spec2D.height, true);
            byte[] bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(String.Format(
                "export/{0}/tex2d.normal.png",
                name), bytes);
        }


        if (_ta.specularTexture is Texture2DArray spec2Darr)
        {
            for (int i = 0; i < spec2Darr.depth; i++)
            {
                Texture2D copy = new Texture2D(spec2Darr.width, spec2Darr.height, spec2Darr.format, true, true);
                Graphics.CopyTexture(spec2Darr, i, copy, 0);
                Texture2D tex = copy.DeCompress();
                byte[] bytes = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(String.Format(
                    "export/{0}/tex2darr-{1}.specular.png",
                    name, i), bytes);
            }
        }
        else if (_ta.specularTexture is Texture2D spec2D)
        {
            Texture2D tex = ResizeTexture(spec2D, spec2D.width, spec2D.height, true);
            byte[] bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(String.Format(
                "export/{0}/tex2d.specular.png",
                name), bytes);
        }

    }

    static bool IsPow2(int n)
    {
        int count = 0;
        for (int i = 0; i < 32; i++)
        {
            count += (n >> i & 1);
        }
        return count == 1 && n > 0;
    }

    static Texture2D GetUniformTexture(int width, int height, Color color, bool quality = false)
    {
        Texture2D spec = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
        for (int y = 0; y < spec.height; y++)
        {
            for (int x = 0; x < spec.width; x++)
            {
                spec.SetPixel(x, y, color, 0);
            }
        }
        spec.Apply(true);
        spec.Compress(quality);
        return spec;
    }

    static Texture2D GetNormalTexture(int width, int height)
    {
        return GetUniformTexture(width, height, new Color(1f, 0.5f, 0.5f, 0.5f), true);
    }

    static Texture2D GetSpecularTexture(int width, int height)
    {
        return GetUniformTexture(width, height, Color.green);
    }

    static void CreateNormalTextures(ref Texture texture, int sides)
    {
        if (texture is Texture2DArray arr)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);

            // Create a copy and add space for 4 more textures
            Texture2DArray copy = new Texture2DArray(arr.width,
                arr.height, arr.depth + sides, arr.format, true, linear);
            // Copy from old texture to new copy
            for (int i = 0; i < arr.depth; i++)
                Graphics.CopyTexture(arr, i, copy, i);
            // Create a neutral normal texture
            Texture2D normal = GetNormalTexture(arr.width, arr.height);
            // Copy Texture2D to Texture2DArray
            for (int i = 0; i < sides; i++)
            {
                int off = normal.mipmapCount - copy.mipmapCount;
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(normal.GetPixelData<byte>(n + off), n, arr.depth + i);
                }

            }
            copy.Apply(false);
            // Assign the copy back
            texture = copy;
            return;
        }
        // else if (texture is Texture2D) {}
        throw new Exception("Can only patch Texture2DArray");
    }

    static void CreateSpecularTextures(ref Texture texture, int sides)
    {
        if (texture is Texture2DArray arr)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);

            // Create a copy and add space for 4 more textures
            Texture2DArray copy = new Texture2DArray(arr.width,
                arr.height, arr.depth + sides, arr.format, true, linear);
            // Copy from old texture to new copy
            for (int i = 0; i < arr.depth; i++)
                Graphics.CopyTexture(arr, i, copy, i);
            // Create a neutral specular texture
            Texture2D spec = GetSpecularTexture(arr.width, arr.height);
            // Copy Texture2D to Texture2DArray
            for (int i = 0; i < sides; i++)
            {
                int off = spec.mipmapCount - copy.mipmapCount;
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(spec.GetPixelData<byte>(n + off), n, arr.depth + i);
                }

            }
            copy.Apply(false);
            // Assign the copy back
            texture = copy;
            return;
        }
        // else if (texture is Texture2D) {}
        throw new Exception("Can only patch Texture2DArray");
    }

    static Texture2D LoadTexture(string bundle, string asset)
    {
        // Get the texture from pre-loaded bundles
        Texture2D tex = AssetBundleManager.Instance
            .Get<Texture2D>(bundle, asset);
        // Check if loading was successful
        if (tex == null) throw new Exception(String.Format(
            "Could not load asset {1} from bundle {0}",
            bundle, asset));
        if (tex.width != tex.height) throw new Exception(String.Format(
            "Texture must be a square, is {0}x{1} at ({2})",
            tex.width, tex.height, asset));
        if (!IsPow2(tex.width)) throw new Exception(String.Format(
            "Texture dimension must be power of two ({0}) at {2}",
            tex.width, tex.height, asset));
        return tex;
    }

    static Texture2D LoadTexture(string bundle, string asset, TextureFormat format)
    {
        var tex = LoadTexture(bundle, asset);
        // Check if the format is compatible
        if (tex.format != format) throw new Exception(String.Format(
            "Texture format {0} not compatible with atlas {1} ({2})",
            tex.format, format, asset));
        return tex;
    }

    static int PatchTexture(ref Texture texture, string url)
    {
        if (texture is Texture2DArray arr)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);
            // Get the resource bundle and asset path
            var path = DataLoader.ParseDataPathIdentifier(url);
            // Try to load the (cached) asset bundle resource (once)
            AssetBundleManager.Instance.LoadAssetBundle(path.BundlePath);
            // Support different face textures
            var assets = path.AssetName.Split(',');
            // Create a copy and add space for 4 more textures
            Texture2DArray copy = new Texture2DArray(arr.width,
                arr.height, arr.depth + assets.Length, arr.format, true, linear);
            // Copy old textures to new copy
            for (int i = 0; i < arr.depth; i++)
                Graphics.CopyTexture(arr, i, copy, i);
            // Only add as many textures as needed
            for (int i = 0; i < assets.Length; i++)
            {
                var tex = LoadTexture(path.BundlePath, assets[i], arr.format);
                // This will automatically do the resize for us, neat!
                int off = tex.mipmapCount - copy.mipmapCount;
                // Copy the loaded texture (use same for every side for now)
                // ToDo: add different config to set them separately
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(tex.GetPixelData<byte>(n + off), n, arr.depth + i);
                }
            }
            // Apply new pixels
            copy.Apply(false);
            // Assign the copy back
            texture = copy;
            return assets.Length;
        }
        // else if (texture is Texture2D) {}
        throw new Exception("Can only patch Texture2DArray");
    }

    static int PatchBlocksAtlas(MeshDescription mesh, TexInfo tex)
    {

        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        var uvmap = atlas.uvMapping.Length;

        if (atlas.diffuseTexture is Texture2DArray tex2Darr)
        {
            tex.tiling.index = tex2Darr.depth;
        }
        else
        {
            throw new Exception("Expected Texture2DArray");
        }

        if (UvMap.ContainsKey(tex.ID)) Log.Warning(
            "Overwriting texture key {0}", tex.ID);
        UvMap[tex.ID] = uvmap;

        int sides = PatchTexture(ref atlas.diffuseTexture, tex.Diffuse);

        Array.Resize(ref atlas.uvMapping, uvmap + 1);
        atlas.uvMapping[uvmap] = tex.tiling;

        if (tex.Normal != null)
        {
            PatchTexture(ref atlas.normalTexture, tex.Normal);
        }
        else
        {
            CreateNormalTextures(ref atlas.normalTexture, sides);
        }

        if (tex.Specular != null)
        {
            PatchTexture(ref atlas.specularTexture, tex.Specular);
        }
        else
        {
            CreateSpecularTextures(ref atlas.specularTexture, sides);
        }

        // mesh.TexDiffuse = atlas.diffuseTexture;
        // mesh.TexNormal = atlas.normalTexture;
        // mesh.TexSpecular = atlas.specularTexture;
        
        mesh.ReloadTextureArrays(false);

        // Log.Warning("Patched Mesh Atlas now has {0} items",
        //     (atlas.diffuseTexture as Texture2DArray).depth);

        // if (Dump) DumpAtlas(atlas, "opaque");

        Quality = GameOptionsManager.GetTextureQuality();

        return uvmap;

    }

    static int PatchTerrainAtlas(MeshDescription mesh, TexInfo tex)
    {

        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasTerrain;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        var uvmap = atlas.uvMapping.Length;

        if (UvMap.ContainsKey(tex.ID)) Log.Warning(
            "Overwriting texture key {0}", tex.ID);
        tex.tiling.index = atlas.diffuse.Length;
        UvMap[tex.ID] = uvmap;

        // Get the resource bundle and asset path
        var path = DataLoader.ParseDataPathIdentifier(tex.Diffuse);
        AssetBundleManager.Instance.LoadAssetBundle(path.BundlePath);
        var texture = LoadTexture(path.BundlePath, path.AssetName);
        Array.Resize(ref atlas.diffuse, atlas.diffuse.Length + 1);
        atlas.diffuse[atlas.diffuse.Length - 1] = texture;

        Array.Resize(ref atlas.uvMapping, uvmap + 1);
        atlas.uvMapping[uvmap] = tex.tiling;

        if (tex.Normal != null)
        {
            var norm = DataLoader.ParseDataPathIdentifier(tex.Normal);
            AssetBundleManager.Instance.LoadAssetBundle(norm.BundlePath);
            var normal = LoadTexture(norm.BundlePath, norm.AssetName);
            Array.Resize(ref atlas.normal, atlas.normal.Length + 1);
            atlas.normal[atlas.normal.Length - 1] = normal;
        }
        else
        {
            var normal = GetNormalTexture(texture.width, texture.height);
            normal.filterMode = FilterMode.Trilinear;
            normal.wrapMode = TextureWrapMode.Clamp;
            Array.Resize(ref atlas.normal, atlas.normal.Length + 1);
            atlas.normal[atlas.normal.Length - 1] = normal;
        }

        if (tex.Specular != null)
        {
            var spec = DataLoader.ParseDataPathIdentifier(tex.Specular);
            AssetBundleManager.Instance.LoadAssetBundle(spec.BundlePath);
            var specular = LoadTexture(spec.BundlePath, spec.AssetName);
            Array.Resize(ref atlas.specular, atlas.specular.Length + 1);
            atlas.specular[atlas.specular.Length - 1] = specular;
        }
        else
        {
            var specular = GetUniformTexture(texture.width, texture.height, new Color(0f, 0f, 0f));
            Array.Resize(ref atlas.specular, atlas.specular.Length + 1);
            atlas.specular[atlas.specular.Length - 1] = null; ;
        }

        mesh.ReloadTextureArrays(false);

        if (Dump) DumpTerrainAtlas(atlas, "terrain");

        Quality = GameOptionsManager.GetTextureQuality();

        return uvmap;

    }

    [HarmonyPatch(typeof(BlockTexturesFromXML))]
    [HarmonyPatch("CreateBlockTextures")]
    public class Patches
    {
        class SimpleEnumerator : IEnumerable
        {

            readonly XmlFile XmlFile;

            public SimpleEnumerator(XmlFile XmlFile)
            {
                this.XmlFile = XmlFile;
            }

            // Patched BlockTexturesFromXML.CreateBlockTextures
            public IEnumerator GetEnumerator()
            {
                XmlElement documentElement = XmlFile.XmlDoc.DocumentElement;
                if (documentElement.ChildNodes.Count == 0)
                    throw new Exception("No element <block_textures> found!");
                IEnumerator enumerator = documentElement.ChildNodes.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        XmlNode current = (XmlNode)enumerator.Current;

                        if (current.NodeType == XmlNodeType.Element && current.Name.Equals("paint"))
                        {

                            XmlElement xmlElement = (XmlElement)current;
                            DynamicProperties dynamicProperties = new DynamicProperties();
                            foreach (XmlNode childNode in xmlElement.ChildNodes)
                            {
                                if (childNode.NodeType == XmlNodeType.Element && childNode.Name.Equals("property"))
                                    dynamicProperties.Add(childNode);
                            }
                            BlockTextureData blockTextureData = new BlockTextureData()
                            {
                                Name = xmlElement.GetAttribute("name")
                            };

                            var mesh = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];

                            // Containing a diffuse property is our magic token
                            if (dynamicProperties.Values.ContainsKey("Diffuse"))
                            {

                                var texture = new TexInfo(xmlElement, dynamicProperties);
                                int uvmap = PatchBlocksAtlas(mesh, texture);
                                blockTextureData.TextureID = (ushort)(uvmap);
                                blockTextureData.ID = ++PaintID;
                                Textures.Add(texture);

                            }
                            else
                            {

                                // This is the path the vanilla code takes
                                PaintID = blockTextureData.ID = int.Parse(xmlElement.GetAttribute("id"));
                                if (dynamicProperties.Values.ContainsKey("TextureId"))
                                    blockTextureData.TextureID = Convert.ToUInt16(dynamicProperties.Values["TextureId"]);

                            }

                            blockTextureData.LocalizedName = Localization.Get(blockTextureData.Name);
                            if (dynamicProperties.Values.ContainsKey("Group"))
                                blockTextureData.Group = dynamicProperties.Values["Group"];
                            blockTextureData.PaintCost = !dynamicProperties.Values.ContainsKey("PaintCost") ?
                                (ushort)1 : Convert.ToUInt16(dynamicProperties.Values["PaintCost"]);
                            if (dynamicProperties.Values.ContainsKey("Hidden"))
                                blockTextureData.Hidden = Convert.ToBoolean(dynamicProperties.Values["Hidden"]);
                            if (dynamicProperties.Values.ContainsKey("SortIndex"))
                                blockTextureData.SortIndex = Convert.ToByte(dynamicProperties.Values["SortIndex"]);

                            blockTextureData.Init();

                        }
                        else if (current.NodeType == XmlNodeType.Element && current.Name.Equals("terrain"))
                        {

                            XmlElement xmlElement = (XmlElement)current;
                            DynamicProperties dynamicProperties = new DynamicProperties();
                            foreach (XmlNode childNode in xmlElement.ChildNodes)
                            {
                                if (childNode.NodeType == XmlNodeType.Element && childNode.Name.Equals("property"))
                                    dynamicProperties.Add(childNode);
                            }

                            var mesh = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
                            var texture = new TexInfo(xmlElement, dynamicProperties);
                            PatchTerrainAtlas(mesh, texture);

                        }
                    }
                    yield break;
                }
                finally
                {
                    if (enumerator is IDisposable disposable2)
                        disposable2.Dispose();
                }
            }
        }

        static bool Prefix(
            XmlFile _xmlFile,
            ref IEnumerator __result)
        {
            BlockTextureData.InitStatic();
            var myEnumerator = new SimpleEnumerator(_xmlFile);
            __result = myEnumerator.GetEnumerator();
            return false;
        }
    }

    static Texture2D ResizeTexture(Texture2D texture2D, int width, int height, bool color32)
    {
        RenderTexture rt = new RenderTexture(width, height, color32 ? 32 : 24);
        RenderTexture.active = rt;
        Graphics.Blit(texture2D, rt);
        Texture2D resize = new Texture2D(width, height);
        resize.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resize.Apply(true);
        return resize;
    }

    [HarmonyPatch(typeof(int))]
    [HarmonyPatch("Parse")]
    [HarmonyPatch(new Type[] { typeof(string) })]
    class BlockTexturesFromXML_CreateBlockTextures
    {
        static bool Prefix(string s, ref int __result)
        {
            // This is a somewhat dirty hack, but works none the less
            // Given that parsing anything is a expensive task, this
            // lookup doesn't take much from runtime performance.
            if (UvMap.TryGetValue(s, out int value))
            {
                __result = value;
                return false;
            }
            return true;
        }

    }

    [HarmonyPatch(typeof(MeshDescription))]
    [HarmonyPatch("Init")]
    public class MeshDescription_Init
    {
        static void Postfix(int _idx, ref TextureAtlas _ta)
        {
            if (Dump == false) return;
            DumpedDiffuse = 0; DumpedSpecular = 0; DumpedNormal = 0;
            System.IO.Directory.CreateDirectory("export");
            System.IO.Directory.CreateDirectory("export/opaque");
            System.IO.Directory.CreateDirectory("export/terrain");
            System.IO.Directory.CreateDirectory("export/decals");
            // if (_idx == MeshDescription.MESH_OPAQUE) DumpAtlas(_ta, "opaque");
            if (_idx == MeshDescription.MESH_TERRAIN) DumpTerrainAtlas(_ta, "terrain");
            if (_idx == MeshDescription.MESH_DECALS) DumpTerrainAtlas(_ta, "decals");
        }

    }

}