using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class BlockCustomTerrain : Block
{

    // Struct to pass settings into a map
    public struct CustomTerrainBlend
    {
        public int texID;
        public float Dirt;
        public float Gravel;
        public float OreCoal;
        public float Asphalt;
        public float OreIron;
        public float OreNitrate;
        public float OreOil;
        public float OreLead;
        public float StoneDesert;
        public float StoneRegular;
        public float StoneDestroyed;
        public float TerrainBlend;
    }

    // Parsed blend settings for this block
    private CustomTerrainBlend Blending
        = new CustomTerrainBlend();

    // Create counter for virtual ID space
    private static int VirtualIDs = 1300;
    private readonly int VirtualID = VirtualIDs++;

    // Get Accessor for private field we need to access
    private readonly FieldInfo FieldTextureForEachSide =
        AccessTools.Field(typeof(Block), "bTextureForEachSide");

    // Map for virtual ids to custom terrain blend settings
    private static readonly Dictionary<int, CustomTerrainBlend>
        CustomBlends = new Dictionary<int, CustomTerrainBlend>();

    // Harmony Patch to support custom terrain blends
    // We basically create virtual texture IDs that we
    // query in the relevant function we are patching up.
    // When we see an existing virtual ID in the patched
    // function, we act accordingly to create a custom
    // blend setting for the MicroSplat shader.
    [HarmonyPatch()]
    public class VoxelMeshTerrain_GetColorForTextureId
    {

        // Use function to select method to patch
        static MethodBase TargetMethod()
        {
            // Pretty unreliable, but works as long as source doesn't change
            return AccessTools.GetDeclaredMethods(typeof(VoxelMeshTerrain))
                .Find(x => x.Name == "GetColorForTextureId"
                    && x.GetParameters().Length == 8);
        }

        static bool Prefix(
            VoxelMeshTerrain __instance,
            int _subMeshIdx, ref int _fullTexId,
            bool _bTopSoil, ref Color _color,
            ref Vector2 _uv, ref Vector2 _uv2,
            ref Vector2 _uv3, ref Vector2 _uv4)
        {
            // This might be our fantasy ID, intercept and correct
            var texID = VoxelMeshTerrain.DecodeMainTexId(_fullTexId);
            // Check if this texture ID is known to use as a virtual ID
            // If found we have custom terrain blend settings to apply
            if (CustomBlends.TryGetValue(texID, out CustomTerrainBlend blend))
            {
                // In some cases we need to fallback to the fallback
                if (!World.IsSplatMapAvailable || __instance.IsPreviewVoxelMesh)
                {
                    _uv = _uv2 = _uv3 = _uv4 = Vector2.zero;
                    // Pack the original texture id again and pass to original code
                    // Will e.g. render the preview with a single fallback texture
                    // Re-implemented from original `GetColorForTextureId`
                    _fullTexId = VoxelMeshTerrain.EncodeTexIds(blend.texID, 0);
                    _color = __instance.submeshes[_subMeshIdx].GetColorForTextureId(_fullTexId);
                    Log.Out("Encoded texture ID {0} to get fallback Color {1}", blend.texID, _color);
                }
                else
                {
                    // Set custom MicroSplat blend settings
                    _color.r = blend.Dirt;
                    _color.g = blend.Gravel;
                    _color.b = blend.OreCoal;
                    _color.a = blend.TerrainBlend;
                    _uv.x = blend.Asphalt;
                    _uv.y = blend.OreIron;
                    _uv2.x = blend.OreNitrate;
                    _uv2.y = blend.StoneRegular;
                    _uv3.x = blend.StoneDesert;
                    _uv3.y = blend.OreOil;
                    _uv4.x = blend.OreLead;
                    _uv4.y = blend.StoneDestroyed;
                    return false;
                }
            }

            // Invoke regular code
            return true;
        }
    }

    // Cleanup static numbers on game exit
    public static void GameShutdown()
    {
        CustomBlends.Clear();
        VirtualIDs = 1300;
    }

    // Parse custom properties on init
    // Overrides texture ID with virtual one
    public override void Init()
    {
        base.Init();
        // Make sure we only have a single texture ID
        // Important for following call to succeed correctly
        if ((bool)FieldTextureForEachSide.GetValue(this)) throw new
            System.Exception( "Terrain Blend must have single texture ID!");
        // Easiest way to query the single texture id after above condition
        // The arguments passed into the function are void in that context
        Blending.texID = GetSideTextureId(BlockValue.Air, BlockFace.Top);
        // Register us at a fantasy ID
        SetSideTextureId(VirtualID);
        // This is the most important setting AFAICT
        // without you may not see any results at all
        Properties.ParseFloat("TerrainBlend", ref Blending.TerrainBlend);
        // Parse the configuration from the properties
        Properties.ParseFloat("BlendDirt", ref Blending.Dirt);
        Properties.ParseFloat("BlendGravel", ref Blending.Gravel);
        Properties.ParseFloat("BlendOreCoal", ref Blending.OreCoal);
        Properties.ParseFloat("BlendAsphalt", ref Blending.Asphalt);
        Properties.ParseFloat("BlendOreIron", ref Blending.OreIron);
        Properties.ParseFloat("BlendOreNitrate", ref Blending.OreNitrate);
        Properties.ParseFloat("BlendOreOil", ref Blending.OreOil);
        Properties.ParseFloat("BlendOreLoad", ref Blending.OreLead);
        Properties.ParseFloat("BlendStoneDesert", ref Blending.StoneDesert);
        Properties.ParseFloat("BlendStoneRegular", ref Blending.StoneRegular);
        Properties.ParseFloat("BlendStoneDestroyed", ref Blending.StoneDestroyed);
        // Remember the settings in a map from virtual IDs to config
        // When we need them, we only get passed the texture ID, which
        // will be the virtual one we registered. Then we can act upon
        // checking within the virtual map first, to see if there is
        // any specific and custom terrain blend config registered.
        CustomBlends.Add(VirtualID, Blending);
    }

}
