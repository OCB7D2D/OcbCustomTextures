using UnityEngine;
using System.Collections.Generic;
using static OCB.TextureUtils;
using System.Runtime.ConstrainedExecution;
using System;


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
                    "{0}/array.{1}.diffuse.png", path, i));
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
                    "{0}/array.{1}.normal.png", path, i));
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
                    "{0}/array.{1}.specular.png", path, i));
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

    public static Coroutine FooBarRunner = null;


    readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainPropTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msPropTex");
    readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcCurveTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcCurveTex");
    readonly HarmonyFieldProxy<Texture2D> VoxelMeshTerrainProcParamTex =
        new HarmonyFieldProxy<Texture2D>(typeof(VoxelMeshTerrain), "msProcParamTex");
    readonly HarmonyFieldProxy<MicroSplatPropData> VoxelMeshTerrainPropData =
        new HarmonyFieldProxy<MicroSplatPropData>(typeof(VoxelMeshTerrain), "msPropData");
    readonly HarmonyFieldProxy<MicroSplatProceduralTextureConfig> VoxelMeshTerrainProcData = 
        new HarmonyFieldProxy<MicroSplatProceduralTextureConfig>(typeof(VoxelMeshTerrain), "msProcData");


    readonly HarmonyFieldProxy<Texture2D> MicroSplatPropDataTexture =
        new HarmonyFieldProxy<Texture2D>(typeof(MicroSplatPropData), "tex");


    private static Texture2D PatchBiomeMask(Texture2D tex, Color col)
    {
        var cpy = new Texture2D(tex.width, tex.height,
            tex.format, mipChain: false);
        cpy.filterMode = FilterMode.Bilinear;
        for (int x = 0; x < tex.width; x++)
            for (int y = 0; y < tex.height; y++)
                cpy.SetPixel(x, y, col);
        cpy.Apply(false, true);
        return cpy;
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {

        if (_params.Count == 1)
        {
            if (_params[0] == "patchms")
            {
                // procBiomeMask1.r => Distant texture 0 (my custom texture)
                // procBiomeMask1.g => grass (texture 2)
                // procBiomeMask1.b => texture index 10
                // procBiomeMask1.a => texture index 8 (and 10?)
                if (GameManager.Instance?.World?.ChunkCache?.ChunkProvider
                    is ChunkProviderGenerateWorldFromRaw cpr)
                {
                    var color = new Color(0f, 0f, 0f, 0f);
                    cpr.procBiomeMask1 = PatchBiomeMask(
                        cpr.procBiomeMask1, color);
                    var color2 = new Color(0f, 0f, 0f, 1f);
                    cpr.procBiomeMask2 = PatchBiomeMask(
                        cpr.procBiomeMask2, color2);
                }
            }
            else if (_params[0] == "layers")
            {
                if (VoxelMeshTerrainProcData.Get(null) is MicroSplatProceduralTextureConfig cfg)
                {
                    for (int i = 0; i < cfg.layers.Count; i++)
                    {
                        var layer = cfg.layers[i];
                        Log.Out("Layer {0} => {1}/{2} #{3}", i,
                            layer.biomeWeights, layer.biomeWeights2,
                            layer.textureIndex);
                    }
                }

            }
        }
        else if (_params.Count == 2)
        {
            switch (_params[0])
            {
                case "dump":
                    System.IO.Directory.CreateDirectory("export");
                    if (_params[1] == "microsplat")
                    {
                        var path = "export/" + _params[1];
                        System.IO.Directory.CreateDirectory(path);
                        if (GameManager.Instance?.World?.ChunkCache?.ChunkProvider
                            is ChunkProviderGenerateWorldFromRaw cpr)
                        {
                            DumpTexure(cpr.procBiomeMask1, string.Format(
                                "{0}/world.biome.mask.1.png", path));
                            DumpTexure(cpr.procBiomeMask2, string.Format(
                                "{0}/world.biome.mask.2.png", path));
                            for (int i = 0; i < cpr.splats.Length; i++)
                                DumpTexure(cpr.splats[i], string.Format(
                                    "{0}/world.splat.{1}.png", path, i));
                        }
                        
                        if (VoxelMeshTerrainPropTex.Get(null) is Texture2D msPropTex)
                            DumpTexure(msPropTex, string.Format("{0}/mesh.prop.png", path));
                        if (VoxelMeshTerrainProcCurveTex.Get(null) is Texture2D msProcCurveTex)
                            DumpTexure(msProcCurveTex, string.Format("{0}/mesh.proc.curve.png", path));
                        if (VoxelMeshTerrainProcParamTex.Get(null) is Texture2D msProcParamTex)
                            DumpTexure(msProcParamTex, string.Format("{0}/mesh.proc.param.png", path));
                        
                        if (VoxelMeshTerrainProcData.Get(null) is MicroSplatProceduralTextureConfig cfg)
                        {
                            Log.Out("Terrain Layer Count: {0}", cfg.layers.Count);
                        }

                    }
                    else
                    {
                        DumpMeshAtlas(GetMesh(_params[1]), "export/" + _params[1]);
                    }
                    break;
                case "uvs":
                    var uvs = GetMesh(_params[1]).textureAtlas.uvMapping;
                    for (var i = 0; i < uvs.Length; i++)
                    {
                        if (string.IsNullOrEmpty(uvs[i].textureName)) continue;
                        Log.Out("{0}: {1} {2}", i, uvs[i].textureName, uvs[i].ToString());
                    }
                    break;
                case "layer":
                    var idx = int.Parse(_params[1]);
                    var data = VoxelMeshTerrainProcData.Get(null);
                    var layer = data.layers[idx];
                    Log.Out("<property name=\"weight\" value=\"{0}\"/>", layer.weight);
                    Log.Out("<property name=\"noise-active\" value=\"{0}\"/>", layer.noiseActive);
                    Log.Out("<property name=\"noise-frequency\" value=\"{0}\"/>", layer.noiseFrequency);
                    Log.Out("<property name=\"noise-offset\" value=\"{0}\"/>", layer.noiseOffset);
                    Log.Out("<property name=\"noise-range\" value=\"{0}\"/>", layer.noiseRange);

                    Log.Out("<property name=\"height-active\" value=\"{0}\"/>", layer.heightActive);
                    Log.Out("<property name=\"slope-active\" value=\"{0}\"/>", layer.slopeActive);
                    Log.Out("<property name=\"erosion-active\" value=\"{0}\"/>", layer.erosionMapActive);
                    Log.Out("<property name=\"cavity-active\" value=\"{0}\"/>", layer.cavityMapActive);

                    Log.Out("<property name=\"height-curve-mode\" value=\"{0}\"/>", layer.heightCurveMode);
                    Log.Out("<property name=\"slope-curve-mode\" value=\"{0}\"/>", layer.slopeCurveMode);
                    Log.Out("<property name=\"erosion-curve-mode\" value=\"{0}\"/>", layer.erosionCurveMode);
                    Log.Out("<property name=\"cavity-curve-mode\" value=\"{0}\"/>", layer.cavityCurveMode);

                    Log.Out("<property name=\"microsplat-index\" value=\"{0}\"/>", layer.textureIndex);
                    Log.Out("<property name=\"biome-weight-1\" value=\"{0}\"/>", layer.biomeWeights);
                    Log.Out("<property name=\"biome-weight-2\" value=\"{0}\"/>", layer.biomeWeights2);

                    if (layer.heightCurve?.length >= 0)
                    {
                        Log.Out("<height-keyframes>");
                        foreach (var frame in layer.heightCurve.keys)
                            Log.Out("  <keyframe time=\"{0}\" value=\"{1}\" />",
                                frame.time, frame.value);
                        Log.Out("</height-keyframes>");
                    }

                    if (layer.slopeCurve?.length >= 0)
                    {
                        Log.Out("<slope-keyframes>");
                        foreach (var frame in layer.slopeCurve.keys)
                            Log.Out("  <keyframe time=\"{0}\" value=\"{1}\" />",
                                frame.time, frame.value);
                        Log.Out("</slope-keyframes>");
                    }
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
                    if (FooBarRunner == null)
                    {
                        Log.Out("Start Develop Watcher");
                        FooBarRunner = GameManager.Instance.
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
                            StopCoroutine(FooBarRunner);
                        FooBarRunner = null;
                    }
                    break;
                // ct layer 14 weight 123
                case "layer":
                    var idx = int.Parse(_params[1]);
                    var data = VoxelMeshTerrainProcData.Get(null);
                    var layer = data.layers[idx];
                    switch (_params[2])
                    {
                        case "weight": layer.weight = float.Parse(_params[3]); break;
                        case "noise-active": layer.noiseActive = bool.Parse(_params[3]); break;
                        case "noise-frequency": layer.noiseFrequency = float.Parse(_params[3]); break;
                        case "noise-offset": layer.noiseOffset = float.Parse(_params[3]); break;
                        case "noise-range": layer.noiseRange = StringParsers.ParseVector2(_params[3]); break;
                        case "height-active": layer.heightActive = bool.Parse(_params[3]); break;
                        case "slope-active": layer.slopeActive = bool.Parse(_params[3]); break;
                        case "erosion-active": layer.erosionMapActive = bool.Parse(_params[3]); break;
                        case "cavity-active": layer.cavityMapActive = bool.Parse(_params[3]); break;
                        case "height-curve-mode": layer.heightCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "slope-curve-mode": layer.slopeCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "erosion-curve-mode": layer.erosionCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "cavity-curve-mode": layer.cavityCurveMode = EnumUtils.Parse(_params[3],
                            MicroSplatProceduralTextureConfig.Layer.CurveMode.Curve); break;
                        case "microsplat-index": layer.textureIndex = int.Parse(_params[3]); break;
                        case "biome-weight-1": layer.biomeWeights = OcbCustomTextures.ParseVector4(_params[3]); break;
                        case "biome-weight-2": layer.biomeWeights2 = OcbCustomTextures.ParseVector4(_params[3]); break;
                        default: Log.Warning("Unknown param " + _params[2]); break;
                    }
                    OcbCustomTextures.LockCustomBiomeLayers = true;
                    VoxelMeshTerrainProcCurveTex.Set(null, data.GetCurveTexture());
                    VoxelMeshTerrainProcParamTex.Set(null, data.GetParamTexture());
                    OcbCustomTextures.LockCustomBiomeLayers = false;
                    DynamicMeshManager.Instance.RefreshAll();
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
