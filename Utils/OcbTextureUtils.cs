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
using System.Text.RegularExpressions;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public static class OcbTextureUtils
{

    // ####################################################################
    // ####################################################################

    public class ResetQualitySettings : IDisposable
    {
        readonly int masterTextureLimit = 0;
        readonly int mipmapsReduction = 0;
        readonly bool streamingMips = false;
        readonly float mipSlope = 0.6776996f;
        public ResetQualitySettings()
        {
            masterTextureLimit = QualitySettings.globalTextureMipmapLimit;
            streamingMips = QualitySettings.streamingMipmapsActive;
            mipmapsReduction = QualitySettings.streamingMipmapsMaxLevelReduction;
            mipSlope = Shader.GetGlobalFloat("_MipSlope");
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.streamingMipmapsActive = false;
            QualitySettings.streamingMipmapsMaxLevelReduction = 0;
            Shader.SetGlobalFloat("_MipSlope", 0.6776996f);
        }
        public void Dispose()
        {
            QualitySettings.globalTextureMipmapLimit = masterTextureLimit;
            QualitySettings.streamingMipmapsActive = streamingMips;
            QualitySettings.streamingMipmapsMaxLevelReduction = mipmapsReduction;
            Shader.SetGlobalFloat("_MipSlope", mipSlope);
        }
    }

    // ####################################################################
    // ####################################################################

    // Blit texture from GPU into RenderTexture and copy pixels back
    public static Texture2D TextureFromGPU(Texture src, int idx, bool linear)
    {
        if (src == null) return null;
        // Create render texture target
        var fmt = GraphicsFormat.R8G8B8A8_SRGB;
        using (ResetQualitySettings reset = new ResetQualitySettings())
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                src.width, src.height, 32, fmt);
            RenderTexture active = RenderTexture.active;
            Graphics.Blit(src, rt, idx, 0);
            // Create CPU texture to hold the pixels
            Texture2D tex = new Texture2D(src.width, src.height,
                fmt, src.mipmapCount, TextureCreationFlags.MipChain);
            // Read pixels from current render texture as set by Blit
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0, false);
            // Apply new pixels to CPU texture
            tex.Apply(true, false);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }
    }

    // ####################################################################
    // ####################################################################

    public static Texture LoadTexture(string bundle, string asset, out int idx)
    {
        idx = 0; // Unity APIs will accept zero
        string re_rgba = @"^[0-9.:]+$";
        if (asset.EndsWith("]"))
        {
            var start = asset.LastIndexOf("[");
            if (start != -1)
            {
                idx = int.Parse(asset.Substring(
                    start + 1, asset.Length - start - 2));
                AssetBundleManager.Instance.LoadAssetBundle(bundle);
                return AssetBundleManager.Instance.Get<Texture>(
                    bundle, asset.Substring(0, start));
            }
            else
            {
                throw new Exception("Missing `[` to match ending `]`");
            }
        }
        else if (!string.IsNullOrWhiteSpace(bundle))
        {
            AssetBundleManager.Instance.LoadAssetBundle(bundle);
            return AssetBundleManager.Instance.Get<Texture>(bundle, asset);
        }
        // Experimental feature to create uniform textures
        else if (Regex.Match(asset, re_rgba).Success)
        {
            var parts = asset.Split(new char[] { ':' });
            if (parts.Length >= 5 && parts.Length <= 6)
            {
                int width = int.Parse(parts[0]);
                int height = int.Parse(parts[1]);
                bool rgba = parts.Length == 6;
                Color color = new Color(
                    float.Parse(parts[2]),
                    float.Parse(parts[3]),
                    float.Parse(parts[4]),
                    rgba ? float.Parse(parts[5]) : 1);
                var tex = new Texture2D(width, height, rgba ?
                    TextureFormat.RGBA32 : TextureFormat.RGB24, true);
                var pixels = tex.GetPixels();
                for (int i = 0; i < pixels.Length; i += 1)
                    pixels[i] = color;
                tex.SetPixels(pixels);
                tex.Apply(true, false);
                tex.Compress(true);
                return tex;
            }
            else
            {
                Log.Error("Invalid on-demand texture {0}", asset);
            }
        }
        else
        {
            Log.Error("Can't load texture from disk {0}", asset);
        }
        return null;
    }

    public static Texture LoadTexture(DataLoader.DataPathIdentifier path, out int idx)
        => LoadTexture(path.BundlePath, path.AssetName, out idx);

    // ####################################################################
    // ####################################################################

    public static Texture2DArray ResizeTextureArray(CommandBuffer cmd, Texture2DArray array,
        int size, bool mipChain = false, bool linear = true, bool destroy = false)
    {
        if (array.depth >= size) return array;
        // Create a copy and add space for more textures
        var copy = new Texture2DArray(array.width, array.height,
            size, array.graphicsFormat, TextureCreationFlags.MipChain,
            array.mipmapCount);
        // Keep readable state same as original
        if (!array.isReadable) copy.Apply(false, true);
        copy.filterMode = array.filterMode;
        if (!copy.name.Contains("extended_"))
            copy.name = "extended_" + array.name;
        // Copy old textures to new copy (any better way?)
        for (int i = 0; i < Mathf.Min(array.depth, size); i++)
            cmd.CopyTexture(array, i, copy, i);
        // Optionally destroy the original object
        if (destroy) UnityEngine.Object.Destroy(array);
        // Return the copy
        return copy;
    }

    // ####################################################################
    // ####################################################################

    static int GetMipMapOffset()
    {
        int quality = GameOptionsManager.GetTextureQuality();
        return quality > 2 ? 2 : quality;
    }

    public static NativeArray<byte> GetPixelData(Texture src, int idx, int mip = 0)
    {
        if (src is Texture2DArray arr) return arr.GetPixelData<byte>(idx, mip);
        else if (src is Texture2D tex) return tex.GetPixelData<byte>(mip);
        else throw new Exception("Ivalid texture type to get pixel data");
    }

    public static void SetPixelData(NativeArray<byte> pixels, Texture src, int idx, int mip = 0)
    {
        if (src is Texture2DArray arr) arr.SetPixelData(pixels, mip, idx);
        else if (src is Texture2D tex) tex.SetPixelData(pixels, mip);
        else Log.Error("Invalid texture type to set pixels");
    }

    public static void ApplyPixelData(
        Texture src, bool updateMipmaps = true,
        bool makeNoLongerReadable = false)
    {
        if (src is Texture2DArray arr) arr.Apply(updateMipmaps, makeNoLongerReadable);
        else if (src is Texture2D tex) tex.Apply(updateMipmaps, makeNoLongerReadable);
        else Log.Error("Invalid texture type to apply pixels");
    }

    // Copy `src` into `dst[idx]`, assuming that
    // `src` is full 2k texture into array that is
    // quality constrained (e.g. 1024 for half).
    // Thus we copy the appropriate mipmaps only!
    // E.g. for 2k into 1k we skip one mipmap level
    public static void PatchTexture(
        CommandBuffer cmds,
        Texture dst, int dstidx,
        Texture src, int srcidx = 0)
    {
        var offset = GetMipMapOffset();
        // Copy all mips individually, could optimize ideal case
        // Given that we don't do this often, not much to gain
        if (dst.isReadable && src.isReadable)
        {
            for (int m = 0; m < dst.mipmapCount; m++)
                SetPixelData(GetPixelData(src, srcidx, offset + m), dst, dstidx, m);
            ApplyPixelData(dst, false, false);
        }
        else
        {
            for (int m = 0; m < dst.mipmapCount; m++) cmds.
                CopyTexture(src, srcidx, m + offset, dst, dstidx, m);
        }
    }

    public static void PatchTexture(
        CommandBuffer cmds, Texture2DArray dst,
        int dstidx, string bundle, string asset)
    {
        var tex = LoadTexture(bundle, asset, out int srcidx);
        PatchTexture(cmds, dst, dstidx, tex, srcidx);
    }

    public static void PatchTexture(
        CommandBuffer cmds, Texture2DArray dst,
        int dstidx, DataLoader.DataPathIdentifier path)
    {
        var tex = LoadTexture(path, out int srcidx);
        PatchTexture(cmds, dst, dstidx, tex, srcidx);
    }

    public static void PatchMicroSplatNormal(
        CommandBuffer cmds, Texture2DArray dst,
        int dstidx, DataLoader.DataPathIdentifier path,
        bool convert = false)
    {
        var gpu = LoadTexture(path, out int srcidx);
        if (convert)
        {
            var tex = TextureFromGPU(gpu, srcidx, true);
            for (var m = 0; m < tex.mipmapCount; m++)
            {
                var pixels = tex.GetPixels(m);
                for (var i = 0; i < pixels.Length; i++)
                    (pixels[i].g, pixels[i].a) =
                        (pixels[i].a, pixels[i].g);
                tex.SetPixels(pixels, m);
            }
            tex.Compress(true);
            tex.Apply(false, false);
            PatchTexture(cmds, dst, dstidx, tex, 0);
        }
        else
        {
            // Just copy from one array slot to another
            PatchTexture(cmds, dst, dstidx, gpu, srcidx);
        }
    }

    // ####################################################################
    // ####################################################################

}
