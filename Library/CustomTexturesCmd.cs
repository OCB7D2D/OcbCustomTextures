using UnityEngine;
using System.Collections.Generic;
using static OCB.TextureUtils;

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

        else
        {
            Log.Warning("Invalid `ct` command");
        }

    }
}
