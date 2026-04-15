#nullable enable
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Unity.MCP.Editor.API;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    [TestFixture]
    public class Tool2DWorkflowTests : BaseTest
    {
        const string TestRootFolder = "Assets/Unity-MCP-Test/2DTools";
        const string TexturesFolder = TestRootFolder + "/Textures";
        const string TilesFolder = TestRootFolder + "/Tiles";

        [UnitySetUp]
        public override IEnumerator SetUp()
        {
            yield return base.SetUp();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DeleteTestRootIfExists();
            EnsureFolder("Assets/Unity-MCP-Test");
            EnsureFolder(TestRootFolder);
            EnsureFolder(TexturesFolder);
        }

        [UnityTearDown]
        public override IEnumerator TearDown()
        {
            DeleteTestRootIfExists();
            yield return base.TearDown();
        }

        [UnityTest]
        public IEnumerator SpriteImportConfigure_ShouldConfigureTextureImporterFor2D()
        {
            var texturePath = CreateTextureAsset("import-sheet.png", 32, 32);

            var json = $@"{{
                ""assetPath"": ""{texturePath}"",
                ""spriteMode"": ""Multiple"",
                ""pixelsPerUnit"": 32,
                ""filterMode"": ""Point"",
                ""wrapMode"": ""Clamp"",
                ""compression"": ""Uncompressed"",
                ""alphaIsTransparency"": true,
                ""isReadable"": true
            }}";

            yield return RunToolMainThreadCoop(Tool_2D_Sprites.SpriteImportConfigureToolId, json);

            var result = RunTool(Tool_2D_Sprites.SpriteImportConfigureToolId, json);
            using var doc = JsonDocument.Parse(result.Value!.GetMessage()!);
            var root = doc.RootElement.GetProperty("result");

            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.AreEqual(32f, importer.spritePixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.IsTrue(importer.isReadable);

            Assert.AreEqual(texturePath, root.GetProperty("AssetPath").GetString());
            Assert.AreEqual("Multiple", root.GetProperty("SpriteMode").GetString());
            Assert.AreEqual(32, root.GetProperty("PixelsPerUnit").GetInt32());
        }

        [UnityTest]
        public IEnumerator SpriteSliceGrid_ShouldCreateExpectedSprites()
        {
            var texturePath = CreateTextureAsset("slice-sheet.png", 32, 32);
            ConfigureSpriteSheet(texturePath);

            var json = $@"{{
                ""assetPath"": ""{texturePath}"",
                ""cellWidth"": 16,
                ""cellHeight"": 16,
                ""padding"": 0,
                ""offsetX"": 0,
                ""offsetY"": 0,
                ""pivot"": ""Center""
            }}";

            var result = RunTool(Tool_2D_Sprites.SpriteSliceGridToolId, json);
            using var doc = JsonDocument.Parse(result.Value!.GetMessage()!);
            var root = doc.RootElement.GetProperty("result");

            var sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().OrderBy(x => x.name).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "slice-sheet_0_0", "slice-sheet_0_1", "slice-sheet_1_0", "slice-sheet_1_1" },
                sprites.Select(x => x.name).ToArray());

            Assert.AreEqual(4, sprites.Length);
            Assert.AreEqual(4, root.GetProperty("SpriteCount").GetInt32());
            Assert.AreEqual(16, root.GetProperty("CellWidth").GetInt32());
            Assert.AreEqual(16, root.GetProperty("CellHeight").GetInt32());
            yield return null;
        }

        [UnityTest]
        public IEnumerator TileAssetsCreate_ShouldCreateTileAssetsForAllSprites()
        {
            var texturePath = CreateTextureAsset("tiles-sheet.png", 32, 32);
            ConfigureSpriteSheet(texturePath);
            SliceSpriteSheet(texturePath);

            var json = $@"{{
                ""spriteSheetAssetPath"": ""{texturePath}"",
                ""outputFolder"": ""{TilesFolder}"",
                ""tileNamePrefix"": ""tile_"",
                ""createFolderRecursively"": true
            }}";

            var result = RunTool(Tool_2D_Sprites.TileAssetsCreateToolId, json);
            using var doc = JsonDocument.Parse(result.Value!.GetMessage()!);
            var root = doc.RootElement.GetProperty("result");

            var tilePaths = new[]
            {
                $"{TilesFolder}/tile_tiles-sheet_0_0.asset",
                $"{TilesFolder}/tile_tiles-sheet_0_1.asset",
                $"{TilesFolder}/tile_tiles-sheet_1_0.asset",
                $"{TilesFolder}/tile_tiles-sheet_1_1.asset"
            };

            foreach (var tilePath in tilePaths)
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                Assert.IsNotNull(tile, $"Expected tile asset at {tilePath}");
                Assert.IsNotNull(tile!.sprite, $"Tile asset {tilePath} should reference a sprite");
            }

            Assert.AreEqual(TilesFolder, root.GetProperty("OutputFolder").GetString());
            Assert.AreEqual(4, root.GetProperty("SpriteCount").GetInt32());
            Assert.AreEqual(4, root.GetProperty("TileAssetPaths").EnumerateArray().Count());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tilemap2DCreate_ShouldCreateGridAndTilemapInActiveScene()
        {
            var json = @"{
                ""gridName"": ""TestGrid"",
                ""tilemapName"": ""Ground"",
                ""layout"": ""Isometric"",
                ""sortingLayerName"": ""Default"",
                ""orderInLayer"": 5,
                ""addTilemapCollider2D"": true
            }";

            var result = RunTool(Tool_2D_Tilemap.Tilemap2DCreateToolId, json);
            using var doc = JsonDocument.Parse(result.Value!.GetMessage()!);
            var root = doc.RootElement.GetProperty("result");

            var gridGo = GameObject.Find("TestGrid");
            Assert.IsNotNull(gridGo, "Grid GameObject should be created");
            var grid = gridGo!.GetComponent<Grid>();
            Assert.IsNotNull(grid, "Grid component should be added");
            Assert.AreEqual(GridLayout.CellLayout.Isometric, grid!.cellLayout);

            var tilemapGo = GameObject.Find("Ground");
            Assert.IsNotNull(tilemapGo, "Tilemap GameObject should be created");
            Assert.AreEqual(gridGo.transform, tilemapGo!.transform.parent);
            Assert.IsNotNull(tilemapGo.GetComponent<Tilemap>());
            Assert.IsNotNull(tilemapGo.GetComponent<TilemapRenderer>());
            Assert.IsNotNull(tilemapGo.GetComponent<TilemapCollider2D>());
            Assert.AreEqual(5, tilemapGo.GetComponent<TilemapRenderer>()!.sortingOrder);

            Assert.AreEqual("TestGrid", root.GetProperty("GridName").GetString());
            Assert.AreEqual("Ground", root.GetProperty("TilemapName").GetString());
            Assert.AreEqual("Isometric", root.GetProperty("Layout").GetString());
            Assert.IsTrue(root.GetProperty("AddedTilemapCollider2D").GetBoolean());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tilemap2DPaint_ShouldPaintTilesIntoNamedTilemap()
        {
            var texturePath = CreateTextureAsset("paint-sheet.png", 32, 32);
            ConfigureSpriteSheet(texturePath);
            SliceSpriteSheet(texturePath);
            CreateTileAssets(texturePath, "paint_");
            CreateTilemap("PaintGrid", "PaintMap", "Rectangular", 0, false);

            var firstTilePath = $"{TilesFolder}/paint_paint-sheet_0_0.asset";
            var secondTilePath = $"{TilesFolder}/paint_paint-sheet_0_1.asset";
            var json = $@"{{
                ""tilemapName"": ""PaintMap"",
                ""cells"": [
                    {{ ""x"": 0, ""y"": 0, ""z"": 0, ""tileAssetPath"": ""{firstTilePath}"" }},
                    {{ ""x"": 1, ""y"": 0, ""z"": 0, ""tileAssetPath"": ""{secondTilePath}"" }}
                ]
            }}";

            var result = RunTool(Tool_2D_Tilemap.Tilemap2DPaintToolId, json);
            using var doc = JsonDocument.Parse(result.Value!.GetMessage()!);
            var root = doc.RootElement.GetProperty("result");

            var tilemap = GameObject.Find("PaintMap")!.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap);

            var firstTile = AssetDatabase.LoadAssetAtPath<TileBase>(firstTilePath);
            var secondTile = AssetDatabase.LoadAssetAtPath<TileBase>(secondTilePath);
            Assert.AreEqual(firstTile, tilemap!.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(secondTile, tilemap.GetTile(new Vector3Int(1, 0, 0)));

            Assert.AreEqual("PaintMap", root.GetProperty("TilemapName").GetString());
            Assert.AreEqual(2, root.GetProperty("PaintedCount").GetInt32());
            Assert.AreEqual(2, root.GetProperty("PaintedCells").EnumerateArray().Count());
            yield return null;
        }

        string CreateTextureAsset(string fileName, int width, int height)
        {
            EnsureFolder(TexturesFolder);
            var assetPath = $"{TexturesFolder}/{fileName}";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Range(0, width * height)
                .Select(i => (i % 2 == 0) ? Color.white : new Color(0.2f, 0.8f, 0.3f, 1f))
                .ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return assetPath;
        }

        void ConfigureSpriteSheet(string texturePath)
        {
            var json = $@"{{
                ""assetPath"": ""{texturePath}"",
                ""spriteMode"": ""Multiple"",
                ""pixelsPerUnit"": 16,
                ""filterMode"": ""Point"",
                ""wrapMode"": ""Clamp"",
                ""compression"": ""Uncompressed"",
                ""alphaIsTransparency"": true,
                ""isReadable"": true
            }}";
            RunTool(Tool_2D_Sprites.SpriteImportConfigureToolId, json);
        }

        void SliceSpriteSheet(string texturePath)
        {
            var json = $@"{{
                ""assetPath"": ""{texturePath}"",
                ""cellWidth"": 16,
                ""cellHeight"": 16,
                ""padding"": 0,
                ""offsetX"": 0,
                ""offsetY"": 0,
                ""pivot"": ""Center""
            }}";
            RunTool(Tool_2D_Sprites.SpriteSliceGridToolId, json);
        }

        void CreateTileAssets(string texturePath, string prefix)
        {
            var json = $@"{{
                ""spriteSheetAssetPath"": ""{texturePath}"",
                ""outputFolder"": ""{TilesFolder}"",
                ""tileNamePrefix"": ""{prefix}"",
                ""createFolderRecursively"": true
            }}";
            RunTool(Tool_2D_Sprites.TileAssetsCreateToolId, json);
        }

        void CreateTilemap(string gridName, string tilemapName, string layout, int orderInLayer, bool addCollider)
        {
            var json = $@"{{
                ""gridName"": ""{gridName}"",
                ""tilemapName"": ""{tilemapName}"",
                ""layout"": ""{layout}"",
                ""sortingLayerName"": ""Default"",
                ""orderInLayer"": {orderInLayer},
                ""addTilemapCollider2D"": {(addCollider ? "true" : "false")}
            }}";
            RunTool(Tool_2D_Tilemap.Tilemap2DCreateToolId, json);
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void DeleteTestRootIfExists()
        {
            if (AssetDatabase.IsValidFolder(TestRootFolder))
            {
                AssetDatabase.DeleteAsset(TestRootFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
