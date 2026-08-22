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
        {
            ParseOpaqueConfig(_xmlFile.XmlDoc.Root);
            // Pre-size BlockTextureData.list BEFORE any chunk
            // deserialization can access high paint IDs from saved
            // data. Without this, reloading a save built against a
            // larger texture set than the current process can fit
            // throws IndexOutOfRange from the chunk-paint accessor.
            EarlyResizeBlockTextureList();
        }
    }

    static void EarlyResizeBlockTextureList()
    {
        // Generous floor: covers the existing vanilla range plus a
        // sensible margin so a paint pack with several hundred
        // entries doesn't trigger a resize during config-load. The
        // GetFreePaintID safety net below grows the array further
        // on demand if a config truly exceeds this.
        const int minSize = 1024;
        if (BlockTextureData.list == null)
        {
            BlockTextureData.list = new BlockTextureData[minSize];
            Log.Out("[OcbCustomTextures] Created BlockTextureData.list (was null) size={0}", minSize);
            return;
        }
        var required = System.Math.Max(minSize, 512 + OpaqueConfigs.Count + 256);
        if (BlockTextureData.list.Length < required)
        {
            var oldLen = BlockTextureData.list.Length;
            Array.Resize(ref BlockTextureData.list, required);
            Log.Out("[OcbCustomTextures] Pre-resized BlockTextureData.list {0} -> {1} (save reload protection)",
                oldLen, required);
        }
    }

    // Safety net: if any code path tries to access
    // BlockTextureData.list with an index >= Length, auto-grow.
    // Catches chunk deserialization that runs before InitOpaqueConfig
    // has a chance to size the array. No-op for the common case
    // where the array is already large enough.

    [HarmonyPatch(typeof(BlockTextureData), nameof(BlockTextureData.Init))]
    static class BlockTextureDataInitPatch
    {
        static bool Prefix(BlockTextureData __instance)
        {
            if (BlockTextureData.list == null)
            {
                BlockTextureData.list = new BlockTextureData[System.Math.Max(1024, __instance.ID + 256)];
                Log.Out("[OcbCustomTextures] Created BlockTextureData.list on-demand for ID {0}, size={1}",
                    __instance.ID, BlockTextureData.list.Length);
                return true;
            }
            if (__instance.ID >= BlockTextureData.list.Length)
            {
                var oldLen = BlockTextureData.list.Length;
                var newLen = System.Math.Max(1024, (((__instance.ID + 1) / 256) + 1) * 256);
                Array.Resize(ref BlockTextureData.list, newLen);
                Log.Out("[OcbCustomTextures] Safety resize for ID {0}: {1} -> {2}",
                    __instance.ID, oldLen, newLen);
            }
            return true; // continue with original Init
        }
    }

    static int GetFreePaintID()
    {
        for (var i = 0; i < BlockTextureData.list.Length; i++)
            if (BlockTextureData.list[i] == null) return i;
        // Was: throw new Exception("No more free Paint IDs"). Grow the
        // backing array instead so a paint pack with more entries than
        // the current capacity registers cleanly. This only fires when
        // the array is genuinely full, so vanilla-sized configs never
        // hit it.
        var oldLen = BlockTextureData.list.Length;
        Array.Resize(ref BlockTextureData.list, oldLen + 256);
        Log.Out("[OcbCustomTextures] BlockTextureData.list grown {0} -> {1} to make room for new paint",
            oldLen, BlockTextureData.list.Length);
        return oldLen;
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
        // Dedicated server has no GPU atlas, so the diffuse texture
        // check above never resolves a depth. Treating that as -1 falls
        // through to incorrect ID assignment downstream; clamp to 0 so
        // the headless install runs cleanly. tiling.index is a render
        // concern, so leaving it 0 on server is fine.
        if (builtinOpaques == -1)
        {
            builtinOpaques = 0;
            Log.Out("[OcbCustomTextures] Dedicated server: no diffuse texture, builtinOpaques clamped to 0");
        }
        var textures = OpaqueConfigs.Values.ToList();
        // Pre-resize BlockTextureData.list to fit the about-to-be-
        // assigned paint IDs. Grows in 256-slot blocks so a small
        // config gets a small bump and a large pack doesn't trigger
        // a resize per registered paint.
        var idFloor = System.Math.Max(builtinOpaques, 512);
        var required = idFloor + textures.Count + 1;
        if (BlockTextureData.list != null && required > BlockTextureData.list.Length)
        {
            var oldLen2 = BlockTextureData.list.Length;
            var newLen2 = ((required / 256) + 1) * 256;
            Array.Resize(ref BlockTextureData.list, newLen2);
            Log.Out("[OcbCustomTextures] Pre-resized BlockTextureData.list {0} -> {1}", oldLen2, newLen2);
        }
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
        // Was: texture.depth + OpaquesAdded. That undersizes the
        // atlas whenever builtinOpaques > texture.depth (e.g. when the
        // initial texture was allocated against an older atlas size).
        // Max() keeps the original behavior in the common case where
        // texture.depth >= builtinOpaques.
        var copy = ResizeTextureArray(cmds, texture,
            System.Math.Max(texture.depth, builtinOpaques) + OpaquesAdded, true, true);
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
