using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;

public class OcbCustomTextures : IModApi
{

    // ####################################################################
    // ####################################################################

    public static string GrassBundle;
    public static string CommonBundle;

    // ####################################################################
    // ####################################################################

    public void InitMod(Mod mod)
    {
        Log.Out("OCB Harmony Patch: " + GetType().ToString());
        Harmony harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GrassBundle = System.IO.Path.Combine(mod.Path, "Resources/Grass.unity3d");
        CommonBundle = System.IO.Path.Combine(mod.Path, "Resources/Common.unity3d");
    }

    // ####################################################################
    // ####################################################################

    static public DynamicProperties GetDynamicProperties(XElement xml)
    {
        DynamicProperties props = new DynamicProperties();
        foreach (XElement el in xml.Elements())
            if (el.Name == "property") props.Add(el);
        return props;
    }

    // ####################################################################
    // Additional texture and UV infos are stored in here
    // ####################################################################

    public static readonly Dictionary<string, int>
        UvMap = new Dictionary<string, int>();

    // ####################################################################
    // End-Users must use names instead of IDs for custom/new textures
    // Mainly because they can't know what ID we will assign to those
    // Therefore we hook before the actual parsing to patch the XML
    // ####################################################################

    [HarmonyPatch(typeof(BlocksFromXml), "CreateBlocks")]
    static class BlocksFromXmlCreateBlocksPrefix
    {
        static void Prefix(XmlFile _xmlFile)
        {
            if (_xmlFile == null) return;
            XElement root = _xmlFile.XmlDoc.Root;
            if (!root.HasElements) return;
            foreach (XElement blk in root.Elements("block"))
                foreach (XElement prop in blk.Elements("property"))
                    ResolveTextureProperties(prop);
            // Execute the patching right away at this point
            // This seems to work ok in SP and on Dedi
            GrassTextures.ExecuteGrassPatching(MeshDescription.
                meshes[MeshDescription.MESH_GRASS]);
        }

    }

    // Resolve string texture references in various properties
    private static void ResolveTextureProperties(XElement prop)
    {
        ResolveTexture("Texture", prop);
        ResolveTexture("UiBackgroundTexture", prop);
    }

    // Resolve string texture reference in property by `name`
    private static void ResolveTexture(string name, XElement prop)
    {
        if (!prop.HasAttribute("name")) return;
        if (prop.Attribute("name").Value != name) return;
        if (!prop.HasAttribute("value")) return;
        var value = prop.Attribute("value").Value;
        if (value.All(x => x >= '0' && x <= '9' || x == ',')) return;
        // Update the XML Attribute for vanilla to parse
        var resolved = ResolveTextureIDs(value);
        prop.SetAttributeValue("value", resolved);
        // Check that result is now in a valid format for vanilla
        // Otherwise this will error down the block loading chain
        if (!resolved.All(x => x >= '0' && x <= '9' || x == ','))
            Log.Error("Texture name(s) not resolved: {0}", resolved);
    }

    // Resolve list of named textures to list of numeric ids
    private static string ResolveTextureIDs(string value)
    {
        // Check if we have something to reslove in the value
        if (!value.All(x => x >= '0' && x <= '9' || x == ','))
        {
            // Check if we should resolve a list
            if (value.Contains(","))
            {
                string[] ids = value.Split(new char[] { ',' });
                for (int i = 0; i < ids.Length; i += 1)
                    if (UvMap.TryGetValue(ids[i], out int idx))
                        ids[i] = idx.ToString();
                return string.Join(",", ids);
            }
            // Otherwise resolve the literal name
            else if (UvMap.TryGetValue(value, out int idx))
                return idx.ToString();
        }
        // Return what we got
        return value;
    }

    // ####################################################################
    // Only assets loaded from Bundles/Disk can be "Unloaded".
    // Unfortunately Unity will give an ugly error message if
    // that contract is violated, which is the case for our
    // runtime created extended textures. To catch these we
    // add a "heuristic" patch, as I didn't find any other
    // direct way to see if calling unload is legal or not.
    // ####################################################################

    [HarmonyPatch(typeof(MeshDescription), "Unload")]
    static class MeshDescriptionUnload
    {
        static bool Prefix(Texture tex)
        {
            if (tex == null) return true;
            return !tex.name.StartsWith("extended_");
        }
    }

    // ####################################################################
    // Provide internal function to help with releasing textures
    // when we copy/create runtime textures. Has a "bit unqiue"
    // API signature to support our regular workflow.
    // ####################################################################

    internal static void ReleaseTexture(Texture org, Texture cpy, bool addressable = true)
    {
        if (org == cpy) return;
        if (org == null) return;
        if (addressable)
        {
            Log.Out("Release: {0}", org);
            if (!org.name.StartsWith("extended_"))
                LoadManager.ReleaseAddressable(org);
        }
        else
        {
            Log.Out("Unload: {0}", org);
            if (!org.name.StartsWith("extended_"))
                Resources.UnloadAsset(org);
        }
    }

    // ####################################################################
    // ####################################################################

}