# 2.5D 正交视角小样

在 Unity 中选择 **Tools → 2.5D视角 → 打开占位小样**，然后点击 Play。

- WASD / 方向键：按屏幕方向移动。
- Shift：加速；滚轮：缩放；R：回到起点。
- 1：全景展示；2：跟随角色。
- 从楼梯走上 1.6 米高台，观察角色与墙体、栏杆的遮挡和碰撞。

场景：`Assets/Scenes/Orthographic25DDemo.unity`。
固定正交相机俯角 35°、朝向 45°；地形使用简单 3D 几何，角色复用现有 2D Sprite。
这是独立的地图视角原型，尚未接入战斗、剧情、地图编辑器和存档。编辑器运行本场景时会暂时抑制原游戏自动启动，退出 Play 后恢复设置。

`Validation/orthographic25d-preview.png` 为实际运行截图，`Validation/orthographic25d-results.json` 为移动、台阶、墙体碰撞及场景隔离检查结果。
