# 河岸繁荣初始界面

2026-10-14，基于用户提供的 `Assets/World/StartEnum.uxml` 构建。

在 Unity 菜单选择 **五行世界 → 初始界面 → 打开初始界面**，或打开 `Assets/Scenes/StartMenu.unity` 后 Play。该场景已设为 Build Settings 的第一个场景，现有河岸关卡第二，原 SampleScene 保留。

## 界面与选择

- 保留 Start、Honor、End 名称并绑定开始、称号、退出；Select 改为 Label，季节选择不再是一个大按钮。左右箭头各为独立按钮，支持循环切换。
- 顺序为春、夏、秋、冬、随机，初始秋。右箭头从秋切到冬，之后随机、春、夏、秋；左箭头反向循环。
- 四张背景直接引用 `Assets/World/WorldArtExpansionV1/Backgrounds/season_spring.png`、`season_summer.png`、`season_autumn.png`、`season_winter.png`。随机在菜单中显示秋景，开始时随机抽取四季之一。
- USS 提供深绿半透明卡片、金色主按钮、描边、悬停、按下、键盘焦点状态和窄屏纵向布局。背景按比例裁切铺满，不拉伸。
- 初始季节通过 RiverbankMenuSession 传入游戏，时段栏显示季节。2026-10-15已接入季节事件：春雷雨、夏大旱、冬雪、春季鸟群等；不自动推进四季。
- 称号面板只展示历史已获得的8种称号中的实际记录；通过游戏结束结算保存，可以一局获得多个。悬停称号显示条件，空列表不展示锁定称号。详见 `../EmergenceV1/UIAndHonors-20261015.md`。
- 编辑器内退出按钮停止 Play，正式运行时调用 Application.Quit。

## 文件

- `Assets/World/StartEnum.uxml`：布局。
- `Assets/World/StartMenu.uss`：颜色、按钮、排版；UI Builder 中也能显示默认秋景。
- `Assets/World/StartMenu/StartMenuPanel.asset`：1920×1080 参考尺寸、屏幕适配、主题与字体配置。
- `Assets/Scripts/StartMenu/RiverbankStartMenu.cs`：季节、按钮、背景与面板交互。
- `Assets/Scripts/StartMenu/RiverbankMenuSession.cs`：初始季节和称号记录。
- `Assets/World/StartMenu/Editor/RiverbankStartMenuBuilder.cs`：创建场景、配置背景与 Build Settings。
- `V1HUD.cs`／`V1World.cs`：显示季节、坐标投放、结束结算及本局累计行为标记。

中文字体使用 [Noto Sans CJK SC 官方字体](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf)，[官方许可](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE)保存在 Fonts/LICENSE.txt。原系统动态字体无法为 UI Toolkit 加载字形，已改用随项目提供的字体与固定 SDF 图集，图集、材质作为子资产保存，避免切换场景或退出 Play 时丢失。当前图集覆盖UXML、菜单脚本和称号文案。构建时先检查字符覆盖，确有缺字才恢复源字体引用并补字，不重复修改完整静态图集。

## 验收

实际 Unity Play 报告 `Verification/20261014/UnityPlay/report.json` 为 passed，运行错误 0。验证秋默认值、Select 为 Label、五种选择循环、左右方向、四张素材匹配、随机秋景与实际季节抽取、称号打开／关闭、实际加载河岸关卡、季节传入时间栏、字体图集存在、菜单处于可视范围。实际鼠标点击右箭头确认秋→冬及冬景；退出按钮停止 Play。

检查了小 Game 面板与放大面板的文字、按钮和背景；`menu-final.png` 为最终秋景，其他四季与随机、称号、游戏入口截图保存在同目录。完成后恢复默认秋景初始界面。

原 UXML、Build Settings 与游戏脚本备份位于 `Verification/20261014/Before`。`InstallAndVerify.ps1` 安装源码并提交一次性验收请求，普通打开场景不会自动测试。
