# 河岸繁荣 · 像素美术扩展 V1.2

19 张独立 PNG，参考现有树、山、水和小型村庄，由内置 image_gen 生成。完整提示词和原始生成路径见 prompts.json；污染水提示词另存 polluted-water-prompt.json，土坡与堆积河坝提示词另存 terrain-prompts.json。原始 PNG 保留，无额外裁剪或重采样。

## 素材清单

| 文件 | 内容 | 背景／建议宽度 |
| --- | --- | --- |
| Sprites/gold_mine_mountain.png | 有入口和金脉的金矿山 | 透明／3 格 |
| Sprites/gold_ore_exposed.png | 未建设的暴露金矿 | 透明／1.5 格 |
| Sprites/gold_mine_abandoned.png | 枯竭、残破的废弃金矿 | 透明／3 格 |
| Sprites/bridge_wood.png | 横向木桥 | 透明／3 格 |
| Sprites/bridge_stone.png | 横向石桥 | 透明／3 格 |
| Sprites/ground_dead.png | 灰褐裂土、碎石、枯草死地 | 不透明方形地面／1 格 |
| Sprites/pollution_fog.png | 中央透明的环状黑灰雾 | 透明／1.5 格 |
| Sprites/water_polluted.png | 暗青水流、绿褐浮污、油膜与脏泡沫 | 不透明方形水面／1 格 |
| Sprites/soil_slope.png | 带裸露土层、冲刷沟槽和稀疏草的低土坡 | 透明／2 格 |
| Sprites/river_sediment_dam.png | 泥沙碎石自然堆积形成的横向河坝 | 透明／3 格 |
| Sprites/village_medium.png | 六栋房屋、水井和菜地 | 透明／4.5 格 |
| Sprites/village_large.png | 多住宅、会堂、作坊与农田 | 透明／6 格 |
| Backgrounds/season_spring.png | 春：嫩绿、开花 | 不透明／16 格 |
| Backgrounds/season_summer.png | 夏：茂盛绿地 | 不透明／16 格 |
| Backgrounds/season_autumn.png | 秋：金黄与橙红 | 不透明／16 格 |
| Backgrounds/season_winter.png | 冬：积雪与裸树 | 不透明／16 格 |
| Backgrounds/background_morning.png | 早晨：日出与远处薄雾 | 不透明／16 格 |
| Backgrounds/background_noon.png | 中午：明亮日光 | 不透明／16 格 |
| Backgrounds/background_evening.png | 傍晚：暖色夕阳与窗口灯光 | 不透明／16 格 |

背景均为 1672×941 的近 16:9 图片，使用同一河道和建筑布局作为参考，适用于菜单、季节页面或远景。它们不是可行走 Tilemap，也不是四季×三个时段的 12 张组合背景。

## Unity 导入

将本文件夹复制到项目 `Assets/World/WorldArtExpansionV1`。附带的 Editor 导入器只作用于这个路径，自动设为 Sprite、Single、Point、无 Mipmap、无压缩、保留原图尺寸和透明通道。不会修改已有场景与玩法脚本。

导入后自动创建 Prefabs、Tiles、Animations；也可使用菜单 **五行世界 → 扩展美术 → 配置导入并创建缺失预制体**。已有预制体不会被覆盖，Inspector 内改过的导入设置也会保留。

Prefabs 包含全部 19 张图的 SpriteRenderer，可直接拖入场景。包内 PixelSprite 材质来自原项目；无原材质时按可用 Shader 创建。背景排序为 −100，死地 −5，污染水 −3，主要物件 10，污染雾 30，按自己的 Sorting Layer 调整。

`Tiles/ground_dead.asset` 可加入 Tile Palette。死地贴图按可重复地面生成，但生成贴图并不保证数学意义上的无缝边缘；大面积铺设前需在实际地图观察接缝，必要时制作过渡 Tile。

`Tiles/water_polluted.asset` 可加入水流 Tile Palette。水面全幅不透明、没有河岸，适合填河道内部；河岸沿用现有边缘 Tile。用独立水面 Tilemap 的排序覆盖正常水面，污染消退后切回正常水 Tile。若使用 Prefab，则按地块定位与显示状态切换。现有 pollution_fog 可叠在水面周围；水纹是静态美术，不会自行改变水流或污染数值，也不是逐帧流水动画。贴图生成时要求可重复，但仍需在实际铺设时检查接缝。

污染 Prefab 对雾气进行深灰色调制，并附带 3 秒透明度／大小循环。中央透明，不遮掉整格。该循环是显示效果，污染数值与扩散仍由游戏逻辑决定。若要按污染量实时控制透明度，应停用默认 Animation 后由环境视图设置 alpha，避免同时写同一属性。

桥梁是整座横向素材，不含河水或河岸。人物可走区域、护栏碰撞和水流逻辑需在关卡中分别配置；美术预制体没有自动生成物理碰撞。

土坡和堆积河坝均为完整透明地形物件。土坡建议宽 2 格；河坝建议宽 3 格，横向叠放在河道上，泥沙堆两端自然收窄，没有嵌入固定水面。沿用现有河流作为下层。地形高度、可侵蚀土体和通水能力由环境数据决定；拖入美术 Prefab 不会自动改高差、阻水或触发洪水。人物碰撞应另行配置，不要把整张透明图片边界作为实际占地。

村庄图是完整阶段图，适用于切换规模表现；尚未拆成独立房屋。真实逐栋扩建和精确碰撞需要另配建筑数据或拆分素材。图像保留透明阴影，不能用外轮廓直接当居住范围。

## 验证与文件

Verification/png-report.json 记录尺寸和透明通道抽样检查：透明物件都有零 alpha 区域，污染中央 alpha 为零；死地和背景不透明。

Unity 导入器运行后在项目资源目录生成 import-report.json，检查 19 个 Sprite、19 个 Prefab、死地和污染水 Tile 及导入设置。该报告不代表新生态规则已经实现。

gallery.html 可在浏览器查看完整素材目录，透明素材以棋盘背景展示。
