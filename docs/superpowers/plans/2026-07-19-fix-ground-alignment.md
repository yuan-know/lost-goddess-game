# 修复各房间人物浮空 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 修正 `SceneRoomBuilder` 各 `SceneDef` 的 `groundFromBottom`/`groundY`，让老人脚底精确踩在画面地平线上；从用户报告的梯子密室开始，并提供运行时校准工具便于逐一检查其余房间。

**Architecture:** 统一公式 `groundY = -5 + worldHeight * groundFromBottom`（相机视口底 Y=-5，背景图底贴视口底）。`SandboxBootstrap` 的 `[ / ]` 微调键和 `P` 打印键改为基于当前房间真实 `groundFromBottom` 输出建议值，而不是硬编码 0.13。

**Tech Stack:** Unity 2022.3, C#。

---

## Task 1: 让 `SceneRoomBuilder` 暴露当前房间的 `groundFromBottom`

**文件：**
- Modify: `Assets/_Project/Scripts/Content/SceneRoomBuilder.cs`

- [ ] **Step 1: 在 `SceneRoomBuilder` 类中加静态字段**

```csharp
public static SceneDef LastBuiltDef { get; private set; }
```

- [ ] **Step 2: 在 `Build(SceneDef def)` 开头赋值**

```csharp
public static GameObject Build(SceneDef def)
{
    LastBuiltDef = def;
    ...
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Content/SceneRoomBuilder.cs
git commit -m "feat(scene): 暴露 LastBuiltDef 供运行时地平线校准使用"
```

---

## Task 2: 修复 `SandboxBootstrap` 校准打印逻辑

**文件：**
- Modify: `Assets/_Project/Scripts/Utils/SandboxBootstrap.cs`

- [ ] **Step 1: 修改 `PrintGround()`**

替换为：

```csharp
void PrintGround()
{
    var def = SceneRoomBuilder.LastBuiltDef;
    float basePct = def != null ? def.groundFromBottom : 0.13f;
    float suggested = basePct - _groundOffsetPct;
    Debug.Log($"[Sandbox] {GameState.CurrentRoom} 累计微调 {(_groundOffsetPct>=0?"+":"")}{_groundOffsetPct*100f:F0}%, " +
              $"当前 groundFromBottom={basePct:F3}, 建议改为 {suggested:F3} " +
              $"(groundY={(-5f + 12f * suggested):F2})");
    Flash($"建议 groundFromBottom={suggested:F3} groundY={(-5f + 12f * suggested):F2}");
}
```

- [ ] **Step 2: 修改 `NudgeGround()` 的 Flash 提示**

替换为：

```csharp
void NudgeGround(float deltaPct)
{
    var scene = GameObject.Find("Room_" + GameState.CurrentRoom);
    if (scene == null) { Flash("当前不是美术场景,无法微调"); return; }
    float dy = 12f * deltaPct;
    foreach (var sr in scene.GetComponentsInChildren<SpriteRenderer>())
    {
        var p = sr.transform.position; p.y += dy; sr.transform.position = p;
    }
    _groundOffsetPct += deltaPct;

    var def = SceneRoomBuilder.LastBuiltDef;
    float basePct = def != null ? def.groundFromBottom : 0.13f;
    float suggested = basePct - _groundOffsetPct;
    Flash($"地平线 {(_groundOffsetPct>=0?"+":"")}{_groundOffsetPct*100f:F0}% → 建议 groundFromBottom={suggested:F3}");
}
```

- [ ] **Step 3: 运行验证**

进任意美术房间（如按 `8` 进 Foyer），按 `[`/`]` 微调，按 `P`，确认 HUD 显示的建议值基于当前房间真实 `groundFromBottom`。

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Utils/SandboxBootstrap.cs
git commit -m "fix(sandbox): 地平线校准打印基于当前房间 groundFromBottom"
```

---

## Task 3: 修正梯子密室（LadderChamber）地平线

**背景：** 用户截图显示老人在 LadderChamber 明显浮空，脚底离地约角色身高 1/4。

**文件：**
- Modify: `Assets/_Project/Scripts/Content/SceneRoomBuilder.cs`

- [ ] **Step 1: 调整 `LadderChamber` 的 `groundFromBottom` 与 `groundY`**

将：

```csharp
public static readonly SceneDef LadderChamber = new SceneDef
{
    ...
    groundFromBottom = 0.26f,       // 实测图上地平线在图底 26% 处(祭台底 = 地面)
    groundY = -3.44f,
    ...
};
```

改为：

```csharp
public static readonly SceneDef LadderChamber = new SceneDef
{
    ...
    // 2026-07-19 修正:原 0.26 导致老人踩在祭台上方,实际地面更靠近图底
    groundFromBottom = 0.10f,       // 祭台底/地面约图底 10% 处
    groundY = -3.80f,               // = -5 + 12*0.10
    ...
};
```

**说明：** 该值为基于截图的初步估计；用户可用 `[ / ]` 微调后按 `P` 获得精确值。

- [ ] **Step 2: 同步修正复用 LadderChamber 美术的 `UpperChamber`**

`PrologueUpperChamberScene` 使用 `SceneRoomBuilder.LadderChamber` 作为美术。由于它复用同一张背景，地平线应保持一致，无需额外修改。

- [ ] **Step 3: 运行验证**

按 `8` 进 Foyer，左 Portal 进 LadderChamber，观察老人脚底是否贴地。若仍浮空，按 `]` 降低地面（或 `[` 抬高），按 `P` 记录建议值。

- [ ] **Step 4: Commit**

```bash
git add Assets/_Project/Scripts/Content/SceneRoomBuilder.cs
git commit -m "fix(scene): LadderChamber 地平线校正,老人不再浮空"
```

---

## Task 4: 检查并修正其余密室

**目标：** 对 `SceneRoomBuilder` 中其余 `SceneDef` 做一致性检查，把 `groundY` 统一为公式值，并标出需要用户目视确认的房间。

**文件：**
- Modify: `Assets/_Project/Scripts/Content/SceneRoomBuilder.cs`

- [ ] **Step 1: 一致性修复**

对以下 `SceneDef`，把 `groundY` 改为 `-5 + 12 * groundFromBottom`（worldHeight=12）：

| SceneDef | 当前 pct | 当前 groundY | 应改 groundY | 备注 |
|---|---|---|---|---|
| `TempleChamber1F` | 0.28 | -3.44 | -1.64 | Chamber3 用，需用户确认是否浮空 |
| `PrologueUpperHall` | 0.12 | -3.44 | -3.56 | 接近，基本不影响 |
| `PrologueChamber2` | 0.25 | -3.44 | -2.00 | Chamber2 用，需用户确认 |
| `GearRoom` | 0.16 | -3.44 | -3.08 | 前厅右1，需用户确认 |
| `LadderChamber` | 0.10（已改） | -3.80 | -3.80 | 本次修正 |

保持不变的（已一致）：
- `DarkForest` 0.13 → -3.44
- `TempleEntry` 0.13 → -3.44
- `TempleGate` 0.13 → -3.44
- `TempleFoyer` 0.13 → -3.44
- `StatueRoom` 0.08 → -4.04

- [ ] **Step 2: 更新注释**

在每个修改的 `SceneDef` 注释里加 `2026-07-19 一致性修正` 并说明是基于公式计算，待运行时校准。

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Content/SceneRoomBuilder.cs
git commit -m "refactor(scene): 统一 SceneDef groundY 公式值,便于逐房校准"
```

---

## Task 5: 提供用户校准清单

**不改代码，只输出检查步骤。**

- [ ] **Step 1: 运行并进入各房间**

| 快捷键 | 房间 | 重点看 |
|---|---|---|
| `7` | Prologue_Woods | 荒山地面 |
| `8` → 右 Portal | Prologue_GearRoom | 齿轮骨骸间地面 |
| `8` → 右 Portal ×2 | Prologue_StatueRoom | 雕像基座底 |
| `8` → 左 Portal | Prologue_LadderChamber | 祭台底（本次已修） |
| `8` → 左 Portal ×2 | Prologue_Chamber3 | 洗礼池地面 |
| `9` | Prologue_UpperHall | 二楼回廊地面 |
| `-` | Prologue_Chamber2 | 棺材密室地面 |
| `=` | Prologue_Chase | 门厅地面（复用 TempleFoyer） |

- [ ] **Step 2: 校准方法**

1. 进入房间，看老人脚底是否贴地。
2. 若不贴地，按 `[`（地面抬高/人物下沉）或 `]`（地面降低/人物浮起）微调。
3. 按 `P`，HUD 会显示建议的 `groundFromBottom` 和 `groundY`。
4. 把建议值告诉我，我更新到 `SceneRoomBuilder.cs`。

- [ ] **Step 3: 收集反馈**

用户把每个房间按 `P` 后的建议值发给我，我再发一个 commit 统一更新。

---

## Self-Review

1. **Spec coverage:** 用户要求"梯子密室人物浮空，其他密室也检查"——本计划先修 LadderChamber，再提供工具让用户能自行校准其余房间。
2. **Placeholder scan:** 无 TBD/TODO；LadderChamber 的 0.10 是截图估计值，计划中明确说明需运行时验证。
3. **Type consistency:** 只改 `SceneDef` 的 float 字段和 `SandboxBootstrap` 的打印逻辑，无接口变化。
