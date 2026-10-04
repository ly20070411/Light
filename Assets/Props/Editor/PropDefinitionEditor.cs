using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Props.Editor
{
    [CustomEditor(typeof(PropDefinition))]
    public sealed class PropDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var edited = (PropDefinition)target;
            if (edited.itemHandover != null && edited.itemHandover.enabled)
                EditorGUILayout.HelpBox("交付物品键：" + edited.itemHandover.itemKey + " × " + edited.itemHandover.amount +
                    "\n交给：" + FirstLine(edited.itemHandover.acceptedDialogue) +
                    "\n不给：" + FirstLine(edited.itemHandover.declinedDialogue) +
                    "\n没有物品：" + FirstLine(edited.itemHandover.missingItemDialogue) +
                    "\n已交付：" + FirstLine(edited.itemHandover.completedDialogue), MessageType.Info);
            else if (edited.HasAction(PropActions.Pickup))
                EditorGUILayout.HelpBox("拾取后加入背包：" + edited.DisplayName + " × " + edited.pickupAmount + "（物品键：" + edited.InventoryKey + "）", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(true)) Field("id", "道具 ID（存档键）");
            Section("基本属性"); Field("displayName", "名称"); Field("category", "分类"); Field("description", "说明 / 查看文本");
            Section("外观与动画"); Field("visualMode", "表现形式"); Field("sprite", "外形贴图"); Field("visualPrefab", "可选外观预制体");
            Field("worldSize", "世界尺寸"); Field("tint", "颜色");
            var mode = (PropVisualMode)serializedObject.FindProperty("visualMode").enumValueIndex;
            if (mode == PropVisualMode.SpriteFrames)
            { Field("idleFrames", "待机帧"); Field("interactionFrames", "交互帧（播完返回待机）"); Field("framesPerSecond", "每秒帧数"); Field("loop", "待机循环"); }
            if (mode == PropVisualMode.Animator)
            { Field("animatorController", "Animator Controller"); Field("interactionTrigger", "交互 Trigger"); }
            Field("sortingLayerName", "排序层"); Field("sortingOrder", "排序偏移"); Field("sortByY", "按脚底 Y 排序"); Field("allowRotation", "允许旋转");
            Section("实体碰撞"); Field("isSolid", "是否实体（阻挡角色）");
            if (serializedObject.FindProperty("isSolid").boolValue)
            { Field("colliderSize", "碰撞尺寸"); Field("colliderOffset", "碰撞偏移"); Field("physicsMaterial", "2D 物理材质"); }
            Section("交互行为（可组合）");
            var actions = serializedObject.FindProperty("actions");
            actions.intValue = (int)(PropActions)EditorGUILayout.EnumFlagsField("行为", (PropActions)actions.intValue);
            Field("interactionLabel", "提示动作"); Field("interactionRange", "交互距离"); Field("singleUse", "仅交互一次"); Field("cooldown", "冷却秒数");
            Field("checkEvent", "检定事件（优先执行）");
            Field("battleEncounter", "战斗遭遇（优先于检定）");
            if (serializedObject.FindProperty("checkEvent").objectReferenceValue != null)
                EditorGUILayout.HelpBox("交互将进入检定事件；行动成本、成功与失败分支及继续剧情在检定事件资产中配置。", MessageType.Info);
            var flags = (PropActions)actions.intValue;
            if ((flags & PropActions.Dialogue) != 0)
            {
                DrawItemHandover();
                if (!serializedObject.FindProperty("itemHandover").FindPropertyRelative("enabled").boolValue)
                    Field("dialogue", "对话（顺序播放）");
            }
            if ((flags & PropActions.Pickup) != 0)
            { Field("inventoryKey", "背包物品键（空 = 道具 ID）"); Field("pickupAmount", "拾取数量"); Field("hideAfterPickup", "拾取后隐藏"); }
            if ((flags & PropActions.Custom) != 0)
                EditorGUILayout.HelpBox("将道具放入地图后，在实例 Inspector 的 On Interacted 中绑定场景事件。", MessageType.Info);
            Section("游戏流程条件"); Field("requiredFlags", "前置标记（全部满足）"); Field("requiredItemKey", "前置物品键");
            if (!string.IsNullOrWhiteSpace(serializedObject.FindProperty("requiredItemKey").stringValue))
            { Field("requiredItemAmount", "前置物品数量"); Field("consumeRequiredItem", "完成交互后消耗"); }
            Field("grantedFlags", "完成后设置标记"); Field("lockedHint", "条件未满足提示");
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed) ApplyToScene((PropDefinition)target);
            var definition = (PropDefinition)target;
            if (definition.checkEvent == null && definition.HasAction(PropActions.Dialogue) && definition.dialogue.Count == 0 &&
                (definition.itemHandover == null || !definition.itemHandover.enabled))
                EditorGUILayout.HelpBox("请添加至少一条对话；空对话将直接执行其余行为。", MessageType.Warning);
            if (definition.visualMode == PropVisualMode.SpriteFrames && definition.idleFrames.Length == 0)
                EditorGUILayout.HelpBox("请添加待机帧，或改用静态贴图。", MessageType.Warning);
            if (definition.visualMode == PropVisualMode.Animator && definition.animatorController == null)
                EditorGUILayout.HelpBox("Animator 模式需要 Controller。", MessageType.Warning);
        }
        private void Section(string title) { EditorGUILayout.Space(7f); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
        private static string FirstLine(System.Collections.Generic.List<PropDialogueLine> lines)
            => lines != null && lines.Count > 0 && lines[0] != null ? lines[0].text : "未填写";
        private void DrawItemHandover()
        {
            Section("物品交付与分支对话");
            var handover = serializedObject.FindProperty("itemHandover");
            HandoverField(handover, "enabled", "启用物品交付选择");
            if (!handover.FindPropertyRelative("enabled").boolValue) return;
            HandoverField(handover, "itemKey", "需要的背包物品键");
            HandoverField(handover, "amount", "交付数量");
            HandoverField(handover, "completionFlag", "交付完成标记（空 = 可重复）");
            HandoverField(handover, "acceptLabel", "交给按钮文字");
            HandoverField(handover, "declineLabel", "不给按钮文字");
            HandoverField(handover, "offerDialogue", "有物品时：询问对话");
            HandoverField(handover, "acceptedDialogue", "选择交给：新对话");
            HandoverField(handover, "declinedDialogue", "选择不给：新对话");
            HandoverField(handover, "missingItemDialogue", "没有物品：对话");
            HandoverField(handover, "completedDialogue", "已经交付：后续对话");
            EditorGUILayout.HelpBox("询问结束后，玩家按 1 交给、2 不给，或点击按钮。只有交给分支完整结束才消耗物品并设置完成标记；取消不扣物品。", MessageType.None);
        }
        private static void HandoverField(SerializedProperty source, string name, string label)
            => EditorGUILayout.PropertyField(source.FindPropertyRelative(name), new GUIContent(label), true);
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        public static void ApplyToScene(PropDefinition definition)
        {
            if (Application.isPlaying) return;
            foreach (var instance in Resources.FindObjectsOfTypeAll<PropInstance>())
            {
                if (instance.Definition != definition || EditorUtility.IsPersistent(instance) || !instance.gameObject.scene.IsValid()) continue;
                Undo.RegisterFullObjectHierarchyUndo(instance.gameObject, "同步道具属性");
                instance.ApplyDefinition(true);
                EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            }
            SceneView.RepaintAll();
        }
    }

    [CustomEditor(typeof(PropInstance))]
    public sealed class PropInstanceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("definition"), new GUIContent("道具定义"));
            bool changed = EditorGUI.EndChangeCheck();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("instanceId"), new GUIContent("实例 ID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("placementMode"), new GUIContent("放置模式"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("gridCoordinate"), new GUIContent("网格坐标"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onInteracted"), new GUIContent("完成交互事件"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onPickedUp"), new GUIContent("拾取事件"));
            serializedObject.ApplyModifiedProperties();
            var instance = (PropInstance)target;
            if (changed) { instance.ApplyDefinition(true); EditorSceneManager.MarkSceneDirty(instance.gameObject.scene); }
            if (instance.Definition != null && GUILayout.Button("在道具编辑器中编辑定义")) PropEditorWindow.Open(instance.Definition);
        }
        private void OnSceneGUI()
        {
            var prop = (PropInstance)target;
            if (prop.Definition == null) return;
            Handles.color = new Color(0.2f, 1f, 0.7f, 0.6f);
            Handles.DrawWireDisc(prop.transform.position, Vector3.forward, prop.Definition.interactionRange);
            if (prop.Definition.isSolid)
                using (new Handles.DrawingScope(prop.transform.localToWorldMatrix))
                    Handles.DrawWireCube(prop.Definition.colliderOffset, prop.Definition.colliderSize);
        }
    }
}
