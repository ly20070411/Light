using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Emerge.PixelMap.Editor
{
    [InitializeOnLoad]
    internal static class PixelMapAssetFactory
    {
        internal const string RootFolder = "Assets/PixelMap";
        internal const string LibraryFolder = RootFolder + "/Library";
        internal const string DefinitionFolder = LibraryFolder + "/Definitions";
        internal const string TextureFolder = RootFolder + "/DefaultTextures";
        internal const string DefaultLibraryPath = LibraryFolder + "/DefaultBlockLibrary.asset";

        private struct DefaultBlock
        {
            public string Key;
            public string DisplayName;
            public Color32 Primary;
            public Color32 Secondary;
            public BlockColliderShape2D Collider;

            public DefaultBlock(string key, string displayName, Color32 primary, Color32 secondary,
                BlockColliderShape2D collider)
            {
                Key = key;
                DisplayName = displayName;
                Primary = primary;
                Secondary = secondary;
                Collider = collider;
            }
        }

        private static readonly DefaultBlock[] Defaults =
        {
            new DefaultBlock("Grass", "草地方块", new Color32(74, 160, 67, 255), new Color32(46, 112, 52, 255), BlockColliderShape2D.Box),
            new DefaultBlock("Stone", "石头方块", new Color32(126, 132, 139, 255), new Color32(88, 94, 103, 255), BlockColliderShape2D.Box),
            new DefaultBlock("Brick", "砖块", new Color32(173, 82, 63, 255), new Color32(105, 48, 45, 255), BlockColliderShape2D.Box),
            new DefaultBlock("Water", "水面", new Color32(58, 139, 201, 220), new Color32(103, 194, 229, 220), BlockColliderShape2D.None)
        };

        static PixelMapAssetFactory()
        {
            EditorApplication.update += InitializeOnNextEditorUpdate;
        }

        private static void InitializeOnNextEditorUpdate()
        {
            EditorApplication.update -= InitializeOnNextEditorUpdate;
            EnsureDefaultLibrary();
        }

        internal static MapBlockLibrary EnsureDefaultLibrary()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(LibraryFolder);
            EnsureFolder(DefinitionFolder);
            EnsureFolder(TextureFolder);

            var library = AssetDatabase.LoadAssetAtPath<MapBlockLibrary>(DefaultLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MapBlockLibrary>();
                AssetDatabase.CreateAsset(library, DefaultLibraryPath);
            }

            foreach (var item in Defaults)
            {
                var definitionPath = DefinitionFolder + "/" + item.Key + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<MapBlockDefinition>(definitionPath);
                if (definition == null)
                {
                    var sprite = EnsurePixelSprite(item);
                    definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
                    definition.ConfigureDefaults(item.DisplayName, sprite, Color.white, item.Collider);
                    AssetDatabase.CreateAsset(definition, definitionPath);
                }

                if (library.Add(definition)) EditorUtility.SetDirty(library);
            }

            AssetDatabase.SaveAssets();
            return library;
        }

        internal static IReadOnlyList<MapBlockDefinition> ImportObjects(MapBlockLibrary library,
            UnityEngine.Object[] selectedObjects)
        {
            var imported = new List<MapBlockDefinition>();
            if (library == null || selectedObjects == null) return imported;

            foreach (var selected in selectedObjects)
            {
                if (selected == null || selected is MapBlockDefinition) continue;

                Sprite sprite = selected as Sprite;
                GameObject prefab = selected as GameObject;
                Material material = selected as Material;

                if (selected is Texture2D texture)
                {
                    sprite = ConvertTextureToSprite(texture);
                }

                if (prefab != null && sprite == null)
                {
                    var renderer = prefab.GetComponentInChildren<SpriteRenderer>();
                    if (renderer != null) sprite = renderer.sprite;
                }

                if (sprite == null && prefab == null && material == null) continue;

                string assetName = SanitizeFileName(selected.name);
                string path = AssetDatabase.GenerateUniqueAssetPath(DefinitionFolder + "/" + assetName + ".asset");
                var definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
                definition.ConfigureImported(selected.name, sprite, prefab, material);
                AssetDatabase.CreateAsset(definition, path);
                library.Add(definition);
                imported.Add(definition);
            }

            if (imported.Count > 0)
            {
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }
            return imported;
        }

        internal static MapBlockDefinition CreateBlankDefinition(MapBlockLibrary library)
        {
            EnsureDefaultLibrary();
            var definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
            definition.ConfigureImported("新方块", null, null, null);
            string path = AssetDatabase.GenerateUniqueAssetPath(DefinitionFolder + "/NewBlock.asset");
            AssetDatabase.CreateAsset(definition, path);
            library.Add(definition);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static Sprite EnsurePixelSprite(DefaultBlock item)
        {
            string texturePath = TextureFolder + "/" + item.Key + ".png";
            if (!File.Exists(Path.GetFullPath(texturePath)))
            {
                const int size = 16;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool accent;
                        if (item.Key == "Brick") accent = y % 5 == 0 || (x + (y / 5) * 4) % 8 == 0;
                        else if (item.Key == "Water") accent = (y % 5 == 1 && (x + y) % 4 < 2);
                        else accent = ((x * 7 + y * 11 + x * y) % 13) < 3;
                        pixels[y * size + x] = accent ? item.Secondary : item.Primary;
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(Path.GetFullPath(texturePath), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            }

            ConfigureTextureImporter(texturePath);
            return AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault();
        }

        private static Sprite ConvertTextureToSprite(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(path)) return null;
            ConfigureTextureImporter(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static void ConfigureTextureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.filterMode != FilterMode.Point || importer.mipmapEnabled;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (changed) importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "ImportedBlock" : value;
        }
    }
}
