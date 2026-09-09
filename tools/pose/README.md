# 动作捕捉管线:参考视频 → 骨骼动画

用真人/参考视频驱动**项目现有的 2D 骨骼**,替代手工摆关键帧。

## 为什么走这条路

`2026-08-22` 评估过三条路,这是选定的第三条:

| 方案 | 结论 |
|---|---|
| ① AI 视频当序列帧直接播 | ❌ 会重踩三个老坑:抠图白边、脚底抖、角色漂形连带废掉 18 张表情 + 20 个道具图标 |
| ② 改进现有生成器的正弦曲线 | 🟡 能改善,但摆幅仍是手写猜的 |
| ③ **视频 → 姿态估计 → 骨骼曲线** | ✅ **本管线**。只取角度不取像素,所以白边/脚底/一致性三个问题全不存在 |

关键点:**输出的是骨骼旋转角,不是图片**。渲染路线完全不变,`CharacterCutout.shader`、
BottomCenter 锚点、yOffset 落地逻辑一个都不动。

## 装依赖

```bash
python -m pip install mediapipe          # 1.0.1 起支持 Python 3.13
```

模型(44MB,已在 .gitignore,需自己下一次):

```bash
mkdir -p tools/pose/models && cd tools/pose/models
curl -L -O https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/latest/pose_landmarker_heavy.task
```

> MediaPipe 1.0 删掉了老的 `mp.solutions` API,只有 Tasks API,所以必须有 .task 文件。

## 用法

```bash
# Idle(左右肢同相)
python tools/pose/run_pipeline.py --video "参考.mp4" --form young --clip young_Idle --loop --dur 4.0

# Walk(遮挡侧要反相半周期)
python tools/pose/run_pipeline.py --video "走路.mp4" --form young --clip young_Walk --loop --dur 0.8 --mirror-phase
```

然后在 Unity:`失落的女神 ▸ 动作捕捉 ▸ 导入姿态 Clip (JSON)`,选 `tools/pose/out/clip_*.json`。
导入会**先把同名 clip 备份成 `.bak.anim`** 再覆盖,手 K 过的曲线不会丢。

## 四步都在干什么

| 步骤 | 输入 → 输出 |
|---|---|
| `extract_skeleton.py` | `young.psb.meta` 的 `characterData` → 骨架 rest pose(FK 算出每根骨的世界角) |
| `extract_pose.py` | 视频 → 12 个关节的世界角序列 + 每帧 visibility |
| `retarget.py` | 世界角 − rest 世界角 = **骨骼局部角** → 曲线 JSON |
| `PoseClipImporter.cs` | JSON → `.anim` |

角度换算全在 Python,C# 只做写入,方便快速迭代。

## 三个必须做的修正(不做就出问题)

1. **遮挡关节镜像**
   参考视频是侧身站,右臂 `visibility` 只有 **0.35** —— 角度纯属瞎猜。低于 0.6 的关节
   改用对侧关节的摆动量驱动。walk 加 `--mirror-phase` 反相半周期,idle 不要加。

2. **循环闭合**
   首尾帧原始差最多 **2.7°**,直接循环会"抽一下"。做线性去漂移强制末帧 == 首帧
   (现在实测 loopGap 全部 = 0.000)。

3. **切线不归零** ← 这是老动画僵硬的真正原因
   旧的 `YoungAnimationGenerator.cs` 写 `new Keyframe(t, v, 0f, 0f)`,切线全 0,
   骨头速度在每个关键帧归零,所以"每帧顿一下"。这里用中心差分给真实斜率。

## 信息量对比

| clip | 来源 | 每曲线关键帧 |
|---|---|---|
| `young_Idle`(旧生成器) | 手写正弦,5 个时间点 | 3 |
| `young_Idle`(本管线) | 真人视频 97 帧 | **49** |
| `old_Walk`(当年手 K) | 手工 | 228 |

## 已知限制

- **一个关节 = 一根骨的 Z 轴**。2D 骨骼只绕 Z 转,所以正面朝向的视频最好;
  侧身视频的遮挡侧一律靠镜像。
- `JOINT_TO_BONE` 目前只配了 `young`(9 根骨,语义已 FK 验证)。
  middle(13 根)、old(18 根)骨骼数和层级都不同,加之前**必须先跑
  `extract_skeleton.py <form>` 看 FK 输出确认每根骨是哪个部位**,别照抄 young。
- 手头 `Downloads/角色待机循环视频生成*.mp4` 五个都是同一个侧身 idle 镜头;
  其中 `(3)`/`(4)` 检测质量差(4 个关节遮挡、摆幅异常到 90°+),**不要用**。
  想要 walk 得另找正面/侧面的走路参考视频。
