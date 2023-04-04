using System;
using System.Xml;
using UnityEngine;


// A structure holding all the info for custom terrain textures
public struct MicroSplatConfig
{
    public int Index;
    public DynamicProperties Props;
    public TextureAssetUrl Diffuse;
    public TextureAssetUrl Normal;
    public TextureAssetUrl Specular;

    public MicroSplatConfig(XmlElement xml, DynamicProperties props)
    {

        Props = props;

        if (!xml.HasAttribute("index")) throw new Exception("Mandatory attribute `index` missing");
        if (!props.Contains("Diffuse")) throw new Exception("Mandatory property `Diffuse` missing");
        // if (!props.Contains("Normal")) throw new Exception("Mandatory property `Normal` missing");
        // if (!props.Contains("Material")) throw new Exception("Mandatory property `Material` missing");

        Index = int.Parse(xml.GetAttribute("index"));

        string Diffuse = props.Contains("Diffuse") ?
            props.GetString("Diffuse") : null;
        this.Diffuse = new TextureAssetUrl(Diffuse);

        string Normal = props.Contains("Normal") ?
            props.GetString("Normal") : null;
        if (Normal == null) this.Normal = null;
        else this.Normal = new TextureAssetUrl(Normal);

        string Specular = props.Contains("Specular") ?
            props.GetString("Specular") : null;
        if (Specular == null) this.Specular = null;
        else this.Specular = new TextureAssetUrl(Specular);

    }

}

// A structure holding all the info for custom block textures
public struct TextureConfig
{
    public string ID;
    public DynamicProperties Props;
    public TextureAssetUrl Diffuse;
    public TextureAssetUrl Normal;
    public TextureAssetUrl Specular;
    public UVRectTiling tiling;
    public readonly int Length;

    public TextureConfig(XmlElement xml, DynamicProperties props)
    {

        Props = props;

        tiling = new UVRectTiling();

        if (!xml.HasAttribute("id")) throw new Exception("Mandatory attribute `id` missing");
        if (!props.Contains("Diffuse")) throw new Exception("Mandatory property `Diffuse` missing");
        // if (!props.Contains("Normal")) throw new Exception("Mandatory property `Normal` missing");
        // if (!props.Contains("Material")) throw new Exception("Mandatory property `Material` missing");

        ID = xml.GetAttribute("id");

        tiling.uv.x = xml.HasAttribute("x") ? float.Parse(xml.GetAttribute("x")) : 0;
        tiling.uv.y = xml.HasAttribute("y") ? float.Parse(xml.GetAttribute("y")) : 0;
        tiling.uv.width = xml.HasAttribute("w") ? float.Parse(xml.GetAttribute("w")) : 1;
        tiling.uv.height = xml.HasAttribute("h") ? float.Parse(xml.GetAttribute("h")) : 1;
        tiling.blockW = xml.HasAttribute("blockw") ? int.Parse(xml.GetAttribute("blockw")) : 1;
        tiling.blockH = xml.HasAttribute("blockh") ? int.Parse(xml.GetAttribute("blockh")) : 1;

        tiling.textureName = xml.HasAttribute("name") ? xml.GetAttribute("name") : ID;

        tiling.color = !props.Contains("Color") ? Color.white :
             StringParsers.ParseColor(props.GetString("Color"));
        tiling.material = !props.Contains("Material") ? null :
            MaterialBlock.fromString(props.GetString("Material"));
        tiling.bGlobalUV = props.Contains("GlobalUV") ?
            props.GetBool("GlobalUV") : false;
        tiling.bSwitchUV = props.Contains("SwitchUV") ?
            props.GetBool("SwitchUV") : false;
        
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

    }

}

