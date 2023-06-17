using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using static OcbTextureUtils;

public static class GrassTextures
{

    // ####################################################################
    // ####################################################################

    // Holding the parsed configurations
    static readonly List<TextureConfig>
        CustomGrass = new List<TextureConfig>();

    // Helper for tile distribution
    static readonly TilingArea
        Tilings = new TilingArea();

    // Map for texture name lookups
    static Dictionary<string, int> UvMap
        => OcbCustomTextures.UvMap;

    // ####################################################################
    // Parse grass texture configs to be used later
    // ####################################################################

    static void ParseGrassConfig(XElement root)
    {
        Tilings.Clear();
        CustomGrass.Clear();
        foreach (XElement el in root.Elements())
        {
            if (el.Name != "grass") continue;
            var props = OcbCustomTextures.GetDynamicProperties(el);
            var texture = new TextureConfig(el, props);
            CustomGrass.Add(texture);
        }
        // Init config right away
        InitGrassConfig();
    }

    // Not really necessary to do this in two steps
    // Nice to have in case we need to move the call
    public static void InitGrassConfig()
    {
        if (CustomGrass.Count == 0) return;
        MeshDescription grass = MeshDescription.meshes[MeshDescription.MESH_GRASS];
        var xmlcfg = new XmlFile(grass.MetaData);
        var xmldoc = xmlcfg.XmlDoc.Root;
        foreach (XElement xml in xmldoc.Elements())
        {
            if (xml.Name != "uv") continue;
            // if (!xmlElement.HasAttribute("id")) continue;
            int id = int.Parse(xml.GetAttribute("id"));
            grass.textureAtlas.uvMapping[id].FromXML(xml);
            var mapping = grass.textureAtlas.uvMapping[id];
            mapping.index = id; // Update reference index
            grass.textureAtlas.uvMapping[id] = mapping;
            Tilings.Add(new TilingAtlas(mapping));
        }

        foreach (TextureConfig custom in CustomGrass)
        {
            if (custom.Diffuse.Assets.Length == 0) continue;
            UVRectTiling tile = new UVRectTiling();
            tile.uv = new Rect(); // Create objects on struct
            tile.index = GetNextFreeUV(grass.textureAtlas);
            if (!UvMap.ContainsKey(custom.ID)) UvMap[custom.ID] = tile.index;
            tile.index = UvMap[custom.ID]; // Assign from real UV
            tile.textureName = custom.Diffuse.Assets[0];
            Tilings.Add(new TilingTexture(custom, tile));
        }

        var atlasWidth = Tilings.Width;
        var atlasHeight = Tilings.Height;

        foreach (var entry in Tilings.List)
        {
            var tiling = entry.Tiling;
            tiling.bGlobalUV = false;
            tiling.blockW = tiling.blockH = 1;
            tiling.uv.Set(
                (entry.Dst.x * 580 + 34f) / atlasWidth,
                (entry.Dst.y * 580 + 34f) / atlasHeight,
                512f / atlasWidth, 512f / atlasHeight);
            grass.textureAtlas.uvMapping[tiling.index] = tiling;
        }
    }

    // ####################################################################
    // ####################################################################

    static int GetNextFreeUV(TextureAtlas textureAtlas)
    {
        int id = textureAtlas.uvMapping.Length;
        Array.Resize(ref textureAtlas.uvMapping, id + 1);
        return id;
    }

    // ####################################################################
    // ####################################################################

    public static void ExecuteGrassPatching(MeshDescription grass)
    {

        if (GameManager.IsDedicatedServer) return;
        if (Tilings.List.Count == 0) return;

        var dw = Tilings.Width;
        var dh = Tilings.Height;

        // Create a new copy on the CPU to hold additional textures
        Texture2D albedo = new Texture2D(dw, dh, TextureFormat.RGBA32, true, false);
        Texture2D normal = new Texture2D(dw, dh, TextureFormat.RGBA32, true, true);
        Texture2D aost = new Texture2D(dw, dh, TextureFormat.RGBA32, true, false);

        // Mark the name to indicate CPU texture for unload
        if (!grass.TexDiffuse.name.StartsWith("extended_"))
            albedo.name = "extended_" + grass.TexDiffuse.name;
        if (!grass.TexNormal.name.StartsWith("extended_"))
            normal.name = "extended_" + grass.TexNormal.name;
        if (!grass.TexSpecular.name.StartsWith("extended_"))
            aost.name = "extended_" + grass.TexSpecular.name;

        // Do all the manipulation on the CPU as that seems to be the easiest was
        // to get this all working. I tried hard to all this on the GPU, as that should
        // be possible in technical terms; but unity API in that regard is too complicated
        // to wrap my head around. To have it GPU based we^d need a way to copy all MipMaps!
        // I've come up with way to do this to some satisfaction, but never 100%!
        // So doing it all on the CPU side seems fair ênough for what it does!

        // Initialize pixel data on CPU
        var px = albedo.GetPixels();
        for (int p = 0; p < px.Length; p++)
            px[p] = Color.clear;
        albedo.SetPixels(px);
        normal.SetPixels(px);
        aost.SetPixels(px);

        // Load original atlas from our own resource, as it was impossible to get the original
        // mip (best quality) reliably. It works when the game loads first, but when another
        // game is started from the main menu, the resulting textures are always blurry. Tried
        // really hard to avoid to embed to original textures, but that was the easiest fix.
        // Alternative was to hold on to initial textures, but that would waste 50MB of RAM.
        var AlbedoAtlas = LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass", out int _) as Texture2D;
        var NormalAtlas = LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass_n", out int _) as Texture2D;
        var AostAtlas = LoadTexture(OcbCustomTextures.GrassBundle, "ta_grass_s", out int _) as Texture2D;

        // OcbTextureDumper.DumpTexure("exports/ta_grass.png", AlbedoAtlas);
        // OcbTextureDumper.DumpTexure("exports/ta_grass_n.png", NormalAtlas);
        // OcbTextureDumper.DumpTexure("exports/ta_grass_s.png", AostAtlas);

        // We assume here that UVs are in the same direct order as sprites are distributed.
        // This saves us from mapping the UV coordinates back to tile position to copy from.
        // Hard coding all this also makes it more resilitent to potential rounding errors.
        var size = new Vector2i(576, 576);
        for (int i = 0; i < Tilings.List.Count; i++)
        {
            TilingSource tile = Tilings.List[i];
            if (tile is TilingAtlas tilings)
            {
                var to = new Vector2i(tile.Dst.x * 580, tile.Dst.y * 580);
                var from = new Vector2i((int)(tilings.Tiling.uv.x * 4096 - 34.5), (int)(tilings.Tiling.uv.y * 4096 - 34.5));
                albedo.SetPixels(to.x, to.y, size.x, size.y, AlbedoAtlas.GetPixels(from.x, from.y, size.x, size.y));
                normal.SetPixels(to.x, to.y, size.x, size.y, NormalAtlas.GetPixels(from.x, from.y, size.x, size.y));
                aost.SetPixels(to.x, to.y, size.x, size.y, AostAtlas.GetPixels(from.x, from.y, size.x, size.y));
            }
            else if (tile is TilingTexture texture)
            {
                var to = new Vector2i(tile.Dst.x * 580, tile.Dst.y * 580);
                texture.Cfg.Diffuse.CopyTo(albedo, to.x + 32, to.y + 32);
                texture.Cfg.Normal.CopyTo(normal, to.x + 32, to.y + 32);
                texture.Cfg.Specular.CopyTo(aost, to.x + 32, to.y + 32);
            }
        }

        // Apply CPU changes
        // Generate MipMaps
        albedo.Apply(true);
        normal.Apply(true);
        aost.Apply(true);

        // Compress to save VRAM
        albedo.Compress(true);
        normal.Compress(true);
        aost.Compress(true);

        // Release CPU side of memory
        // Only GPU needed from here on
        // Breaks in-game quality change!
        // albedo.Apply(false, true);
        // normal.Apply(false, true);
        // aost.Apply(false, true);

        // OcbTextureDumper.DumpTexure("exports/patched_grass.png", albedo);
        // OcbTextureDumper.DumpTexure("exports/patched_grass_n.png", normal);
        // OcbTextureDumper.DumpTexure("exports/patched_grass_s.png", aost);

        Log.Out("Unload custom atlas loaded for patching");

        OcbCustomTextures.ReleaseTexture(AlbedoAtlas, null, false);
        OcbCustomTextures.ReleaseTexture(NormalAtlas, null, false);
        OcbCustomTextures.ReleaseTexture(AostAtlas, null, false);

        Log.Out("Unload original as replaced with patched atlas");

        OcbCustomTextures.ReleaseTexture(grass.TexDiffuse, albedo, false);
        OcbCustomTextures.ReleaseTexture(grass.TexNormal, normal, false);
        OcbCustomTextures.ReleaseTexture(grass.TexSpecular, aost, false);

        grass.TexDiffuse = grass.textureAtlas.diffuseTexture = albedo;
        grass.TexNormal = grass.textureAtlas.normalTexture = normal;
        grass.TexSpecular = grass.textureAtlas.specularTexture = aost;

        grass.material.SetTexture("_Albedo", albedo);
        grass.material.SetTexture("_Normal", normal);
        grass.material.SetTexture("_Gloss_AO_SSS", aost);
    }

    // ####################################################################
    // ####################################################################

    // Hook into xml reader for paintings, but will need to wait
    // until the original coroutine is finished (see hook below)
    [HarmonyPatch(typeof(BlockTexturesFromXML), "CreateBlockTextures")]
    public class BlockTexturesFromXMLCreateBlockTexturesPostfix
    {
        public static void Postfix(XmlFile _xmlFile)
            => ParseGrassConfig(_xmlFile.XmlDoc.Root);
    }

    // ####################################################################
    // ####################################################################

}

