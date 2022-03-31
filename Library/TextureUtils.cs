using System;
using UnityEngine;

namespace OCB
{

    static public class TexUtils
    {

        // OcbMaurice's unity texture utilities
        // Seems utility support in unity for textures is lacking
        // Or I just didn't see the proper interfaces for it yet

        // DXT5 is BC3 is 5:6:5 bit (2 bytes) format (plus 1 byte for alpha)

        static Texture2D ResizeTexture(Texture2D texture2D, int width, int height, bool color32)
        {
            RenderTexture rt = new RenderTexture(width, height, color32 ? 32 : 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D resize = new Texture2D(width, height);
            resize.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resize.Apply(true);
            return resize;
        }

        static Texture2D CopyTexture(Texture2D texture2D, int width, int height, bool color32)
        {
            RenderTexture rt = new RenderTexture(width, height, color32 ? 32 : 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D resize = new Texture2D(width, height);
            resize.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            resize.Apply(true);
            return resize;
        }

        static Color[] UnpackNormalPixels(Color[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = pixels[i].a;
                pixels[i].g = pixels[i].b;
                pixels[i].b = 1.0f;
            }
            return pixels;
        }

        static Color[] UnpackSpecularPixels(Color[] pixels)
        {
            for (int i = pixels.Length - 1; i >= 0; i--)
            {
                pixels[i].r = 1f - pixels[i].g;
                pixels[i].b = 1f - pixels[i].g;
                pixels[i].g = 1f - pixels[i].g;
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
            else
            {
                Log.Error("Invalid texture to dump " + tex);
            }
        }

        static public void DumpTexure(Texture2D tex, string path,
            Func<Color[], Color[]> converter = null)
        {
            Texture2D cpy = ResizeTexture(tex, tex.width, tex.height, false);
            cpy.filterMode = FilterMode.Trilinear;
            cpy.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = cpy.GetPixels();
            if (converter != null) pixels = converter(pixels);
            cpy.SetPixels(pixels);
            byte[] bytes = cpy.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }

        static public void DumpTexure(Texture2DArray arr, int idx, string path,
            Func<Color[], Color[]> converter = null)
        {
            Texture2D cpy = new Texture2D(
                arr.width, arr.height,
                TextureFormat.RGB24, false);
            cpy.filterMode = FilterMode.Trilinear;
            cpy.wrapMode = TextureWrapMode.Clamp;
            var pixels = arr.GetPixels(idx);
            if (converter != null) pixels = converter(pixels);
            cpy.SetPixels(pixels);
            byte[] bytes = cpy.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }

        static public void DumpNormalTexure(Texture2DArray arr, int idx, string path)
        {
            DumpTexure(arr, idx, path, UnpackNormalPixels);
        }

        static public void DumpNormalTexure(Texture2D tex, string path)
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

