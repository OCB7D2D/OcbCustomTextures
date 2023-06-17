using System;
using System.Xml.Linq;
using UnityEngine;


// ####################################################################
// A structure holding all the info for custom block textures
// ####################################################################

public class TextureConfig
{

    // ####################################################################
    // ####################################################################

    // Identifier name or integer
    // To create new or overwrite
    public string ID;

    // Name to be shown in the UI
    // Key into the translation CSV
    public string Name;

    // Paint cost only usefull for opaques
    // Just ignored for any other mesh type
    public int PaintCost = 1;

    // Define a sort order (unused by vanilla)
    // You can basically only prepend or append
    // Initialization copied from vanilla
    public int SortIndex = byte.MaxValue;

    // Hide paint option for user painting
    // Does nothing in editor or CM mode
    public bool Hidden = false;

    // Same applies to paint groups
    // Default by us, vanilla is null
    public string Group;

    // The accociated propertes
    // Not really used I believe
    public DynamicProperties Props;

    // Hold the atlas UV mapping info
    public UVRectTiling tiling;

    // Hold custom texture configs
    public TextureAssetUrl Diffuse;
    public TextureAssetUrl Normal;
    public TextureAssetUrl Specular;

    // Number of texture slots (for opaques)
    // Some mesh atlases support texture tiling
    // Can be used to basically get 1k textures
    public readonly int Length;

    // ####################################################################
    // ####################################################################

    public TextureConfig(XElement xml, DynamicProperties props)
    {

        Props = props;

        tiling = new UVRectTiling();

        if (!xml.HasAttribute("id")) throw new Exception("Mandatory attribute `id` missing");
        if (!props.Contains("Diffuse")) throw new Exception("Mandatory property `Diffuse` missing");
        // if (!props.Contains("Normal")) throw new Exception("Mandatory property `Normal` missing");
        // if (!props.Contains("Material")) throw new Exception("Mandatory property `Material` missing");

        // Our ID as string or numeric
        // String means add new textures
        // Numeric means overwrite existing
        ID = xml.GetAttribute("id");
        Name = xml.GetAttribute("name");

        // Copied from `CreateBlockTextures`
        props.ParseString("Group", ref Group);
        props.ParseInt("PaintCost", ref PaintCost);
        props.ParseBool("Hidden", ref Hidden);
        props.ParseInt("SortIndex", ref SortIndex);

        tiling.uv.x = xml.HasAttribute("x") ? float.Parse(xml.GetAttribute("x")) : 0;
        tiling.uv.y = xml.HasAttribute("y") ? float.Parse(xml.GetAttribute("y")) : 0;
        tiling.uv.width = xml.HasAttribute("w") ? float.Parse(xml.GetAttribute("w")) : 1;
        tiling.uv.height = xml.HasAttribute("h") ? float.Parse(xml.GetAttribute("h")) : 1;
        tiling.blockW = xml.HasAttribute("blockw") ? int.Parse(xml.GetAttribute("blockw")) : 1;
        tiling.blockH = xml.HasAttribute("blockh") ? int.Parse(xml.GetAttribute("blockh")) : 1;

        tiling.material = !props.Contains("Material") ? null :
            MaterialBlock.fromString(props.GetString("Material"));
        tiling.bSwitchUV = props.Contains("SwitchUV") ?
            props.GetBool("SwitchUV") : false;
        tiling.bGlobalUV = props.Contains("GlobalUV") ?
            props.GetBool("GlobalUV") : false;

        tiling.textureName = xml.HasAttribute("name") ? xml.GetAttribute("name") : ID;

        tiling.color = !props.Contains("Color") ? Color.white :
             StringParsers.ParseColor(props.GetString("Color"));
        
        string Diffuse = props.Contains("Diffuse") ?
            props.GetString("Diffuse") : null;
        this.Diffuse = new TextureAssetUrl(Diffuse);
        this.Length = this.Diffuse.Assets.Length;

        string Normal = props.Contains("Normal") ?
            props.GetString("Normal") : null;
        if (Normal == null) this.Normal = null;
        else this.Normal = new TextureAssetUrl(Normal);

        string Specular = props.Contains("Specular") ?
            props.GetString("Specular") : null;
        if (Specular == null) this.Specular = null;
        else this.Specular = new TextureAssetUrl(Specular);

        if (this.Normal != null && this.Normal.Assets.Length != this.Length)
            throw new Exception("Amount of normal maps different than diffuse maps!");
        if (this.Specular != null && this.Specular.Assets.Length != this.Length)
            throw new Exception("Amount of specular maps different than diffuse maps!");

        tiling.index = -1;
    }

    // ####################################################################
    // ####################################################################

}

