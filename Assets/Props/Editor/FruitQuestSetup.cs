using System.Collections.Generic;
using System.IO;
using Emerge.PixelMap;
using Emerge.PixelMap.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Props.Editor
{
    public static class FruitQuestSetup
    {
        public const string FruitPath = "Assets/Props/Library/Definitions/Fruit.asset";
        public const string GuidePath = "Assets/Props/Library/Definitions/Guide.asset";
        public const string FruitArtPath = "Assets/Props/Art/Fruit.png";

        [MenuItem("Tools/道具系统/创建果实与向导交付示例")]
        public static void Install()
        {
            var library = PropAssetFactory.EnsureDefaultLibrary();
            var fruit = AssetDatabase.LoadAssetAtPath<PropDefinition>(FruitPath);
            if (fruit == null)
            {
                fruit = ScriptableObject.CreateInstance<PropDefinition>();
                fruit.displayName = "果实"; fruit.category = "任务物品";
                fruit.description = "鲜红的果实。拾取后放入背包，可交给向导；也可以选择暂时保留。";
                fruit.sprite = MakeFruitSprite(); fruit.worldSize = new Vector2(.85f, .85f);
                fruit.actions = PropActions.Pickup; fruit.inventoryKey = "fruit"; fruit.pickupAmount = 1;
                fruit.isSolid = false; fruit.singleUse = true; fruit.hideAfterPickup = true;
                fruit.interactionLabel = "拾取"; fruit.interactionRange = 1.5f; fruit.allowRotation = false;
                AssetDatabase.CreateAsset(fruit, FruitPath);
            }
            var guide = AssetDatabase.LoadAssetAtPath<PropDefinition>(GuidePath);
            if (guide == null) throw new System.InvalidOperationException("默认向导资产未创建。");
            if (guide.itemHandover == null || !guide.itemHandover.enabled)
            {
                Undo.RecordObject(guide, "配置向导果实交付对话");
                guide.description = "需要一颗果实的向导。对白随背包、交给或不给的选择，以及交付完成状态变化。";
                guide.actions |= PropActions.Dialogue; guide.singleUse = false;
                guide.interactionLabel = "交谈";
                guide.itemHandover = new PropItemHandover
                {
                    enabled = true, itemKey = "fruit", amount = 1, completionFlag = "guide_fruit_given",
                    acceptLabel = "交给果实", declineLabel = "不给",
                    offerDialogue = Lines("你找到果实了！能分给我一颗吗？我已经很久没吃东西了。"),
                    acceptedDialogue = Lines("谢谢你，这颗果实正好能让我恢复体力。", "有了这份补给，我可以继续为过路的人指路了。你也记得照顾好自己。"),
                    declinedDialogue = Lines("没关系，果实先留给你自己吧。改变主意时再来找我。"),
                    missingItemDialogue = Lines("我有些饿了。附近似乎掉落了一颗果实，找到后可以带给我吗？"),
                    completedDialogue = Lines("多亏了你带来的果实，我现在精神多了。谢谢你！")
                };
                EditorUtility.SetDirty(guide);
            }
            if (library.Add(fruit)) EditorUtility.SetDirty(library);
            if (library.Add(guide)) EditorUtility.SetDirty(library);
            var mapLibrary = PixelMapAssetFactory.EnsureDefaultLibrary();
            if (mapLibrary.PropLibrary == null) { mapLibrary.SetPropLibrary(library); EditorUtility.SetDirty(mapLibrary); }
            else if (mapLibrary.PropLibrary != library)
            {
                mapLibrary.PropLibrary.Add(fruit); mapLibrary.PropLibrary.Add(guide); EditorUtility.SetDirty(mapLibrary.PropLibrary);
            }
            AssetDatabase.SaveAssets();
            // Save existing edits before loading the demo; never regenerate the user's room.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
            var demo = SceneManager.GetActiveScene();
            if (demo.path != PropBootstrap.DemoScenePath) demo = EditorSceneManager.OpenScene(PropBootstrap.DemoScenePath);
            PropBootstrap.InstallPlayer(demo);
            PixelMapRoot mapRoot = null;
            PropInstance guideInstance = null, fruitInstance = null;
            foreach (var prop in Object.FindObjectsOfType<PropInstance>(true))
            {
                if (prop.gameObject.scene != demo) continue;
                if (prop.Definition == guide) { guideInstance = prop; mapRoot = prop.GetComponentInParent<PixelMapRoot>(); }
                if (prop.Definition == fruit) fruitInstance = prop;
            }
            if (mapRoot == null)
            { mapRoot = new GameObject("果实交付示例").AddComponent<PixelMapRoot>(); mapRoot.Configure(mapLibrary); }
            Vector3 origin = new Vector3(-5f, -3f, 0);
            foreach (var player in Object.FindObjectsOfType<PlayerInteractor>())
                if (player.gameObject.scene == demo) { origin = player.transform.position; break; }
            if (fruitInstance == null)
                PropPlacementService.Place(fruit, mapRoot.transform, origin + Vector3.down, 0, MapPlacementMode.Grid, mapLibrary.GridSize);
            if (guideInstance == null)
                PropPlacementService.Place(guide, mapRoot.transform, origin + new Vector3(4, 0, 0), 0, MapPlacementMode.Grid, mapLibrary.GridSize);
            PropDefinitionEditor.ApplyToScene(guide);
            EditorSceneManager.SaveScene(demo); AssetDatabase.SaveAssets();
            ShowEditors();
            Debug.Log("FRUIT_QUEST_SETUP_OK: 果实、向导分支、背包与两个编辑器已关联。");
        }
        private static List<PropDialogueLine> Lines(params string[] text)
        {
            var lines = new List<PropDialogueLine>();
            foreach (string value in text) lines.Add(new PropDialogueLine { speaker = "向导", text = value });
            return lines;
        }
        public static void ShowEditors()
        {
            var fruit = AssetDatabase.LoadAssetAtPath<PropDefinition>(FruitPath);
            var guide = AssetDatabase.LoadAssetAtPath<PropDefinition>(GuidePath);
            PropEditorWindow.Open(guide);
            PixelMapEditorWindow.OpenPropsTab(fruit);
            Selection.activeObject = fruit;
        }
        private static Sprite MakeFruitSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(FruitArtPath);
            if (existing != null) return existing;
            PropAssetFactory.EnsureFolder("Assets/Props/Art");
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.SetPixels(new Color[16 * 16]);
            Color outline = new Color32(102, 43, 44, 255), red = new Color32(234, 75, 55, 255);
            Color shadow = new Color32(181, 45, 44, 255), highlight = new Color32(255, 155, 94, 255);
            for (int y = 2; y <= 12; y++) for (int x = 2; x <= 13; x++)
            {
                if (!FruitPixel(x, y)) continue;
                bool edge = !FruitPixel(x - 1, y) || !FruitPixel(x + 1, y) || !FruitPixel(x, y - 1) || !FruitPixel(x, y + 1);
                texture.SetPixel(x, y, edge ? outline : (x >= 9 || y <= 4 ? shadow : red));
            }
            texture.SetPixel(5, 8, highlight); texture.SetPixel(5, 9, highlight); texture.SetPixel(6, 9, highlight);
            texture.SetPixel(7, 11, new Color32(117, 80, 44, 255)); texture.SetPixel(7, 12, new Color32(117, 80, 44, 255));
            texture.SetPixel(8, 13, new Color32(100, 151, 60, 255)); texture.SetPixel(9, 13, new Color32(154, 192, 77, 255));
            texture.SetPixel(10, 13, new Color32(100, 151, 60, 255)); texture.SetPixel(9, 14, new Color32(100, 151, 60, 255));
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "Props/Art/Fruit.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(FruitArtPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FruitArtPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, 3f / 16f);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(FruitArtPath);
        }
        private static bool FruitPixel(int x, int y)
        {
            if (y == 3) return x >= 6 && x <= 9;
            if (y == 4 || y == 10) return x >= 4 && x <= 11;
            if (y >= 5 && y <= 9) return x >= 3 && x <= 12;
            if (y == 11) return (x >= 5 && x <= 6) || (x >= 9 && x <= 10);
            return false;
        }
    }
}
