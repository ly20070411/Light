# 角色库

从 **Tools → 角色编辑器** 管理八名已录入角色。可以按姓名、固定 ID、势力、部门、职能或卦象搜索，编辑人物资料、第一日安排，以及地图形象和对话头像。

每名角色是独立的 `CharacterDefinition` 资产；运行时角色库位于 `Assets/Resources/Characters/CharacterCatalog.asset`。四位未命名队员的姓名留空，暂以职能显示，之后补充姓名不会改变 ID 或资产引用。

| 固定 ID | 当前显示名 | 队伍 | 卦象与层级 |
| --- | --- | --- | --- |
| `huan-yujian` | 桓玉鉴 | 当前勘察队 | 恒·卦主 |
| `lin-xi` | 林溪 | 当前勘察队 | 比·掌卦 |
| `hydrologist` | 水文学家 | 当前勘察队 | 屯·天眼 |
| `geologist` | 地质学家 | 当前勘察队 | 屯·天眼 |
| `yang-yinglong` | 杨应隆 | 当前勘察队 | 大过·掌卦 |
| `containment-researcher` | 收容部安保 | 当前勘察队 | 井·天眼 |
| `mechanic` | 机械师 | 当前勘察队 | 丰·天眼 |
| `tan-yue` | 谭礿 | 前一支科考队 | 萃·卦主 |

## 地图与对话

- 角色编辑器中点击“在地图中放置”，打开现有像素地图编辑器的道具笔刷。角色地图定义已加入默认道具库，也可以直接拖入 `Assets/Characters/Prefabs` 下的预制体。
- 占位形象为 32×48 像素地图小人与 64×64 像素头像，采用 Point 过滤、透明背景和脚底锚点。以势力服饰、色彩和工具区分角色，后续可以直接替换资源引用。
- 预制体仅提供静态形象、脚底碰撞和角色引用。桓玉鉴的预制体同样是静态占位；现有玩家移动与动画由原有玩家对象负责。
- 在道具编辑器中配置角色交互。对话行的“关联角色”可引用任一角色；“说话人覆盖”留空时显示角色名，头像留空时使用角色头像。填写覆盖项可显示“无名之人”等身份。原有文本说话人仍然适用。
- 当前只录入人物资料和第一日任务说明，角色地图定义的交互行为默认为关闭。尚未制作剧情台词、任务执行、第一日影子演出、隐藏值或结局判定。策划背景不会自动向玩家显示。
- 角色编辑器修改地图形象或姓名，会同步关联地图定义和已打开场景中的该角色形象。使用现有道具编辑器调整碰撞、尺寸、动画与具体交互。

## 代码调用

```csharp
using Emerge.Characters;
using Emerge.Props;
using UnityEngine;

var catalog = CharacterCatalog.LoadDefault();
var linXi = catalog.Find(CharacterIds.LinXi);
var npc = linXi.Spawn(new Vector3(2, 3, 0));
var identity = npc.GetComponent<PropInstance>().Character;
var line = new PropDialogueLine { character = linXi, text = "在此填写剧情台词。" };
// line.SpeakerName / line.Portrait 自动读取角色；允许手动覆盖。
```

使用 `Spawn` 会为每个场景实例分配独立的道具存档 ID，同时保持角色 ID。玩家对象如需绑定身份，可引用 `CharacterDefinition`，不必生成另一个静态角色。

## 补齐与验证

**Tools → 角色系统 → 补齐初始角色资源** 仅创建缺失资源、补齐空引用并加入默认库；不会重新覆盖已有角色资料、贴图、头像、预制体或剧情配置。**验证角色资源** 检查运行时加载、队伍与身份、地图库和预制体、改名、对话兼容及实例 ID，结果输出至 `Validation/characters-results.json`。

批处理安装与验证入口：`Emerge.Characters.Editor.CharacterValidation.InstallAndValidate`。

保留的待定项包括四位队员姓名、收容部门名称，以及桓玉鉴“总管／掌鉴”的称谓差异。杨应隆按人物表录入大过卦，原备注的卦名差异记在策划备注中。
