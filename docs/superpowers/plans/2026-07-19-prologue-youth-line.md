# 序幕青年线补通 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把序幕中"陶罐钥匙切青年 → 拿铁撬棍 → 二楼回廊取透镜/底座 → 拨齿轮进棺材密室"这一段跑通，并补上门厅的中年组合台，使从切青年到开门剧情杀形成完整可玩链路。

**Architecture:** 完全复用现有 `InteractableBase`/`Cutscene`/`ScenePortal`/`PlayerBuilder` 接口，不新增系统。只在已有场景 Builder 里把注释掉的 helper 调用接回，并修正道具来源与拓扑流向。

**Tech Stack:** Unity 2022.3, C#, 程序化房间构建（无 .unity 场景文件）。

---

## 文件结构

| 文件 | 责任 |
|---|---|
| `Assets/_Project/Scripts/Content/Prologue/PrologueChamber3Scene.cs` | 在密室 3 增加地面铁撬棍拾取物 |
| `Assets/_Project/Scripts/Content/Prologue/PrologueUpperHallScene.cs` | 接回 UpperHall 的四件交互物：铁撬棍地面（备选）/铁笼/齿轮箱/齿轮机关，并加返回二楼密室的 Portal |
| `Assets/_Project/Scripts/Content/Prologue/PrologueFoyerScene.cs` | 生成岁月洞察壁画、二楼高亮提示、中年组合台；展台保持现状 |
| `Assets/_Project/Scripts/Content/Prologue/FoyerInteractables.cs` | 已含 `Interact_Assemble_Middle` 与 `Interact_Stairs`，无需修改 |
| `Assets/_Project/Scripts/Content/Prologue/Chamber3AndUpperHallInteractables.cs` | 已含 `Interact_CrowbarPickup`/`Interact_IronCage`/`Interact_GearBox`/`Interact_Gears`，无需修改 |

---

## Task 1: 在密室 3（Chamber3）放置铁撬棍

**背景：** 青年在密室 3 醒来后，剧本要求"环顾四周，拾取一根铁撬棍"。当前 `PrologueChamber3Scene` 只有陶罐和锁孔，没有铁撬棍。

**文件：**
- Modify: `Assets/_Project/Scripts/Content/Prologue/PrologueChamber3Scene.cs`

- [ ] **Step 1: 在 Build() 中调用 BuildCrowbarPickup**

在 `BuildLockhole` 之后、`BuildBackPortal` 之前插入：

```csharp
// 铁撬棍：青年醒来后在密室地面拾取
BuildCrowbarPickup(room.transform, new Vector2(-5f, groundY + 0.2f), groundY);
```

- [ ] **Step 2: 确认 Interact_CrowbarPickup 逻辑已存在**

`Chamber3AndUpperHallInteractables.cs` 或 `PrologueUpperHallScene.cs` 中已有 `Interact_CrowbarPickup`：

```csharp
public class Interact_CrowbarPickup : InteractableBase
{
    bool _taken;
    public override void OnClick()
    {
        if (_taken) return;
        if (GameState.CurrentEra != Era.Young)
        {
            DialogueSystem.ShowText("地上有根铁棍……但这把老骨头搬不动。");
            return;
        }
        _taken = true;
        InventorySystem.Add(Items.Crowbar);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) { var c = sr.color; c.a = 0.15f; sr.color = c; }
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        DialogueSystem.Show(Dialogues.prologue_2_got_crowbar);
    }
}
```

若 `PrologueUpperHallScene.cs` 里的实现与上述一致，可直接使用；若冲突以 `Chamber3AndUpperHallInteractables.cs` 为准。

- [ ] **Step 3: 运行验证**

运行 Sandbox 场景，按 `9` 进密室 3，确认：
1. 地面出现铁撬棍占位方块。
2. 老年/中年点击显示"搬不动"。
3. 青年点击后 `InventorySystem.Has(Items.Crowbar)` 为 true，方块变淡。

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Content/Prologue/PrologueChamber3Scene.cs
git commit -m "feat(prologue): 在密室3放置铁撬棍，青年可拾取"
```

---

## Task 2: 接回 UpperHall 四件交互物

**背景：** `PrologueUpperHallScene.Build()` 中所有交互物都被注释掉，玩家进入二楼回廊后无事可做。需要按新 `bg_full` 背景摆上：铁撬棍地面（若不在 Chamber3 放则此处放）、铁笼、齿轮箱、齿轮机关，并在左端加返回二楼密室（UpperChamber）的 Portal。

**文件：**
- Modify: `Assets/_Project/Scripts/Content/Prologue/PrologueUpperHallScene.cs`

- [ ] **Step 1: 确定交互物世界坐标**

根据 `UpperHall/bg_full.png` 视觉锚点（3400×1200，PPU=100，世界宽 34，中心原点）：

| 物件 | 建议世界坐标 | 说明 |
|---|---|---|
| 铁撬棍地面 | `(-10f, groundY + 0.2f)` | 楼梯口附近地面，作为找不到 Chamber3 铁撬棍的兜底 |
| 铁笼 | `(-2f, groundY + 1.0f)` | 回廊中段靠墙 |
| 齿轮箱 | `(4f, groundY + 0.8f)` | 回廊中右段 |
| 齿轮机关 | `(10f, groundY + 1.0f)` | 回廊尽头，触发后进 Chamber2 |
| 返回 Portal | `(-15f, groundY + 1.5f)` | 左端回 UpperChamber |

- [ ] **Step 2: 在 Build() 中接回 helper 调用**

替换当前注释块为：

```csharp
// 铁撬棍地面（兜底）：若 Chamber3 没拿到，上楼口还能补一把
BuildCrowbarPickup(room.transform, new Vector2(-10f, groundY + 0.2f), groundY);

// 铁笼：需铁撬棍撬开，获得聚焦透镜
BuildIronCage(room.transform, new Vector2(-2f, groundY + 1.0f), groundY);

// 齿轮箱：青年拽出黄铜底座
BuildGearBox(room.transform, new Vector2(4f, groundY + 0.8f), groundY);

// 齿轮机关：拨动后进棺材密室（Chamber2）
BuildGearsMechanism(room.transform, new Vector2(10f, groundY + 1.0f), groundY);

// 左端 Portal：回二楼密室 UpperChamber
BuildStairsPortal(room.transform, new Vector2(-15f, groundY + 1.5f), groundY);
```

- [ ] **Step 3: 验证交互物依赖**

确认 `Chamber3AndUpperHallInteractables.cs` 中：
- `Interact_IronCage` 检查 `GameState.CurrentEra == Era.Young` 与 `InventorySystem.Has(Items.Crowbar)`，成功后 `InventorySystem.Add(Items.FocusLens)`。
- `Interact_GearBox` 检查青年，成功后 `InventorySystem.Add(Items.BrassBase)`。
- `Interact_Gears` 检查青年，成功后切场景到 `Rooms.Prologue_Chamber2`。

- [ ] **Step 4: 运行验证**

按 `0` 进 UpperHall：
1. 青年形态可见四件交互物 + 左端 Portal。
2. 未持撬棍点铁笼提示"徒手拧不开"。
3. 持撬棍点铁笼获得 `FocusLens`。
4. 点齿轮箱获得 `BrassBase`。
5. 点齿轮机关切到 Chamber2。
6. 左端 Portal 能回到 UpperChamber。

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Content/Prologue/PrologueUpperHallScene.cs
git commit -m "feat(prologue): UpperHall 交互物接回（铁笼/齿轮箱/齿轮机关/返回Portal）"
```

---

## Task 3: 在门厅生成岁月洞察壁画、高亮提示与中年组合台

**背景：** `PrologueFoyerScene` 里 `BuildMuralPhantom`、`BuildUpperHallGlow`、`BuildAssembleTable` 三个 helper 已写好但未被调用。需要把它们接入，使老年 Q 键洞察能看到壁画图腾和二楼高亮，中年能在组合台拼出光幕投影仪。

**文件：**
- Modify: `Assets/_Project/Scripts/Content/Prologue/PrologueFoyerScene.cs`

- [ ] **Step 1: 在 Build() 中调用三个 helper**

在 `BuildEdgePortal` 调用之后、`root.AddComponent<PrologueFoyerDirector>()` 之前插入：

```csharp
// 岁月洞察时才显影的壁画图腾
BuildMuralPhantom(root.transform, new Vector2(-6f, groundY + 2.5f));

// 岁月洞察时高亮提示：二楼坍塌处有可探索物
BuildUpperHallGlow(root.transform, new Vector2(12f, groundY + 3.5f));

// 中年组合台：把 FocusLens + BrassBase 拼成 LightProjector
BuildAssembleTable(root.transform, new Vector2(-4f, groundY + 0.6f), groundY);
```

- [ ] **Step 2: 验证 helper 实现**

`BuildMuralPhantom` 已存在：加载 `Closeups/mural_prologue_v2` 或 `Closeups/mural_prologue`，默认 alpha=0，挂 `InsightPhantom`。

`BuildUpperHallGlow` 已存在：纯色黄光斑，挂 `InsightPulseHighlight`（该组件应在 `InsightHighlights.cs` 中定义）。

`BuildAssembleTable` 已存在：创建方块并挂 `Interact_Assemble_Middle`。

- [ ] **Step 3: 运行验证**

按 `8` 进门厅：
1. 按 `Q` 进入洞察模式，壁画与高亮淡入。
2. 再按 `Q` 退出，两者淡出。
3. 中年形态点击组合台：未持有透镜/底座提示缺件；持有后消耗两件并获得 `LightProjector`。
4. 中年点击展台：放置 `LightProjector` 后设置 `Prologue_ProjectorSolved` 与 `Prologue_DoorOpen`。
5. 点击石门进入 Chase 剧情杀。

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Content/Prologue/PrologueFoyerScene.cs
git commit -m "feat(prologue): 门厅接入洞察壁画、二楼高亮、中年组合台"
```

---

## Task 4: 全链路联调

**目标：** 从 "新游戏 → 第 0 幕 → 门厅 → 左链 → 密室 3 → 切青年 → 回门厅 → 梯子密室 → 二楼密室 → UpperHall → 棺材密室 → 切中年 → 回门厅 → 组合投影仪 → 开门 → Chase" 跑通。

**文件：**
- 不改代码，只验证并修 bug。

- [ ] **Step 1: 准备干净存档**

运行后按 `R` 重置控制，必要时删掉本地存档（`%USERPROFILE%/AppData/LocalLow` 下对应公司名/产品名，或看 `SaveSystem` 实际路径）。

- [ ] **Step 2: 跑主线**

1. 启动 Sandbox 场景。
2. 按 `7` 开始第 0 幕，走完自动切 Gate → Foyer。
3. 在 Foyer 走左 Portal 到 LadderChamber，再左到 Chamber3。
4. 在 Chamber3 打碎陶罐拿到 `PotteryKey`，插锁孔切青年，回 Foyer。
5. 从 Foyer 左 Portal 到 LadderChamber，向上爬梯到 UpperChamber，再右到 UpperHall。
6. 在 UpperHall 拿铁撬棍（如 Chamber3 已拿则跳过），撬铁笼拿 `FocusLens`，齿轮箱拿 `BrassBase`，拨齿轮进 Chamber2。
7. 在 Chamber2 点棺材 3 次切中年，回 Foyer。
8. 在 Foyer 组合台拼 `LightProjector`，展台开门，石门进 Chase。
9. Chase 按 `1` 切青年，跑向右，黑屏后进 Chapter1_Hall。

- [ ] **Step 3: 记录并修复阻塞 bug**

每遇到卡死、对白不推进、黑屏不恢复、角色不能控制，记录复现步骤并修复。常见排查点：
- `Cutscene` 结束后是否恢复 `PlayerController.SetControllable(true)`。
- `GoToSceneStep` 后旧 `Cutscene` 是否 `Stop()`。
- `FadeOverlayColored` 黑幕在 Chapter1_Hall 是否被清掉。

- [ ] **Step 4: Commit 修复**

```bash
git add -A
git commit -m "fix(prologue): 青年线联调，修复 X/Y/Z"
```

---

## Self-Review

1. **Spec coverage:** 序幕脚本.md 第二幕（青年拿撬棍/透镜/底座）、第三幕（棺材切中年）、第四幕（开门剧情杀）均有对应任务覆盖。
2. **Placeholder scan:** 计划内无 TBD/TODO；坐标采用基于 bg_full 的临时建议值，在 Step 中给出具体数字。
3. **Type consistency:** 所有物品/Flag/房间常量均引用 `Utils/Keys.cs`；交互物类名与现有代码一致。
