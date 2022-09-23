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

To overwrite any existing texture in the atlas, you just need to
define the id as the number of the texture ID you want overwritten:
E.g. `<paint id="141" ... >` to overwrite `Garage Door 2` texture.

`Config/blocks.xml`:

```xml
<block name="demo_block">
	<property name="Texture" value="demo_terrain"/>
</block>
```

## Custom grass/plant textures

Grass textures are stored in an "old-school" image atlas, aka sprite atlas.
Meaning that all textures are merged into one big texture and models/meshes
just reference the partial square via UV coordinates. In order to patch such
an existing atlas, we first and foremost need to know what parts are already
used and which parts are free to re-use. In the end I implemented a pretty
sophisticated workflow to enable patching of such atlases.

- Reading existing sub-textures from the atlas
- Done by evaluating the existing XML UV mappings
- All sub-textures are cut out from the existing atlas
- Then we add the newly added sub-textures to that array
- Once done, we create a new atlas and update all UV configs

See https://github.com/OCB7D2D/OcbCustomTexturesPlants for more info

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

### Custom MicroSplat Terrain Blending

Even though I haven't been able to crack to add really new textures to the current
MicroSplat terrain rendering shader, I still got something working that at least
allows to create additional slightly distinct variations from the existing terrain
textures. MicroSplat can blend between multiple terrain textures, not just two.

```xml
<block name="terrOreCustom">
	<!-- Important to give it this class to parse new properts -->
	<!-- we could do it more agnostic, but this performs better -->
	<property name="Class" value="CustomTerrain, CustomTextures"/>
	<!-- this is the fallback texture, e.g. some preview will be -->
	<!-- rendered with this single texture. Also if you have the -->
	<!-- legacy distant terrain mod enabled, this texture will be -->
	<!-- used. This feature is meant to be used with SplatMap. -->
	<!-- Although you may hockup a new texture to support both -->
	<property name="Texture" value="demo_terrain"/>
	<!-- Determines how the preview looks when placing? -->
	<!-- Not sure yet how this relates to texture indexes? -->
	<property name="TerrainIndex" value="9"/>
	<!-- Main setting needed to enable custom terrain blends -->
	<!-- It seems this is the main knob the tell micro splat -->
	<!-- to also sample additional texture for the final result -->
	<property name="TerrainBlend" value="1.0"/>
	<!-- Below are the blend settings -->
	<!-- Most times you want 2 or 3 blends -->
	<!-- Some compinations work ok, some work badly -->
	<!-- You'll need to figure out a combo that fits -->
	<property name="BlendDirt" value="0.0"/>
	<property name="BlendGravel" value="0.2"/>
	<property name="BlendOreCoal" value="1.0"/>
	<property name="BlendAsphalt" value="0.0"/>
	<property name="BlendOreIron" value="0.0"/>
	<property name="BlendOreNitrate" value="0.8"/>
	<property name="BlendOreOil" value="0.0"/>
	<property name="BlendOreLead" value="0.4"/>
	<property name="BlendStoneDesert" value="0.0"/>
	<property name="BlendStoneRegular" value="0.0"/>
	<property name="BlendStoneDestroyed" value="0.0"/>
	<!-- Further Additional terrain block settings ... -->
</block>
```

Mileage may vary what you can achieve with certain combinations and I'm not
even really sure myself how it all works. I only know that this exposes all the
low-level settings that go directly into the SplatMap shader. It might be good
enough for most people to create additional ores or terrains. But you'll need
to go through the trail and error process yourself :)

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

### Version 0.4.0

- Implement custom MicroSplat terrain blends

### Version 0.3.1

- Allow to overwrite existing paint textures

### Version 0.3.0

- Add experimental grass atlas patching
- "live" helper functions via CMD options
- Fix UV ID assignment

### Version 0.2.1

- Fix for dedicated servers

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