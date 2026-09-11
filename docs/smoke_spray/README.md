# 齿轮面板 × 烟雾喷出特效(资产说明 + 触发接口)

> 2026-09-11 落地。齿轮机构特写场景图 + AI 生成的"烟雾喷出"序列帧已结合成可用场景,
> 并留好运行时触发接口 `SmokeSprayTrigger`。

## 资产清单

| 资产 | 路径 | 说明 |
|------|------|------|
| 齿轮面板特写图 | `Assets/_Project/Resources/Closeups/gear_mechanism_closeup.png` | 1920x677,喷口红标定稿在像素 (903,393),`_white` 为白底版 |
| 烟雾序列帧(34 帧) | `Assets/_Project/Art/AIAnimations/SmokeSpray/smoke_000~033.png` | 480x204@PPU100,已做绿幕溢出抑制(源脚本 `tools/ai_video/despill_smoke.py`) |
| 动画 Clip | `Assets/_Project/Art/AIAnimations/_Controllers/Smoke_Spray.anim` | 34 帧 @24fps ≈ 1.42s,loopTime=true(首末帧全空,无缝循环) |
| Animator Controller | `Assets/_Project/Art/AIAnimations/_Controllers/Smoke_Spray.controller` | 单状态 `Spray` |
| 调试场景 | `Assets/_Project/Scenes/SmokeSprayDebug.unity` | 纯烟雾,1.6x 主实例 + 0.8/0.5/0.3x 尺寸参考 |
| 结合场景 | `Assets/_Project/Scenes/SmokeSprayInScene.unity` | 齿轮面板 + 喷口摆位定稿(缩放 0.45 / 倾角 -8.9°),已入 Build Settings |
| 摆放组件 | `Assets/_Project/Scripts/Utils/SmokeSprayEmitterAim.cs` | 喷口轴心摆放 + 场景内拖拽手柄(编辑期工具) |
| **触发接口** | `Assets/_Project/Scripts/Utils/SmokeSprayTrigger.cs` | 运行时控制喷烟,见下 |

## 触发接口 SmokeSprayTrigger

挂在 `SmokeEmitter` 物体上(和 `SmokeSprayEmitterAim` 同一物体),命名空间 `LostGoddess.VFX`。

### 代码调用(内容层 B 用这个)

```csharp
using LostGoddess.VFX;

var spray = GetComponent<SmokeSprayTrigger>();

spray.PlayOnce();     // 喷一次:播完(≈1.42s)自动停并隐藏
spray.PlayLooped();   // 持续喷:clip 自循环,直到 Stop()
spray.Stop();         // 停止并隐藏
bool playing = spray.IsPlaying;

spray.onSprayFinished.AddListener(OnSprayDone);  // 喷完回调(如解锁机关 / 推剧情)
```

### 不写代码

Inspector 上有 `onSprayStart` / `onSprayFinished` 两个 UnityEvent,
把别的物体的事件直接拖上去即可(如按钮点击 → `PlayOnce`)。

### 参数

| 字段 | 默认 | 说明 |
|------|------|------|
| `mode` | OneShot | OneShot=喷一次;Loop=持续喷 |
| `autoPlayOnStart` | false | 启用即播。调试场景为 true;**正式玩法一律 false**,由谜题/剧情调 `Play()` |
| `hideWhenIdle` | true | 没在播时隐藏烟雾精灵(免得首帧空图挂着) |
| `smokeAnimator` | 自动找子物体 | 烟雾动画所在 Animator,一般留空 |

### 已接好线的地方

- `SmokeSprayInScene.unity`:SmokeEmitter 上已挂好(调试用 Loop + autoPlayOnStart)。
- `SmokeSprayDebugBuilder.BuildInScene()`:重新生成场景时自动挂上,接口不丢。

## 摆位定稿参数(勿随意改)

```csharp
EmitPxX = 903, EmitPxY = 393   // 喷口在齿轮面板图上的像素位置
SmokeScale = 0.45f             // 烟雾整体缩放
EmitAngleDeg = -8.9f           // 负角=往左上喷(符号约定见 SmokeSprayDebugBuilder 注释)
```

在场景里手动微调后,用菜单「失落的女神 ▸ AI 动画 ▸ 烟雾:复制当前摆放参数到剪贴板」导出常量。

## 效果预览

见本目录:`齿轮面板_烟雾预览.gif`、`unity实拍_预览.gif`、`烟雾喷出_预览.gif`。
