using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using static OcbTextureUtils;

public static class OpaqueTextures
{

    // ####################################################################
    // ####################################################################

    // Store configs after initial parsing for later use
    // We parse once and use it when texture quality changes
    static readonly Dictionary<string, TextureConfig> OpaqueConfigs
        = new Dictionary<string, TextureConfig>();

    // Need to count this seperately, as we may add multiple
    // texture tiles for one given opaque paint texture.
    static int OpaquesAdded = 0;

    // UvMap to resolve any block texture reference
    // Used to replace texture string ids in xml config
    static Dictionary<string, int> UvMap
        => OcbCustomTextures.UvMap;

    // ####################################################################
    // Parse the opaque paint/texture configs
    // ####################################################################

    static void ParseOpaqueConfig(XElement root)
    {
        UvMap.Clear();
        OpaquesAdded = 0;
        OpaqueConfigs.Clear();
        foreach (XElement el in root.Elements())
        {
            if (el.Name != "opaque") continue;
            var props = OcbCustomTextures.GetDynamicProperties(el);
            var texture = new TextureConfig(el, props);
            OpaqueConfigs[texture.ID] = texture;
        }
    }

    // ####################################################################
    // ####################################################################

    // Hook into xml reader for paintings, but will need to wait
    // until the original coroutine is finished (see hook below)
    [HarmonyPatch(typeof(BlockTexturesFromXML), "CreateBlockTextures")]
    public class BlockTexturesFromXMLCreateBlockTexturesPrefix
    {
        public static void Prefix(XmlFile _xmlFile)
            => ParseOpaqueConfig(_xmlFile.XmlDoc.Root);
    }

    // ####################################################################
    // ####################################################################

    static int GetFreePaintID()
    {
        for (var i = 0; i < BlockTextureData.list.Length; i++)
            if (BlockTextureData.list[i] == null) return i;
        throw new Exception("No more free Paint IDs");
    }

    private static ushort PatchAtlasBlocks(MeshDescription mesh, TextureConfig tex)
    {
        if (mesh == null) throw new Exception("MESH MISSING");
        var atlas = mesh.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        if (atlas.uvMapping.Length > ushort.MaxValue)
            throw new Exception("INVALID ATLAS SIZE");
        ushort textureID = (ushort)atlas.uvMapping.Length;
        if (!UvMap.ContainsKey(tex.ID)) UvMap[tex.ID] = textureID;
        else if (UvMap[tex.ID] != textureID) Log.Warning(
                 "Overwriting texture key {0}", tex.ID);
        if (atlas.uvMapping.Length < textureID + 1)
            Array.Resize(ref atlas.uvMapping, textureID + 1);
        atlas.uvMapping[textureID] = tex.tiling;
        return textureID;
    }

    // ####################################################################
    // ####################################################################

    // Only get this number once, before we applied any patches
    // Later on we can't get this again as we free original array
    static int builtinOpaques = -1;

    // Hooked via `BlockTexturesFromXML.CreateBlockTextures`
    static void InitOpaqueConfig()
    {
        var opaque = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
        var opaqueAtlas = opaque.textureAtlas as TextureAtlasBlocks;
        if (builtinOpaques == -1 && opaqueAtlas.diffuseTexture != null)
            builtinOpaques = (opaqueAtlas.diffuseTexture as Texture2DArray).depth;
        var textures = OpaqueConfigs.Values.ToList();
        if (opaque == null) throw new Exception("MESH MISSING");
        var atlas = opaque.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        // ToDo: Resize UvMapping in once go here!?
        for (int i = 0; i < textures.Count; i++)
        {
            TextureConfig cfg = textures[i];
            if (ushort.TryParse(cfg.ID, out ushort idx))
            {
                // overwrite existing texture with new patch
                cfg.tiling.index = atlas.uvMapping[idx].index;
                // ToDo: allow to overwrite any other data?
                // Can already be done with xml patching!
            }
            else
            {
                // Create new texture and assign new index
                cfg.tiling.index = builtinOpaques + OpaquesAdded;
                var data = new BlockTextureData
                {
                    Name = cfg.Name,
                    Group = cfg.Group,
                    Hidden = cfg.Hidden,
                    SortIndex = (byte)cfg.SortIndex,
                    PaintCost = (ushort)cfg.PaintCost,
                    TextureID = PatchAtlasBlocks(opaque, cfg),
                    LocalizedName = Localization.Get(cfg.Name),
                    ID = GetFreePaintID()
                };
                OpaquesAdded += cfg.Length;
                // Assign to static BlockTextureData.list
                // BlockTextureData.list[data.ID] = data;
                data.Init(); // does exactly the same
            }
        }
        // Patch textures right away
        // Re-Do when options change
        PatchCustomOpaques();
    }

    // ####################################################################
    // ####################################################################

    [HarmonyPatch] static class BlockTexturesFromXMLCreateBlockTexturesHook
    {

        // Select the target dynamically to patch `MoveNext`
        // Coroutine/Enumerator is compiled to a hidden class
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.EnumeratorMoveNext(AccessTools.
                Method(typeof(BlockTexturesFromXML), "CreateBlockTextures"));
        }

        // Main function handling the IL patching
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            // Execute our static function to do our thing
            codes.Insert(codes.Count - 1, CodeInstruction.Call(
                typeof(OpaqueTextures), "InitOpaqueConfig"));
            // Return new IL codes
            return codes;
        }
    }

    // ####################################################################
    // Patch all opaque textures after everything was loaded.
    // Unfortunately this may mean it needs to be sent "twice"
    // to the GPU, although overhead for it should be minimal.
    // ####################################################################

    private static void PatchCustomOpaques()
    {
        if (OpaquesAdded == 0) return;
        if (GameManager.IsDedicatedServer) return;
        var opaque = MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
        // ToDo: Resize UvMapping in once go here!?
        if (opaque == null) throw new Exception("MESH MISSING");
        var atlas = opaque.textureAtlas as TextureAtlasBlocks;
        if (atlas == null) throw new Exception("INVALID ATLAS TYPE");
        if (!(atlas.diffuseTexture is Texture2DArray diff2Darr))
            throw new Exception("Diffuse not a texture2Darray!");
        if (!(atlas.normalTexture is Texture2DArray norm2Darr))
            throw new Exception("Normal not a texture2Darray!");
        if (!(atlas.specularTexture is Texture2DArray spec2Darr))
            throw new Exception("Specular not a texture2Darray!");
        // Create command buffer to execute in background
        var cmds = new CommandBuffer();
        cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        var diff2Dcpy = ApplyDiffuseTextures(diff2Darr, cmds);
        var norm2Dcpy = ApplyNormalTextures(norm2Darr, cmds);
        var spec2Dcpy = ApplySpecularTexture(spec2Darr, cmds);
        var old = QualitySettings.globalTextureMipmapLimit;
        QualitySettings.globalTextureMipmapLimit = 0;
        Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);
        QualitySettings.globalTextureMipmapLimit = old;
        // Post patch, since our xml config is loaded too late
        OcbCustomTextures.ReleaseTexture(diff2Darr, diff2Dcpy);
        opaque.TexDiffuse = atlas.diffuseTexture = diff2Dcpy;
        Log.Out("Set Opaque diffuse: {0}", opaque.TexDiffuse);
        OcbCustomTextures.ReleaseTexture(norm2Darr, norm2Dcpy);
        opaque.TexNormal = atlas.normalTexture = norm2Dcpy;
        Log.Out("Set Opaque normal: {0}", opaque.TexNormal);
        OcbCustomTextures.ReleaseTexture(spec2Darr, spec2Dcpy);
        opaque.TexSpecular = atlas.specularTexture = spec2Dcpy;
        Log.Out("Set Opaque MOER: {0}", opaque.TexSpecular);
        opaque.ReloadTextureArrays(false);
    }

    // ####################################################################
    // Hook directly into `MeshDescription.loadSingleArray` to patch
    // right after the original textures are loaded. Unfortunately
    // this only works for "in-game" changes, as regular game-startup
    // will load these before loading any of the game xml, which are
    // needed to decide first which textures are to be added or not.
    // ####################################################################

    [HarmonyPatch]
    static class MeshDescriptionLoadSingleArray
    {

        // Select the target dynamically to patch `MoveNext`
        // Coroutine/Enumerator is compiled to a hidden class
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.EnumeratorMoveNext(AccessTools.
                Method(typeof(MeshDescription), "loadSingleArray"));
        }

        // Main function handling the IL patching
        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode != OpCodes.Stfld) continue;
                if (!(codes[i].operand is FieldInfo field)) continue;
                if (field.Name == "TexDiffuse")
                    codes.Insert(i++, CodeInstruction.Call(
                        typeof(OpaqueTextures), "PatchDiffuse"));
                else if (field.Name == "TexNormal")
                    codes.Insert(i++, CodeInstruction.Call(
                        typeof(OpaqueTextures), "PatchNormal"));
                else if (field.Name == "TexSpecular")
                    codes.Insert(i++, CodeInstruction.Call(
                        typeof(OpaqueTextures), "PatchSpecular"));
            }
            return codes;
        }
    }

    // ####################################################################
    // ####################################################################

    static Texture2DArray PatchDiffuse(Texture2DArray texture)
        => PatchTextureAtlas(texture, ApplyDiffuseTextures);

    static Texture2DArray PatchNormal(Texture2DArray texture)
        => PatchTextureAtlas(texture, ApplyNormalTextures);

    static Texture2DArray PatchSpecular(Texture2DArray texture)
        => PatchTextureAtlas(texture, ApplySpecularTexture);

    // ####################################################################
    // ####################################################################

    static Texture2DArray PatchTextureAtlas(Texture2DArray texture,
        Func<Texture2DArray, CommandBuffer, Texture2DArray> apply)
    {
        if (OpaquesAdded == 0) return texture;
        if (GameManager.IsDedicatedServer) return texture;
        if (!texture.name.StartsWith("ta_opaque")) return texture;
        using (ResetQualitySettings reset = new ResetQualitySettings())
        { 
            var cmds = new CommandBuffer();
            cmds.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            var patched = apply(texture, cmds);
            Graphics.ExecuteCommandBufferAsync(cmds, ComputeQueueType.Default);
            OcbCustomTextures.ReleaseTexture(texture, patched);
            Log.Out("Patched: {0}", patched);
            return patched;
        }
    }

    // ####################################################################
    // ####################################################################

    static Texture2DArray ApplyDiffuseTextures(Texture2DArray texture, CommandBuffer cmds)
        => ApplyTextures(texture, cmds, x => x.Diffuse, "assets/defaultdiffuse.png");

    static Texture2DArray ApplyNormalTextures(Texture2DArray texture, CommandBuffer cmds)
        => ApplyTextures(texture, cmds, x => x.Normal, "assets/defaultnormal.png");

    static Texture2DArray ApplySpecularTexture(Texture2DArray texture, CommandBuffer cmds)
        => ApplyTextures(texture, cmds, x => x.Specular, "assets/defaultspecular.png");

    // ####################################################################
    // ####################################################################

    static Texture2DArray ApplyTextures(Texture2DArray texture, CommandBuffer cmds,
        Func<TextureConfig, TextureAssetUrl> lookup, string fallback)
    {
        if (OpaquesAdded == 0) return texture;
        if (GameManager.IsDedicatedServer) return texture;
        if (!texture.name.StartsWith("ta_opaque")) return texture;
        var copy = ResizeTextureArray(cmds, texture,
            texture.depth + OpaquesAdded, true, true);
        foreach (TextureConfig cfg in OpaqueConfigs.Values)
            for (int i = 0; i < cfg.Length; i += 1)
                PatchTextures(cmds, copy, lookup(cfg), cfg.tiling, i, fallback);
        return copy;
    }

    // ####################################################################
    // ####################################################################

    static void PatchTextures(CommandBuffer cmds, Texture2DArray copy,
        TextureAssetUrl src, UVRectTiling tiling, int i, string fallback)
    {
        if (src == null && string.IsNullOrEmpty(fallback)) return;
        var tex = src != null ? LoadTexture(src.Path.BundlePath, src.Assets[i], out int srcidx)
            : LoadTexture(OcbCustomTextures.CommonBundle, fallback, out srcidx);
        PatchTexture(cmds, copy, tiling.index + i, tex, srcidx);
    }

    // ####################################################################
    // ####################################################################

}
