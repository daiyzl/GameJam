# 河岸繁荣 V1

Unity项目：`C:\Users\DLQian\GameJam`，编辑器2022.3.62f2c1。

初始界面已加入：打开 `Assets/Scenes/StartMenu.unity`，或 **五行世界 → 初始界面 → 打开初始界面**。季节默认秋，左右箭头切换春／夏／秋／冬／随机，四季背景与自定义按钮样式已配置。开始游戏加载本关卡。详见 [初始界面记录](../StartMenu/README.md)。

新场景：`Assets/Scenes/RiverbankEmergenceV1.unity`。菜单 **五行世界 → V1 → 打开河岸涌现关卡**。Play运行，点击Game画面获取键盘焦点；需要大画面时使用 Unity 的 Shift+Space 放大 Game 面板。旧场景与旧脚本保留。

## 操作

| 输入 | 功能 |
| --- | --- |
| WASD／方向键 | 人物移动 |
| 渡过时段按钮 | 唯一推进环境的入口，早晨→中午→傍晚→次日早晨 |
| Tab | 打开／关闭五行独立页 |
| 1…5 | 金、木种子、水、火、土养分 |
| 近处鼠标点击／E | 投放，距人物3.2单位内 |
| Q／工具页降雨 | 安排人物附近5×5格下一时段降雨 |
| 工具页垫高／挖低 | 每次地形高度±0.2，与土养分独立 |
| 鼠标移到村庄 | 人口、按规模36／49／64格范围、资源库存、健康、动物和污染面板 |
| R／重开 | 恢复初始地图与居民 |

先按提示渡过一个时段，然后到黄色标记 (7,6) 播种。选择每次1份水，适量补土，再渡过时段；木量3变树、8变树林。投水过多可能积水，种子在水量≥5时暂不建立。村庄需要的木材从树采伐，金材从矿点开采，不能用草替代木材。

教学通关需完成六步提示，并使繁荣≥65持续5时段。随后可以继续自由沙盒，不设倒计时失败。

## 修改入口

- `Assets/Scripts/EmergenceV1/V1State.cs`：集中数据、参数、手动时间入口。
- `V1Ecology.cs`：3×3密度、木生长、金富集、死地、火和污染。
- `V1Hydrology.cs`：四邻格流动、泥沙搬运、侵蚀、沉积。
- `V1Terrain.cs`：两层梯度Perlin Noise高度生成；平原、坡地、山地分类。
- `V1Settlements.cs`：按规模读取36／49／64格、资源生产、扩建、减员迁移、繁荣。
- `V1Animals.cs`：动物生境、取食、迁移、繁殖。
- `V1World.cs`：Tilemap读数、人物投放、数据到Prefab／碰撞的显示。
- `V1HUD.cs`：运行时文字与按钮事件；布局在场景 **V1 Canvas** 中编辑。
- `Assets/World/EmergenceV1/Editor/V1SceneBuilder.cs`：首次创建新场景；已有场景不会整张重建。

编辑地图时，在Ground画地面，在River bed markers标河床，在Upstream sources／Downstream outlets标源出口，在Bridge markers标可通行桥，在Rock barriers画障碍。普通地面使用V1GroundTile存默认属性；特殊点在V1World的Cell Seeds列表集中覆写，树、矿、污染等对象由状态生成。不必每格挂MonoBehaviour。

## 验证

2026-10-15：升级游戏Canvas，新增结束结算、坐标投放、8个多项称号及历史称号列表；时段图片移除，教学在玩家种出的树长成后隐藏。HasPlaced／HasMoved为本局累计bool，仅重开清零。见 [界面与称号说明](UIAndHonors-20261015.md)。

2026-10-15：鸟群改为像素燕子Sprite动画；雨改为全图，增加春雷雨／夏大旱／冬雪的季节事件结算与雨雷雪表现，连续干旱增加自然起火，火可直接清理无植被污染格，并加入土壤保水防止全图雨集中淹地。见 [事件与火净化说明](EventsAndFire-20261015.md)。

2026-10-15：增加春季鸟群迁移，每只落地鸟独立有30%概率带来1粒种子；统一玩家播种、树木散播和鸟群带种的每格2粒上限，多余种子每粒转为0.5土养分。鸟群概率与季节接入、动画和验证说明见 [鸟群与种子规则](BirdsAndSeeds-20261015.md)。

2026-10-12 地表显示修订：移除RiverEmbankment，包括桥面淤积；平原矿改为新矿脉，坡地／高地矿用露矿，枯竭后恢复地表。三种地形改为共同世界纹理坐标、按真实高度渐变混合的连续地表，见 [地形过渡与矿物显示记录](PresentationFixes-20261012.md)。最新实际Unity Play报告为 `Verification/Presentation20261012/UnityPlay/report.json`，`connectedTerrainAndOre=true`、运行错误0。

2026-10-12：Perlin生成地形与三种地形Sprite、湿树林燃烧／普通火传播、河道下游输送／植被岸阻力、沉积高度守恒、水下矿物显示已更新，见 [地形与生态修复记录](TerrainFixes-20261012.md)。规则检查454749次断言通过；实际Unity Play报告为 `Verification/Terrain20261012/UnityPlay/report.json`，运行错误0。实际场景连续运行到第751天，最大积水陆地52／898格，最终50格。地形种子、频率、振幅、分类阈值可在V1World的Settings中调整。

2026-10-11：村庄采木、污染与环境统计范围统一为小型36格、中型49格、大型64格，迁移保留原村编号和规模，分村同步更新原村规模，禁止在现有村庄范围内生成中心。第六天原村变化的复现原因与验证见 [村庄修复记录](VillageFixes-20261011.md)。规则检查437042次断言通过；最新实际Unity Play报告为 `Verification/Villages20261011/UnityPlay/report.json`，`villageRegionsAndContinuity=true`，运行错误0。

2026-10-10：水面、污染水、水平桥面、死地粒子与火传播平衡已更新，见 [本轮修复记录](PolishFixes-20261010.md)。规则检查 437002 次断言通过；该轮真实 Play 报告为 `Verification/Polish20261010/UnityPlay/report.json`，`waterBridgeFirePolish=true`，运行错误 0。火参数以2026-10-12修复记录为准。

2026-10-10 验收修复已安装到上述 Unity 项目与已有场景。逐项变化、复测方法与数值口径见 [验收修复记录](AcceptanceFixes-20261010.md)。本次规则检查通过 437000 次断言；真实 Unity Play 回归与原教学通关均通过，运行错误为 0。最新报告为 `Verification/Acceptance20261010/UnityPlay/report.json`，其中 `acceptanceRegressions=true`。

`Checks/Verify.ps1` 是无Unity依赖的规则检查；`Verification/core-report.txt` 保存结果。`Verification/UnityPlay/report.json` 和 PNG 保存真实Play结果。所有运行测试均通过，运行错误0。

`InstallAndVerify.ps1` 复制本目录新脚本到项目并放置一次性测试请求；要重复验证需主动运行此脚本。它记录已安装文件哈希，有用户修改时停止覆盖。Editor桥只在请求存在时自动测试，普通打开项目不会自行推进时间或运行检查。

完整规则与本次补充见 `../Design/河岸繁荣-V1实现与补充规则.md`。这是可玩的首版，数值仍需平衡；动物使用单帧像素图，每局随机生境路线、局部游走与停顿，村民暂为聚合数据，泥沙与水流为简化模型。

真实性能记录：编辑器中单时段环境结算最高约22.85毫秒（Stopwatch，不包含显示刷新）。`Verification/UnityPlay/v1-profiler.data` 是保存的Unity Profiler录制。自动标记提取没有取得有效值，因此报告中Profiler数值0代表不可用，不能当成零耗时或帧率承诺。

2026-10-16：四季及雷雨／雪／风共七首循环音乐、以生态平衡计算的繁荣、每局随机地形与初始生态、污染助燃风险已实现，见 [音乐与繁荣平衡](MusicAndBalance-20261016.md)。自然空转仍有机会繁荣；抽样24局100天中4局曾达成，属于样本观察，不是固定成功率。

2026-10-16 后续：完整内部地图生成取代固定河道底图，只有外围边界固定；镜头拉近、M状态页、透明HUD及教程完成后隐藏说明已实现，见 [视野与随机开局](HUDAndWorldGeneration-20261016.md)。上一段4/24为旧生成器观察，不能用于新版自然繁荣概率。
