#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public static partial class Tool_2D_Tilemap
    {
        public const string Tilemap2DCreateToolId = "tilemap-2d-create";
        public const string Tilemap2DPaintToolId = "tilemap-2d-paint";

        [McpPluginTool(Tilemap2DCreateToolId, Title = "2D / Create Tilemap")]
        [Description("Create a Grid root and a Tilemap child in the active scene for 2D level building. Supports Rectangular, Isometric, IsometricZAsY, Hexagon, and HexagonFlatTop layouts, plus optional TilemapCollider2D setup.")]
        public static TilemapCreateResult CreateTilemap(
            [Description("Name of the Grid root GameObject.")]
            string gridName = "Grid",
            [Description("Name of the Tilemap child GameObject.")]
            string tilemapName = "Tilemap",
            [Description("Grid cell layout. Valid values: Rectangular, Isometric, IsometricZAsY, Hexagon, HexagonFlatTop.")]
            string layout = "Rectangular",
            [Description("Sorting layer name for the TilemapRenderer.")]
            string sortingLayerName = "Default",
            [Description("Renderer order in layer.")]
            int orderInLayer = 0,
            [Description("Whether to add TilemapCollider2D to the created tilemap.")]
            bool addTilemapCollider2D = false)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(gridName))
                    throw new ArgumentException("gridName cannot be null or empty.", nameof(gridName));
                if (string.IsNullOrWhiteSpace(tilemapName))
                    throw new ArgumentException("tilemapName cannot be null or empty.", nameof(tilemapName));
                if (string.IsNullOrWhiteSpace(sortingLayerName))
                    throw new ArgumentException("sortingLayerName cannot be null or empty.", nameof(sortingLayerName));

                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || !scene.isLoaded)
                    throw new InvalidOperationException("No active loaded scene is available.");

                var gridGo = new GameObject(gridName);
                var grid = gridGo.AddComponent<Grid>();
                ConfigureGridLayout(grid, layout);
                SceneManager.MoveGameObjectToScene(gridGo, scene);

                var tilemapGo = new GameObject(tilemapName);
                tilemapGo.transform.SetParent(gridGo.transform, false);
                var tilemap = tilemapGo.AddComponent<Tilemap>();
                var renderer = tilemapGo.AddComponent<TilemapRenderer>();
                renderer.sortingLayerName = sortingLayerName;
                renderer.sortingOrder = orderInLayer;

                if (addTilemapCollider2D)
                    tilemapGo.AddComponent<TilemapCollider2D>();

                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = tilemapGo;
                EditorUtils.RepaintAllEditorWindows();

                return new TilemapCreateResult
                {
                    ScenePath = scene.path,
                    GridName = gridGo.name,
                    TilemapName = tilemapGo.name,
                    Layout = grid.cellLayout.ToString(),
                    SortingLayerName = renderer.sortingLayerName,
                    OrderInLayer = renderer.sortingOrder,
                    AddedTilemapCollider2D = addTilemapCollider2D
                };
            });
        }

        [McpPluginTool(Tilemap2DPaintToolId, Title = "2D / Paint Tilemap")]
        [Description("Paint Tile assets into a Tilemap in the active scene using explicit cell coordinates. Use tile-assets-create first to generate the Tile assets that this tool places.")]
        public static TilemapPaintResult PaintTilemap(
            [Description("Name of the target Tilemap GameObject in the active scene.")]
            string tilemapName,
            [Description("List of cells to paint. Each cell contains x, y, z grid coordinates and a Tile asset path.")]
            TilemapPaintCellInput[] cells)
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(tilemapName))
                    throw new ArgumentException("tilemapName cannot be null or empty.", nameof(tilemapName));
                if (cells == null || cells.Length == 0)
                    throw new ArgumentException("cells cannot be null or empty.", nameof(cells));

                var tilemap = FindUniqueTilemap(tilemapName);
                var painted = new List<string>();
                foreach (var cell in cells)
                {
                    if (cell == null)
                        throw new ArgumentException("cells contains a null entry.", nameof(cells));
                    if (string.IsNullOrWhiteSpace(cell.TileAssetPath))
                        throw new ArgumentException("TileAssetPath cannot be null or empty.", nameof(cells));
                    if (!cell.TileAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
                        throw new ArgumentException($"Tile asset path '{cell.TileAssetPath}' must start with 'Assets/'.", nameof(cells));

                    var tile = AssetDatabase.LoadAssetAtPath<TileBase>(cell.TileAssetPath)
                        ?? throw new InvalidOperationException($"Tile asset not found at '{cell.TileAssetPath}'.");

                    var position = new Vector3Int(cell.X, cell.Y, cell.Z);
                    tilemap.SetTile(position, tile);
                    painted.Add($"({cell.X},{cell.Y},{cell.Z}) <- {cell.TileAssetPath}");
                }

                EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
                EditorUtility.SetDirty(tilemap);
                EditorUtils.RepaintAllEditorWindows();

                return new TilemapPaintResult
                {
                    TilemapName = tilemap.gameObject.name,
                    PaintedCount = painted.Count,
                    PaintedCells = painted
                };
            });
        }

        private static void ConfigureGridLayout(Grid grid, string layout)
        {
            switch (layout.Trim().ToLowerInvariant())
            {
                case "rectangular":
                    grid.cellLayout = GridLayout.CellLayout.Rectangle;
                    break;
                case "isometric":
                    grid.cellLayout = GridLayout.CellLayout.Isometric;
                    break;
                case "isometriczasy":
                    grid.cellLayout = GridLayout.CellLayout.IsometricZAsY;
                    break;
                case "hexagon":
                    grid.cellLayout = GridLayout.CellLayout.Hexagon;
                    grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
                    break;
                case "hexagonflattop":
                    grid.cellLayout = GridLayout.CellLayout.Hexagon;
                    grid.cellSwizzle = GridLayout.CellSwizzle.YXZ;
                    break;
                default:
                    throw new ArgumentException("layout must be Rectangular, Isometric, IsometricZAsY, Hexagon, or HexagonFlatTop.", nameof(layout));
            }
        }

        private static Tilemap FindUniqueTilemap(string tilemapName)
        {
            var matches = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(x => string.Equals(x.gameObject.name, tilemapName, StringComparison.Ordinal))
                .ToArray();

            if (matches.Length == 0)
                throw new InvalidOperationException($"No Tilemap named '{tilemapName}' was found in the open scenes.");
            if (matches.Length > 1)
                throw new InvalidOperationException($"Multiple Tilemaps named '{tilemapName}' were found. Rename them to make the target unique.");
            return matches[0];
        }

        public class TilemapPaintCellInput
        {
            [Description("Cell X coordinate.")]
            public int X { get; set; }
            [Description("Cell Y coordinate.")]
            public int Y { get; set; }
            [Description("Cell Z coordinate.")]
            public int Z { get; set; }
            [Description("Unity asset path to a Tile asset. Example: Assets/Game/TileAssets/Grass/grass_0_0.asset")]
            public string TileAssetPath { get; set; } = string.Empty;
        }

        public class TilemapCreateResult
        {
            public string? ScenePath { get; set; }
            public string? GridName { get; set; }
            public string? TilemapName { get; set; }
            public string? Layout { get; set; }
            public string? SortingLayerName { get; set; }
            public int OrderInLayer { get; set; }
            public bool AddedTilemapCollider2D { get; set; }
        }

        public class TilemapPaintResult
        {
            public string? TilemapName { get; set; }
            public int PaintedCount { get; set; }
            public List<string>? PaintedCells { get; set; }
        }
    }
}
