using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Emerge.Battle.Editor
{
    public static class BattlePlaceholderAssets
    {
        private const string Folder = "Assets/Battle/Art/";
        private static readonly Color Ink = new Color(.07f, .11f, .17f), Gold = new Color(.87f, .69f, .35f);
        public static void Install(BattleCatalog catalog)
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            const string path = "Assets/Resources/Battle/BattlePresentation.asset";
            var art = AssetDatabase.LoadAssetAtPath<BattlePresentation>(path);
            if (art == null) { art = ScriptableObject.CreateInstance<BattlePresentation>(); AssetDatabase.CreateAsset(art, path); }
            if (art.background == null) art.background = Sprite("Arena", 1600, 610, Arena);
            if (art.heroPortrait == null) art.heroPortrait = Sprite("Hero", 256, 384, c => Person(c, new Color(.25f, .46f, .57f), false, 0));
            if (art.defaultEnemyPortrait == null) art.defaultEnemyPortrait = Sprite("Shadow", 256, 384, c => Person(c, new Color(.61f, .32f, .37f), true, 0));
            foreach (var enemy in catalog.enemies)
            {
                if (enemy.battlePortrait != null) continue;
                string id = enemy.id; int type = id == "E02" ? 1 : id == "E03" ? 2 : id == "B01" ? 3 : 0;
                enemy.battlePortrait = type == 0 ? art.defaultEnemyPortrait : Sprite("Enemy-" + id, 256, 384, c => Person(c, type == 1 ? new Color(.38f, .49f, .43f) : type == 2 ? new Color(.48f, .36f, .61f) : new Color(.6f, .35f, .27f), true, type));
                EditorUtility.SetDirty(enemy);
            }
            if (art.skillIcon == null) art.skillIcon = Sprite("Icon-Skills", 64, 64, c => { c.Line(12, 12, 52, 52, 5, Gold); c.Line(52, 12, 12, 52, 5, Gold); c.Line(10, 24, 24, 10, 4, Gold); c.Line(40, 10, 54, 24, 4, Gold); });
            if (art.bagIcon == null) art.bagIcon = Sprite("Icon-Bag", 64, 64, c => { c.Rect(14, 12, 36, 38, Gold); c.Rect(23, 48, 18, 8, Gold); c.Rect(20, 18, 24, 14, Ink); c.Rect(29, 33, 6, 8, Ink); });
            if (art.talkIcon == null) art.talkIcon = Sprite("Icon-Talk", 64, 64, c => { c.Rect(8, 22, 48, 32, Gold); c.Triangle(new Vector2(12, 24), new Vector2(12, 9), new Vector2(30, 24), Gold); for (int i = 0; i < 3; i++) c.Circle(20 + i * 12, 38, 3, Ink); });
            if (art.fleeIcon == null) art.fleeIcon = Sprite("Icon-Escape", 64, 64, c => { c.Rect(10, 9, 24, 46, Gold); c.Rect(16, 14, 14, 36, Ink); c.Line(27, 32, 56, 32, 4, Gold); c.Line(45, 21, 56, 32, 4, Gold); c.Line(45, 43, 56, 32, 4, Gold); });
            if (art.itemIcon == null) art.itemIcon = Sprite("Icon-Item", 64, 64, c => { c.Rect(14, 10, 36, 42, new Color(.3f, .59f, .62f)); c.Rect(23, 50, 18, 7, Gold); c.Rect(29, 19, 6, 24, Color.white); c.Rect(20, 28, 24, 6, Color.white); });
            if (art.coinFront == null) art.coinFront = Sprite("Coin-Front", 96, 96, c => Coin(c, false));
            if (art.coinBack == null) art.coinBack = Sprite("Coin-Back", 96, 96, c => Coin(c, true));
            if (art.attackEffect == null) art.attackEffect = Sprite("FX-Slash", 128, 128, c => { c.Line(18, 16, 108, 108, 9, Color.white); c.Line(40, 12, 115, 86, 4, Color.white); c.Line(12, 40, 86, 115, 4, Color.white); });
            if (art.healingEffect == null) art.healingEffect = Sprite("FX-Heal", 128, 128, c => { c.Rect(56, 25, 16, 78, Color.white); c.Rect(25, 56, 78, 16, Color.white); c.Ring(64, 64, 57, 2, Color.white); });
            if (art.shieldEffect == null) art.shieldEffect = Sprite("FX-Shield", 128, 128, c => { c.Line(24, 96, 64, 112, 6, Color.white); c.Line(64, 112, 104, 96, 6, Color.white); c.Line(24, 96, 30, 48, 6, Color.white); c.Line(104, 96, 98, 48, 6, Color.white); c.Line(30, 48, 64, 16, 6, Color.white); c.Line(98, 48, 64, 16, 6, Color.white); });
            EditorUtility.SetDirty(art); AssetDatabase.SaveAssets();
        }
        private static void Coin(Canvas c, bool back)
        { c.Circle(48, 48, 45, Gold); c.Circle(48, 48, 39, new Color(.52f, .36f, .16f)); c.Circle(48, 48, 35, Gold); c.Rect(35, 35, 26, 26, Ink); if (back) { for (int i = 0; i < 4; i++) c.Rect(18 + i * 15, 70, 8, 5, Ink); } else { c.Rect(43, 68, 10, 14, Ink); c.Rect(14, 43, 14, 10, Ink); c.Rect(68, 43, 14, 10, Ink); c.Rect(43, 14, 10, 14, Ink); } }
        private static void Person(Canvas c, Color color, bool enemy, int type)
        {
            c.Circle(128, 329, type == 3 ? 38 : 28, color);
            c.Triangle(new Vector2(94, 295), new Vector2(36, 61), new Vector2(197, 61), color * .65f);
            c.Rect(88, 174, 80, 120, color); c.Line(99, 180, 88, 25, 25, color); c.Line(153, 180, 167, 25, 25, color);
            c.Line(89, 270, 52, 176, 20, color); c.Line(165, 270, 202, 176, 20, color);
            c.Rect(98, 173, 60, 10, Gold); c.Rect(116, 286, 24, 8, Gold);
            if (!enemy) { c.Line(201, 119, 201, 300, 7, new Color(.7f, .82f, .85f)); c.Line(183, 186, 219, 186, 8, Gold); c.Rect(117, 329, 24, 7, Ink); }
            else { c.Rect(112, 330, 10, 5, Gold); c.Rect(135, 330, 10, 5, Gold); }
            if (type == 1) { c.Circle(126, 236, 58, color * .6f); c.Ring(126, 236, 56, 5, Gold); }
            if (type == 2) { c.Circle(128, 333, 20, color); c.Ring(128, 333, 47, 3, Gold); c.Line(203, 120, 214, 320, 6, Gold); c.Circle(214, 320, 15, color); }
            if (type == 3) { c.Triangle(new Vector2(94, 340), new Vector2(82, 378), new Vector2(110, 355), Gold); c.Triangle(new Vector2(146, 355), new Vector2(174, 378), new Vector2(162, 340), Gold); c.Line(56, 195, 14, 267, 18, color); c.Line(199, 195, 242, 267, 18, color); }
        }
        private static void Arena(Canvas c)
        {
            for (int y = 0; y < c.h; y++) for (int x = 0; x < c.w; x++) c.Set(x, y, Color.Lerp(new Color(.14f, .21f, .28f), new Color(.055f, .09f, .14f), (float)y / c.h));
            var line = new Color(.23f, .3f, .35f);
            for (int y = 20; y < 200; y += 45) c.Line(0, y, 1600, y, 2, line);
            for (int x = -800; x < 2400; x += 250) c.Line(x, 0, 800 + (x - 800) / 3, 210, 2, line);
            c.Line(0, 210, 1600, 210, 3, line);
            foreach (int x in new[] { 80, 560, 980, 1450 }) { c.Rect(x, 215, 22, 330, new Color(.10f, .15f, .20f)); c.Rect(x - 12, 210, 46, 12, line); }
            c.Ring(785, 370, 85, 2, new Color(.2f, .26f, .29f));
        }
        private static Sprite Sprite(string name, int w, int h, Action<Canvas> draw)
        {
            string path = Folder + name + ".png";
            if (!File.Exists(path)) { var c = new Canvas(w, h); draw(c); var texture = new Texture2D(w, h, TextureFormat.RGBA32, false); texture.SetPixels(c.pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit = 100; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private sealed class Canvas
        {
            public readonly int w, h; public readonly Color[] pixels;
            public Canvas(int width, int height) { w = width; h = height; pixels = new Color[w * h]; }
            public void Set(int x, int y, Color color) { if (x >= 0 && y >= 0 && x < w && y < h) { color.a = color == Color.clear ? 0 : 1; pixels[x + y * w] = color; } }
            public void Rect(int x, int y, int width, int height, Color color) { for (int i = x; i < x + width; i++) for (int j = y; j < y + height; j++) Set(i, j, color); }
            public void Circle(int x, int y, int r, Color color) { for (int i = x - r; i <= x + r; i++) for (int j = y - r; j <= y + r; j++) if ((i - x) * (i - x) + (j - y) * (j - y) <= r * r) Set(i, j, color); }
            public void Ring(int x, int y, int r, int thickness, Color color) { for (int i = x - r; i <= x + r; i++) for (int j = y - r; j <= y + r; j++) { int d = (i - x) * (i - x) + (j - y) * (j - y); if (d <= r * r && d >= (r - thickness) * (r - thickness)) Set(i, j, color); } }
            public void Line(int x1, int y1, int x2, int y2, int thickness, Color color) { int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1)); for (int i = 0; i <= steps; i++) { float t = steps == 0 ? 0 : (float)i / steps; Circle(Mathf.RoundToInt(Mathf.Lerp(x1, x2, t)), Mathf.RoundToInt(Mathf.Lerp(y1, y2, t)), thickness / 2, color); } }
            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
            { for (int x = (int)Mathf.Min(a.x, b.x, c.x); x <= Mathf.Max(a.x, b.x, c.x); x++) for (int y = (int)Mathf.Min(a.y, b.y, c.y); y <= Mathf.Max(a.y, b.y, c.y); y++) { var p = new Vector2(x, y); float d1 = Cross(p - a, b - a), d2 = Cross(p - b, c - b), d3 = Cross(p - c, a - c); if (!(d1 < 0 || d2 < 0 || d3 < 0) || !(d1 > 0 || d2 > 0 || d3 > 0)) Set(x, y, color); } }
            private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        }
    }
}
