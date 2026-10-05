using System;
using System.IO;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Characters.Editor
{
    // Small code-generated pixel sprites, following the existing project's placeholder workflow.
    internal static class CharacterPlaceholderArt
    {
        private const string Folder = "Assets/Characters/Art";
        private static readonly Color32 Ink = Hex("242536"), Skin = Hex("efc49b"), Hair = Hex("35303c"), Gold = Hex("e7bb69");

        public static Sprite MapSprite(string file, string id) => Make(file + "-Map", 32, 48, new Vector2(.5f, 3f / 48), c => DrawMap(c, id));
        public static Sprite Portrait(string file, string id) => Make(file + "-Portrait", 64, 64, new Vector2(.5f, .5f), c => DrawPortrait(c, id));

        private static void Palette(string id, out Color32 coat, out Color32 accent)
        {
            switch (id)
            {
                case CharacterIds.HuanYujian: coat = Hex("363b60"); accent = Gold; break;
                case CharacterIds.LinXi: coat = Hex("418b92"); accent = Hex("b4ddd0"); break;
                case CharacterIds.Hydrologist: coat = Hex("dde5de"); accent = Hex("4e99b7"); break;
                case CharacterIds.Geologist: coat = Hex("dde5de"); accent = Hex("688d5d"); break;
                case CharacterIds.YangYinglong: coat = Hex("453743"); accent = Hex("b85651"); break;
                case CharacterIds.ContainmentResearcher: coat = Hex("65788c"); accent = Hex("89e3db"); break;
                case CharacterIds.Mechanic: coat = Hex("b38b4d"); accent = Hex("e5ca86"); break;
                default: coat = Hex("806779"); accent = Hex("d9b69b"); break;
            }
        }

        private static void DrawMap(Canvas c, string id)
        {
            Palette(id, out var coat, out var accent);
            bool female = id == CharacterIds.LinXi || id == CharacterIds.ContainmentResearcher || id == CharacterIds.TanYue;
            bool robe = id == CharacterIds.HuanYujian || id == CharacterIds.LinXi || id == CharacterIds.TanYue;
            bool broad = id == CharacterIds.YangYinglong;
            c.Rect(7, 1, 18, 3, Hex("252736"));
            c.Rect(10, 3, 5, 7, Ink); c.Rect(17, 3, 5, 7, Ink);
            c.Rect(8, 9, 16, 19, Ink); c.Rect(9, 10, 14, 17, coat);
            if (robe) { c.Rect(7, 8, 18, 8, Ink); c.Rect(8, 9, 16, 7, coat); }
            c.Rect(broad ? 4 : 6, 15, broad ? 24 : 20, 12, Ink);
            c.Rect(broad ? 5 : 7, 16, broad ? 22 : 18, 10, coat);
            c.Rect(5, 14, 4, 6, Skin); c.Rect(23, 14, 4, 6, Skin);
            c.Rect(11, 24, 10, 6, Ink); c.Rect(12, 25, 8, 5, Skin);
            c.Rect(9, 29, 14, 13, Ink); c.Rect(10, 30, 12, 11, Skin);
            c.Rect(8, 36, 16, 7, Hair); c.Rect(9, 34, 3, 6, Hair);
            if (female) { c.Rect(7, 28, 3, 11, Hair); c.Rect(22, 28, 3, 11, Hair); }
            c.Rect(12, 34, 2, 2, Ink); c.Rect(18, 34, 2, 2, Ink);
            c.Rect(15, 30, 3, 1, Hex("b3766d"));
            c.Rect(14, 11, 3, 15, accent); c.Rect(9, 15, 14, 2, accent);
            switch (id)
            {
                case CharacterIds.HuanYujian:
                    c.Rect(11, 42, 10, 3, Hair); c.Rect(15, 43, 2, 3, Gold);
                    c.Rect(18, 21, 3, 4, Gold); c.Rect(19, 22, 1, 2, Ink); break;
                case CharacterIds.LinXi:
                    c.Rect(14, 41, 4, 5, Hair); c.Rect(8, 40, 17, 2, accent);
                    c.Rect(10, 19, 12, 2, accent); c.Rect(11, 21, 2, 3, accent); break;
                case CharacterIds.Hydrologist:
                    Glasses(c, 10, 33); c.Rect(24, 10, 5, 9, Ink); c.Rect(25, 11, 3, 6, Hex("7bcbd8")); c.Rect(25, 18, 3, 2, accent); break;
                case CharacterIds.Geologist:
                    c.Rect(9, 40, 15, 3, accent); c.Rect(7, 39, 19, 2, accent);
                    c.Rect(23, 11, 5, 6, Hex("976b4c")); c.Rect(25, 16, 1, 10, accent);
                    c.Rect(23, 21, 3, 3, accent); c.Rect(26, 24, 3, 3, accent); break;
                case CharacterIds.YangYinglong:
                    c.Rect(4, 24, 7, 5, accent); c.Rect(21, 24, 7, 5, accent); c.Rect(10, 19, 12, 5, accent);
                    c.Rect(28, 5, 2, 31, Gold); c.Rect(27, 30, 4, 11, Hex("b9c8cb")); break;
                case CharacterIds.ContainmentResearcher:
                    c.Rect(23, 12, 6, 10, Ink); c.Rect(24, 13, 4, 8, accent); c.Rect(25, 17, 2, 2, coat);
                    c.Rect(19, 23, 3, 2, accent); break;
                case CharacterIds.Mechanic:
                    c.Rect(9, 38, 14, 3, accent); Glasses(c, 10, 38);
                    c.Rect(25, 9, 2, 14, Hex("bdc9c9")); c.Rect(23, 21, 6, 3, Hex("bdc9c9")); c.Rect(25, 23, 2, 2, Ink); break;
                case CharacterIds.TanYue:
                    c.Rect(10, 17, 12, 2, accent); c.Rect(20, 9, 7, 9, Hex("a6795b"));
                    c.Rect(3, 13, 7, 9, Ink); c.Rect(4, 14, 5, 7, Hex("e3d4ba")); c.Rect(4, 14, 1, 7, accent); break;
            }
        }

        private static void DrawPortrait(Canvas c, string id)
        {
            Palette(id, out var coat, out var accent);
            bool female = id == CharacterIds.LinXi || id == CharacterIds.ContainmentResearcher || id == CharacterIds.TanYue;
            c.Rect(10, 0, 44, 22, Ink); c.Rect(12, 0, 40, 20, coat);
            c.Rect(25, 17, 14, 12, Ink); c.Rect(27, 19, 10, 10, Skin);
            if (female) { c.Rect(16, 20, 8, 29, Hair); c.Rect(40, 20, 8, 29, Hair); }
            c.Rect(18, 28, 28, 28, Ink); c.Rect(20, 30, 24, 24, Skin);
            c.Rect(17, 48, 30, 10, Hair); c.Rect(18, 43, 6, 10, Hair);
            c.Rect(24, 40, 4, 4, Ink); c.Rect(36, 40, 4, 4, Ink);
            c.Rect(29, 33, 6, 2, Hex("b3766d"));
            c.Rect(28, 0, 8, 18, accent); c.Rect(16, 8, 32, 4, accent);
            switch (id)
            {
                case CharacterIds.HuanYujian: c.Rect(24, 57, 16, 4, Hair); c.Rect(30, 58, 4, 6, Gold); c.Rect(42, 13, 5, 6, Gold); break;
                case CharacterIds.LinXi: c.Rect(28, 57, 8, 6, Hair); c.Rect(16, 54, 32, 3, accent); c.Rect(20, 12, 24, 3, accent); break;
                case CharacterIds.Hydrologist: Glasses(c, 22, 38, 2); c.Rect(48, 3, 8, 12, Hex("7bcbd8")); break;
                case CharacterIds.Geologist: c.Rect(18, 55, 28, 5, accent); c.Rect(13, 53, 38, 4, accent); c.Rect(49, 0, 2, 16, accent); c.Rect(44, 9, 5, 5, accent); break;
                case CharacterIds.YangYinglong: c.Rect(9, 13, 13, 9, accent); c.Rect(42, 13, 13, 9, accent); c.Rect(52, 0, 3, 28, Hex("b9c8cb")); break;
                case CharacterIds.ContainmentResearcher: c.Rect(44, 2, 13, 16, Ink); c.Rect(46, 4, 9, 12, accent); break;
                case CharacterIds.Mechanic: c.Rect(18, 48, 28, 6, accent); Glasses(c, 22, 47, 2); c.Rect(50, 1, 3, 20, Hex("bdc9c9")); break;
                case CharacterIds.TanYue: c.Rect(8, 0, 14, 16, Hex("e3d4ba")); c.Rect(8, 0, 3, 16, accent); break;
            }
        }

        private static void Glasses(Canvas c, int x, int y, int scale = 1)
        {
            c.Rect(x, y, 5 * scale, 4 * scale, Ink); c.Rect(x + 7 * scale, y, 5 * scale, 4 * scale, Ink);
            c.Rect(x + 4 * scale, y + 2 * scale, 4 * scale, scale, Ink);
            c.Rect(x + scale, y + scale, 3 * scale, 2 * scale, Hex("9fc3c9"));
            c.Rect(x + 8 * scale, y + scale, 3 * scale, 2 * scale, Hex("9fc3c9"));
        }

        private static Sprite Make(string file, int width, int height, Vector2 pivot, Action<Canvas> draw)
        {
            PropAssetFactory.EnsureFolder(Folder);
            string path = Folder + "/" + file + ".png";
            if (!File.Exists(path))
            {
                var canvas = new Canvas(width, height); draw(canvas);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try { texture.SetPixels32(canvas.pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 32;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color32 Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color); return color;
        }

        private sealed class Canvas
        {
            private readonly int width, height;
            public readonly Color32[] pixels;
            public Canvas(int w, int h) { width = w; height = h; pixels = new Color32[w * h]; }
            public void Rect(int x, int y, int w, int h, Color32 color)
            {
                for (int row = Math.Max(0, y); row < Math.Min(height, y + h); row++)
                    for (int column = Math.Max(0, x); column < Math.Min(width, x + w); column++) pixels[column + row * width] = color;
            }
        }
    }
}
