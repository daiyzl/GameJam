# GameJam · 河岸繁荣

基于 Unity 的五行生态模拟游戏原型。玩家通过播种、投水、地形调整和手动推进时段，观察植物、河流、动物与村庄之间的相互作用。

## 打开与运行

1. 安装 Unity **2022.3.62f2c1**。
2. 在 Unity Hub 中添加本仓库的 **`UnityProject`** 文件夹。
3. 等待 Unity 导入素材和解析 Packages。
4. 打开 `Assets/Scenes/StartMenu.unity` 并运行；开始游戏后进入 `RiverbankEmergenceV1` 场景。

`UnityProject` 包含完整的 `Assets`（含 `.meta`）、`Packages` 与 `ProjectSettings`。这是 2026-10-20 从本地开发工程整理的源码快照；后续修改应在此目录进行，或从开发工程同步后再提交。

## 基本操作

| 输入 | 功能 |
| --- | --- |
| WASD / 方向键 | 移动 |
| 渡过时段按钮 | 推进早晨、正午、傍晚与下一日 |
| Tab | 打开 / 关闭五行页 |
| 1–5 | 选择金、木种子、水、火、土养分 |
| 鼠标点击 / E | 在附近投放 |
| Q | 安排附近区域下一时段降雨 |
| R / 重开按钮 | 重置关卡 |

详细玩法及开发记录见 [河岸繁荣 V1](EmergenceV1/README.md) 和 [初始界面](StartMenu/README.md)。

## 目录

| 目录 | 内容 |
| --- | --- |
| `UnityProject/` | 可用 Unity 打开的工程 |
| `Design/`、`DevLogs/` | 设计文档、开发记录 |
| `EmergenceV1/`、`StartMenu/` | 当前玩法与界面说明、验证记录 |
| `PixelArtPack/`、`WorldArtExpansionV1/` | 美术素材与说明 |
| `ReleaseAssets/` | 游戏封面、图标与宣传素材 |
| 其他 Prototype / World / Tutorial 目录 | 原型、脚本与历史开发资料 |

仓库排除 Unity 缓存、临时文件、构建产物、Profiler 二进制记录和重复导出包。验证记录属于历史开发结果，不代表每次提交都重新运行了 Unity 测试。
