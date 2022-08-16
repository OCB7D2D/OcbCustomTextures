// Runtime tools for texture manipulation (unitycoder.com)
// https://github.com/unitycoder/NormalMapFromTexture (MIT License)

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class NormalMapTools 
{

	public static Texture2D CreateAOSTMap(Texture2D t, DynamicProperties props)
	{

		float AO = 1f;
		float MinSpecular = 0f;
		float MaxSpecular = 1f;
		float SpecularFactor = 1f;
		float SpecularPower = 4f;
		float MinTranslucency = 0f;
		float MaxTranslucency = 1f;
		float TranslucencyFactor = 1f;
		float TranslucencyPower = 2f;

		props.ParseFloat("AO", ref AO);
		props.ParseFloat("MinSpecular", ref MinSpecular);
		props.ParseFloat("MaxSpecular", ref MaxSpecular);
		props.ParseFloat("SpecularFactor", ref SpecularFactor);
		props.ParseFloat("SpecularPower", ref SpecularPower);
		props.ParseFloat("MinTranslucency", ref MinTranslucency);
		props.ParseFloat("MaxTranslucency", ref MaxTranslucency);
		props.ParseFloat("TranslucencyFactor", ref TranslucencyFactor);
		props.ParseFloat("TranslucencyPower", ref TranslucencyPower);

		return CreateAOSTMap(t, AO,
			MinSpecular, MaxSpecular, SpecularFactor, SpecularPower,
			MinTranslucency, MaxTranslucency, TranslucencyFactor, TranslucencyPower);
	}

	/// <summary>
	/// Creates the specular map
	/// </summary>
	/// <returns>specular map texture</returns>
	/// <param name="t">source texture</param>
	/// <param name="specularContrast">Specular contrast float (example: 0-2)</param>
	/// <param name="specularCutOff">Specular cut off float (example: 0-1)</param>

	public static Texture2D CreateAOSTMap(Texture2D t, float AO = 1f,
		float MinSpecular = 0f, float MaxSpecular = 1f, float SpecularFactor = 1f, float SpecularPower = 4f,
		float MinTranslucency = 0f, float MaxTranslucency = 1f, float TranslucencyFactor = 1f, float TranslucencyPower = 2f)
	{

		Color[] pixels = t.GetPixels();

		// Create new texture to hold Specular (R), Occlusion (G), Translucency/Smoothness (B)
		Texture2D specular = new Texture2D(t.width, t.height, TextureFormat.RGB24, false, false);

		for (int y = 0; y < t.height; y++)
		{
			for (int x = 0; x < t.width; x++)
			{
				Color px = pixels[x + y * t.width];
				// Use own formula to create specular on flowers
				float spc = px.r * 0.45f + px.b * 0.35f + px.g * 0.2f;
				spc = Mathf.Pow(spc * SpecularFactor, SpecularPower);
				spc = Mathf.Lerp(MinSpecular, MaxSpecular, spc);
				// Use own formula to create translucency on flowers
				float tr = Mathf.Max(px.r, px.b) * 0.75f + px.g * 0.25f;
				tr = Mathf.Pow(tr * TranslucencyFactor, TranslucencyPower);
				tr = Mathf.Lerp(MinTranslucency, MaxTranslucency, tr);
				pixels[x+y*t.width] = new Color(spc, AO, tr, 1);
			}
		}

		specular.SetPixels(pixels);
		specular.Apply();
		return specular;
	}


	/// <summary>
	/// Create the normalmap
	/// </summary>
	/// <returns>normal map color array</returns>
	/// <param name="t">source texture</param>
	/// <param name="normalStrength">normal map strength float (example: 1-20)</param>

	public static Texture2D CreateNormalmap(Texture2D t, float normalStrength, bool compressed = false)
	{
		Color[] pixels = new Color[t.width*t.height];
		Texture2D texNormal = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false, false);			
		Vector3 vScale = new Vector3(0.3333f,0.3333f,0.3333f);

		// TODO: would be faster using pixel array, instead of getpixel
		for (int y=0;y<t.height;y++)
		{
			for (int x=0;x<t.width;x++)
			{
				Color tc = t.GetPixel(x-1, y-1);
				Vector3 cSampleNegXNegY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x, y-1);
				Vector3 cSampleZerXNegY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x+1, y-1);
				Vector3 cSamplePosXNegY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x-1, y);
				Vector3 cSampleNegXZerY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x+1,y);
				Vector3 cSamplePosXZerY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x-1,y+1);
				Vector3 cSampleNegXPosY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x,y+1);
				Vector3 cSampleZerXPosY = new Vector3(tc.r,tc.g,tc.g);
				tc = t.GetPixel(x+1,y+1);
				Vector3 cSamplePosXPosY = new Vector3(tc.r,tc.g,tc.g);
				float fSampleNegXNegY = Vector3.Dot(cSampleNegXNegY, vScale);
				float fSampleZerXNegY = Vector3.Dot(cSampleZerXNegY, vScale);
				float fSamplePosXNegY = Vector3.Dot(cSamplePosXNegY, vScale);
				float fSampleNegXZerY = Vector3.Dot(cSampleNegXZerY, vScale);
				float fSamplePosXZerY = Vector3.Dot(cSamplePosXZerY, vScale);
				float fSampleNegXPosY = Vector3.Dot(cSampleNegXPosY, vScale);
				float fSampleZerXPosY = Vector3.Dot(cSampleZerXPosY, vScale);
				float fSamplePosXPosY = Vector3.Dot(cSamplePosXPosY, vScale);							
				float edgeX = (fSampleNegXNegY - fSamplePosXNegY) * 0.25f+(fSampleNegXZerY - fSamplePosXZerY) * 0.5f+ (fSampleNegXPosY - fSamplePosXPosY) * 0.25f;
				float edgeY = (fSampleNegXNegY - fSampleNegXPosY) * 0.25f+(fSampleZerXNegY - fSampleZerXPosY)*0.5f+(fSamplePosXNegY - fSamplePosXPosY)*0.25f;
				Vector2 vEdge = new Vector2(edgeX,edgeY)*normalStrength;
				Vector3 norm = new Vector3(vEdge.x,vEdge.y, 1.0f).normalized;
				
				if (compressed)
                {
					var r = norm.x * 0.5f + 0.5f;
					var g = norm.y * 0.5f + 0.5f;
					pixels[x + y * t.width] = new Color(255f, g, g, r);
				}
				else
                {
					pixels[x + y * t.width].r = norm.x * 0.5f + 0.5f;
					pixels[x + y * t.width].g = norm.y * 0.5f + 0.5f;
					pixels[x + y * t.width].b = norm.z * 0.5f + 0.5f;
					pixels[x + y * t.width].a = 1f;
				}
			} // for x
		} // for y

		texNormal.SetPixels(pixels);
		texNormal.Apply();

		return texNormal;
	} // CreateNormalmap




	/// <summary>
	/// Median filter
	/// </summary>
	/// <returns>filtered color array</returns>
	/// <param name="t">t = source texture</param>
	/// <param name="filterSize">fSize = filter size</param>
	public static Texture2D FilterMedian(Texture2D t, int filterSize)
	{
		Color[] pixels = new Color[t.width*t.height];
		Texture2D texFiltered = new Texture2D(t.width, t.height, TextureFormat.RGB24, false, false);	
		int tIndex = 0;
		int medianMin = -(filterSize/2);
		int medianMax = (filterSize/2);
		List<float> r = new List<float>();
		List<float> g = new List<float>();
		List<float> b = new List<float>();
		for (int x = 0; x < t.width; ++x)
		{
			for (int y = 0; y < t.height; ++y)
			{
				r.Clear();
				g.Clear();
				b.Clear();
				for (int x2 = medianMin; x2 < medianMax; ++x2)
				{
					int tx = x + x2;
					if (tx >= 0 && tx < t.width) // TODO: should wrap around? use modulus..
					{
						for (int y2 = medianMin; y2 < medianMax; ++y2)
						{
							int ty = y + y2;
							if (ty >= 0 && ty < t.height)
							{
								Color c = t.GetPixel(tx, ty);
								r.Add(c.r);
								g.Add(c.g);
								b.Add(c.b);
							}
						}
					}
				}
				r.Sort();
				g.Sort();
				b.Sort();
				pixels[x+y*t.width]=new Color(r[r.Count/2],g[g.Count/2], b[b.Count/2]);
				tIndex++;
			}
		}
		texFiltered.SetPixels(pixels);
		texFiltered.Apply();
		return texFiltered;
	} // filtersMedian()



	/// <summary>
	/// Combines the RGB and specular into one texture
	/// </summary>
	/// <returns>ARGB32 texture with RGB + Alpha as specular(gloss)</returns>
	/// <param name="t">source RGB texture (rgb)</param>
	/// <param name="s">source Specular texture (rgb)</param>
	public static Texture2D CombineRGBAndSpecular(Texture2D t, Texture2D s)
	{
		Color[] pixels = t.GetPixels();
		Color[] pixelsSpec = s.GetPixels();
		Texture2D texCombined = new Texture2D(t.width, t.height, TextureFormat.ARGB32, false, false);

		for (int y=0;y<t.height;y++)
		{
			for (int x=0;x<t.width;x++)
			{
				pixels[x+y*t.width].a = pixelsSpec[x+y*t.width].grayscale; // take grayscale value from specular texture
			}
		}

		texCombined.SetPixels(pixels);
		texCombined.Apply();
		return texCombined;

	}


}
