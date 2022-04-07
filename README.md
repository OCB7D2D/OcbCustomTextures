# OCB Core Mod for Custom Texture Atlas  - 7 Days to Die (A20) Addon

This mod doesn't do much on it's own, but it allows other mods to
add custom block paint and terrain textures.

See [OcbCustomTexturesDemo][4] for some demo config and resources!

## Custom Block Paints

Block paints are internally handled by a [`Texture2DArray`][1], which
is only available since recent times (e.g. [OpenGL 3][2]). This means
we have some limitations here, as every texture in the array must be
of the same dimension (512x512), type and the same mipmap levels.
You achieve this with correct texture settings in unity.

![Unity texture settings](Screens/unity-paint-texture-settings.png)

You can use [AssetStudio][3] to check the exported `unity3d`
resource to see the format of your textures. If the formats
are not correct, the code will either outright reject your
new texture or you can expect to see weird visual glitches!

![AssetStudio paint diffuse](Screens/asset-studio-paint-diffuse.png)
![AssetStudio paint normal](Screens/asset-studio-paint-normal.png)

For lower texture resolutions we "resize" the texture accordingly.
Actually we don't really resize it, but we simply re-use the
existing texture mipmaps to create lower dimension renditions.

### XML Config For Custom Paints

`Config/painting.xml`:

```xml
<configs><append xpath="/paints">
	<paint id="bark_pine_all_faces" name="txName_bark_pine_all_faces" x="0" y="0" w="2" h="2" blockw="2" blockh="2">
		<property name="Diffuse" value="#@modfolder:Resources/Atlas.unity3d?assets/bark_pine_002_diffuse.jpg,assets/bark_pine_001_diffuse.jpg,assets/bark_pine_002_diffuse.jpg,assets/bark_pine_003_diffuse.jpg"/>
		<property name="Normal" value="#@modfolder:Resources/Atlas.unity3d?assets/bark_pine_002_normal.jpg,assets/bark_pine_001_normal.jpg,assets/bark_pine_002_normal.jpg,assets/bark_pine_003_normal.jpg"/>
		<property name="Group" value="txGroupDecoration"/>
		<property name="GlobalUV" value="False" />
		<property name="SwitchUV" value="False" />
		<property name="Color" value="0.4823529,0.3490196,0.2901961"/>
		<property name="Material" value="wood" />
		<property name="PaintCost" value="1" />
		<property name="Hidden" value="false" />
	</paint>
</append></configs>
```

I can't really explain all the properties here, since they are
mostly just passed through and I couldn't always figure out what
they are intended to influence, but they match 1-to-1 the options
that are normally set via the `uv.xml` fragment.

`Config/blocks.xml`:

```xml
<block name="demo_block">
	<property name="Texture" value="demo_terrain"/>
</block>
```

## Custom Terrain Textures

Terrain textures seem to use an a two folded (and confusing approach).
It seems even in vanilla you can't really add a snow block to a none
snow biome (it will always look like the original terrain, e.g. topsoil).
https://steamcommunity.com/app/251570/discussions/4/2264691750504420653/

In order to properly use custom terrain texture you must have
[SphereII's Legacy Distant Terrain][5] mod installed. It seems to
disable the MicroSplat rendering for an older implementation.

### Terrain MicroSplat Rendering

From what I gathered it seems that 7D2D uses [MicroSplat rendering][6] for the
terrain, which is somehow Biome dependent (e.g. TopSoil look depends on Biome).
Unfortunately I couldn't really figure out how one could even possibly influence
this. And [SphereII's Legacy Distant Terrain][5] mod seems to solve all issues.

I know that MicroSplat rendering is using e.g. `TextureAtlas.diffuseTexture`,
which is of type `Texture2DArray`. Unfortunately the highest asset rendition
of the Map-Array is read-only protected, so I couldn't even alter it if I wanted.
Funny enough the lower renditions (half/quarter) are readable, so I was able
to do some tests, e.g. I can change textures for existing MicroSplat terrains.

On the other hand, "legacy terrain" and as it seems the editor, use a legacy
terrain atlas via e.g. `TextureAtlasTerrain.diffuse`. By Patching the function
`GameManager.IsSplatMapAvailable` to always return false, I was able to bring the
custom terrain textures to show, but then distant terrain wouldn't render anymore.

### XML Config For Custom Terrains

`Config/painting.xml`:

```xml
<configs><append xpath="/paints">
	<terrain id="demo_terrain">
		<property name="Diffuse" value="#@modfolder:Resources/Atlas.unity3d?assets/bark_pine_002_diffuse.jpg"/>
		<property name="Normal" value="#@modfolder:Resources/Atlas.unity3d?assets/bark_pine_002_normal.jpg"/>
		<property name="Color" value="0.2588235,0.2705882,0.1921569" />
		<property name="SwitchUV" value="True" />
		<property name="GlobalUV" value="False" />
		<property name="Material" value="dirt" />
	</terrain >
</append></configs>
```

I can't really explain all the properties here, since they are
mostly just passed through and I couldn't always figure out what
they are intended to influence, but they match 1-to-1 the options
that are normally set via the `uv.xml` fragment.

`Config/blocks.xml`:

```xml
<block name="demo_terrain">
	<property name="Shape" value="Terrain"/>
	<property name="Mesh" value="terrain"/>
	<property name="Texture" value="demo_terrain"/>
</block>
```

## Custom Grass Textures (not supported yet)

Grass textures use a "true texture atlas", which is IMO normally just
one big texture/image where each block just uses a small rectangle
of that atlas (via UV coordinates). I believe the different ways
texture atlases are handled now, has simply historically grown. We can
certainly also extend this big atlas texture by blitting new textures
into the existing atlas texture (and setting the UVs correctly). At
some point we even would need to grow the original texture. This all
seems feasible, but probably a lot of tedious work to get it right.
Reminds me of some work I've done years ago to create web-sprites.

## Changelog

### Version 0.2.0

- Major code refactoring
- Loading speed improvements

### Version 0.1.0

- Initial version

## Compatibility

I've developed and tested this Mod against version a20.3(b3).

[1]: https://docs.unity3d.com/ScriptReference/Texture2DArray.html
[2]: https://www.khronos.org/opengl/wiki/Array_Texture
[3]: https://github.com/Perfare/AssetStudio
[4]: https://github.com/OCB7D2D/OcbCustomTexturesDemo
[5]: https://gitlab.com/sphereii/SphereII-Mods/-/archive/master/SphereII-Mods-master.zip?path=SphereII%20Legacy%20Distant%20Terrain
[6]: https://assetstore.unity.com/packages/tools/terrain/microsplat-96478