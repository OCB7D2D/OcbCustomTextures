using System.Collections.Generic;
using UnityEngine;
using static OcbTextureDumper;


public class CustomTexturesCmd : ConsoleCmdAbstract
{

    // ####################################################################
    // ####################################################################

    private static string info = "CustomTextures";
    public override string[] getCommands()
    {
        return new string[2] { info, "ct" };
    }

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => true;

    public override string getDescription() => "Custom Textures";

    public override string getHelp() => "Custom Textures\n";

    // ####################################################################
    // ####################################################################

    static public MeshDescription GetMesh(string name)
    {
        switch (name)
        {
            case "opaque":
                return MeshDescription.meshes[MeshDescription.MESH_OPAQUE];
            case "terrain":
                return MeshDescription.meshes[MeshDescription.MESH_TERRAIN];
            case "grass":
                return MeshDescription.meshes[MeshDescription.MESH_GRASS];
            case "models":
                return MeshDescription.meshes[MeshDescription.MESH_MODELS];
            case "transparent":
                return MeshDescription.meshes[MeshDescription.MESH_TRANSPARENT];
            case "water":
                return MeshDescription.meshes[MeshDescription.MESH_WATER];
            case "decals":
                return MeshDescription.meshes[MeshDescription.MESH_DECALS];
            default:
                Log.Warning("Invalid mesh {0}", name);
                return null;
        }
    }

    // ####################################################################
    // ####################################################################

    static void DumpMeshAtlas(MeshDescription mesh, string name)
    {

        var path = "export/" + name;

        TextureAtlas atlas = mesh.textureAtlas;

        System.IO.Directory.CreateDirectory(path);

        if (atlas is TextureAtlasTerrain terrain)
        {
            for (int i = 0; i < terrain.diffuse.Length; i++)
            {
                DumpTexure(string.Format("{0}/terrain.{1}.diffuse.png", path, i),
                    terrain.diffuse[i], false);
            }
            for (int i = 0; i < terrain.normal.Length; i++)
            {
                DumpTexure(string.Format("{0}/terrain.{1}.normal.png", path, i),
                    terrain.normal[i], true, UnpackNormalPixels);
            }
            for (int i = 0; i < terrain.specular.Length; i++)
            {
                DumpTexure(string.Format("{0}/terrain.{1}.specular.png", path, i),
                    terrain.specular[i], true);
            }
        }

        if (atlas.diffuseTexture is Texture2DArray diff)
        {
            for (int i = 0; i < diff.depth; i++)
            {
                DumpTexure(string.Format("{0}/array.{1}.diffuse.png", path, i),
                    diff, i, true);
            }
        }
        else if (atlas.diffuseTexture is Texture2D tex2d)
        {
            DumpTexure(string.Format("{0}/atlas.diffuse.png", path), tex2d, false);
        }
        else if (atlas.diffuseTexture != null)
        {
            Log.Warning("atlas.diffuseTexture has unknown type");
        }

        if (atlas.normalTexture is Texture2DArray norm)
        {
            for (int i = 0; i < norm.depth; i++)
            {
                var dst = string.Format("{0}/array.{1}.normal.png", path, i);
                // Opaque atlas seems to not use unity normal packing!?
                if (name == "opaque") DumpTexure(dst, norm, i, true, UnpackNormalPixels);
                else DumpTexure(dst, norm, i, true);
            }
        }
        else if (atlas.normalTexture is Texture2D tex2d)
        {
            DumpTexure(string.Format("{0}/atlas.normal.png", path),
                tex2d, true, UnpackNormalPixels);
        }
        else if (atlas.normalTexture != null)
        {
            Log.Warning("atlas.normalTexture has unknown type");
        }

        if (atlas.specularTexture is Texture2DArray spec)
        {
            for (int i = 0; i < spec.depth; i++)
            {
                if (name == "opaque")
                {
                    DumpTexure(string.Format("{0}/array.{1}.metallic.png",
                        path, i), spec, i, true, ExtractRedChannel);
                    DumpTexure(string.Format("{0}/array.{1}.occlusion.png",
                        path, i), spec, i, true, ExtractGreenChannel);
                    DumpTexure(string.Format("{0}/array.{1}.emission.png",
                        path, i), spec, i, true, ExtractBlueChannel);
                    DumpTexure(string.Format("{0}/array.{1}.roughness.png",
                        path, i), spec, i, true, ExtractAlphaChannel);
                }
                else
                {
                    DumpTexure(string.Format("{0}/array.{1}.specular.png",
                        path, i), spec, i, true);
                }
            }
        }
        else if (atlas.specularTexture is Texture2D tex2d)
        {
            DumpTexure(string.Format("{0}/atlas.specular.png", path),
                tex2d, true);
        }
        else if (atlas.specularTexture != null)
        {
            Log.Warning("atlas.specularTexture has unknown type");
        }
        /*
        if (atlas.occlusionTexture is Texture2DArray occl)
        {
            for (int i = 0; i < occl.depth; i++)
            {
                DumpSpecular(occl, i, string.Format(
                    "{0}/array.{1}.occlusion.png", path, i));
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
                    "{0}/array.{1}.emission.png", path, i));
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
        */

    }

    // ####################################################################
    // ####################################################################

    public static Coroutine GrassHelperRunner = null;

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        if (_params.Count == 1 && _params[0] == "test")
        {

            // GrassTextures.PostProcessGrass();

        }

        else if (_params.Count == 2)
        {
            switch (_params[0])
            {
                case "dump":
                    System.IO.Directory.CreateDirectory("export");
                    DumpMeshAtlas(GetMesh(_params[1]), _params[1]);
                    break;
                case "uvs":
                    var uvs = GetMesh(_params[1]).textureAtlas.uvMapping;
                    for (var i = 0; i < uvs.Length; i++)
                    {
                        if (string.IsNullOrEmpty(uvs[i].textureName)) continue;
                        Log.Out("{0}: {1} {2}", i, uvs[i].textureName, uvs[i].ToString());
                    }
                    Log.Out("With maximum size of {0}", uvs.Length);
                    break;
                default:
                    Log.Warning("Unknown command " + _params[0]);
                    break;
            }
        }
        
        else if (_params.Count == 4)
        {
            switch (_params[0])
            {
                // Use `ct dump grass` to check the generate atlas
                // Then see which index in the "grid" you want to patch
                // Use `ct dump grass` again to see if you got it right

                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower01 5 3
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower02 5 4
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower03 5 5
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower04 5 6
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower05 6 0
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower06 6 1
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower07 6 2
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower08 6 3
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Flowers/flower09 6 4

                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/tomato.grown 6 5
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/tomato.small 6 6
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/wheat.grown 7 0
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/wheat.small 7 1
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/onion.grown 7 2
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/onion.small 7 3
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/cabbage.grown 7 4
                // ct grasshelper Mods/OcbCustomTexturesPlants/UnityPlants/Assets/Plants/cabbage.small 7 5

                case "grasshelper":
                    if (GrassHelperRunner == null)
                    {
                        Log.Out("Start Develop Watcher");
                        GrassHelperRunner = GameManager.Instance.
                            StartCoroutine(HelperGrassTextures
                                .StartGrassHelper(_params[1],
                                    int.Parse(_params[2]),
                                    int.Parse(_params[3])));
                    }
                    else
                    {
                        Log.Out("Stop Develop Watcher");
                        Log.Out("Execute again to start");
                        GameManager.Instance.
                            StopCoroutine(GrassHelperRunner);
                        GrassHelperRunner = null;
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

    // ####################################################################
    // ####################################################################

}
