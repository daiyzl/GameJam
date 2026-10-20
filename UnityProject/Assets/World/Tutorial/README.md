# 村口试炼 · 五行教学关卡

Unity 2022.3 项目：`C:\Users\DLQian\GameJam`。

打开 `Assets/Scenes/FiveElementsTutorial.unity`，点击 Play。也可以使用菜单 **五行世界 → 教学关卡 → 打开村口试炼**。这是新建的独立场景，原来的 SampleScene、自由世界和逻辑验证场景保留。

## 新美术

- `Assets/World/Tutorial/Art/rock.png`：带苔藓的灰色岩石，透明背景。
- `Assets/World/Tutorial/Art/village.png`：三间房屋与水井组成的村庄，透明背景。
- 对应可拖入场景的 Prefab 在 `Assets/World/Tutorial/Prefabs/`。
- 两张图参考已有树木、山体生成；使用 Point 过滤、无压缩、无 mipmap。生成提示词保存在 `art-prompts.json`。

## 操作与通关

| 操作 | 按键 |
| --- | --- |
| 移动 | WASD / 方向键 |
| 选择金、木、水、火、土 | 数字 1、2、3、4、5 |
| 向当前黄色框投放教学资源 | 靠近后按 E |
| 最后一站局部降雨 | Q |
| 重试当前教学步骤，保留前面进度 | T /「重试本站」按钮 |
| 从村口重新开始全部教学 | R /「重开关卡」按钮 |
| 暂停 / 继续环境计算 | 空格 /「暂停模拟」按钮 |

沿小径依次完成：

1. **滋养树苗**：在 `(3,8)` 加 4 水，木从 1 增到 3，消耗 2 土和 4 水。
2. **金伐木**：在 `(10,8)` 加 2 金，等待木从 8 减少至 4 以下。密林的碰撞同时解除。
3. **水灭火**：在 `(17,8)` 加 4 水，熄灭火并保住树木。
4. **火燃木生土**：在 `(24,8)` 加 1 火，观察燃烧留下至少 1 土。
5. **雨中再生**：在同一地块按 Q，先灭火，再让幸存的木利用水与灰烬生长。
6. 返回村口 `(2,3)` 的黄色框，完成教学。

教学没有死亡惩罚。选错元素或距离太远会给出提示，资源投放不执行；卡住可重试本站。为便于观察，环境只在开始当前试验后按 0.75 秒一步推进，成功后暂停，等待下一次教学操作。暂停环境时人物仍然可以移动。

## 编辑地图

**先退出 Play 再编辑**，否则运行时修改不会保存。

场景使用 Grid + 多层 Tilemap：Ground 地面、Plants 植被、Water 水、Metal 金、Fire 火、Obstacles 岩石障碍、Collision 运行时碰撞。山体与村庄使用单独的 SpriteRenderer；人物使用 Rigidbody2D 与脚部圆形碰撞。

Tile Palette 的 Active Target 选中的是整个 Tilemap；每个格子仍有独立坐标。`TutorialWorld` 用 `WorldToCell` 获取人物脚下格子，再减去地图起始坐标 `Origin` 映射到模拟数组。当前地图起始坐标是 `(-4,-2)`，共 34×22 = 748 格。**每格不需要挂 MonoBehaviour**。

初始数量由 `Assets/World/Tutorial/Tiles/` 中的 `TutorialTile` 资源提供。四个试验地块的特殊数值在根物体 `TutorialWorld → Cell Seeds` 中配置，并覆盖 Tile 的初始值。Tile 资源共享外观和初始参数；运行后每格的元素量独立保存，不会因为共用一个 Tile 而一起改变。

调整教学布局时，需要同步修改 `Stations`、`Cell Seeds`、道路和植被位置。模拟地图最大为 64×64。当前教学目标数量写在 `TutorialWorld.cs` 的步骤判断中；这版用于教学体验，不是自由放置所有元素的完整沙盒。

## 界面、镜头和雨

- 界面是可编辑的 Canvas；文本与按钮在 Hierarchy 中可以直接修改，`TutorialHUD` 更新任务与读数。
- `TutorialFollowCamera` 跟随人物，`Look Ahead = 3` 让提示面板下方留出观察地块的位置。
- `TutorialWeather` 生成雨丝、落地水花和水圈三个粒子系统，使用已有雨素材。规则通过 `AddResource` 给以试验地块为中心的 5×5 区域加水；不依赖粒子碰撞，视觉与数值保持稳定。
- 木的数量控制树苗、单树、树林切换；水和火叠加在独立 Tilemap 上。同一地块可显示多种元素，不需要为每种混合组合制作专用图片。

## 验证

`Verification/Final/report.json` 保存 Unity 实际 Play 验证结果，覆盖五项教学、返回村庄通关、重试与重开按钮、人物移动、Tilemap 碰撞、负坐标转换、降雨粒子和资源守恒。另有起点、降雨与地图全景截图。

`TutorialEditorBridge` 仅在项目根目录存在显式 `TutorialVerification/request.json` 时自动验证，完成后消费该请求，平时不会自行启动 Play 或重置关卡。场景生成器再次打开已有教学场景时保留其内容；如需重新生成，请另行备份已有场景。
