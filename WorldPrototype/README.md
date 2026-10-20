# 五行小世界

Unity 项目：`C:\Users\DLQian\GameJam`，版本 2022.3.62f2c1。

打开 `Assets/Scenes/FiveElementsWorld.unity` 后进入 Play。旧的 `FiveElementsPrototype.unity` 保留为规则与解谜验证场景。

## 操作

- WASD / 方向键：自由移动，镜头跟随。岩石和密集树木阻挡人物；水域当前可以涉水。
- 1–5：选择金、木、水、火、土。E 向人物面前的地块添加 1 份元素（当前为无限资源的沙盒操作）。
- 空格：暂停或继续环境；`.`：推进一步；R：按地块初始配置重置世界和人物位置。

这个场景没有解谜胜负目标，供搭建和探索一个小世界。当前美术是程序生成的占位图。

## 搭建地形

场景层级“​​五行世界 · 地块与环境 / 地形”下，每块地有一个 `ElementTile`，在 Inspector 配置坐标、地形高度、初始金木水火土数量、阻挡标志。Play 时 Inspector 底部显示当前环境的只读数量。

`ElementWorld` 从所有子地块建立统一状态，每秒推进一次规则；地块自身没有 Update，不会各自执行一套模拟。初始值和运行值分开，修改初始值后按“重置世界”生效。整体数量与世界大小、固定频率相关，而不是每个地块每帧遍历整个世界。

场景使用 24×18 的隐藏数据格，实际人物通过 Rigidbody2D 连续移动。可以替换每个地块的渲染子物体及人物 Sprite，不需要改变规则代码。新增地块需要设置唯一坐标，落在世界宽高内；删掉地块形成不能模拟的空缺。较大地图建议改成 Tilemap 或笔刷批量配置，当前组件形式方便初版直接编辑。

`ElementWorld` 的 Rules 可配置更新间隔、木阻挡阈值等。木消耗土、水增长，金伐木，火燃木产生土，水灭火并流向低处，继续使用此前验证过的规则。投放资源通过模拟器 AddResource 统一写入并更新资源账。

菜单“​​五行世界 / 生成或打开简易世界”只在场景不存在时生成，不覆盖已编辑的世界。世界脚本位于 `Assets/Scripts/World`，场景工具和 Inspector 位于 `Assets/World/Editor`，占位图位于 `Assets/World/Art`。

## 验证

`Verification/report.json` 记录实际编辑器 Play 检查：432 个地块、真实 FixedUpdate 移动、岩石物理碰撞、自动环境推进、投放数量与资源守恒、世界重置。规则回归通过 34686 项断言。`Verification/world-preview.png` 是实际 Game 视图截图。

`Checks/CompileUnity.ps1` 对安装的 Unity 程序集编译世界脚本；`Checks/InstallAndOpen.ps1` 安装并生成一次显式验证请求。验证工具平时不自动工作，没有请求文件时不会修改场景或启动 Play。
