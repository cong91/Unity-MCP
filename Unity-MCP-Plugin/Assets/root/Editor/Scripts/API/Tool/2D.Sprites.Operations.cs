#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public static partial class Tool_2D_Sprites
    {
        public const string SpriteImportConfigureToolId = "sprite-import-configure";
        public const string SpriteSliceGridToolId = "sprite-slice-grid";
        public const string TileAssetsCreateToolId = "tile-assets-create";

        [McpPluginTool(SpriteImportConfigureToolId, Title = "2D / Sprite Import Configure")]
        [Description("Configure a texture asset for 2D sprite or sprite-sheet use. Use this before slicing or tile creation. Supports Single or Multiple sprite modes, pixels-per-unit, filter mode, wrap mode, compression, alpha transparency, and readability.")]
        public static SpriteImportSettingsResult ConfigureSpriteImport(
            [Description("Texture asset path inside the Unity project. Must start with 'Assets/'. Example: Assets/Art/Tiles/terrain.png")]
            string assetPath,
            [Description("Sprite import mode. Valid values: Single, Multiple.")]
            string spriteMode = "Multiple",
            [Description("Pixels per unit for generated sprites.")]
            int pixelsPerUnit = 16,
            [Description("Texture filter mode. Valid values: Point, Bilinear, Trilinear.")]
            string filterMode = "Point",
            [Description("Texture wrap mode. Valid values: Clamp, Repeat, Mirror, MirrorOnce.")]
            string wrapMode = "Clamp",
            [Description("Texture compression. Valid values: Uncompressed, Compressed, CompressedHQ, CompressedLQ.")]
            string compression = "Uncompressed",
            [Description("Whether the texture has transparency/alpha.")]
            bool alphaIsTransparency = true,
            [Description("Whether Read/Write should be enabled.")]
            bool isReadable = false)
        {
            return MainThread.Instance.Run(() =>
            {
                ValidateAssetPath(assetPath, ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff");

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)
                    ?? throw new ArgumentException($"Texture asset not found at '{assetPath}'.", nameof(assetPath));
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                    ?? throw new InvalidOperationException($"Asset '{assetPath}' is not a TextureImporter asset.");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = ParseSpriteImportMode(spriteMode);
                importer.spritePixelsPerUnit = pixelsPerUnit > 0 ? pixelsPerUnit : throw new ArgumentException("pixelsPerUnit must be > 0.", nameof(pixelsPerUnit));
                importer.filterMode = ParseFilterMode(filterMode);
                importer.wrapMode = ParseWrapMode(wrapMode);
                importer.textureCompression = ParseTextureCompression(compression);
                importer.alphaIsTransparency = alphaIsTransparency;
                importer.isReadable = isReadable;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();

                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                EditorUtils.RepaintAllEditorWindows();

                return new SpriteImportSettingsResult
                {
                    AssetPath = assetPath,
                    TextureWidth = texture != null ? texture.width : 0,
                    TextureHeight = texture != null ? texture.height : 0,
                    SpriteMode = importer.spriteImportMode.ToString(),
                    PixelsPerUnit = importer.spritePixelsPerUnit,
                    FilterMode = importer.filterMode.ToString(),
                    WrapMode = importer.wrapMode.ToString(),
                    Compression = importer.textureCompression.ToString(),
                    AlphaIsTransparency = importer.alphaIsTransparency,
                    IsReadable = importer.isReadable
                };
            });
        }

        [McpPluginTool(SpriteSliceGridToolId, Title = "2D / Sprite Slice Grid")]
        [Description("Slice a sprite-sheet texture into a regular grid using the current Unity Sprite Editor data-provider APIs. Use after configuring the texture as Sprite Multiple. Generates sprite names as <texture>_<row>_<column>.")]
        public static SpriteSliceResult SliceSpriteSheetGrid(
            [Description("Texture asset path inside the Unity project. Must start with 'Assets/'.")]
            string assetPath,
            [Description("Cell width in pixels.")]
            int cellWidth,
            [Description("Cell height in pixels.")]
            int cellHeight,
            [Description("Horizontal pixel offset from the left edge before slicing begins.")]
            int offsetX = 0,
            [Description("Vertical pixel offset from the top edge before slicing begins.")]
            int offsetY = 0,
            [Description("Padding in pixels between cells on both axes.")]
            int padding = 0,
            [Description("Sprite pivot preset. Valid values: Center, Bottom, BottomLeft, BottomRight, Left, Right, Top, TopLeft, TopRight.")]
            string pivot = "Center")
        {
            return MainThread.Instance.Run(() =>
            {
                ValidateAssetPath(assetPath, ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff");
                if (cellWidth <= 0) throw new ArgumentException("cellWidth must be > 0.", nameof(cellWidth));
                if (cellHeight <= 0) throw new ArgumentException("cellHeight must be > 0.", nameof(cellHeight));
                if (padding < 0) throw new ArgumentException("padding must be >= 0.", nameof(padding));
                if (offsetX < 0 || offsetY < 0) throw new ArgumentException("offsetX and offsetY must be >= 0.");

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath)
                    ?? throw new ArgumentException($"Texture asset not found at '{assetPath}'.", nameof(assetPath));
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                    ?? throw new InvalidOperationException($"Asset '{assetPath}' is not a TextureImporter asset.");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.SaveAndReimport();

                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer)
                    ?? throw new InvalidOperationException("Could not acquire ISpriteEditorDataProvider for the texture importer.");
                dataProvider.InitSpriteEditorDataProvider();

                var spriteRects = new List<SpriteRect>();
                var pivotValue = ParsePivot(pivot);
                var baseName = Path.GetFileNameWithoutExtension(assetPath);
                var row = 0;

                for (var yTop = offsetY; yTop + cellHeight <= texture.height; yTop += cellHeight + padding)
                {
                    var column = 0;
                    for (var x = offsetX; x + cellWidth <= texture.width; x += cellWidth + padding)
                    {
                        var rectY = texture.height - yTop - cellHeight;
                        spriteRects.Add(new SpriteRect
                        {
                            name = $"{baseName}_{row}_{column}",
                            rect = new Rect(x, rectY, cellWidth, cellHeight),
                            alignment = SpriteAlignment.Custom,
                            pivot = pivotValue,
                            border = Vector4.zero,
                            spriteID = GUID.Generate()
                        });
                        column++;
                    }
                    row++;
                }

                if (spriteRects.Count == 0)
                    throw new InvalidOperationException("No sprite cells were generated. Check texture size, offsets, padding, and cell dimensions.");

                dataProvider.SetSpriteRects(spriteRects.ToArray());
                var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
                if (nameFileIdProvider != null)
                {
                    nameFileIdProvider.SetNameFileIdPairs(spriteRects
                        .Select(x => new SpriteNameFileIdPair(x.name, x.spriteID))
                        .ToArray());
                }
                dataProvider.Apply();

                AssetDatabase.ForceReserializeAssets(new[] { assetPath }, ForceReserializeAssetsOptions.ReserializeMetadata);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().OrderBy(x => x.name).ToArray();
                return new SpriteSliceResult
                {
                    AssetPath = assetPath,
                    TextureWidth = texture.width,
                    TextureHeight = texture.height,
                    CellWidth = cellWidth,
                    CellHeight = cellHeight,
                    Padding = padding,
                    OffsetX = offsetX,
                    OffsetY = offsetY,
                    SpriteCount = sprites.Length,
                    SpriteNames = sprites.Select(x => x.name).ToList()
                };
            });
        }

        [McpPluginTool(TileAssetsCreateToolId, Title = "2D / Create Tile Assets")]
        [Description("Create or update Tile assets from all sprites contained in a sliced sprite-sheet texture. This is the missing bridge between sprite slicing and tilemap painting workflows.")]
        public static TileAssetsCreateResult CreateTileAssets(
            [Description("Sliced sprite-sheet texture asset path inside the Unity project.")]
            string spriteSheetAssetPath,
            [Description("Folder under Assets where Tile assets will be created. Example: Assets/Game/TileAssets/Grassland")]
            string outputFolder,
            [Description("Optional prefix added to each created Tile asset name.")]
            string tileNamePrefix = "",
            [Description("Whether to create the output folder recursively if it does not already exist.")]
            bool createFolderRecursively = true)
        {
            return MainThread.Instance.Run(() =>
            {
                ValidateAssetPath(spriteSheetAssetPath, ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff");
                ValidateFolderPath(outputFolder);
                EnsureFolder(outputFolder, createFolderRecursively);

                var sprites = AssetDatabase.LoadAllAssetsAtPath(spriteSheetAssetPath)
                    .OfType<Sprite>()
                    .OrderBy(x => x.name)
                    .ToArray();

                if (sprites.Length == 0)
                    throw new InvalidOperationException($"No sprites found at '{spriteSheetAssetPath}'. Slice the sprite sheet first.");

                var createdOrUpdated = new List<string>();
                foreach (var sprite in sprites)
                {
                    var safeName = SanitizeFileName(string.IsNullOrWhiteSpace(tileNamePrefix) ? sprite.name : $"{tileNamePrefix}{sprite.name}");
                    var tileAssetPath = $"{outputFolder}/{safeName}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);
                    if (existing == null)
                    {
                        var tile = ScriptableObject.CreateInstance<Tile>();
                        tile.sprite = sprite;
                        AssetDatabase.CreateAsset(tile, tileAssetPath);
                    }
                    else
                    {
                        existing.sprite = sprite;
                        EditorUtility.SetDirty(existing);
                    }
                    createdOrUpdated.Add(tileAssetPath);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return new TileAssetsCreateResult
                {
                    SpriteSheetAssetPath = spriteSheetAssetPath,
                    OutputFolder = outputFolder,
                    SpriteCount = sprites.Length,
                    TileAssetPaths = createdOrUpdated
                };
            });
        }

        private static void ValidateAssetPath(string assetPath, params string[] extensions)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Asset path cannot be null or empty.", nameof(assetPath));
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Asset path must start with 'Assets/'.", nameof(assetPath));
            if (extensions.Length > 0 && !extensions.Any(x => assetPath.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Asset path must end with one of: {string.Join(", ", extensions)}", nameof(assetPath));
        }

        private static void ValidateFolderPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Folder path cannot be null or empty.", nameof(assetPath));
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) && !string.Equals(assetPath, "Assets", StringComparison.Ordinal))
                throw new ArgumentException("Folder path must start with 'Assets/'.", nameof(assetPath));
        }

        private static void EnsureFolder(string folderPath, bool createFolderRecursively)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;
            if (!createFolderRecursively)
                throw new DirectoryNotFoundException($"Folder '{folderPath}' does not exist.");

            var normalized = folderPath.Replace('\\', '/');
            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static SpriteImportMode ParseSpriteImportMode(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "single" => SpriteImportMode.Single,
                "multiple" => SpriteImportMode.Multiple,
                _ => throw new ArgumentException("spriteMode must be Single or Multiple.", nameof(value))
            };
        }

        private static FilterMode ParseFilterMode(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "point" => FilterMode.Point,
                "bilinear" => FilterMode.Bilinear,
                "trilinear" => FilterMode.Trilinear,
                _ => throw new ArgumentException("filterMode must be Point, Bilinear, or Trilinear.", nameof(value))
            };
        }

        private static TextureWrapMode ParseWrapMode(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "clamp" => TextureWrapMode.Clamp,
                "repeat" => TextureWrapMode.Repeat,
                "mirror" => TextureWrapMode.Mirror,
                "mirroronce" => TextureWrapMode.MirrorOnce,
                _ => throw new ArgumentException("wrapMode must be Clamp, Repeat, Mirror, or MirrorOnce.", nameof(value))
            };
        }

        private static TextureImporterCompression ParseTextureCompression(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "uncompressed" => TextureImporterCompression.Uncompressed,
                "compressed" => TextureImporterCompression.Compressed,
                "compressedhq" => TextureImporterCompression.CompressedHQ,
                "compressedlq" => TextureImporterCompression.CompressedLQ,
                _ => throw new ArgumentException("compression must be Uncompressed, Compressed, CompressedHQ, or CompressedLQ.", nameof(value))
            };
        }

        private static Vector2 ParsePivot(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "center" => new Vector2(0.5f, 0.5f),
                "bottom" => new Vector2(0.5f, 0f),
                "bottomleft" => new Vector2(0f, 0f),
                "bottomright" => new Vector2(1f, 0f),
                "left" => new Vector2(0f, 0.5f),
                "right" => new Vector2(1f, 0.5f),
                "top" => new Vector2(0.5f, 1f),
                "topleft" => new Vector2(0f, 1f),
                "topright" => new Vector2(1f, 1f),
                _ => throw new ArgumentException("pivot must be Center, Bottom, BottomLeft, BottomRight, Left, Right, Top, TopLeft, or TopRight.", nameof(value))
            };
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');
            return fileName.Replace('/', '_').Replace('\\', '_');
        }

        public class SpriteImportSettingsResult
        {
            public string? AssetPath { get; set; }
            public int TextureWidth { get; set; }
            public int TextureHeight { get; set; }
            public string? SpriteMode { get; set; }
            public float PixelsPerUnit { get; set; }
            public string? FilterMode { get; set; }
            public string? WrapMode { get; set; }
            public string? Compression { get; set; }
            public bool AlphaIsTransparency { get; set; }
            public bool IsReadable { get; set; }
        }

        public class SpriteSliceResult
        {
            public string? AssetPath { get; set; }
            public int TextureWidth { get; set; }
            public int TextureHeight { get; set; }
            public int CellWidth { get; set; }
            public int CellHeight { get; set; }
            public int Padding { get; set; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public int SpriteCount { get; set; }
            public List<string>? SpriteNames { get; set; }
        }

        public class TileAssetsCreateResult
        {
            public string? SpriteSheetAssetPath { get; set; }
            public string? OutputFolder { get; set; }
            public int SpriteCount { get; set; }
            public List<string>? TileAssetPaths { get; set; }
        }
    }
}
