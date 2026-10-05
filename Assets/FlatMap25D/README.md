# 2D 地图 / 2.5D 外观小样

在 Unity 中选择 **Tools → 2.5D视角 → 打开2D地图小样**，点击 Play。

- WASD / 方向键移动；Shift 加速；滚轮缩放；R 回到起点。
- 1 全景，2 跟随；F3 显示空气墙的轮廓。
- 从楼梯上下高台，绕木桶、灯柱与矮墙移动，观察遮挡。

场景为 `Assets/Scenes/FlatMap25DDemo.unity`，与 `Orthographic25DDemo.unity` 的构图一致。
制作时将 3D 参考场景烘焙为 `Art/Map.png`，再根据每个物体在整图中实际可见的轮廓提取透明前景图层，沿用整图的颜色和阴影。
运行时使用 SpriteRenderer、Rigidbody2D 和手工配置的投影占地 PolygonCollider2D；没有 3D 地形或 3D 碰撞。
地面脚点与视觉高度分开，楼梯区域改变视觉高度，高台边缘的空气墙限制入口。
前景层按照脚点与高度切换到角色前面或后面。当前遮挡方案用于单主角对照小样。

这是独立的视角与地图方案验证，尚未接入剧情、战斗、存档和正式地图编辑流程。
编辑器运行该场景时会暂时抑制原游戏自动启动，退出 Play 后恢复设置。
后续使用手绘地图时，需要按画面标注通行、前景遮挡和高度区域。

验证截图与结果保存在 `Validation/flatmap25d-*.png` 和 `Validation/flatmap25d-results.json`。
