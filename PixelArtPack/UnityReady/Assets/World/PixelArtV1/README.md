# 五行世界 · 像素素材第一版

Unity 项目中的安装位置：`Assets/World/PixelArtV1`。

## 使用

打开项目后，在 Project 面板找到 `Assets/World/PixelArtV1/Prefabs`，拖入场景即可显示。`Sprites` 保存原始 PNG；展开图集可选择单个 Sprite。`Animations` 保存人物和效果的 `.anim` 文件。

已经设置 Sprite (2D and UI)、Point 过滤、关闭 Mip Maps、无压缩，并根据图集和建议世界尺寸分别配置 Pixels Per Unit。地面单片宽度对应 1 个世界单位。图像是较高分辨率的像素风 PNG，实际尺寸和每帧尺寸见 `Verification/image-inspection.json`，并非所有原图都是 32×32。

| PNG | 内容与排列 |
| --- | --- |
| sapling.png | 透明树苗 |
| tree.png | 透明单棵树 |
| forest.png | 透明五棵树的树林组合 |
| mountain.png | 透明山体组合 |
| character.png | 3 列×4 行；从上到下为下、左、右、上；每行为待机、行走一、行走二 |
| ground_elements.png | 3 列×2 行；上排为金、木、水，下排为火、土、普通草地 |
| water.png | 3×3；四个外转角、四个直边和中心水面 |
| rain.png | 3 列×2 行；上排雨线、雨滴、小水花，下排大水花、小涟漪、大涟漪 |
| fire.png | 2×2，按从左到右、从上到下排列四帧火焰 |

## 分层混合地块

用草地或土底图铺地，再叠水面和树木。火焰单独覆盖在物体上。单一元素地面可以用于明显的元素区域；混合地块通过组合渲染层实现。世界中运行的元素数量仍由 ElementWorld 管理，素材只负责显示。

水域提供基础直边和外转角；内凹角、分叉等复杂岸线可以后续补充。树林组合适合大块植被；每块地独立变化时更适合使用单棵树。

## 接入现有世界

选中原来的“木 / 水 / 火 / 人物”渲染子物体，替换 Sprite 或参考新预制体。新素材自带颜色，替换旧白色占位图时应检查 SpriteRenderer 的 Color，避免被旧的水域颜色重复染色。保留 ElementTile 已引用的 SpriteRenderer 与碰撞组件，并调整显示尺寸及位置。

人物四方向行走和待机共 8 个动画，火焰循环动画 1 个，水花和涟漪各 1 个。动画绑定到同一物体上的 SpriteRenderer，添加 Animator 并配置状态即可使用。预制体是可显示的美术对象，人物移动和方向切换由角色控制器接入。

## 雨粒子

将 `rain_streak` 用于下落粒子，`rain_drop` 用于较大的水滴；地面单独播放 splash，水面播放 ripple。为粒子创建使用透明 Sprite 纹理的材质，并保持小尺寸和清晰边缘。降雨范围及地块加水由天气逻辑统一处理。

雨的六个切片是不同用途的效果；雨线、雨滴、水花、涟漪分别使用，避免把整张图集的六格当成连续动画。水花和涟漪已有独立两帧动画文件。

## 工具与验证

菜单 `五行世界 / 像素素材 / 配置导入与切片` 可重新配置当前图集并补齐缺少的预制体和动画；重新切片保留既有 Sprite ID。现有世界场景不自动替换素材。

`Verification/unity-import.json` 是 Unity 2022.3 实际导入验证报告，检查 PNG 导入设置、切片数量及资产引用。`Verification/image-inspection.json` 检查 PNG、透明通道、图集等分和非空帧。

素材使用内置 image_gen 生成，提示词和透明边缘细化要求完整保存在 `prompts.json`。Sources 保存选用细化版之前的树苗、树林与雨效果原图。
