# 背包 UI 调试场景(新版格子美术) · 2026-09-14

## 1. 这次做了什么

用新美术 `背包空白格子.psd` 换了背包底图，并把**现阶段游戏里真正能拿到的 9 件道具**
按「内容框居中 + 等比缩放」摆进 4×4 格子，单独做了一个调试场景评审大小与位置。

★ **调试场景 = 真实游戏场景 + 真实 `InventoryUI`，不是另写一套预览**：
- 背景走 `SceneRoomBuilder.Build(SceneDef)` → 真房间美术（默认 序幕·神庙门厅 TempleFoyer）
- 相机构型与 `SandboxBootstrap` 完全一致（ortho 6 + letterbox rect）
- 背包面板直接由 `GameManager` 建出的**正式 `InventoryUI`** 显示
- 道具尺寸/位置算法与可调参数都在 `InventoryPanelLayout`，游戏读的也是它
  ⇒ HUD 上调出来的值就是游戏里生效的值，不会出现「调试好看、进游戏不对」

因此**正式 `InventoryUI` 已经一并改成新底图 + 新算法**（不再是硬编码格位）。

| 产出 | 路径 |
|---|---|
| 新底图（Sprite） | `Assets/_Project/Resources/UI/Inventory/bag_panel_v2.png` |
| 版式单一真源 | `Assets/_Project/Scripts/Core/UI/InventoryPanelLayout.cs` |
| 调试场景 | `Assets/_Project/Scenes/InventoryDebug.unity` |
| 场景行为 | `Assets/_Project/Scripts/Utils/InventoryDebugBootstrap.cs` |
| 菜单生成 | 失落的女神 ▸ 生成背包 UI 调试场景 |
| 生成/量测工具 | `tools/inventory/*.py` |
| 几何数据 | `tools/inventory/panel_grid.json` |
| 评审图 | `docs/inventory/背包UI_调试预览.png` 等 |

## 2. PSD 拆解（重要）

`背包空白格子.psd` 是 3400×1200，只有两层：

- **图层 1** —— 整幅 3400×1200，**纯黑 alpha = 153/255 ≈ 60%**。
  这是美术给的「弹出背包后压暗整个场景」的遮罩层，**不能烘进底图**，
  否则它只会盖住裁切框而不是全屏。调试场景里用独立的全屏 `DimBackground`
  还原，alpha 取 `InventoryPanelLayout.DimAlphaFromArt = 153/255`。
- **背包空白格子** —— 背包本体（含右侧挂的油灯），bbox `(1092,208)-(2173,1076)` = 1081×868。

底图 = 只取第二层，外扩 20px 裁切 → **1121×908**。
另外清掉了 9 块生成残留（背包下方 y≈870 处一串 alpha 21~84 的细线，最大 216×7）。

## 3. 格子几何

羊皮纸格心 = 16 个独立连通块（枝桠把它们完全隔开），逐格实测：

- 列节距 `129.4 / 122.4 / 128.3`，行节距 `147.0 / 153.5 / 143.9` —— **并不等距**
- 逐格中心与「4 列/4 行均值」最多差 **8.4px**（折到屏幕上约 5px，看得见）
- 所以排版真源用**逐格实测中心** `SlotCx[16] / SlotCy[16]`，不用等距公式
- 单格可见羊皮纸内框 = **101.5 × 124.6 px**（16 格均值，用来定道具大小）

## 4. 道具大小的算法

关键点：图标是 500×500 全画布带透明，**自带留白差异极大**
（`gear_big` 内容只占 274px，`wrench` 占 417px），直接 `preserveAspect` 塞进同一个框
会让有的道具显得很小。所以按**内容框（alpha 外框）**归一化：

```
k        = 面板显示高 / 908
占用框    = (101.5*k*fill, 124.6*k*fill)
道具       等比 contain 进占用框（以内容框尺寸算，不是 500×500）
再把内容框中心对齐到该格中心
```

`fill` 默认 **0.70**（2026-09-14 用户定稿:比初版 0.88 整体缩小 1/5）。
每件还能单独乘 `sizeMul` 与平移 `nudge`。

## 5. 调试场景用法

开 Unity → 开 `Assets/_Project/Scenes/InventoryDebug.unity` → Play
（场景没进 Build Settings，直接双击打开即可；也可用菜单重新生成）。
左上角 HUD 实时显示所有参数。

- `Tab / Shift+Tab` 选件；**方向键**微调选中道具位置（`Shift` = 0.25px 细调）
- `[ / ]` 选中道具单独缩放；`- / =` 全部道具统一大小
- `, / .` 背包整体大小；`; / '` 遮罩 alpha
- `T` 格子框 + 中心点（**检查"是否居中"就看这个**）
- `A` 追加未实装道具（提灯 / 放大镜）对比观感
- `B` 开关背包面板（= 游戏里点背包按钮）
- `Y` 换真实房间背景（神庙门厅 / 大殿 / 餐厅 / 武器室 / 密室2）
- `P` 隐藏整个背包 UI 层看纯场景
- `L` 把当前参数打印到 Console（定稿照抄）；`R` 复位

## 6. 已经改好的正式接入

`InventoryUI.cs`：

1. 底图 `UI/Inventory/slot_empty` → `InventoryPanelLayout.PanelResourcePath`（`bag_panel_v2`）
2. 面板从「拉伸填满条带」改成 **按高度等比缩放 + 屏幕居中**（`PanelHeightRatio`）
3. 删掉 `kSlotColX/kSlotRowY/kSlotW/kSlotH` 与 `GetIconPath()` 那套硬编码，
   统一走 `InventoryPanelLayout`（格位 → `SlotCenter` / `ComputeSlot`，图标 → 几何表）
4. 新增：`SetOpen(bool)` / `LayoutAll()` / `SetSlotDebugOutline(bool)`；`Instance.IsOpen`
5. 遮罩 alpha：`0.75` → `DimAlphaFromArt`（0.60，= 美术 PSD「图层 1」）
6. 条带高度：硬编码 `0.2018` → `LetterboxOverlay.BarHeightPct`（`0.1863`）
   ⚠ 这会同时挪动背包按钮 / 关闭按钮 / 底部描述条的位置，**属于修 bug**，但请看一眼有没有撞边

参数微调不需要改代码：调试场景 HUD 调完按 `L` 打印，把值抄进 `InventoryPanelLayout` 的默认值即可。

## 7. 顺带发现的几个问题（未改，等你定）

1. **`hex_wrench.png` 和 `wrench.png` 是同一个文件**（md5 完全相同）。
   「六角扳手」和「铸铁扳手」在背包里会长得一模一样，美术那边要补图。
2. **正式 `InventoryUI` 的 `BarPct` 硬编码 0.2018，而 `LetterboxOverlay.BarHeightPct`
   是 0.1863** —— 两个数不一致，条带位置会对不上，接入时统一成一个。
3. 图标是**白底抠图**，透明区 RGB 是纯白、半透明边缘被白底洗成灰
   （实测 `crowbar` 软边均值 RGB 124 灰，实体是 93,61,46 棕）。
   放在羊皮纸上会有一圈灰晕。项目里 `tools/fix_portrait_fringe.py` 那套「A 类」治法
   可以直接套到图标上，需要的话我单独做一版对比。
4. `lantern` / `magnifier` / `lost_relic` 在 `Items` 里定义了但**代码从没 Add 过**，
   所以没放进默认展示（调试场景按 `A` 可以看提灯/放大镜的观感）。
5. `light_projector` 的图标用的是 `Props/Prologue/prop_projector`（已经是 Sprite 导入）；
   `Closeups/light_projector` 是同一张图但按 Default 导入，`Resources.Load<Sprite>` 拿不到。
