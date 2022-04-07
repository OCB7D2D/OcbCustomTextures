using HarmonyLib;
using UnityEngine;
using System;
using System.Xml;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using static OCB.TextureUtils;
using static OCB.TextureAtlasUtils;

public class OcbCustomTextures : IModApi
{

    // Last active texture quality
    static int Quality = -1;

    // DImensions of Full Atlas Textures
    // static int OpaqueAtlasDim = 512;
    // static int TerrainAtlasDim = 2048;

    // Flag if texture quality change handler is hooked
    static bool Registered = false;

    // Global mapping to be used by integer parsing hook
    static readonly Dictionary<string, int> UvMap = new Dictionary<string, int>();

    // A static list of additional texture (used for hot reload)
    static readonly List<TextureConfig> CustomOpaques = new List<TextureConfig>();
    static readonly List<TextureConfig> CustomTerrains = new List<TextureConfig>();

    // Counter how many individual textures are added
    static int OpaquesAdded = 0;
    static int TerrainsAdded = 0;

    public void InitMod(Mod mod)
    {
        Debug.Log("Loading OCB Texture Atlas Patch: " + GetType().ToString());
        if (GameManager.IsDedicatedServer) return; // Don't patch server instance
        new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
        ModEvents.GameStartDone.RegisterHandler(GameStartDone);
        ModEvents.GameShutdown.RegisterHandler(GameShutdown);
    }

    void TextureQualityChanged(int quality)
    {
        if (Quality == -1) Quality = quality;
        else if (Quality == 2 && quality == 3) { Quality = 3; return; }
        else if (Quality == 3 && quality == 2) { Quality = 2; return; }
        var opaque = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
        var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
        // Make enough space available
        if (OpaquesAdded > 0)
        {
            if (opaque.TexDiffuse is Texture2DArray diff2DArr)
            {
                bool linear = !GraphicsFormatUtility.IsSRGBFormat(diff2DArr.graphicsFormat);
                opaque.TexDiffuse = opaque.textureAtlas.diffuseTexture = ResizeTextureArray(
                    diff2DArr, diff2DArr.depth + OpaquesAdded, true, linear, true);
            }

            if (opaque.TexNormal is Texture2DArray norm2DArr)
            {
                bool linear = !GraphicsFormatUtility.IsSRGBFormat(norm2DArr.graphicsFormat);
                opaque.TexNormal = opaque.textureAtlas.normalTexture = ResizeTextureArray(
                    norm2DArr, norm2DArr.depth + OpaquesAdded, true, linear, true);
            }

            if (opaque.TexSpecular is Texture2DArray spec2DArr)
            {
                bool linear = !GraphicsFormatUtility.IsSRGBFormat(spec2DArr.graphicsFormat);
                opaque.TexSpecular = opaque.textureAtlas.specularTexture = ResizeTextureArray(
                    spec2DArr, spec2DArr.depth + OpaquesAdded, true, linear, true);
            }
        }
        // Patch the textures into new array
        foreach (var texture in CustomOpaques)
            PatchAtlasBlocks(opaque, texture);
        // Terrain seems to scale automatically?
        // Note: we don't alter MicroSplat-Maps yet
        // foreach (var texture in CustomTerrains)
        //     PatchAtlasTerrain(terrain, texture);
        // Apply pixel changes (expensive)
        if (OpaquesAdded > 0)
        {
            // Apply pixel changes only when finished
            // Reduces loading times to nearly instantly
            ApplyPixelChanges(opaque.textureAtlas.diffuseTexture, false);
            ApplyPixelChanges(opaque.textureAtlas.normalTexture, false);
            ApplyPixelChanges(opaque.textureAtlas.specularTexture, false);
            opaque.ReloadTextureArrays(false);
        }
        if (GameManager.Instance != null && GameManager.Instance.prefabLODManager != null)
            GameManager.Instance.prefabLODManager.UpdateMaterials();
    }

    public void GameStartDone()
    {
        if (Registered) return;
        GameOptionsManager.TextureQualityChanged += TextureQualityChanged;
        GameManager.Instance.prefabLODManager.UpdateMaterials();
        Registered = true;
    }

    public void GameShutdown()
    {
        if (!Registered) return;
        GameOptionsManager.TextureQualityChanged -= TextureQualityChanged;
        Registered = false;
    }


    static int PatchAtlasBlocks(MeshDescription mesh, TextureConfig tex)
    {

        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        var textureID = atlas.uvMapping.Length;

        if (!(atlas.diffuseTexture is Texture2DArray tex2Darr))
        { 
            throw new Exception("Expected Texture2DArray");
        }

        // Log.Out("Adding opaque texture {0} at uvMapping[{1}] with index[{2}]", tex.ID, textureID, tex.tiling.index);

        if (!UvMap.ContainsKey(tex.ID)) UvMap[tex.ID] = textureID;
        else if (UvMap[tex.ID] != textureID) Log.Warning(
                 "Overwriting texture key {0}", tex.ID);

        PatchOpaqueTexture(ref tex2Darr, tex.Diffuse, tex.tiling.index);

        if (atlas.uvMapping.Length < textureID + 1)
        {
            Array.Resize(ref atlas.uvMapping, textureID + 1);
        }
        atlas.uvMapping[textureID] = tex.tiling;

        if (tex.Normal != null && atlas.normalTexture is Texture2DArray norm2Darr)
        {
            PatchOpaqueTexture(ref norm2Darr, tex.Normal, tex.tiling.index);
        }
        else if (atlas.normalTexture is Texture2DArray norm2Darr2)
        {
            PatchOpaqueNormal(ref norm2Darr2, tex.tiling.index, tex.Diffuse.Assets.Length);
        }

        if (tex.Specular != null && atlas.specularTexture is Texture2DArray spec2Darr)
        {
            PatchOpaqueTexture(ref spec2Darr, tex.Specular, tex.tiling.index);
        }
        else if (atlas.specularTexture is Texture2DArray spec2Darr2)
        {
            PatchOpaqueSpecular(ref spec2Darr2, tex.tiling.index, tex.Diffuse.Assets.Length);
        }

        mesh.TexDiffuse = atlas.diffuseTexture;
        mesh.TexNormal = atlas.normalTexture;
        mesh.TexSpecular = atlas.specularTexture;

        // mesh.ReloadTextureArrays(false);
        // mesh.UnloadTextureArrays(MeshDescription.MESH_OPAQUE);
        // Log.Warning("Patched Mesh Atlas now has {0} items",
        //     (atlas.diffuseTexture as Texture2DArray).depth);

        Quality = GameOptionsManager.GetTextureQuality();

        return textureID;

    }

    static int PatchAtlasTerrain(MeshDescription mesh, TextureConfig tex)
    {

        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasTerrain;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        var textureID = atlas.uvMapping.Length;

        if (!UvMap.ContainsKey(tex.ID)) UvMap[tex.ID] = textureID;
        else if (UvMap[tex.ID] != textureID) Log.Warning(
                 "Overwriting texture key {0}", tex.ID);

        // Make sure all our arrays really have enough space
        // This should already have happened for better performance
        if (atlas.diffuse.Length <= tex.tiling.index)
        {
            Log.Out("Resize diffuse to {0}", tex.tiling.index + 1);
            Array.Resize(ref atlas.diffuse, tex.tiling.index + 1);
        }
        if (atlas.normal.Length <= tex.tiling.index)
        {
            Log.Out("Resize normal to {0}", tex.tiling.index + 1);
            Array.Resize(ref atlas.normal, tex.tiling.index + 1);
        }
        if (atlas.specular.Length <= tex.tiling.index)
        {
            Log.Out("Resize specular to {0}", tex.tiling.index + 1);
            Array.Resize(ref atlas.specular, tex.tiling.index + 1);
        }

        // Hasn't been optimized, but should be OKish
        Array.Resize(ref atlas.uvMapping, textureID + 1);
        atlas.uvMapping[textureID] = tex.tiling;

        // Get the resource bundle and asset path
        var texture = LoadTexture(tex.Diffuse.Path);
        // texture = GetBestMipMapTexture(texture, 256);

        // Log.Out("Adding terrain texture {0} at uvMapping[{1}] with index[{2}] ({3}x{4})",
        //     tex.ID, textureID, tex.tiling.index, texture.width, texture.height);

        atlas.diffuse[tex.tiling.index] = texture;

        if (tex.Normal != null)
        {
            var normal = LoadTexture(tex.Normal.Path);
            // normal = GetBestMipMapTexture(normal, 256);
            atlas.normal[tex.tiling.index] = normal;
        }
        else
        {
            var normal = GetTerrainNormal(texture.width);
            normal.filterMode = FilterMode.Trilinear;
            normal.wrapMode = TextureWrapMode.Repeat;
            atlas.normal[tex.tiling.index] = normal;
        }

        if (tex.Specular != null)
        {
            var specular = LoadTexture(tex.Specular.Path);
            // specular = GetBestMipMapTexture(specular, 256);
            atlas.specular[tex.tiling.index] = specular;
        }
        else
        {
            // Specular seems OK if it is null (same as black?)
            // var specular = GetTerrainSpecular(texture.width);
            // specular.filterMode = FilterMode.Trilinear;
            // specular.wrapMode = TextureWrapMode.Repeat;
            // atlas.specular[tex.tiling.index] = specular;
        }

        mesh.TexDiffuse = atlas.diffuseTexture;
        mesh.TexNormal = atlas.normalTexture;
        mesh.TexSpecular = atlas.specularTexture;

        // Only Terrain uses SplatMap?
        // mesh.ReloadTextureArrays(true);

        Quality = GameOptionsManager.GetTextureQuality();

        return textureID;

    }

    static int GetFreePaintID()
    {
        
        for (var i = 0; i < BlockTextureData.list.Length; i++)
        {
            if (BlockTextureData.list[i] == null) return i;
        }
        throw new Exception("No more free Paint IDs");
    }

    static public DynamicProperties GetDynamicProperties(XmlNode xml)
    {
        DynamicProperties props = new DynamicProperties();
        foreach (XmlNode childNode in xml.ChildNodes)
        {
            if (childNode.NodeType != XmlNodeType.Element) continue;
            if (childNode.Name.Equals("property") == false) continue;
            props.Add(childNode);
        }
        return props;

    }

    [HarmonyPatch(typeof(BlockTexturesFromXML))]
    [HarmonyPatch("CreateBlockTextures")]
    public class Patches
    {
        class PatchedEnumerator : IEnumerable
        {

            readonly XmlFile XmlFile;

            public PatchedEnumerator(XmlFile XmlFile)
            {
                this.XmlFile = XmlFile;
            }

            // Patched BlockTexturesFromXML.CreateBlockTextures
            public IEnumerator GetEnumerator()
            {
                XmlElement documentElement = XmlFile.XmlDoc.DocumentElement;
                if (documentElement.ChildNodes.Count == 0)
                    throw new Exception("No element <block_textures> found!");

                var opaque = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
                var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
                var opaqueAtlas = opaque.textureAtlas as TextureAtlasBlocks;
                var terrainAtlas = terrain.textureAtlas as TextureAtlasTerrain;

                int builtinTerrains = terrainAtlas.diffuse.Length;
                int builtinOpaques = (opaqueAtlas.diffuseTexture as Texture2DArray).depth;

                /* PRE-PARSE STEP TO DETERMINE FINAL NUMBERS FIRST */

                var children = documentElement.ChildNodes;
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i].NodeType != XmlNodeType.Element) continue;
                    if (!(children[i] is XmlElement el)) continue;
                    if (el.Name.Equals("paint"))
                    {
                        // Containing a diffuse property is our magic token
                        DynamicProperties props = GetDynamicProperties(el);
                        if (!props.Values.ContainsKey("Diffuse")) continue;
                        var texture = new TextureConfig(el, props);
                        texture.tiling.index = builtinOpaques + OpaquesAdded;
                        OpaquesAdded += texture.Length;
                    }
                    else if (el.NodeType == XmlNodeType.Element && el.Name.Equals("terrain"))
                    {
                        DynamicProperties props = GetDynamicProperties(el);
                        var texture = new TextureConfig(el, props);
                        texture.tiling.index = builtinTerrains + TerrainsAdded;
                        TerrainsAdded += texture.Length;
                    }
                }

                /* ADD SPACE FOR ADDITIONAL TEXTURES IN ONE GO FOR BEST PERFORMANCE */

                if (OpaquesAdded > 0)
                {
                    if (opaque.TexDiffuse is Texture2DArray diff2DArr)
                    {
                        bool linear = !GraphicsFormatUtility.IsSRGBFormat(diff2DArr.graphicsFormat);
                        opaque.TexDiffuse = opaque.textureAtlas.diffuseTexture = ResizeTextureArray(
                            diff2DArr, diff2DArr.depth + OpaquesAdded, true, linear, true);
                    }

                    if (opaque.TexNormal is Texture2DArray norm2DArr)
                    {
                        bool linear = !GraphicsFormatUtility.IsSRGBFormat(norm2DArr.graphicsFormat);
                        opaque.TexNormal = opaque.textureAtlas.normalTexture = ResizeTextureArray(
                            norm2DArr, norm2DArr.depth + OpaquesAdded, true, linear, true);
                    }

                    if (opaque.TexSpecular is Texture2DArray spec2DArr)
                    {
                        bool linear = !GraphicsFormatUtility.IsSRGBFormat(spec2DArr.graphicsFormat);
                        opaque.TexSpecular = opaque.textureAtlas.specularTexture = ResizeTextureArray(
                            spec2DArr, spec2DArr.depth + OpaquesAdded, true, linear, true);
                    }
                }

                if (TerrainsAdded > 0)
                {
                    Array.Resize(ref terrainAtlas.diffuse, terrainAtlas.diffuse.Length + TerrainsAdded);
                    Array.Resize(ref terrainAtlas.normal, terrainAtlas.normal.Length + TerrainsAdded);
                    Array.Resize(ref terrainAtlas.specular, terrainAtlas.specular.Length + TerrainsAdded);
                }

                // Reset counters
                OpaquesAdded = 0;
                TerrainsAdded = 0;

                /* ACTUAL PARSING AND ASSIGNING (PATCHED VANILLA CODE) */

                IEnumerator enumerator = children.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        if (!(enumerator.Current is XmlElement xmlElement)) continue;
                        if (xmlElement.Name.Equals("paint"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            BlockTextureData blockTextureData = new BlockTextureData()
                            {
                                Name = xmlElement.GetAttribute("name")
                            };
                            // Containing a diffuse property is our magic token
                            if (props.Values.ContainsKey("Diffuse"))
                            {
                                var texture = new TextureConfig(xmlElement, props);
                                texture.tiling.index = builtinOpaques + OpaquesAdded;
                                int TextureID = PatchAtlasBlocks(opaque, texture);
                                blockTextureData.TextureID = (ushort)(TextureID);
                                blockTextureData.ID = GetFreePaintID();
                                OpaquesAdded += texture.Length;
                                CustomOpaques.Add(texture);
                            }
                            else
                            {
                                // This is the path vanilla code takes
                                blockTextureData.ID = int.Parse(xmlElement.GetAttribute("id"));
                                if (props.Values.ContainsKey("TextureId"))
                                    blockTextureData.TextureID = Convert.ToUInt16(props.Values["TextureId"]);
                            }
                            blockTextureData.LocalizedName = Localization.Get(blockTextureData.Name);
                            if (props.Values.ContainsKey("Group"))
                                blockTextureData.Group = props.Values["Group"];
                            blockTextureData.PaintCost = !props.Values.ContainsKey("PaintCost") ?
                                (ushort)1 : Convert.ToUInt16(props.Values["PaintCost"]);
                            if (props.Values.ContainsKey("Hidden"))
                                blockTextureData.Hidden = Convert.ToBoolean(props.Values["Hidden"]);
                            if (props.Values.ContainsKey("SortIndex"))
                                blockTextureData.SortIndex = Convert.ToByte(props.Values["SortIndex"]);
                            blockTextureData.Init();
                        }
                        // terrain elements are only known by us
                        else if (xmlElement.Name.Equals("terrain"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            var texture = new TextureConfig(xmlElement, props);
                            texture.tiling.index = builtinTerrains + TerrainsAdded;
                            PatchAtlasTerrain(terrain, texture);
                            TerrainsAdded += texture.Length;
                            CustomTerrains.Add(texture);
                        }
                    }
                    if (OpaquesAdded > 0)
                    {
                        // Apply pixel changes only when finished
                        // Reduces loading times to nearly instantly
                        ApplyPixelChanges(opaqueAtlas.diffuseTexture, false);
                        ApplyPixelChanges(opaqueAtlas.normalTexture, false);
                        ApplyPixelChanges(opaqueAtlas.specularTexture, false);
                        opaque.ReloadTextureArrays(false);
                    }
                    if (TerrainsAdded > 0)
                    {
                        // Apply pixel changes only when finished
                        // Reduces loading times to nearly instantly
                        // Note: we don't alter MicroSplat-Maps yet
                        //ApplyPixelChanges(terrainAtlas.diffuseTexture, false);
                        //ApplyPixelChanges(terrainAtlas.normalTexture, false);
                        //ApplyPixelChanges(terrainAtlas.specularTexture, false);
                        //terrain.ReloadTextureArrays(false);
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
            var patchedEnumerator = new PatchedEnumerator(_xmlFile);
            __result = patchedEnumerator.GetEnumerator();
            return false;
        }
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

    [HarmonyPatch(typeof(TextureAtlasBlocks))]
    [HarmonyPatch("Cleanup")]
    public class TextureAtlasBlocks_Cleanup
    {
        public static bool Prefix(TextureAtlasBlocks __instance)
        {
            CleanupTextureAtlasBlocks(__instance);
            Resources.UnloadUnusedAssets();
            return false;
        }
    }

    [HarmonyPatch(typeof(TextureAtlasTerrain))]
    [HarmonyPatch("Cleanup")]
    public class TextureAtlasTerrain_Cleanup
    {
        public static bool Prefix(TextureAtlasTerrain __instance)
        {
            CleanupTextureAtlasBlocks(__instance);
            CleanupTextureAtlasTerrain(__instance);
            Resources.UnloadUnusedAssets();
            return false;
        }
    }

}