# 五行局部环境原型

目标 Unity 版本：2022.3.62f2c1。将三个 `.cs` 文件放进 `Assets/Scripts`，将 `GameRuler` 挂到场景中的空物体。

## 操作

1. Inspector 的 `Initial Elements` 配置坐标、类型和数量；坐标从 `(0,0)` 开始。
2. 进入 Play 模式，打开 Scene 视图的 Gizmos。默认网格为 7×3，木位于 `(3,1)`，周围金量为 2+3、水量为 8。
3. 组件菜单中的 `Simulation/Step once` 推进一步；`Resume` 连续运行；`Pause` 暂停；`Reset from initial elements` 恢复初始布局。
4. 勾选 `Log Each Step`，在 Console 查看每步的坐标和数量。Gizmos 是调试预览，不是正式游戏画面。
5. 修改初始布局后使用 Reset 重新加载。清除 `Start Paused` 可在进入 Play 模式时自动运行。

## 数据与规则

- 每格可同时保存金、木、水、火、土五种数量。
- `GetEnvironment(x,y)` 只汇总上下左右四格，不包含对角格和自身格。
- 计算反应时额外加入自身格的元素，因此同格内的不同元素也能反应。
- 五行相克：金克木、木克土、土克水、水克火、火克金。
- 基础反应速率 = 克制方数量 × Reaction Rate ÷ 抵抗系数。
- 抵抗系数 = `max(1, 自身数量 / 克制方数量)`；自身数量较大时，初期变化较慢。
- 金对木额外乘上持续切割倍率，倍率随连续作用时间增加，上限由 `Max Metal Multiplier` 控制。金的影响中断后，切割积累清零。
- 水促进已有木的生长，速率为附近水量 × Wood Growth Rate。木量为零时不会自行复活。
- 非整数变化通过余量累积；每一步读取旧状态，统一提交新状态，避免更新顺序影响反应。
- 生长和损伤同时结算，数量限制为 `0..Max Amount Per Element`。
- 这一版是环境影响模型：克制方不会因为施加影响被扣除数量，生长也暂不消耗水。不包含资源搬运、水流、蔓延、通行判断或胜负条件。

## 对外接口

```csharp
ruler.GetCount(3, 1, Element.Wood);       // 查看数量
ruler.GetEnvironment(3, 1);              // 获取邻格快照
ruler.GetElement(3, 1, Element.Wood);     // 获取状态副本
ruler.TrySetCount(3, 1, Element.Gold, 5); // 暂停时修改，返回是否成功
ruler.StepOnce();
ruler.ResumeSimulation();
ruler.PauseSimulation();
ruler.ResetSimulation();
```

`TrySetCount` 的修改只影响当前模拟；Reset 会恢复 Inspector 中的 Initial Elements。调用 `StepOnce` 会暂停连续运行。`BaseElement` 是普通数据类，只有 `GameRuler` 挂到 GameObject。

编译检查使用本机 Unity 程序集，逻辑检查使用最小 Unity 桩，覆盖 25 项断言；没有在 Unity 编辑器中实际进行场景试玩。检查脚本为旁边的 `PrototypeChecks/Verify.ps1`，不会复制进 Unity 项目。
