using UnityEngine;
using System.Collections.Generic;
using static OCB.TextureUtils;
using System.Xml;
using System.IO;

public class CustomTexturesCmd : ConsoleCmdAbstract
{

    private static string info = "CustomTextures";
    public override string[] GetCommands()
    {
        return new string[2] { info, "ct" };
    }

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => true;

    public override string GetDescription() => "Custom Textures";

    public override string GetHelp() => "Custom Textures\n";

    static void DumpMeshAtlas(MeshDescription mesh, string path)
    {

        TextureAtlas atlas = mesh.textureAtlas;

        System.IO.Directory.CreateDirectory(path);

        if (atlas is TextureAtlasTerrain terrain)
        {
            for (int i = 0; i < terrain.diffuse.Length; i++)
            {
                DumpTexure(terrain.diffuse[i], string.Format(
                    "{0}/terrain.{1}.diffuse.png", path, i));
            }
            for (int i = 0; i < terrain.normal.Length; i++)
            {
                DumpTexure(terrain.normal[i], string.Format(
                    "{0}/terrain.{1}.normal.png", path, i),
                    UnpackNormalGammaPixels);
            }
            for (int i = 0; i < terrain.specular.Length; i++)
            {
                DumpTexure(terrain.specular[i], string.Format(
                    "{0}/terrain.{1}.specular.png", path, i));
            }
        }

        if (atlas.diffuseTexture is Texture2DArray diff)
        {
            for (int i = 0; i < diff.depth; i++)
            {
                DumpTexureArr(diff, i, string.Format(
                    "{0}/atlas.{1}.diffuse.png", path, i));
            }
        }
        else if (atlas.diffuseTexture is Texture2D tex2d)
        {
            DumpTexure(tex2d, string.Format(
                "{0}/atlas.diffuse.png", path));
        }
        else if (atlas.diffuseTexture != null)
        {
            Log.Warning("atlas.diffuseTexture has unknown type");
        }

        if (atlas.normalTexture is Texture2DArray norm)
        {
            for (int i = 0; i < norm.depth; i++)
            {
                DumpNormal(norm, i, string.Format(
                    "{0}/atlas.{1}.normal.png", path, i));
            }
        }
        else if (atlas.normalTexture is Texture2D tex2d)
        {
            DumpNormal(tex2d, string.Format(
                "{0}/atlas.normal.png", path));
        }
        else if (atlas.normalTexture != null)
        {
            Log.Warning("atlas.normalTexture has unknown type");
        }

        if (atlas.specularTexture is Texture2DArray spec)
        {
            for (int i = 0; i < spec.depth; i++)
            {
                DumpSpecular(spec, i, string.Format(
                    "{0}/atlas.{1}.specular.png", path, i));
            }
        }
        else if (atlas.specularTexture is Texture2D tex2d)
        {
            DumpSpecular(tex2d, string.Format(
                "{0}/atlas.specular.png", path));
        }
        else if (atlas.specularTexture != null)
        {
            Log.Warning("atlas.specularTexture has unknown type");
        }

        if (atlas.occlusionTexture is Texture2DArray occl)
        {
            for (int i = 0; i < occl.depth; i++)
            {
                DumpSpecular(occl, i, string.Format(
                    "{0}/atlas.{1}.occlusion.png", path, i));
            }
        }
        else if (atlas.occlusionTexture is Texture2D tex2d)
        {
            DumpSpecular(tex2d, string.Format(
                "{0}/atlas.occlusion.png", path));
        }
        else if (atlas.occlusionTexture != null)
        {
            Log.Warning("atlas.occlusionTexture has unknown type");
        }

        if (atlas.emissionTexture is Texture2DArray emis)
        {
            for (int i = 0; i < emis.depth; i++)
            {
                DumpSpecular(emis, i, string.Format(
                    "{0}/atlas.{1}.emission.png", path, i));
            }
        }
        else if (atlas.emissionTexture is Texture2D tex2d)
        {
            DumpSpecular(tex2d, string.Format(
                "{0}/atlas.emission.png", path));
        }
        else if (atlas.emissionTexture != null)
        {
            Log.Warning("atlas.emissionTexture has unknown type");
        }

        if (mesh.TexDiffuse != atlas.diffuseTexture)
        {
            Log.Warning("Diffuse texture from mesh and atlas differs");
        }
        if (mesh.TexNormal != atlas.normalTexture)
        {
            Log.Warning("Normal texture from mesh and atlas differs");
        }
        if (mesh.TexSpecular != atlas.specularTexture)
        {
            Log.Warning("Specular texture from mesh and atlas differs");
        }
        if (mesh.TexEmission != atlas.emissionTexture)
        {
            Log.Warning("Emission texture differs from atlas differs");
        }
        if (mesh.TexOcclusion != atlas.occlusionTexture)
        {
            Log.Warning("Occlusion texture differs from atlas differs");
        }

    }

    static public MeshDescription GetMesh(string name)
    {
        switch (name)
        {
            case "opaque":
                return MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
            case "dump-opaque2":
                return MeshDescription.meshes[MeshDescription.MESH_OPAQUE2];
            case "moveable":
                return MeshDescription.meshes[MeshDescription.MESH_CUTOUTMOVEABLE];
            case "terrain":
                return MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
            case "grass":
                return MeshDescription.meshes[MeshDescription.MESH_GRASS];
            case "models":
                return MeshDescription.meshes[MeshDescription.MESH_MODELS];
            case "transparent":
                return MeshDescription.meshes[MeshDescription.MESH_TRANSPARENT];
            case "cutout":
                return MeshDescription.meshes[MeshDescription.MESH_CUTOUT];
            case "water":
                return MeshDescription.meshes[MeshDescription.MESH_WATER];
            case "decals":
                return MeshDescription.meshes[MeshDescription.MESH_DECALS];
            default:
                Log.Warning("Invalid mesh {0}", name);
                return null;
        }
    }

    static void FooBar(float param1, float param2)
    {

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

        // Read all sprites from the Atlas back, so we can add more sprites
        List<Texture2D> diffuses = GetAtlasSprites(grass.TexDiffuse as Texture2D, tiles);
        List<Texture2D> normals = GetAtlasSprites(grass.TexNormal as Texture2D, tiles);
        List<Texture2D> speculars = GetAtlasSprites(grass.TexSpecular as Texture2D, tiles);

        foreach (TextureConfig custom in OcbCustomTextures.CustomGrass)
        {
            if (custom.Diffuse.Assets.Length == 0) continue;
            Texture2D filteredTex = null;
            var url = custom.Diffuse.Assets[0];
            var data = File.ReadAllBytes(url);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(data);

            // So far we only support 512x512 sized textures (needs more testing)
            // tex = ResizeTexture(tex, tex.width + 68, tex.height + 68, false, 34, 34);
            RenderTexture rt = new RenderTexture(tex.width, tex.height, 32);
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            Graphics.Blit(tex, rt, new Vector2(1, 1), new Vector2(0, 0));
            Texture2D resize = new Texture2D(tex.width + 68, tex.height + 68);
            // Really? No utility to create a clear Texture2D?
            Color32[] resetColorArray = resize.GetPixels32();
            for (int i = 0; i < resetColorArray.Length; i++)
                resetColorArray[i] = Color.clear;
            resize.SetPixels32(resetColorArray);
            resize.ReadPixels(new Rect(0, 0, tex.width, tex.height), 34, 34);
            resize.Apply(true);
            tex = resize;

            diffuses.Add(tex);

            if (custom.Normal == null)
            {
                if (filteredTex == null) filteredTex = NormalMapTools.FilterMedian(tex, 5);
                normals.Add(NormalMapTools.CreateNormalmap(filteredTex, 4, true));
            }
            else
            {
                var norm_url = custom.Normal.Assets[0];
                var norm_data = File.ReadAllBytes(norm_url);
                var norm_tex = new Texture2D(2, 2);
                norm_tex.LoadImage(norm_data);
                normals.Add(norm_tex);
            }

            if (custom.Specular == null)
            {
                if (filteredTex == null) filteredTex = NormalMapTools.FilterMedian(tex, 5);
                speculars.Add(NormalMapTools.CreateAOSTMap(filteredTex, custom.Props));
            }
            else
            {
                var spec_url = custom.Specular.Assets[0];
                var spec_data = File.ReadAllBytes(spec_url);
                var spec_tex = new Texture2D(2, 2);
                spec_tex.LoadImage(spec_data);
                speculars.Add(spec_tex);
            }

            UVRectTiling tile = new UVRectTiling();
            tile.uv = new Rect();
            tile.textureName = url;
            tiles.Add(tile);
        }

        var t2d = grass.TexDiffuse as Texture2D;
        var t2n = grass.TexNormal as Texture2D;
        var t2s = grass.TexSpecular as Texture2D;

        DumpTexure2D(t2d, "Mods/OcbCustomTextures/org-grass-diff-atlas.png");
        DumpTexure2D(t2n, "Mods/OcbCustomTextures/org-grass-norm-atlas.png");
        DumpTexure2D(t2s, "Mods/OcbCustomTextures/org-grass-spec-atlas.png");

        Texture2D diff_atlas = new Texture2D(8192, 8192, t2d.format, false);
        Texture2D norm_atlas = new Texture2D(8192, 8192, t2n.format, false);
        Texture2D spec_atlas = new Texture2D(8192, 8192, t2s.format, false);

        var diff_rects = diff_atlas.PackTextures(diffuses.ToArray(), 0, 8192, false);
        var norm_rects = norm_atlas.PackTextures(normals.ToArray(), 0, 8192, false);
        var spec_rects = spec_atlas.PackTextures(speculars.ToArray(), 0, 8192, false);

        DumpTexure2D(diff_atlas, "Mods/OcbCustomTextures/patched-grass-diff-atlas.png");
        DumpTexure2D(norm_atlas, "Mods/OcbCustomTextures/patched-grass-norm-atlas.png");
        DumpTexure2D(spec_atlas, "Mods/OcbCustomTextures/patched-grass-spec-atlas.png");

        grass.TexDiffuse = diff_atlas;
        grass.TexNormal = norm_atlas;
        grass.TexSpecular = spec_atlas;

        grass.textureAtlas.diffuseTexture = diff_atlas;
        grass.textureAtlas.normalTexture = norm_atlas;
        grass.textureAtlas.specularTexture = spec_atlas;

        grass.ReloadTextureArrays(false);

        grass.material.SetTexture("_Albedo", grass.textureAtlas.diffuseTexture);
        grass.material.SetTexture("_Normal", grass.textureAtlas.normalTexture);
        grass.material.SetTexture("_Gloss_AO_SSS", grass.textureAtlas.specularTexture);
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        if (_params.Count == 2)
        {
            switch (_params[0])
            {
                case "dump":
                    System.IO.Directory.CreateDirectory("export");
                    DumpMeshAtlas(GetMesh(_params[1]), "export/" + _params[1]);
                    break;
                case "uvs":
                    var uvs = GetMesh(_params[1]).textureAtlas.uvMapping;
                    for(var i = 0; i < uvs.Length; i++)
                    {
                        if (string.IsNullOrEmpty(uvs[i].textureName)) continue;
                        Log.Out("{0}: {1} {2}", i, uvs[i].textureName, uvs[i].ToString());
                    }
                    break;
                default:
                    Log.Warning("Unknown command " + _params[0]);
                    break;
            }
        }

        else if (_params.Count == 3)
        {
            switch (_params[0])
            {
                case "FooBar":
                    Log.Out("Doing Foobar");
                    FooBar(float.Parse(_params[1]),
                        float.Parse(_params[2]));
                    Log.Out("Done Foobar");
                    // 0.6f, 0.03f
                    break;
                default:
                    Log.Warning("Unknown command " + _params[0]);
                    break;
            }
        }

        else
        {
            Log.Warning("Invalid `ct` command");
        }

    }
}
