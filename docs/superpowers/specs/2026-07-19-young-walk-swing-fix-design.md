# 老中青年 Walk 摆动修正设计

## 问题
- 青年 `young_Walk.anim` 右上臂（`bone_4/5`）摆动过大。
- 中年 `middle_Walk.anim` 手臂过大，且大腿存在 ±180° 越界插值。
- 老年 `old_Walk.anim` 是 A 手 K 的动画，手臂/手杖摆幅偏大，需与中青年统一压幅。

根因（中青年）：
- `YoungAnimationGenerator.cs` / `MiddleAnimationGenerator.cs` 生成关键帧时，对 `restEuler.z + offset` 直接做 `NormalizeAngle` 后写入。当休息角度接近 ±180° 并叠加 offset 越界时，关键帧会跳到角度另一边（如 `155°` ↔ `-177°`），导致插值走大圈、肢体剧烈甩动。
- 手臂摆动幅度对青年/中年体型都偏大。

## 方案

### 青年
修改 `Assets/_Project/Scripts/Utils/Editor/YoungAnimationGenerator.cs`：

1. **修复角度越界**：在写入 `AnimationCurve` 前，对同一骨骼的一组关键帧做 unwrap。若相邻值差超过 180°，则加减 360°，保证曲线连续。
2. **分部位振幅倍率**：
   - `ArmSwingScale = 0.4f` — 作用于 `bone_2/3/4/5`（双臂）
   - `LegSwingScale = 1.0f` — 作用于 `bone_6/7/8/9`（双腿）
   - 根骨骼 `bone_1` 的轻微起伏/扭动不受影响。

### 中年
修改 `Assets/_Project/Scripts/Utils/Editor/MiddleAnimationGenerator.cs`：

1. **同样修复角度越界**（新增 `UnwrapAngles`）。
2. **分部位振幅倍率**：
   - `ArmSwingScale = 0.5f` — 作用于 `bone_4/5/13/14`（双臂）
   - `LegSwingScale = 1.0f` — 作用于 `bone_6/7/8/9/15/16`（双腿）

### 老年
老年没有生成脚本，直接对 A 手 K 的 `old_Walk.anim` 做振幅缩放：

- 手臂 `bone_12` / `bone_16`（含子骨 `bone_13`、`bone_17/18`）摆幅乘以 **0.6**。
- 手杖 `bone_14` / `bone_15` 的旋转与位移摆幅乘以 **0.6**。
- 腿部 `bone_6/9` 保持不变。

## 预期结果

### 青年
- `bone_4` 摆动从 ±14° 降到约 ±5.6°。
- `bone_5` 摆动从 ±6° 降到约 ±2.4°。
- `bone_8/9` 保持当前幅度不变。
- 角度越界修复后，`bone_4` 不再出现 `155°` ↔ `-177°` 大圈插值。

### 中年
- `bone_4/13` 大臂摆动从 ±10° 降到约 ±5°。
- `bone_5/14` 小臂摆动从 ±5° 降到约 ±2.5°。
- 腿部 `bone_6/7` 的 ±180° 越界插值被修复。

### 老年
- 手臂/手杖整体摆幅降到原来的 60%。
- 腿部节奏与幅度不变。

## 验证
1. 中青年：Unity 菜单分别运行对应生成菜单重新生成。
2. 老年：无需运行菜单，直接 Play Sandbox 观察。
3. Play Sandbox：
   - 序章/老年形态：观察老头手臂、手杖摆动
   - 按 `1` 切青年，观察手臂
   - 按 `2` 切中年，观察手臂与腿部
4. 如仍偏大：
   - 中青年：调整脚本里的 `ArmSwingScale` / `LegSwingScale` 后重新生成。
   - 老年：告诉我哪根骨头还想压，我继续手动调。

## 影响范围
- `YoungAnimationGenerator.cs` + `young_Walk.anim`
- `MiddleAnimationGenerator.cs` + `middle_Walk.anim`
- `old_Walk.anim`
- Idle 动画均不变。
