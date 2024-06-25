﻿using UnityEngine;

// ####################################################################
// A tile from an existing vanilla texture atlas
// ####################################################################

public class TilingAtlas : TilingSource
{

    // ####################################################################
    // ####################################################################

    public Texture2D Atlas;
    public Vector2i Pos;
    public Vector2i Size;

    // ####################################################################
    // ####################################################################

    public TilingAtlas(UVRectTiling tiling)
    {
        Tiling = tiling;
    }

    // ####################################################################
    // ####################################################################

    public override string ToString() =>
        $"Atlas {Atlas} at {Pos} ({Size}) to {Dst}";

    // ####################################################################
    // ####################################################################

}