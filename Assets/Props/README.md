# 道具系统

## 编辑和放置

1. 打开 **Tools → 道具编辑器**。新建或复制道具；资产保存在 `Assets/Props/Library/Definitions`。道具库是可独立创建的 ScriptableObject。
2. 配置名称、分类、说明、Sprite、颜色和世界尺寸。外观支持静态贴图、序列帧和 Animator Controller，也可指定外观预制体。预制体上的 2D 碰撞和 Rigidbody2D 模拟会禁用，由道具定义统一管理碰撞。
3. 勾选“是否实体”时才阻挡角色。碰撞尺寸和偏移独立于动画，推荐用脚底小碰撞框。非实体道具无需 Trigger 也可以交互。
4. “行为”可同时选择 Dialogue / Pickup / Inspect / Custom。设置交互范围、提示、冷却和是否只交互一次。对话逐条填写 speaker、text 和可选 portrait。
5. 打开 **Tools → 像素地图编辑器 → 交互道具**，选择道具库和道具，在 Scene 视图左键放置。支持网格、自由放置、旋转、连续绘制、擦除和 Ctrl+Z / Ctrl+Y。一格允许地板和道具共存；网格模式避免同格重复道具。
6. 在场景实例 Inspector 的“完成交互事件”和“拾取事件”中绑定 UnityEvent，可扩展机关、任务、对话系统等。修改定义会同步当前已加载场景中的同类实例；事件留在实例上。

图集导入保留原切片。未设置为 Sprite 的 Texture2D，请先在贴图 Inspector 中配置 Sprite；系统不会覆盖其导入设置。帧动画按帧数组顺序播放，交互帧播完返回待机。Animator 模式通过指定的 Trigger 启动交互动画；Controller 的路径应相对于实际 Animator 所在的外观节点。

## 玩家与游戏流程

玩家需要 `PlayerInteractor`，它会自动添加 `PropGameState`。已有的 PlayerMovement 玩家已接入；新场景可运行 **Tools → 道具系统 → 安装玩家交互与示例**，或手动添加组件。

- **E**：交互 / 下一句；**Space**：下一句；**Esc**：取消对话；**I**：背包。移动和 Shift 加速沿用现有玩家控制。
- 对话期间暂停当前玩家的移动，结束或取消后恢复。取消不会拾取、消耗前置物品、授予标记或触发完成事件。
- 前置标记必须全部满足，可同时要求指定物品数量；完成后可消耗该物品并授予新标记。背包按物品键合并数量。
- 拾取始终只执行一次。普通交互可按冷却重复；“仅交互一次”会记录实例 ID。Custom 的完成事件也适用于其他组合行为。
- 拾取后隐藏而不销毁实例，因此恢复较早的存档可以让道具重新出现。

`Assets/Scenes/PropsDemo.unity` 是原场景的副本：先读取“记录终端”，再拾取“补给箱”，按 I 查看 3 份补给；“向导”演示帧动画与对话。原 PixelRoom 保留原有地图内容并接入玩家交互。

## 接入存档

系统提供状态序列化接口，不自动写磁盘或 PlayerPrefs。由游戏存档管理器保存 JSON，并在目标场景和道具加载后恢复：

```csharp
PropGameState state = player.GetComponent<PropGameState>();
string json = state.CaptureJson();
// 将 json 写入你的存档。读档后：
bool restored = state.RestoreJson(json);
```

JSON 包含背包、流程标记和已消耗的实例 ID。PlayerInteractor 收到恢复事件会同步已加载道具；后续再加载地图时，可调用 `PropInstance.RestoreAll(state)`。实例 ID 在地图场景中持久保存；复制实例会分配新的 ID，定义复制也会分配新道具 ID。未设置背包物品键时使用定义 ID，因此已发布存档使用的 ID 不应手动重建。

无玩家交互组件的自定义流程可自行调用 `PropInstance.RestoreAll(state)`。当前默认 UI 和流程面向单玩家；多玩家应把道具消费状态纳入共享状态管理。

## 验证

运行 **Tools → 道具系统 → 运行验证**。编辑器自动验证资产关联、地图放置、复制、擦除与撤销，再进入 Play Mode 验证距离限制、对话取消与完成、组合拾取、背包、流程条件、存档、实体碰撞和帧动画。结果输出至 `Validation/props-results.json`，结束后回到编辑器。


## 果实交付示例

PropsDemo 已放置可拾取的“果实”，向导支持交给、不给、没有果实和已交付后的不同对白。配置保存在同一组道具资产中，两个编辑器都能编辑；具体操作见 [果实与向导示例](FRUIT-EXAMPLE.md)。
