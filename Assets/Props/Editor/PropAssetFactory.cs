using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Emerge.Props.Editor
{
    public static class PropAssetFactory
    {
        public const string LibraryPath = "Assets/Props/Library/DefaultPropLibrary.asset";
        public const string DefinitionsPath = "Assets/Props/Library/Definitions";
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
        public static PropLibrary EnsureDefaultLibrary()
        {
            EnsureFolder(DefinitionsPath);
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(LibraryPath);
            if (library == null)
            { library = ScriptableObject.CreateInstance<PropLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            Default(library, "Terminal", definition =>
            {
                definition.displayName = "记录终端"; definition.category = "剧情";
                definition.sprite = Art("Table"); definition.worldSize = new Vector2(2f, 2f);
                definition.isSolid = true; definition.colliderSize = new Vector2(1.6f, 0.5f);
                definition.physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
                definition.actions = PropActions.Dialogue; definition.interactionLabel = "读取";
                definition.dialogue.Add(new PropDialogueLine { speaker = "终端", text = "欢迎来到道具系统示例。完成这段对话后，旁边的补给箱就可以拾取了。" });
                definition.dialogue.Add(new PropDialogueLine { speaker = "终端", text = "道具属性保存在独立资产中。地图编辑器可以直接放置它们，E 继续，Esc 取消。" });
                definition.grantedFlags = new[] { "terminal_read" };
            });
            Default(library, "Supplies", definition =>
            {
                definition.displayName = "补给箱"; definition.category = "拾取";
                definition.sprite = Art("Crate"); definition.worldSize = new Vector2(1f, 1.5f);
                definition.actions = PropActions.Pickup; definition.interactionLabel = "拾取";
                definition.inventoryKey = "supplies"; definition.pickupAmount = 3;
                definition.requiredFlags = new[] { "terminal_read" };
                definition.lockedHint = "请先读取记录终端";
                definition.description = "非实体道具；拾取后加入背包并从地图隐藏。";
            });
            Default(library, "Guide", definition =>
            {
                definition.displayName = "向导"; definition.category = "角色";
                definition.visualMode = PropVisualMode.SpriteFrames;
                definition.sprite = Art("HeroIdle"); definition.worldSize = new Vector2(1.5f, 2f);
                definition.idleFrames = new[] { Art("HeroIdle"), Art("HeroStepA"), Art("HeroIdle"), Art("HeroStepB") };
                definition.framesPerSecond = 4f;
                definition.isSolid = true; definition.colliderSize = new Vector2(0.65f, 0.38f);
                definition.actions = PropActions.Dialogue;
                definition.dialogue.Add(new PropDialogueLine { speaker = "向导", text = "我的外观使用帧动画。你也可以在道具编辑器中改用 Animator，或指定外观预制体。" });
            });
            AssetDatabase.SaveAssets();
            return library;
        }
        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/" + name + ".png");
        private static void Default(PropLibrary library, string fileName, Action<PropDefinition> configure)
        {
            string path = DefinitionsPath + "/" + fileName + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
            if (definition == null)
            { definition = ScriptableObject.CreateInstance<PropDefinition>(); configure(definition); AssetDatabase.CreateAsset(definition, path); }
            if (library.Add(definition)) EditorUtility.SetDirty(library);
        }
        public static PropDefinition Create(PropLibrary library, string name = "新道具")
        {
            EnsureFolder(DefinitionsPath);
            var definition = ScriptableObject.CreateInstance<PropDefinition>();
            definition.displayName = name;
            AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(DefinitionsPath + "/Prop.asset"));
            Undo.RecordObject(library, "添加道具定义"); library.Add(definition); EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return definition;
        }
        public static PropDefinition Duplicate(PropLibrary library, PropDefinition source)
        {
            var definition = UnityEngine.Object.Instantiate(source);
            definition.RenewIdentity(); definition.displayName += " 副本";
            AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(DefinitionsPath + "/Prop.asset"));
            Undo.RecordObject(library, "复制道具定义"); library.Add(definition); EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets(); return definition;
        }
        public static List<PropDefinition> Import(PropLibrary library, UnityEngine.Object[] selected)
        {
            var result = new List<PropDefinition>();
            foreach (var item in selected)
            {
                if (item is PropDefinition existing)
                { Undo.RecordObject(library, "关联道具定义"); library.Add(existing); result.Add(existing); continue; }
                if (item is Sprite sprite) { result.Add(ImportVisual(library, sprite, null)); continue; }
                if (item is Texture2D)
                {
                    string path = AssetDatabase.GetAssetPath(item);
                    // Preserve the user's sliced sprite sheets and texture importer settings.
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset is Sprite sliced) result.Add(ImportVisual(library, sliced, null));
                }
                else if (item is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab)) result.Add(ImportVisual(library, null, prefab));
            }
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets(); return result;
        }
        private static PropDefinition ImportVisual(PropLibrary library, Sprite sprite, GameObject prefab)
        {
            var definition = Create(library, sprite != null ? sprite.name : prefab.name);
            definition.sprite = sprite; definition.visualPrefab = prefab;
            if (sprite != null) definition.worldSize = sprite.bounds.size;
            EditorUtility.SetDirty(definition); return definition;
        }
    }
}
