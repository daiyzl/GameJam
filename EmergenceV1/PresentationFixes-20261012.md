# 连续地形与矿物显示

## 改动

- 移除 RiverEmbankment 的生成，清空场景中的 `damPrefab`／`slopePrefab` 引用。现有同名对象的 Renderer 停用，旧资产保留用于回溯。淤积继续改变真实高度、河道和水流；显示使用更新后的地形高度，不再盖一个河坝对象，因此桥面也不会出现河坝。
- 平原有效矿点使用新 `PlainGoldVeinV1` 矿脉 Sprite；坡地／高地有效矿点使用已有 `gold_ore_exposed`。散金达到露出阈值时仍使用露矿。矿物耗尽后移除覆盖层，显示原来的地形，不再生成废矿洞外观。矿点的数据状态仍为 Exhausted；水下显示继续使用蓝色半透明，资源规则未改。
- 用连续地表替代逐格独立山峰／坡丘贴纸。三个地形 Prefab 都是严格 1×1 的完整地表 Sprite，使用共同的 `RiverbankV1/ConnectedTerrain` 材质。材质读取双线性采样的真实高度和草量，平原／坡地／高地纹理通过高度渐变混合；所有格子共享世界纹理坐标，相邻格不重新开始纹理。高差形成明暗，草减少时地表逐步转为裸土。垫高、挖低、侵蚀、淤积均更新这份数据，不重新生成 Perlin 高度。
- 平原保留上一轮素材，坡地使用新 `TerrainSlopeSurfaceV2`，高地使用新 `TerrainHighSurfaceV2`。此前的丘状坡地与整座山峰素材不再作为每格地表。

## 验证

实际 Unity Play 报告：`Verification/Presentation20261012/UnityPlay/report.json` 为 `passed`，`connectedTerrainAndOre=true`、`runtimeErrors=0`。

检查包括材质在 DX11 支持且编译无错误、地表的共同高度数据与双线性采样、严格一格的 Sprite bounds、桥面淤积不生成河坝、高地露矿／平原矿脉切换、枯竭后移除矿物与恢复地表、改高度后切换矿物类型，以及上一轮的火、水下矿、村庄、人物、桥、播种、教学通关、降雨、重开。原有 750 天长期水流回归也通过，实际场景最大／最终积水均为 46／898 格陆地。

编辑器在本轮 Play 模式热重载程序集时发生过一次 Mono 原生崩溃，日志中核心类型解析失败；未执行新显示验收。干净重启后完成整套回归，运行无错误。该现象记录为编辑器热重载限制，未在正常 Play 验收中复现。

截图：`perlin-terrain.png` 为整体地形衔接，`level-start.png` 为正常镜头，`day-751-terrain.png` 为长期水流结果。测试结束恢复第一天早晨。修改前脚本与场景保存在 `Verification/Presentation20261012/Before`。
