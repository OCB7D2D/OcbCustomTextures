using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace OCB
{

    // OcbMaurice's unity texture utilities
    // Seems utility support in unity for textures is lacking
    // Or I just didn't see the proper interfaces for it yet

    // DXT5 is BC3 is 5:6:5 bit (2 bytes) format (plus 1 byte for alpha)

    static public class TextureUtils
    {

        static public bool IsPow2(int n)
        {
            int count = 0;
            for (int i = 0; i < 32; i++)
            {
                count += (n >> i & 1);
            }
            return count == 1 && n > 0;
        }

        static public Texture2D LoadTexture(string bundle, string asset)
        {
            // Get the texture from pre-loaded bundles
            Texture2D tex = AssetBundleManager.Instance
                .Get<Texture2D>(bundle, asset);
            // Check if loading was successful
            if (tex == null) throw new Exception(String.Format(
                "Could not load asset {1} from bundle {0}",
                bundle, asset));
            // Check that loaded texture is square format
            if (tex.width != tex.height) throw new Exception(String.Format(
                "Texture must be a square, is {0}x{1} at ({2})",
                tex.width, tex.height, asset));
            // Check that loaded texture is power of two
            if (!IsPow2(tex.width)) throw new Exception(String.Format(
                "Texture dimension must be power of two ({0}) at {2}",
                tex.width, tex.height, asset));
            return tex;
        }

        static public Texture2D LoadTexture(string bundle, string asset, TextureFormat format)
        {
            var tex = LoadTexture(bundle, asset);
            // Check if the format is compatible
            if (tex.format != format) throw new Exception(String.Format(
                "Texture format {0} not compatible with atlas {1} ({2})",
                tex.format, format, asset));
            return tex;
        }

        static public Texture2D LoadTexture(DataLoader.DataPathIdentifier url)
        {
            return LoadTexture(url.BundlePath, url.AssetName);
        }

        static public int GetQualityFactor()
        { 
            switch (GameOptionsManager.GetTextureQuality())
            {
                case 0: return 8; // full
                case 1: return 4; // half
                case 2: return 2; // quarter
                case 3: return 1; // eight
                default: return 2;
            }
        }

        // Return a new (or original texture) for dimension
        // Will "down-scale" texture if original is bigger then `dim`
        // Otherwise will return original as a "best effort" texture
        static public Texture2D GetBestMipMapTexture(Texture2D org, int basis)
        {
            int dim = basis * GetQualityFactor();
            if (org.width <= dim) return org;
            bool linear = !GraphicsFormatUtility.IsSRGBFormat(org.graphicsFormat);
            Texture2D copy = new Texture2D(dim, dim, org.format, true, linear);
            int off = org.mipmapCount - copy.mipmapCount;
            for (int n = 0; n < copy.mipmapCount; n++)
            {
                copy.SetPixelData(org.GetPixelData<byte>(n + off), n);
            }
            copy.Apply(false);
            return copy;
        }

        static public Texture2D CreateUniformTexture(int width, int height, Color color,
            bool quality = false, bool updateMipmaps = true, bool linear = true)
        {
            Texture2D spec = new Texture2D(width, height, TextureFormat.RGBA32, true, linear);
            for (int y = 0; y < spec.height; y++)
            {
                for (int x = 0; x < spec.width; x++)
                {
                    spec.SetPixel(x, y, color, 0);
                }
            }
            spec.Apply(updateMipmaps);
            spec.Compress(quality);
            return spec;
        }

        static public Texture2D CreateNormalTexture(int width, int height)
        {
            return CreateUniformTexture(width, height, new Color(1f, 0.5f, 0.5f, 0.5f), true);
        }

        static public Texture2D CreateGreenTexture(int width, int height)
        {
            return CreateUniformTexture(width, height, Color.green);
        }

        static public Texture2D CreateBlackTexture(int width, int height)
        {
            return CreateUniformTexture(width, height, Color.black);
        }

        public static Texture2DArray ResizeTextureArray(Texture2DArray array,
            int size, bool mipChain = false, bool linear = true, bool destroy = false)
        {
            if (array.depth == size) return array;
            // Create a copy and add space for more textures
            var copy = new Texture2DArray(array.width, array.height,
                size, array.format, mipChain, linear);
            // Copy old textures to new copy (any better way?)
            for (int i = 0; i < Mathf.Min(array.depth, size); i++)
                Graphics.CopyTexture(array, i, copy, i);
            // Optionally destroy the original object
            // if (destroy) UnityEngine.Object.Destroy(array);
            // Return the copy
            return copy;
        }

        public static void ApplyPixelChanges(Texture texture, bool updateMipmaps)
        {
            if (texture == null) return;
            if (texture is Texture2DArray array2D) array2D.Apply(updateMipmaps);
            else if (texture is Texture2D texture2D) texture2D.Apply(updateMipmaps);
            else throw new Exception("Invalid type passed to ApplyPixelChanges");
        }

        public static Texture2D ResizeTexture(Texture2D texture2D, int width, int height, bool color32)
        {
            if (texture2D == null) return texture2D;
            RenderTexture rt = new RenderTexture(width, height, color32 ? 32 : 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D resize = new Texture2D(width, height);
            resize.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resize.Apply(true);
            return resize;
        }

        public static Texture2D CopyTexture(Texture2D texture2D, int width, int height, bool color32)
        {
            if (texture2D == null) return texture2D;
            RenderTexture rt = new RenderTexture(width, height, color32 ? 32 : 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D resize = new Texture2D(width, height);
            resize.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resize.Apply(true);
            return resize;
        }

        static public Color32[] UnpackNormalGammaPixels(Color32[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = pixels[i].a;
                // Convert non-linear (gamma) to linear space (approximation only)
                pixels[i].g = (byte)(Mathf.Pow(pixels[i].b / Byte.MaxValue, 2.2f) * Byte.MaxValue);
                pixels[i].b = Byte.MaxValue;
                pixels[i].a = Byte.MaxValue;
            }
            return pixels;
        }

        static public Color32[] UnpackNormalPixels(Color32[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = pixels[i].a;
                pixels[i].g = pixels[i].b;
                pixels[i].b = Byte.MaxValue;
                pixels[i].a = Byte.MaxValue;
            }
            return pixels;
        }

        static public Color32[] UnpackSpecularPixels(Color32[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = (byte)(Byte.MaxValue - pixels[i].g);
                pixels[i].b = (byte)(Byte.MaxValue - pixels[i].g);
                pixels[i].g = (byte)(Byte.MaxValue - pixels[i].g);
            }
            return pixels;
        }

        static public void DumpTexure(Texture tex, string path,
            Func<Color[], Color[]> converter = null)
        {
            if (tex is Texture2DArray arr)
            {
                DumpTexure(arr, path, converter);
            }
            else if (tex is Texture2D tex2d)
            {
                DumpTexure(tex2d, path, converter);
            }
            else if (tex != null)
            {
                Log.Error("Invalid texture to dump " + tex);
            }
        }

        static public void DumpTexure(Texture2D tex, string path,
            Func<Color32[], Color32[]> converter = null)
        {
            if (tex == null) return;
            Texture2D cpy = CopyTexture(tex, tex.width, tex.height, true);
            // cpy.filterMode = FilterMode.Trilinear;
            // cpy.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = cpy.GetPixels32();
            if (converter != null) pixels = converter(pixels);
            cpy.SetPixels32(pixels);
            byte[] bytes = cpy.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }

        static public void DumpTexure(Texture2DArray arr, int idx, string path,
            Func<Color32[], Color32[]> converter = null)
        {
            if (arr == null) return;

            bool linear = !GraphicsFormatUtility.IsSRGBFormat(arr.graphicsFormat);
            // Create a copy and add space for more textures
            var copy = new Texture2DArray(arr.width, arr.height,
                1, arr.format, true, linear);
            // Copy old textures to new copy (any better way?)
            Graphics.CopyTexture(arr, idx, copy, 0);
            Texture2D cpy = new Texture2D(
                arr.width, arr.height,
                TextureFormat.RGB24, false);
            cpy.filterMode = FilterMode.Trilinear;
            cpy.wrapMode = TextureWrapMode.Clamp;
            var pixels = copy.GetPixels32(0);
            if (converter != null) pixels = converter(pixels);
            cpy.SetPixels32(pixels);
            byte[] bytes = cpy.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }

        static public void DumpNormal(Texture2DArray arr, int idx, string path)
        {
            DumpTexure(arr, idx, path, UnpackNormalPixels);
        }

        static public void DumpNormal(Texture2D tex, string path)
        {
            DumpTexure(tex, path, UnpackNormalPixels);
        }

        static public void DumpSpecular(Texture2DArray arr, int idx, string path)
        {
            DumpTexure(arr, idx, path, UnpackSpecularPixels);
        }

        static public void DumpSpecular(Texture2D tex, string path)
        {
            DumpTexure(tex, path, UnpackSpecularPixels);
        }

    }

}

