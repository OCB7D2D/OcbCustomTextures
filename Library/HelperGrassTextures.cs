using System.Collections;
using System.IO;
using UnityEngine;

// ####################################################################
// Haven't been tested rencently with A21, so use with caution
// ####################################################################

public class HelperGrassTextures
{
    public static IEnumerator StartGrassHelper(string path, int x, int y)
    {
        while (true)
        {
            DynamicGrassPatcher(path, x, y);
            yield return new WaitForSeconds(1);
        }
    }

    static System.DateTime mt1 = new System.DateTime();
    static System.DateTime mt2 = new System.DateTime();
    static System.DateTime mt3 = new System.DateTime();

    private static bool IsSimilar(Color t1, Color t2)
    {
        float max = 1f / 15f;
        if (Mathf.Abs(t1.r - t2.r) > max) return false;
        if (Mathf.Abs(t1.g - t2.g) > max) return false;
        if (Mathf.Abs(t1.b - t2.b) > max) return false;
        if (Mathf.Abs(t1.a - t2.a) > max) return false;
        return true;
    }


    static public Texture2D LoadTexture(string path)
    {
        var data = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2);
        tex.LoadImage(data);
        return tex;
    }

    public static void DynamicGrassPatcher(string path, int x, int y)
    {

        MeshDescription grass = MeshDescription.meshes[MeshDescription.MESH_GRASS];

        var diff_atlas = grass.TexDiffuse as Texture2D;
        var norm_atlas = grass.TexNormal as Texture2D;
        var spec_atlas = grass.TexSpecular as Texture2D;

        System.DateTime nmt1 = File.GetLastWriteTime($"{path}.albedo.png");
        System.DateTime nmt2 = File.GetLastWriteTime($"{path}.normal.png");
        System.DateTime nmt3 = File.GetLastWriteTime($"{path}.aost.png");

        x = 580 * x + 34;
        y = 580 * y + 34;

        if (mt1 == nmt1 && mt2 == nmt2 && mt3 == nmt3) return;

        Log.Out("Reloading {0}", path);

        //DumpTexure2D(diff_atlas, "Mods/OcbCustomTexturesPlants/org-grass-diff-atlas.png");
        //DumpTexure2D(norm_atlas, "Mods/OcbCustomTexturesPlants/org-grass-norm-atlas.png");
        //DumpTexure2D(spec_atlas, "Mods/OcbCustomTexturesPlants/org-grass-spec-atlas.png");

        mt1 = nmt1;
        mt2 = nmt2;
        mt3 = nmt3;

        bool do1 = mt1 != nmt1;
        bool do2 = mt2 != nmt2;
        bool do3 = mt3 != nmt3;

        bool all = true;

        if (do1 || all)
        {
            Log.Out("Reloading Albedo");
            var new_albedo = LoadTexture($"{path}.albedo.png");
            // var t2d = grass.TexDiffuse as Texture2D;
            // DumpTexure2D(t2d, "Mods/OcbCustomTextures/org-grass-diff-atlas.png");
            // Texture2D diff_atlas = new Texture2D(8192, 8192, t2d.format, false);
            // var diff_rects = diff_atlas.PackTextures(diffuses.ToArray(), 0, 8192, false);
            for(int i = 0; i < new_albedo.mipmapCount; i++)
            {
                int factor = (int)Mathf.Pow(2, i);
                Graphics.CopyTexture(new_albedo, 0, i, 0, 0,
                    new_albedo.width / factor, new_albedo.height / factor,
                    diff_atlas, 0, i, x / factor, y / factor);
            }
            grass.TexDiffuse = diff_atlas;
            grass.textureAtlas.diffuseTexture = diff_atlas;
        }

        if (do2 || all)
        {
            Log.Out("Reloading Normal");
            var new_normal = LoadTexture($"{path}.normal.png");
            // var t2n = grass.TexNormal as Texture2D;
            // DumpTexure2D(t2n, "Mods/OcbCustomTextures/org-grass-norm-atlas.png");
            // Texture2D norm_atlas = new Texture2D(8192, 8192, t2n.format, false);
            // var norm_rects = norm_atlas.PackTextures(normals.ToArray(), 0, 8192, false);

            new_normal.filterMode = FilterMode.Trilinear;

            var px = new_normal.GetPixels32();
            for (var i = 0; i < px.Length; i += 1)
            {
                byte r = px[i].r;
                byte g = px[i].g;
                // Linear to gamma
                // r = (byte)(255 * Mathf.Pow(r / 255f, 1f / 2.2f));
                g = (byte)Linear2Gamma(g);
                px[i].a = r;
                px[i].g = g;
                px[i].b = g;
                px[i].r = 255;
            }
            new_normal.SetPixels32(px);
            new_normal.Apply();

            // This only works if nothing has changed yet?
            for (int w = 0; w < new_normal.width; w += 1)
            {
                for (int h = 0; h < new_normal.height; h += 1)
                {
                    var t1 = new_normal.GetPixel(w, h);
                    var t2 = norm_atlas.GetPixel(x + w, y + h);
                    if (!IsSimilar(t1, t2))
                    {
                        // Log.Error("Normal mismatch {0} {1}", t1, t2);
                        // break;
                    }
                }
            }

            for (int i = 0; i < new_normal.mipmapCount; i++)
            {
                int factor = (int)Mathf.Pow(2, i);
                Graphics.CopyTexture(new_normal, 0, i, 0, 0,
                    new_normal.width / factor, new_normal.height / factor,
                    norm_atlas, 0, i, x / factor, y / factor);
            }
            grass.TexNormal = norm_atlas;
            grass.textureAtlas.normalTexture = norm_atlas;
        }

        if (do3 || all)
        {
            Log.Out("Reloading Specular");
            var new_spec = LoadTexture($"{path}.aost.png");
            // var t2s = grass.TexSpecular as Texture2D;
            // DumpTexure2D(t2s, "Mods/OcbCustomTextures/org-grass-spec-atlas.png");
            // Texture2D spec_atlas = new Texture2D(8192, 8192, t2s.format, false);
            // var spec_rects = spec_atlas.PackTextures(speculars.ToArray(), 0, 8192, false);

            new_spec.filterMode = FilterMode.Trilinear;

            var px = new_spec.GetPixels32();
            for (var i = 0; i < px.Length; i += 1)
            {
                //px[i].r = (byte)Gamma2Linear(px[i].r);
                //px[i].b = (byte)Gamma2Linear(px[i].b);
            }
            new_spec.SetPixels32(px);
            new_spec.Apply();


            for (int w = 0; w < new_spec.width; w += 1)
            {
                for (int h = 0; h < new_spec.height; h += 1)
                {
                    var t1 = new_spec.GetPixel(w, h);
                    var t2 = spec_atlas.GetPixel(x + w, y + h);
                    if (!IsSimilar(t1, t2))
                    {
                        // Log.Error("Spec mismatch {0} {1}", t1, t2);
                        // break;
                    }
                }
            }

            for (int i = 0; i < new_spec.mipmapCount; i++)
            {
                int factor = (int)Mathf.Pow(2, i);
                Graphics.CopyTexture(new_spec, 0, i, 0, 0,
                    new_spec.width / factor, new_spec.height / factor,
                    spec_atlas, 0, i, x / factor, y / factor);
            }
            grass.TexSpecular = spec_atlas;
            grass.textureAtlas.specularTexture = spec_atlas;
        }


        // grass.ReloadTextureArrays(false);

        grass.material.SetTexture("_Albedo", grass.textureAtlas.diffuseTexture);
        grass.material.SetTexture("_Normal", grass.textureAtlas.normalTexture);
        grass.material.SetTexture("_Gloss_AO_SSS", grass.textureAtlas.specularTexture);

        // DumpTexure2D(diff_atlas, "Mods/OcbCustomTexturesPlants/patched-grass-diff-atlas.png");
        // DumpTexure2D(norm_atlas, "Mods/OcbCustomTexturesPlants/patched-grass-norm-atlas.png");
        // DumpTexure2D(spec_atlas, "Mods/OcbCustomTexturesPlants/patched-grass-spec-atlas.png");

    }

    private static float Linear2Gamma(byte g)
    {
        return 255 * Mathf.Pow((g + 0.5f) / 255f, 1f / 2.2f);
    }

    private static float Gamma2Linear(byte g)
    {
        return 255 * Mathf.Pow((g + 0.5f) / 255f, 2.2f);
    }

}
