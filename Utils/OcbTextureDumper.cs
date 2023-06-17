/* MIT License

Copyright (c) 2022-2023 OCB7D2D

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.IO;
using UnityEngine;

using static OcbTextureUtils;

public static class OcbTextureDumper
{

    // ####################################################################
    // Main functions to dump textures to disk
    // ####################################################################

    // Dump generic `Texture` to disk at given `path`
    public static void DumpTexure(string path, Texture src, int idx = 0,
        bool linear = true, Func<Color[], Color[]> converter = null)
    {
        if (src == null) return;
        Texture2D tex = TextureFromGPU(src, idx, linear);
        if (converter != null)
        {
            var pixels = tex.GetPixels(0);
            pixels = converter(pixels);
            tex.SetPixels(pixels);
            tex.Apply(true, false);
        }
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
    }

    // Dump `Texture2D` to disk at given `path`
    public static void DumpTexure(string path, Texture2D src,
        bool linear = true, Func<Color[], Color[]> converter = null)
            => DumpTexure(path, src, 0, linear, converter);

    // Dump `Texture2DArray[idx]` to disk at given `path`
    // public static void DumpTexure(string path, Texture2DArray arr, int idx,
    //     bool linear = true, Func<Color[], Color[]> converter = null)
    //         => DumpTexure(path, (Texture)arr, idx, linear, converter);

    // ####################################################################
    // Converter helpers for various texture formats
    // ####################################################################

    // MicroSplat stores the roughness in the albedo channel
    public static Color[] ExtractRoughnessFromTexture(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].r = pixels[i].g;
            pixels[i].g = pixels[i].g;
            pixels[i].b = pixels[i].g;
            pixels[i].a = 1;
        }
        return pixels;
    }

    // MicroSplat stores the ambient occlusion in the alpha channel
    public static Color[] ExtractAmbientOcclusionFromTexture(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].r = pixels[i].a;
            pixels[i].g = pixels[i].a;
            pixels[i].b = pixels[i].a;
            pixels[i].a = 1;
        }
        return pixels;
    }

    // MicroSplat stores the height in the albedo alpha channel
    public static Color[] ExtractHeightFromAlbedoTexture(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].r = pixels[i].a;
            pixels[i].g = pixels[i].a;
            pixels[i].b = pixels[i].a;
            pixels[i].a = 1;
        }
        return pixels;
    }

    // MicroSplat stores the height in the albedo alpha channel
    public static Color[] RemoveHeightFromAlbedoTexture(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].a = 1;
        }
        return pixels;
    }

    // Normals are stored in the green and alpha channel
    // This is due to normals using BC3 block compression
    // BC3 format stores color data using 5:6:5 color (5 bits red,
    // 6 bits green, 5 bits blue) and alpha data using one byte
    // Normals axis range from -1 to 1 while pixel data from 0 to 1
    // We therefore need to "unpack" those axes to get desired range
    // From there we can compute the remaining z vector value
    // Given that we know normal vectors have a length of 1
    // We then pack this info back into 0 to 1 color range
    public static Color[] UnpackNormalPixels(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].r = pixels[i].a;
            //pixels[i].g = pixels[i].g;
            float x = pixels[i].r * 2 - 1;
            float y = pixels[i].g * 2 - 1;
            // Get `z` via `1 = sqrt(x^2 + y^2 + z^2)`
            float z = Mathf.Sqrt(1 - x * x - y * y);
            pixels[i].b = z * 0.5f + 0.5f;
            pixels[i].a = 1f;
        }
        return pixels;
    }

    // Somehow normals in Texture2DArray seem to have xy axis switched
    // This has also been verified in the shader as shadows didn't match
    // I assume 7D2D uses old MicroSplat version that had this inverted!?
    public static Color[] UnpackNormalPixelsSwitched(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            (pixels[i].g, pixels[i].a) = (pixels[i].a, pixels[i].g);
        }
        return UnpackNormalPixels(pixels);
    }

    // ####################################################################
    // ####################################################################

    public static Color[] ExtractRedChannel(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].r = 1 - pixels[i].r;
            pixels[i].g = pixels[i].r;
            pixels[i].b = pixels[i].r;
            pixels[i].a = 1;
        }
        return pixels;
    }

    public static Color[] ExtractGreenChannel(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].g = 1 - pixels[i].g;
            pixels[i].r = pixels[i].g; 
            pixels[i].b = pixels[i].g;
            pixels[i].a = 1;
        }
        return pixels;
    }

    public static Color[] ExtractBlueChannel(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].b = 1 - pixels[i].b;
            pixels[i].r = pixels[i].b; 
            pixels[i].g = pixels[i].b;
            pixels[i].a = 1;
        }
        return pixels;
    }

    public static Color[] ExtractAlphaChannel(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= 0; i--)
        {
            pixels[i].a = 1 - pixels[i].a;
            pixels[i].r = pixels[i].a;
            pixels[i].g = pixels[i].a;
            pixels[i].b = pixels[i].a;
            pixels[i].a = 1;
        }
        return pixels;
    }

    // ####################################################################
    // ####################################################################

}
