# Light

本仓库根目录是统一的 Unity 工程，整合了像素地图编辑器、人物移动和镜头跟随。

## 打开与运行

1. 在 Unity Hub 中添加本仓库根目录，即包含 `Assets`、`Packages` 和 `ProjectSettings` 的目录。
2. 主工程使用 **Unity 2022.3.12f1**。
3. 打开 `Assets/Scenes/PropsDemo.unity`，点击 Play 显示主菜单；点击“新游戏”进入道具演示地图，“继续游戏”恢复最近一次有效存档。
4. 使用 **WASD 或方向键**移动人物，按住 **左／右 Shift** 以 2 倍速度加速，松开即恢复。人物脚底与墙壁、桌子及木箱碰撞，镜头实时跟随角色，包括房间边缘。

`PixelRoom` 已设为构建列表中的第一个场景；原有 `SampleScene` 保留在列表中。

## 像素地图编辑

- 从 **Tools → 像素地图编辑器** 打开编辑窗口，或使用 `Ctrl + Shift + M`。
- 从 **GameObject → 像素地图 → 创建地图根节点** 创建地图。
- 原有地图编辑器代码、方块定义和默认贴图位于 `Assets/PixelMap`。

## 人物与镜头设置

- `Player` 上的 `PlayerMovement` 控制移动，默认行走速度为 4，`Sprint Multiplier` 默认为 2；输入使用旧版 Input Manager 的 `Horizontal` / `Vertical` 轴。
- `Main Camera` 上的 `CameraFollow` 在 `LateUpdate` 中直接跟随角色的插值位置，保留固定偏移，不使用缓动或房间边界限制。
- `PlayerVisual` 按实际速度播放行走帧，加速时同步加快，顶住障碍物时停止踏步；`FeetYSort` 根据脚底 Y 坐标控制遮挡顺序。
- 默认停用 Pixel Perfect Camera 的独立画面量化，避免镜头与角色不同步。贴图继续使用 Point 过滤，保留像素风。刚体保留插值与连续碰撞检测；相邻墙砖使用 CompositeCollider2D 合并边界，并使用零摩擦、零弹性的碰撞材质。

## 验证与场景重建

- **Pixel Prototype → Run PlayMode Validation** 验证行走与加速、斜向速度、释放输入、墙边滑动、高速碰撞和每个实际渲染帧的镜头同步。运行前请保存当前场景。结果写入 `Validation/playmode-results.json`。
- **Pixel Prototype → Create Demo Scene** 重新生成演示素材和 `PixelRoom` 场景，并保留构建列表中的其他场景。此操作会重建演示场景和生成素材，修改过这些内容时请先保存备份。

## 协作目录

- 在主工程的 `Assets` 内添加脚本、素材与场景，提交相应的 `.meta` 文件以保留资源引用。
- 依赖与工程设置分别在根目录的 `Packages` 和 `ProjectSettings` 中维护。
- `PixelDungeonPrototype-main` 保留了原分支提交的子工程，供追溯来源；后续功能迭代应修改根工程内已整合的文件。
- `Library`、`Temp`、`Logs` 和 `UserSettings` 是本地生成目录，无需提交。


## 道具系统

打开 Tools → 道具编辑器 管理道具属性；在像素地图编辑器的“交互道具”页放置。运行 Assets/Scenes/PropsDemo.unity，E 交互，I 背包。支持贴图、帧动画、Animator、实体碰撞、对话、拾取、流程条件和场景事件。使用与存档接口见 [道具系统说明](Assets/Props/README.md)。


## 菜单与存档

主菜单提供继续、新游戏、读取手动／自动存档及退出。游戏中点击右上角设置按钮或 Esc，暂停后可手动保存、保存并返回主菜单。存档包含地图与角色位置、背包、拾取状态及向导交付进度；默认每 10 秒自动保存，也在失去焦点、应用暂停和正常退出时保存。新游戏会重置自动档并保留手动档。详见 [菜单与存档说明](Assets/GameFlow/README.md)。

## 六爻战斗

从 **Tools → 战斗系统 → 打开测试场景** 选择单敌、双敌、三敌、防御或首领，按 Play 后开始新游戏即可试玩。玩家回合可连续使用 MP 约束的技能和背包物品，点击“结束回合”后敌方依次行动。每轮先定卦开放 3 个高级技能并确定本轮倍率；普攻、防御与有限次清心常驻，高级技能受全场次数限制。敌方提前展示随机意图并可施加异常。支持战中存档、起卦途中读档、物品消耗保留，以及胜利回合 / 最佳成绩 / 首胜和速战成就记录。技能、敌人、物品及遭遇均为可编辑配置资产；详见 [战斗系统说明](Assets/Battle/README.md)。

战斗 UI 提供主角与多敌立绘、点击目标高亮、HP/MP 条、技能/背包/交涉/逃跑四个页面、技能与状态悬浮说明、敌方意图提示、铜币动画和简单行动特效。逃跑恢复战前地图与剧情状态，支持战中读档后的回退。简单占位美术可在 `BattlePresentation.asset` 和敌人配置中替换。
