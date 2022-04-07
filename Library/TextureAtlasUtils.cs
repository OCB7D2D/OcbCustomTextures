using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static OCB.TextureUtils;

namespace OCB
{

    static public class TextureAtlasUtils
    {

        // Cache generated textures to re-use
        static public Texture2D OpaqueNormal = null;
        static public Texture2D OpaqueSpecular = null;
        static public Texture2D[] TerrainNormal = new Texture2D[16];
        static public Texture2D[] TerrainSpecular = new Texture2D[16];

        static public Texture2D UpdateCachedTexture(
            ref Texture2D texture, int width, int height,
            Func<int, int, Texture2D> GetTexture)
        {
            // Create a neutral normal texture (once)
            if (texture == null)
            {
                texture = GetTexture(width, height);
            }
            else if (texture.width != width)
            {
                // UnityEngine.Object.Destroy(texture);
                texture = GetTexture(width, height);
            }
            else if (texture.height != height)
            {
                // UnityEngine.Object.Destroy(texture);
                texture = GetTexture(width, height);
            }
            return texture;
        }

        static int GetMipMap(int dim)
        {
            switch (dim)
            {
                case 1 << 0: return 0;
                case 1 << 1: return 1;
                case 1 << 2: return 2;
                case 1 << 3: return 3;
                case 1 << 4: return 4;
                case 1 << 5: return 5;
                case 1 << 6: return 6;
                case 1 << 7: return 7;
                case 1 << 8: return 8;
                case 1 << 9: return 9;
                case 1 << 10: return 10;
                case 1 << 11: return 11;
                case 1 << 12: return 12;
                default: throw new Exception(
                    "Invalid MipMap Dimension");
            }
        }

        static public Texture2D GetTerrainNormal(int dim)
        {
            var mip = GetMipMap(dim);
            return UpdateCachedTexture(ref TerrainNormal[mip],
                dim, dim, CreateNormalTexture);
        }

        static public Texture2D GetTerrainSpecular(int dim)
        {
            var mip = GetMipMap(dim);
            return UpdateCachedTexture(ref TerrainSpecular[mip],
                dim, dim, CreateBlackTexture);
        }

        static public void PatchOpaqueNormal(ref Texture2DArray arr, int idx, int sides)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);

            Texture2DArray copy = arr;
            if (arr.depth < idx + sides)
            {
                copy = ResizeTextureArray(arr,
                    idx + sides, true, linear, true);
            }
            // Create neutral normal texture
            UpdateCachedTexture(ref OpaqueNormal,
                arr.width, arr.height, CreateNormalTexture);
            // Copy Texture2D to Texture2DArray
            for (int side = 0; side < sides; side++)
            {
                int off = OpaqueNormal.mipmapCount - copy.mipmapCount;
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(OpaqueNormal.GetPixelData<byte>(n + off), n, idx + side);
                    // Graphics.CopyTexture(OpaqueNormal, 0, n + off, copy, idx + side, n);
                }

            }
            // Postpone this step
            // copy.Apply(false);
            // Assign the copy back
            arr = copy;
        }

        static public void PatchOpaqueSpecular(ref Texture2DArray arr, int idx, int sides)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);

            Texture2DArray copy = arr;
            if (arr.depth < idx + sides)
            {
                copy = ResizeTextureArray(arr,
                    idx + sides, true, linear, true);
            }
            // Create a neutral specular texture (caches results)
            UpdateCachedTexture(ref OpaqueSpecular,
                arr.width, arr.height, CreateGreenTexture);

            // Copy Texture2D to Texture2DArray
            for (int side = 0; side < sides; side++)
            {
                int off = OpaqueSpecular.mipmapCount - copy.mipmapCount;
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(OpaqueSpecular.GetPixelData<byte>(n + off), n, idx + side);
                    // Graphics.CopyTexture(OpaqueSpecular, 0, n + off, copy, idx + side, n);
                }

            }
            // Postpone this step
            // copy.Apply(false);
            // Assign the copy back
            arr = copy;
        }

        static void CheckBlockTexture(string name, Texture2D texture)
        {
            if (texture.width > 512)
            {
                // Log.Warning("Texture {0} is wider than needed (should be 512 is {1})",
                //     name, texture.width);
                // Log.Warning("Texture {0} is taller than needed (should be 512 is {1})",
                //     name, texture.height);
            }
            else if (texture.width < 512)
            {
                Log.Error("Texture width {0} is too small (must be at least 512, is {1})",
                    name, texture.width);
                Log.Error("Texture height {0} is too small (must be at least 512, is {1})",
                    name, texture.height);
            }
        }

        static public void PatchOpaqueTexture(ref Texture2DArray arr, TextureAssetUrl url, int idx)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);
            // Support different face textures
            Texture2DArray copy = arr;
            if (arr.depth < idx + url.Assets.Length)
            {
                copy = ResizeTextureArray(arr, idx +
                    url.Assets.Length, true, linear, true);
            }
            // Only add as many textures as needed
            for (int side = 0; side < url.Assets.Length; side++)
            {
                var tex = LoadTexture(url.Path.BundlePath, url.Assets[side], arr.format);
                // Make sure dimensions are correct
                CheckBlockTexture(url.Assets[side], tex);
                // This will automatically do the resize for us, neat!
                int off = tex.mipmapCount - copy.mipmapCount;
                // Copy the loaded texture (use same for every side for now)
                // ToDo: add different config to set them separately
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(tex.GetPixelData<byte>(n + off), n, idx + side);
                    // Graphics.CopyTexture(tex, 0, n + off, copy, idx + side, n);
                }
            }
            // Postpone this step
            // copy.Apply(false);
            // Assign the copy back
            arr = copy;
        }

        static public Texture2DArray PatchTerrainTexture(Texture2DArray arr, TextureAssetUrl url, int idx)
        {
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);
            // Support different face textures
            Texture2DArray copy = arr;
            if (arr.depth < idx + url.Assets.Length)
            {
                copy = ResizeTextureArray(arr, idx +
                    url.Assets.Length, true, linear, true);
            }
            // Only add as many textures as needed
            for (int side = 0; side < url.Assets.Length; side++)
            {
                var tex = LoadTexture(url.Path.BundlePath, url.Assets[side], arr.format);
                // Make sure dimensions are correct
                CheckBlockTexture(url.Assets[side], tex);
                // This will automatically do the resize for us, neat!
                int off = tex.mipmapCount - copy.mipmapCount;
                // Copy the loaded texture (use same for every side for now)
                // ToDo: add different config to set them separately
                for (int n = 0; n < copy.mipmapCount; n++)
                {
                    copy.SetPixelData(tex.GetPixelData<byte>(n + off), n, idx + side);
                    // Graphics.CopyTexture(tex, 0, n + off, copy, idx + side, n);
                }
            }
            // Postpone this step
            // copy.Apply(false);
            // Assign the copy back
            return copy;
        }

        static public void CleanupTextureAtlasBlocks(TextureAtlasBlocks atlas)
        {
            atlas.diffuseTexture = null;
            atlas.normalTexture = null;
            atlas.maskTexture = null;
            atlas.maskNormalTexture = null;
            atlas.emissionTexture = null;
            atlas.specularTexture = null;
            atlas.heightTexture = null;
            atlas.occlusionTexture = null;
        }

        static public void CleanupTextureAtlasTerrain(TextureAtlasTerrain atlas)
        {
            for (int i = 0; i < atlas.diffuse.Length; ++i)
            {
                if (atlas.diffuse[i] = null) continue;
                // Resources.UnloadAsset(atlas.diffuse[i]);
                atlas.diffuse[i] = null;
            }
            for (int i = 0; i < atlas.normal.Length; ++i)
            {
                if (atlas.normal[i] = null) continue;
                // Resources.UnloadAsset(atlas.normal[i]);
                atlas.normal[i] = null;
            }
            for (int i = 0; i < atlas.specular.Length; ++i)
            {
                if (atlas.specular[i] = null) continue;
                // Resources.UnloadAsset(atlas.specular[i]);
                atlas.specular[i] = null;
            }
            // Unload unreferenced onces
            Resources.UnloadUnusedAssets();
        }
    }
}