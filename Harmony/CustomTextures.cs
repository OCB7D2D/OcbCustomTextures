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
using static StringParsers;
using XMLData;
using System.Security.Policy;
using UnityEngine.Assertions;

/*
// MicroSplat Texture2DArray
0 - Snow Biome Top-Soil (Top)
1 - Snow Biome Top-Soil (Side)
2 - Forrest Biome Top-Soil (Top)
3 - Desert Biome Top-Soil (Side)
4 - Main Road Top Soil (Asphalt)
5 - Small Road Top Soil (Gravel)
6 - ?? Seldomly seen in destroyed biome (shamway factory)
7 - Desert Biome Top-Soil "Sand" (Top)
8 - Burnt Biome Top-Soil Bricks (Top)
9 - Water (Side/Bottom) Terrain
10 - Burnt Biome Top-Soil Grass (Top) - partially shown for 9 too!?
11 - Desert Biome Top-Soil "Grass" (Top)
12 - Desert Biome Top-Soil "Sand" (Side)
13 - Farmland and below stone blocks
14 - Gravel
15 - Coal
16 - Asphalt
17 - Iron
18 - Potassium
19 - Underground Stone (Others)
20 - Unused? (Looks like Underground Stone Desert)
21 - Oil Shale
22 - Lead
23 - Destroyed Stone
*/

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
    static readonly List<TextureConfig> CustomDecals = new List<TextureConfig>();
    static readonly List<TextureConfig> CustomGrass = new List<TextureConfig>();
    static readonly List<MicroSplatConfig> CustomMicroSplat = new List<MicroSplatConfig>();

    
    static readonly Dictionary<int, CustomBiomeLayer> CustomBiomeLayers = new Dictionary<int, CustomBiomeLayer>();
    public static readonly Dictionary<int, CustomBiomeColor> CustomBiomeColors = new Dictionary<int, CustomBiomeColor>();


    // Counter how many individual textures are added
    static int OpaquesAdded = 0;
    static int TerrainsAdded = 0;
    static int DecalsAdded = 0;
    static int GrassAdded = 0;

    public struct CustomBiomeColor
    {
        public int index;
        public Color32 color1;
        public Color32 color2;
    }

    public struct CustomBiomeLayer
    {
        public int index;
        public DynamicProperties props;
        public List<Keyframe> heights;
        public List<Keyframe> slopes;
        public List<Keyframe> cavities;
        public List<Keyframe> erosions;
    }

    public void InitMod(Mod mod)
    {
        Debug.Log("Loading OCB Texture Atlas Patch: " + GetType().ToString());
        new Harmony(GetType().ToString()).PatchAll(Assembly.GetExecutingAssembly());
        if (GameManager.IsDedicatedServer) return; // Don't patch server instance
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

        ApplyCustomMicroSplats(terrain);

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

    private static void ApplyCustomMicroSplats(MeshDescription terrain)
    {
        int maxMicroSplatIndex = 0;
        foreach (var texture in CustomMicroSplat) maxMicroSplatIndex
            = Math.Max(maxMicroSplatIndex, texture.Index + 1);
        ExtendMicroSplatTexture(terrain, maxMicroSplatIndex);
        foreach (var texture in CustomMicroSplat)
            PatchMicroSplatTexture(terrain, texture);
        terrain.ReloadTextureArrays(false);
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
        BlockCustomTerrain.GameShutdown();
        Registered = false;
    }


    static int PatchAtlasBlocks(MeshDescription mesh, TextureConfig tex)
    {

        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        var textureID = atlas.uvMapping.Length;

        // Log.Out("Adding opaque texture {0} at uvMapping[{1}] with index[{2}]", tex.ID, textureID, tex.tiling.index);

        if (!UvMap.ContainsKey(tex.ID)) UvMap[tex.ID] = textureID;
        else if (UvMap[tex.ID] != textureID) Log.Warning(
                 "Overwriting texture key {0}", tex.ID);

        if (GameManager.IsDedicatedServer)
        {
            if (atlas.uvMapping.Length < textureID + 1)
                Array.Resize(ref atlas.uvMapping, textureID + 1);
            atlas.uvMapping[textureID] = tex.tiling;
            return textureID;
        }

        if (!(atlas.diffuseTexture is Texture2DArray tex2Darr))
        {
            throw new Exception("Expected Texture2DArray");
        }

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

    private static void PatchMicroSplatTexture(Texture2DArray arr,
        DataLoader.DataPathIdentifier path, int index)
    {
        if (path.AssetName.EndsWith("]"))
        {
            var start = path.AssetName.LastIndexOf("[");
            if (start != -1)
            {
                var nr = int.Parse(path.AssetName.Substring(
                    start + 1, path.AssetName.Length - start - 2));
                var tex = AssetBundleManager.Instance.Get<Texture2DArray>(
                    path.BundlePath, path.AssetName.Substring(0, start));
                var off = Quality > 2 ? 2 : Quality;
                // Graphics.CopyTexture(texture, 0, arr, index);
                for (int m = 0; m < arr.mipmapCount; m++)
                    Graphics.CopyTexture(tex, nr, m + off, arr, index, m);
                return;
            }
        }
        PatchMicroSplatTexture(arr, LoadTexture(path), index);
    }

    private static void PatchMicroSplatTexture(Texture2DArray arr,
        Texture2D texture, int index)
    {
        // Can't go lower than 512x512
        var off = Quality > 2 ? 2 : Quality;
        // Log.Out("Patch MicroSplat at index {0} ({1}) {2}", index, arr.name, arr.isReadable);
        // Patch on the CPU if readable
        if (arr.isReadable && off > 0)
        {
            // Do the pixel patching on the CPU
            for (int m = 0; m < arr.mipmapCount; m++)
                arr.SetPixelData(texture.GetPixelData<byte>(m + off), m, index);
            arr.Apply(updateMipmaps: false);
        }
        // Otherwise do it on the GPU directly
        else
        {
            // Graphics.CopyTexture(texture, 0, arr, index);
            for (int m = 0; m < arr.mipmapCount; m++)
                Graphics.CopyTexture(texture, 0, m + off, arr, index, m);
            // if (arr.isReadable) arr.Apply(false, false);
        }
        if (!arr.name.Contains("_patched"))
            arr.name += "_patched";
    }

    static Texture2D FullBlankBlackTexture = null;
    static Texture2D FullBlankNormalTexture = null;

    public static Texture2D GetFullBlackTexture()
    {
        if (FullBlankBlackTexture != null) return FullBlankBlackTexture;
        return FullBlankBlackTexture = CreateBlackTexture(2048, 2048);
    }

    public static Texture2D GetFullNormalTexture()
    {
        if (FullBlankNormalTexture != null) return FullBlankNormalTexture;
        return FullBlankNormalTexture = CreateNormalTexture(2048, 2048);
    }

    // Layer 0 => (1.0, 0.0, 0.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 1
    // Layer 1 => (1.0, 0.0, 0.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 0
    // Layer 2 => (0.0, 1.0, 0.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 1
    // Layer 3 => (0.0, 1.0, 0.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 2
    // Layer 4 => (0.0, 0.0, 1.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 10
    // Layer 5 => (0.0, 0.0, 1.0, 0.0) and (0.0, 0.0, 0.0, 0.0) => texture 1
    // Layer 6 => (0.0, 0.0, 0.0, 1.0) and (0.0, 0.0, 0.0, 0.0) => texture 10
    // Layer 7 => (0.0, 0.0, 0.0, 1.0) and (0.0, 0.0, 0.0, 0.0) => texture 8
    // Layer 8 => (0.0, 0.0, 0.0, 0.0) and (1.0, 0.0, 0.0, 0.0) => texture 7
    // Layer 9 => (0.0, 0.0, 0.0, 0.0) and (1.0, 0.0, 0.0, 0.0) => texture 3
    // Layer 10 => (0.0, 0.0, 0.0, 0.0) and (1.0, 0.0, 0.0, 0.0) => texture 12
    // Layer 11 => (0.0, 0.0, 0.0, 0.0) and (1.0, 0.0, 0.0, 0.0) => texture 11

    public static bool LockCustomBiomeLayers = false;

    [HarmonyPatch(typeof(MicroSplatProceduralTextureConfig), "GetCurveTexture")]
    class MicroSplatProceduralTextureConfigGetCurveTexture
    {
        static void Prefix(MicroSplatProceduralTextureConfig __instance)
        {
            if (LockCustomBiomeLayers) return;
            foreach (var kv in CustomBiomeLayers)
            {
                var index = kv.Key;
                var props = kv.Value.props;
                while (index >= __instance.layers.Count)
                {
                    Log.Out("Adding new Layer {0}", __instance.layers.Count);
                    __instance.layers.Add(new MicroSplatProceduralTextureConfig.Layer());
                }
                var layer = __instance.layers[index];
                props.ParseFloat("weight", ref layer.weight);
                props.ParseBool("noise-active", ref layer.noiseActive);
                props.ParseFloat("noise-frequency", ref layer.noiseFrequency);
                props.ParseFloat("noise-offset", ref layer.noiseOffset);
                props.ParseVec("noise-range", ref layer.noiseRange);
                props.ParseBool("height-active", ref layer.heightActive);
                props.ParseBool("slope-active", ref layer.slopeActive);
                props.ParseBool("erosion-active", ref layer.erosionMapActive);
                props.ParseBool("cavity-active", ref layer.cavityMapActive);
                props.ParseEnum("height-curve-mode", ref layer.heightCurveMode);
                props.ParseEnum("slope-curve-mode", ref layer.slopeCurveMode);
                props.ParseEnum("erosion-curve-mode", ref layer.erosionCurveMode);
                props.ParseEnum("cavity-curve-mode", ref layer.cavityCurveMode);
                props.ParseInt("microsplat-index", ref layer.textureIndex);
                if (props.Contains("biome-weight-1")) layer.biomeWeights =
                    ParseVector4(props.GetString("biome-weight-1"));
                if (props.Contains("biome-weight-2")) layer.biomeWeights2 =
                    ParseVector4(props.GetString("biome-weight-2"));
                if (kv.Value.heights != null) layer.heightCurve.keys = kv.Value.heights.ToArray();
                if (kv.Value.slopes != null) layer.slopeCurve.keys = kv.Value.slopes.ToArray();
                if (kv.Value.erosions != null) layer.erosionMapCurve.keys = kv.Value.erosions.ToArray();
                if (kv.Value.cavities != null) layer.cavityMapCurve.keys = kv.Value.cavities.ToArray();
                Log.Out("Set {0}: tex {1}, weights: {2}/{3}/{4}",
                    index, layer.textureIndex, layer.weight,
                    layer.biomeWeights, layer.biomeWeights2);
            }

        }
    }

    public static void ExtendMicroSplatTexture(MeshDescription mesh, int size)
    {
        if (!mesh.IsSplatmap(MeshDescription.MESH_TERRAIN)) return;
        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasTerrain;
        Quality = GameOptionsManager.GetTextureQuality();
        if (!(atlas.diffuseTexture is Texture2DArray albedos))
            throw new Exception("Expected Texture2DArray for diffuse");
        if (!(atlas.normalTexture is Texture2DArray normals))
            throw new Exception("Expected Texture2DArray for normal");
        if (!(atlas.specularTexture is Texture2DArray speculars))
            throw new Exception("Expected Texture2DArray for specular");
        mesh.TexDiffuse = atlas.diffuseTexture = ExtendMicroSplatTexture(albedos, size);
        mesh.TexNormal = atlas.normalTexture = ExtendMicroSplatTexture(normals, size);
        mesh.TexSpecular = atlas.specularTexture = ExtendMicroSplatTexture(speculars, size);
        mesh.ReloadTextureArrays(true);

    }

    private static Texture2DArray ExtendMicroSplatTexture(Texture2DArray tarr, int size)
    {
        if (tarr.depth >= size) return tarr;
        return ResizeTextureArray2(tarr, size, false, false);
    }

    public static void PatchMicroSplatTexture(MeshDescription mesh, MicroSplatConfig cfg)
    {
        if (!mesh.IsSplatmap(MeshDescription.MESH_TERRAIN)) return;
        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasTerrain;
        Quality = GameOptionsManager.GetTextureQuality();
        if (!(atlas.diffuseTexture is Texture2DArray albedos))
            throw new Exception("Expected Texture2DArray for diffuse");
        if (!(atlas.normalTexture is Texture2DArray normals))
            throw new Exception("Expected Texture2DArray for normal");
        if (!(atlas.specularTexture is Texture2DArray speculars))
            throw new Exception("Expected Texture2DArray for specular");
        PatchMicroSplatTexture(albedos, cfg.Diffuse.Path, cfg.Index);
        if (cfg.Normal != null) PatchMicroSplatTexture(
            normals, cfg.Normal.Path, cfg.Index);
        else PatchMicroSplatTexture(speculars,
            GetFullNormalTexture(), cfg.Index);
        if (cfg.Specular != null) PatchMicroSplatTexture(
            speculars, cfg.Specular.Path, cfg.Index);
        else PatchMicroSplatTexture(speculars,
            GetFullBlackTexture(), cfg.Index);
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

        if (GameManager.IsDedicatedServer)
        {
            if (atlas.uvMapping.Length < textureID + 1)
                Array.Resize(ref atlas.uvMapping, textureID + 1);
            atlas.uvMapping[textureID] = tex.tiling;
            return textureID;
        }

        // Make sure all our arrays really have enough space
        // This should already have happened for better performance
        if (atlas.diffuse.Length <= tex.tiling.index)
        {
            Log.Warning("Resize diffuse to {0}", tex.tiling.index + 1);
            Array.Resize(ref atlas.diffuse, tex.tiling.index + 1);
        }
        if (atlas.normal.Length <= tex.tiling.index)
        {
            Log.Warning("Resize normal to {0}", tex.tiling.index + 1);
            Array.Resize(ref atlas.normal, tex.tiling.index + 1);
        }
        if (atlas.specular.Length <= tex.tiling.index)
        {
            Log.Warning("Resize specular to {0}", tex.tiling.index + 1);
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

    static List<Texture2D> GrassDiffuses = null;
    static List<Texture2D> GrassNormals = null;
    static List<Texture2D> GrassAOST = null;


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
                MicroStopwatch msw = new MicroStopwatch(true);

                XmlElement documentElement = XmlFile.XmlDoc.DocumentElement;
                if (documentElement.ChildNodes.Count == 0)
                    throw new Exception("No element <block_textures> found!");

                // Clear structures before parsing
                OpaquesAdded = 0;
                TerrainsAdded = 0;
                GrassAdded = 0;
                DecalsAdded = 0;
                CustomOpaques.Clear();
                CustomTerrains.Clear();
                CustomGrass.Clear();
                CustomMicroSplat.Clear();
                CustomBiomeColors.Clear();
                CustomBiomeLayers.Clear();
                GrassDiffuses = null;
                GrassNormals = null;
                GrassAOST = null;

                var opaque = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
                var terrain = MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
                var opaqueAtlas = opaque.textureAtlas as TextureAtlasBlocks;
                var terrainAtlas = terrain.textureAtlas as TextureAtlasTerrain;

                int builtinTerrains = terrainAtlas.diffuse.Length;
                int builtinOpaques = opaqueAtlas.diffuseTexture == null ?
                    0 : (opaqueAtlas.diffuseTexture as Texture2DArray).depth;

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

                if (OpaquesAdded > 0 && !GameManager.IsDedicatedServer)
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

                if (TerrainsAdded > 0 && !GameManager.IsDedicatedServer)
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
                                if (int.TryParse(xmlElement.GetAttribute("id"), out int idx))
                                    blockTextureData.ID = idx; // overwrite existing texture
                                else blockTextureData.ID = GetFreePaintID();
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
                        // terrain xml elements are only known by us
                        else if (xmlElement.Name.Equals("terrain"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            var texture = new TextureConfig(xmlElement, props);
                            texture.tiling.index = builtinTerrains + TerrainsAdded;
                            PatchAtlasTerrain(terrain, texture);
                            TerrainsAdded += texture.Length;
                            CustomTerrains.Add(texture);
                        }
                        // microsplat xml elements are only known by us
                        else if (xmlElement.Name.Equals("microsplat"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            var texture = new MicroSplatConfig(xmlElement, props);
                            // PatchMicroSplatTexture(terrain, texture);
                            CustomMicroSplat.Add(texture);
                        }
                        // decal xml elements are only known by us
                        else if (xmlElement.Name.Equals("decal"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            var texture = new TextureConfig(xmlElement, props);
                            DecalsAdded += texture.Length;
                            CustomDecals.Add(texture);
                        }
                        // grass xml elements are only known by us
                        else if (xmlElement.Name.Equals("grass"))
                        {
                            DynamicProperties props = GetDynamicProperties(xmlElement);
                            var texture = new TextureConfig(xmlElement, props);
                            GrassAdded += texture.Length;
                            CustomGrass.Add(texture);
                        }
                        else if (xmlElement.Name.Equals("biome-layer"))
                        {
                            int index = int.Parse(xmlElement.GetAttribute("index"));
                            CustomBiomeLayers.Add(index, new CustomBiomeLayer()
                            {
                                index = index, props = GetDynamicProperties(xmlElement),
                                heights = ParseBiomeLayer(xmlElement, "height-keyframes"),
                                slopes = ParseBiomeLayer(xmlElement, "slope-keyframes"),
                                cavities = ParseBiomeLayer(xmlElement, "cavity-keyframes"),
                                erosions = ParseBiomeLayer(xmlElement, "erosion-keyframes"),
                            });
                        }
                        else if (xmlElement.Name.Equals("biome-color"))
                        {
                            int index = int.Parse(xmlElement.GetAttribute("biome"));
                            CustomBiomeColors.Add(index, new CustomBiomeColor()
                            {
                                index = index,
                                color1 = ParseColor32(xmlElement.GetAttribute("color1")),
                                color2 = ParseColor32(xmlElement.GetAttribute("color2")),
                            });
                        }
                    }
                    // EO read XML

                    if (OpaquesAdded > 0 && !GameManager.IsDedicatedServer)
                    {
                        // Apply pixel changes only when finished
                        // Reduces loading times to nearly instantly
                        ApplyPixelChanges(opaqueAtlas.diffuseTexture, false);
                        ApplyPixelChanges(opaqueAtlas.normalTexture, false);
                        ApplyPixelChanges(opaqueAtlas.specularTexture, false);
                        opaque.ReloadTextureArrays(false);
                        Log.Out("Opaque textures patched in {0}ms",
                            msw.ElapsedMilliseconds);
                    }

                    if (TerrainsAdded > 0 && !GameManager.IsDedicatedServer)
                    {
                        // Apply pixel changes only when finished
                        // Reduces loading times to nearly instantly
                        // Note: we don't alter MicroSplat-Maps yet
                        //ApplyPixelChanges(terrainAtlas.diffuseTexture, false);
                        //ApplyPixelChanges(terrainAtlas.normalTexture, false);
                        //ApplyPixelChanges(terrainAtlas.specularTexture, false);
                        //terrain.ReloadTextureArrays(false);
                    }

                    if (GrassAdded > 0 && GameManager.IsDedicatedServer)
                    {
                        foreach (TextureConfig custom in CustomGrass)
                            UvMap[custom.ID] = 0; // Just make it known
                    }

                    // Experimental Grass Atlas Patching
                    if (GrassAdded > 0 && !GameManager.IsDedicatedServer)
                    {

                        msw = new MicroStopwatch(true);

                        List<UVRectTiling> tiles = new List<UVRectTiling>();
                        MeshDescription grass = MeshDescription.meshes[MeshDescription.MESH_GRASS];
                        // DumpTexure2D(grass.TexDiffuse as Texture2D, "Mods/OcbCustomTextures/grass-atlas.png");

                        var xmlcfg = new XmlFile(grass.MetaData);
                        var xmldoc = xmlcfg.XmlDoc.DocumentElement;
                        foreach (XmlNode xmlNode in xmldoc.ChildNodes)
                        {
                            if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name.Equals("uv"))
                            {
                                if (!(xmlNode is XmlElement xmlElement)) continue;
                                int id = int.Parse(xmlElement.GetAttribute("id"));
                                grass.textureAtlas.uvMapping[id].index = id;
                                tiles.Add(grass.textureAtlas.uvMapping[id]);
                            }
                        }

                        // Read all sprites from the Atlas back, so we can add more sprites (once when game is loaded)
                        if (GrassDiffuses == null) GrassDiffuses = GetAtlasSprites(grass.TexDiffuse as Texture2D, tiles);
                        if (GrassNormals == null) GrassNormals = GetAtlasSprites(grass.TexNormal as Texture2D, tiles);
                        if (GrassAOST == null) GrassAOST = GetAtlasSprites(grass.TexSpecular as Texture2D, tiles);

                        // Make a copy for every save game we load to extend conditionally
                        List<Texture2D> diffuses = new List<Texture2D>(GrassDiffuses);
                        List<Texture2D> normals = new List<Texture2D>(GrassNormals);
                        List<Texture2D> aosts = new List<Texture2D>(GrassAOST);

                        // Dump out all sliced textures to check validity
                        // for (int i = 0; i < diffuses.Count; i += 1)
                        // {
                        //     DumpTexure(CropTexture(diffuses[i], 34, 34, true), string.Format("Mods/OcbCustomTextures/Dump/{0}.albedo.png", i));
                        //     DumpTexure(CropTexture(normals[i], 34, 34, true), string.Format("Mods/OcbCustomTextures/Dump/{0}.normal.png", i), UnpackNormalPixels);
                        //     DumpTexure(CropTexture(speculars[i], 34, 34, true), string.Format("Mods/OcbCustomTextures/Dump/{0}.aost.png", i));
                        // }

                        foreach (TextureConfig custom in CustomGrass)
                        {

                            if (custom.Diffuse.Assets.Length == 0) continue;
                            // For now just must pass all three maps (no runtime creation)
                            diffuses.Add(PadTexture(custom.Diffuse.LoadTexture2D(), 34));
                            normals.Add(PadTexture(custom.Normal.LoadTexture2D(), 34));
                            aosts.Add(PadTexture(custom.Specular.LoadTexture2D(), 34));

                            UVRectTiling tile = new UVRectTiling();
                            tile.uv = new Rect(); // Create objects on struct
                            tile.index = GetNextFreeUV(grass.textureAtlas);
                            if (!UvMap.ContainsKey(custom.ID)) UvMap[custom.ID] = tile.index;
                            else if (UvMap[custom.ID] != tile.index) Log.Warning(
                                     "Tried to overwrite texture key {0}", custom.ID);
                            tile.index = UvMap[custom.ID]; // Assign from real UV
                            tile.textureName = custom.Diffuse.Assets[0];
                            tiles.Add(tile);
                        }

                        var t2d = grass.TexDiffuse as Texture2D;
                        var t2n = grass.TexNormal as Texture2D;
                        var t2s = grass.TexSpecular as Texture2D;

                        // DumpTexure2D(t2d, "Mods/OcbCustomTextures/org-grass-diff-atlas.png");
                        // DumpTexure2D(t2n, "Mods/OcbCustomTextures/org-grass-norm-atlas.png");
                        // DumpTexure2D(t2s, "Mods/OcbCustomTextures/org-grass-spec-atlas.png");

                        Texture2D diff_atlas = new Texture2D(8192, 8192, t2d.format, false);
                        Texture2D norm_atlas = new Texture2D(8192, 8192, t2n.format, false);
                        Texture2D spec_atlas = new Texture2D(8192, 8192, t2s.format, false);

                        // We are assuming that "PackTextures" returns the same results for each variant.
                        // All list have the same dimension and should therefore be distributed the same.
                        // If that proves to be shaky, we're fucked, as there is only one UV map for all.
                        var diff_rects = diff_atlas.PackTextures(diffuses.ToArray(), 0, 8192, false);
                        var norm_rects = norm_atlas.PackTextures(normals.ToArray(), 0, 8192, false);
                        var spec_rects = spec_atlas.PackTextures(aosts.ToArray(), 0, 8192, false);
                        // ToDo: we assume we get the same results back from all three calls

                        // DumpTexure2D(diff_atlas, "Mods/OcbCustomTextures/patched-grass-diff-atlas.png");
                        // DumpTexure2D(norm_atlas, "Mods/OcbCustomTextures/patched-grass-norm-atlas.png");
                        // DumpTexure2D(spec_atlas, "Mods/OcbCustomTextures/patched-grass-spec-atlas.png");

                        // Deduce new UV coordinates after packing
                        for (int i = 0; i < tiles.Count; i++)
                        {
                            var tile = tiles[i];
                            if (tile.index == 0) continue;
                            var rect = diff_rects[i];
                            var org = grass.textureAtlas.uvMapping[tile.index].uv;
                            grass.textureAtlas.uvMapping[tile.index].blockH = 1;
                            grass.textureAtlas.uvMapping[tile.index].blockW = 1;
                            grass.textureAtlas.uvMapping[tile.index].bGlobalUV = false;
                            rect.x = (rect.x * diff_atlas.width + 34f) / diff_atlas.width;
                            rect.y = (rect.y * diff_atlas.height + 34f) / diff_atlas.height;
                            rect.width = (rect.width * diff_atlas.width - 68f) / diff_atlas.width;
                            rect.height = (rect.height * diff_atlas.height - 68f) / diff_atlas.height;
                            grass.textureAtlas.uvMapping[tile.index].uv.Set(
                                rect.x, rect.y, rect.width, rect.height);
                            var uv = grass.textureAtlas.uvMapping[tile.index];
                            uv.textureName = tile.textureName;
                        }

                        grass.TexDiffuse = diff_atlas;
                        grass.TexNormal = norm_atlas;
                        grass.TexSpecular = spec_atlas;

                        grass.textureAtlas.diffuseTexture = diff_atlas;
                        grass.textureAtlas.normalTexture = norm_atlas;
                        grass.textureAtlas.specularTexture = spec_atlas;

                        // grass.ReloadTextureArrays(false);

                        grass.material.SetTexture("_Albedo", grass.textureAtlas.diffuseTexture);
                        grass.material.SetTexture("_Normal", grass.textureAtlas.normalTexture);
                        grass.material.SetTexture("_Gloss_AO_SSS", grass.textureAtlas.specularTexture);

                        Log.Out("Grass textures atlas patched in {0}ms",
                            msw.ElapsedMilliseconds);

                    }

                    // Apply all microsplats at once
                    ApplyCustomMicroSplats(terrain);

                    // Decals patching removed for now since due to available space
                    // in raw block data => 4 bits, 16 different decals max.
                    yield break;
                }
                finally
                {
                    if (enumerator is IDisposable disposable2)
                        disposable2.Dispose();
                }
            }

            private List<Keyframe> ParseBiomeLayer(XmlElement xml, string name)
            {
                List<Keyframe> frames = null;
                foreach (XmlNode outer in xml)
                {
                    if (!(outer is XmlElement node)) continue;
                    if (!node.Name.Equals(name)) continue;
                    frames = new List<Keyframe>();
                    foreach (XmlNode inner in node)
                    {
                        if (!(inner is XmlElement child)) continue;
                        if (!child.Name.Equals("keyframe")) continue;
                        Keyframe frame = new Keyframe();
                        if (child.HasAttribute("time")) frame.time =
                            ParseFloat(child.GetAttribute("time"));
                        if (child.HasAttribute("value")) frame.value =
                            ParseFloat(child.GetAttribute("value"));
                        frames.Add(frame);
                    }
                }
                return frames;
            }

            private Texture2D PadTexture(Texture2D tex, int padding)
            {
                // So far we only support 512x512 sized textures (needs more testing)
                // tex = ResizeTexture(tex, tex.width + 68, tex.height + 68, false, 34, 34);
                RenderTexture rt = new RenderTexture(tex.width, tex.height, padding);
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(tex, rt, new Vector2(1, 1), new Vector2(0, 0));
                Texture2D resize = new Texture2D(tex.width + 68, tex.height + padding * 2);
                // Really? No utility to create a clear Texture2D?
                Color32[] resetColorArray = resize.GetPixels32();
                for (int i = 0; i < resetColorArray.Length; i++)
                    resetColorArray[i] = Color.clear;
                resize.SetPixels32(resetColorArray);
                resize.ReadPixels(new Rect(0, 0, tex.width, tex.height), padding, padding);
                resize.Apply(true);
                return resize;
            }

            private int GetNextFreeUV(TextureAtlas textureAtlas)
            {
                int id = textureAtlas.uvMapping.Length;
                Array.Resize(ref textureAtlas.uvMapping, id + 1);
                return id;
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
            if (GameManager.IsDedicatedServer) return true;
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
            if (GameManager.IsDedicatedServer) return true;
            CleanupTextureAtlasBlocks(__instance);
            CleanupTextureAtlasTerrain(__instance);
            Resources.UnloadUnusedAssets();
            return false;
        }
    }

    /*
    [HarmonyPatch(typeof(VoxelMeshTerrain))]
    [HarmonyPatch("ConfigureTerrainMaterial")]
    public class ConfigureTerrainMaterial
    {
        public static void Prefix(VoxelMeshTerrain __instance,
            MicroSplatProceduralTextureConfig ___msProcData)
        {
            Log.Out("VixelMeshTerrain COnfigure {0}", ___msProcData.layers.Count);
        }
    }
    */

    /*
        MicroSplatPropData prop = VoxelMeshTerrainPropData.Get(null);
            for (int n = 0; n< 32; n++) prop.SetValue(24, n, prop.GetValue(3, n));
            for (int n = 0; n< 32; n++) prop.SetValue(25, n, prop.GetValue(3, n));
            for (int n = 0; n< 32; n++) prop.SetValue(26, n, prop.GetValue(3, n));
            for (int n = 0; n< 32; n++) prop.SetValue(27, n, prop.GetValue(3, n));
    */

    /*
        [HarmonyPatch(typeof(VoxelMeshTerrain))]
        [HarmonyPatch("InitMicroSplat")]
        public class VoxelMeshTerrainInitMicroSplat
        {
            public static void Postfix(MicroSplatProceduralTextureConfig ___msProcData,
                ref Texture2D ___msProcCurveTex, ref Texture2D ___msProcParamTex)
            {
                // ___msProcData = Resources.Load<MicroSplatProceduralTextureConfig>("Shaders/MicroSplat/MicroSplatTerrainInGame_proceduraltexture");
                ___msProcCurveTex = ___msProcData.GetCurveTexture();
                ___msProcParamTex = ___msProcData.GetParamTexture();
            }
        }
    */

    public static Vector4 ParseVector4(string _input)
    {
        SeparatorPositions separatorPositions = GetSeparatorPositions(_input, ',', 3);
        if (separatorPositions.TotalFound != 3) return Vector4.zero;
        return new Vector4(ParseFloat(_input, 0, separatorPositions.Sep1 - 1),
            ParseFloat(_input, separatorPositions.Sep1 + 1, separatorPositions.Sep2 - 1),
            ParseFloat(_input, separatorPositions.Sep2 + 1, separatorPositions.Sep3 - 1),
            ParseFloat(_input, separatorPositions.Sep3 + 1));
    }

}
