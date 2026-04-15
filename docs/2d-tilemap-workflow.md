# 2D Tilemap Workflow

Unity-MCP now includes a practical 2D tilemap workflow that covers the most common sprite-sheet-to-tilemap pipeline inside the Unity Editor.

## Included tools

| Tool | ID | Purpose |
| --- | --- | --- |
| 2D / Sprite Import Configure | `sprite-import-configure` | Configure a texture for Sprite import, including Single/Multiple mode, pixels-per-unit, filtering, wrapping, compression, and readability. |
| 2D / Sprite Slice Grid | `sprite-slice-grid` | Slice a sprite sheet into a regular grid using Unity's current Sprite Editor data-provider APIs. |
| 2D / Create Tile Assets | `tile-assets-create` | Create or update Tile assets from sliced sprites. |
| 2D / Create Tilemap | `tilemap-2d-create` | Create a Grid root and Tilemap child in the active scene. |
| 2D / Paint Tilemap | `tilemap-2d-paint` | Paint Tile assets into named Tilemaps using explicit cell coordinates. |

## Recommended sequence

1. Import a texture under `Assets/`
2. Run `sprite-import-configure`
3. Run `sprite-slice-grid`
4. Run `tile-assets-create`
5. Run `tilemap-2d-create`
6. Run `tilemap-2d-paint`

## Example flow

### 1. Configure the sprite sheet

```json
{
  "assetPath": "Assets/Art/Tiles/terrain.png",
  "spriteMode": "Multiple",
  "pixelsPerUnit": 16,
  "filterMode": "Point",
  "wrapMode": "Clamp",
  "compression": "Uncompressed",
  "alphaIsTransparency": true,
  "isReadable": true
}
```

### 2. Slice it into a 16x16 grid

```json
{
  "assetPath": "Assets/Art/Tiles/terrain.png",
  "cellWidth": 16,
  "cellHeight": 16,
  "offsetX": 0,
  "offsetY": 0,
  "padding": 0,
  "pivot": "Center"
}
```

### 3. Create Tile assets

```json
{
  "spriteSheetAssetPath": "Assets/Art/Tiles/terrain.png",
  "outputFolder": "Assets/Game/TileAssets/Terrain",
  "tileNamePrefix": "terrain_",
  "createFolderRecursively": true
}
```

### 4. Create a Tilemap

```json
{
  "gridName": "WorldGrid",
  "tilemapName": "Ground",
  "layout": "Rectangular",
  "sortingLayerName": "Default",
  "orderInLayer": 0,
  "addTilemapCollider2D": true
}
```

### 5. Paint cells

```json
{
  "tilemapName": "Ground",
  "cells": [
    { "x": 0, "y": 0, "z": 0, "tileAssetPath": "Assets/Game/TileAssets/Terrain/terrain_grass.asset" },
    { "x": 1, "y": 0, "z": 0, "tileAssetPath": "Assets/Game/TileAssets/Terrain/terrain_dirt.asset" }
  ]
}
```

## Notes

- `sprite-slice-grid` names sprites as `<texture>_<row>_<column>`.
- `tile-assets-create` is idempotent for stable asset paths: re-running updates existing Tile assets.
- `tilemap-2d-create` supports `Rectangular`, `Isometric`, `IsometricZAsY`, `Hexagon`, and `HexagonFlatTop`.
- `tilemap-2d-paint` requires exact Tile asset paths.

## Verification

The source repository includes EditMode tests in:

- `Unity-MCP-Plugin/Assets/root/Tests/Editor/Tool/2D/Tool2DWorkflowTests.cs`

These tests cover:
- sprite import configuration
- sprite slicing
- tile asset generation
- tilemap creation
- tilemap painting
