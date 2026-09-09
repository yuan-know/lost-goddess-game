// ============================================================================
//  AIAnimationBuilder.cs —— AI 视频 PNG 序列 → Unity 动画(Editor 工具)
//  菜单:失落的女神 ▸ AI 动画 ▸ 从 PNG 序列生成 Idle 动画
//  功能:扫描指定目录下的 PNG 序列帧 → 设为 Sprite → 生成 AnimationClip
//        → 生成 AnimatorController(Idle 单状态) → 生成一个纯展示用沙盒场景。
//  沙盒场景只有:相机 + 一个 SpriteRenderer + Animator,不接入任何游戏逻辑。
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostGoddess.EditorTools
{
    public static class AIAnimationBuilder
    {
        const string AIAnimDir     = "Assets/_Project/Art/AIAnimations";
        const string ControllerDir = "Assets/_Project/Art/AIAnimations/_Controllers";
        const string SceneDir      = "Assets/_Project/Scenes";
        const string ScenePath     = SceneDir + "/AIAnimSandbox.unity";
        const string FinalScenePath = SceneDir + "/AIAnim_Final3Forms.unity";
        const string CutoutMatPath = "Assets/_Project/Art/Materials/CharacterCutout.mat";

        /// <summary>
        /// 一个待放进对比沙盒的动画条目。加新形态只需往下面的表里加一行。
        /// </summary>
        struct AnimSpec
        {
            public string frameDir;   // AIAnimations 下的子目录名
            public string clipName;   // 生成的 clip/controller 名
            public string display;    // 沙盒里 GameObject 的名字
            public float fps;         // 播放帧率(见下面的步频说明)
            public int steps;         // 这套帧序覆盖几个步态周期(待机填 0)
        }

        // 想加新形态,在这里加两行即可(目录存在才会被放进场景,不存在自动跳过)。
        //
        // ⚠️ fps 不能三套都填一样 —— 因为**每套覆盖的步数不同**:
        //    青年 26 帧走了 2 步(腿部展开量有 2 个波峰,在帧 0 和 14),
        //    中年 16 帧、老年 12 帧各只有 1 步。
        //    所以决定快慢的是"每秒步数 = steps / (帧数 / fps)",不是 fps 本身。
        //    之前三套都写死 12fps,实际步频是 老年1.00 > 青年0.92 > 中年0.75 步/秒
        //    —— 顺序正好是反的,老头走得最快。
        //
        //    现在按 青年 1.38 / 中年 1.19 / 老年 0.92 步/秒 配置。
        //    这是真人步频(青壮年 1.8-2.0、中年 1.6-1.8、老年 1.2-1.5)的 75%,
        //    刻意比真人慢 —— 按真人值配出来实测偏快。
        //    下限卡在老年:它只有 12 帧,再降 fps 每帧就超 80ms。11fps 是 91ms,
        //    但老年逐帧腿部位移只有 3.0px、只占单腿宽度(40px)的 0.07 ——
        //    远低于"看得出跳跃"的 0.33,所以安全。若还要更慢,得先给老年补帧。
        //    改帧数后必须重算 fps,否则步频就跑偏了。
        static readonly AnimSpec[] AllAnims = {
            new AnimSpec { frameDir = "YoungIdle",  clipName = "Young_Idle_AI",  display = "青年Idle", fps = 12f, steps = 0 },
            new AnimSpec { frameDir = "YoungWalk",  clipName = "Young_Walk_AI",  display = "青年Walk", fps = 18f, steps = 2 },
            new AnimSpec { frameDir = "MiddleIdle", clipName = "Middle_Idle_AI", display = "中年Idle", fps = 12f, steps = 0 },
            new AnimSpec { frameDir = "MiddleWalk", clipName = "Middle_Walk_AI", display = "中年Walk", fps = 19f, steps = 1 },
            new AnimSpec { frameDir = "OldIdle",    clipName = "Old_Idle_AI",    display = "老年Idle", fps = 12f, steps = 0 },
            new AnimSpec { frameDir = "OldWalk",    clipName = "Old_Walk_AI",    display = "老年Walk", fps = 11f, steps = 1 },
        };

        // ------------------------------------------------------------------
        //  青年行走的候选版本(用户 2026-09-08 反馈旧版"眼珠转动太快")
        // ------------------------------------------------------------------
        // 新素材 `raw/young_walk_v3` 79 帧,诊断结论:
        //   步态周期 = 22 帧(腿部自相似矩阵 lag 低谷 0.1805),波峰在帧 1/14/34/56
        //   ⇒ 每 22 帧 1 步,2 步需要 44 帧。
        //
        // ⚠️⚠️ 眼珠转太快的**真正根因 = fps 配高了,不是抽帧**(2026-09-08 二次修正)
        //   用户第一次反馈后,我按"步频与旧版一致(1.38 步/秒)"给 44 帧配了 30fps。
        //   但这是错的 —— 步频一致 ≠ 播放速度一致:
        //     旧素材每步 13 帧 @18fps → 1.38 步/秒
        //     新素材每步 22 帧,要凑 1.38 步/秒就得 30fps
        //   新素材帧更密(每步多 69% 的帧),照抄旧步频等于**把整段视频快放 1.25 倍**,
        //   眼珠自然跟着快 25%。用户说"原视频眼珠转动不快"—— 因为原视频是 24fps。
        //   ⇒ **正解:回到 24fps 原生速率**,眼珠速度就和原视频一致。
        //     代价:步频降到 2/(44/24) = 1.09 步/秒,比旧版慢。
        //     移动速度 moveSpeed 要跟着按 1.09/1.38 = 0.79 下调,否则脚底打滑。
        //
        // 另一个已排除的怀疑:窗口里有没有眼珠猛动的帧。
        //   实测源素材眼区逐帧变化的尖峰在帧 10/39/44/49/58(>1.5x 中位),
        //   **全 79 帧里无论怎么切 22/44 帧窗口都躲不开**(最好的窗口 eyeMAX 仍是 31.96)。
        //   所以躲帧无解,只能靠降 fps 把每帧的时间拉长。
        // 注意:下面多版共用同一个 frameDir —— 帧完全一样,**只有 fps 不同**。
        // 眼珠快慢是播放速度问题,不是帧内容问题,所以不需要为每个 fps 复制一份 PNG。
        static readonly AnimSpec[] YoungWalkVariants = {
            // 旧版(已实装)。26帧 2步 @18fps。眼区变化中位 27.18
            new AnimSpec { frameDir = "YoungWalk",   clipName = "Young_Walk_AI",    display = "青年旧26帧", fps = 18f, steps = 2 },
            // ❌ 30fps —— 用户实测"结束时眼珠还是转很快",就是这一版(快放 1.25x)。留作对照。
            new AnimSpec { frameDir = "YoungWalkV2", clipName = "Young_Walk_AI_V2", display = "青年30fps快放", fps = 30f, steps = 2 },
            // ✅ 24fps = 原视频速率。眼珠速度原汁原味;步频 1.09 步/秒。
            new AnimSpec { frameDir = "YoungWalkV2", clipName = "Young_Walk_AI_V4", display = "青年24fps原速", fps = 24f, steps = 2 },
            // 换了更安静的窗口 [23:67] (legIoU 0.9755 更好),但眼区仍是源素材原动
            new AnimSpec { frameDir = "YoungWalkV4", clipName = "Young_Walk_AI_V6", display = "青年v8换窗24fps", fps = 24f, steps = 2 },
            // ⭐ 换窗口 + 眼区时间中值稳态化(5帧循环窗口)。eyeMed 8.31 < 中年的 8.78
            new AnimSpec { frameDir = "YoungWalkV5", clipName = "Young_Walk_AI_V7", display = "青年v10眼稳24fps", fps = 24f, steps = 2 },
            // ⭐⭐ 2026-09-08 定稿:用户第三次生成的素材 `raw/young_walk_v5`(101帧,眼珠不转)。
            //   SSM 验证:平均匹配 IoU 0.930 / IoU>0.85 帧占 95% / 前后半段都 95%(无漂移),
            //   远优于被弃的 v4(0.81/58%,步态天生左右不对称 → 怎么切都跛)。
            //   真周期 lag=19(用腿部自相似矩阵测,不是腿展开量)。
            //   窗口 **[18:56] 共 38 帧 = 2×19 整数周期**,成片 legIoU 0.9863 / 全身 0.9812,
            //   均优于旧 v7(0.9723/0.9786)。步频 = 2/(38/24) = 1.263 步/秒。
            //
            //   ⚠️ 教训(详见记忆 ai-walk-loop-period-method):
            //   - 周期只能用腿部 SSM,禁用腿展开量自相关(噪声);新源先 SSM 验合格(IoU>0.85
            //     占比>75%、lag 不漂移)再做,不合格当场退回重做,别硬切。
            //   - 重采样救不了不闭合,反而破坏闭合+引入定格帧。
            //   - maxR/cv 不预测抽搐;判据是"窗口闭合 + 整数周期"。
            new AnimSpec { frameDir = "YoungWalkV6", clipName = "Young_Walk_AI_V8", display = "青年新素材38帧24fps", fps = 24f, steps = 2 },
        };

        // ------------------------------------------------------------------
        //  中年行走的三个候选版本(新素材 `raw/middle_walk_v2` 89 帧)
        // ------------------------------------------------------------------
        // ✅ 新素材修好了旧版的**周期歧义**:旧 middle_walk_v1 有 lag63/lag40/lag22
        //    三个候选周期,是"取40帧后重采样导致腿IoU 0.9786→0.62"的根源。
        //    新素材 lag=24 单一低谷(0.1179),腿展开量波峰干净地落在 11/37/60/82。
        //
        // ⚠️ 但新素材带来一个**新问题:身高**。
        //    源素材身高 青年1197 / 中年1194 —— 中年只矮 0.3%,而旧素材差 4.0%。
        //    若沿用统一 scale 0.4093,中年成片会和青年一样高,**年龄感消失**。
        //    所以中年这两版用 **scale=0.3941**(= 0.4093 × 1150/1194)压回旧比例,
        //    成片身高 469-470px vs 青年 489px,比例 0.960 与旧素材一致。
        //    这是本轮唯一一个"不能照抄统一 scale"的例外,改素材时要重算。
        //
        // fps:同青年,**必须用原生 24fps**,不能为了凑旧版 1.19 步/秒去配 27fps。
        //   新素材每步 24 帧,24fps 下步频 = 2/(46/24) = 1.04 步/秒。
        //   27fps 那一版是快放 1.125 倍,眼珠也会跟着快 —— 与青年同一个坑。
        static readonly AnimSpec[] MiddleWalkVariants = {
            // 旧版(已实装)。16帧 1步 @19fps。眼区 14.42,节奏最匀(cv 0.412)
            new AnimSpec { frameDir = "MiddleWalk",   clipName = "Middle_Walk_AI",    display = "中年旧16帧", fps = 19f, steps = 1 },
            // ❌ 27fps 版(快放 1.125x),留作对照
            new AnimSpec { frameDir = "MiddleWalkV2", clipName = "Middle_Walk_AI_V2", display = "中年46帧27fps", fps = 27f, steps = 2 },
            // ✅ 同样 46 帧,fps 降到 24 = 原视频速率。接缝 0.9921(六套最高)/腿 0.9768
            new AnimSpec { frameDir = "MiddleWalkV2", clipName = "Middle_Walk_AI_V4", display = "中年46帧24fps", fps = 24f, steps = 2 },
        };

        // ------------------------------------------------------------------
        //  ⭐ 定稿版:三形态 × (待机 + 行走) = 6 个并排的验收/接入参考场景。
        //  三套 2026-09-08 全部敲定。换源后覆盖对应帧目录、重跑本菜单即可。
        //  行走版本选择理由:
        //    老年 Walk   12帧 @11fps 1步 —— 最弱一环但用户已敲定
        //    中年 Walk   46帧 @24fps 2步 —— middle_walk_v2_46,全身接缝 0.9921 六套最高,
        //                                    左右腿对称;24fps 原生速率不快放
        //    青年 Walk   38帧 @24fps 2步 —— young_walk_v5 窗口[18:56],leg 0.9863,
        //                                    SSM 验证步态对称稳定(详见 YoungWalkVariants 注释)
        //  待机三套均 12fps(呼吸/眨眼类动作,不涉及步频)。
        //  ⚠️ 步频梯度(2026-09-08 用户要求拉开差距、老年不能太快):
        //     青年 2×24/38 = 1.26 步/秒(原生24fps,最快)
        //     中年 2×21/46 = 0.91 步/秒(略压,沉稳)
        //     老年 44帧插值补帧@24fps ≈ 0.55 步/秒(最慢;靠帧密度翻倍实现半速,每帧42ms不卡)
        //     正式实装配 moveSpeed 时位移要匹配各自步频,否则脚底打滑。
        //     老年补帧脚本:tools/ai_video 里 22帧闭合窗→相邻帧50%插值→44帧;
        //     直接降 fps 压慢会顿挫(旧12帧@8fps每帧125ms),不可取。
        // ------------------------------------------------------------------
        static readonly AnimSpec[] FinalAnims = {
            new AnimSpec { frameDir = "YoungIdle",    clipName = "Final_Young_Idle",  display = "青年·待机", fps = 12f, steps = 0 },
            new AnimSpec { frameDir = "YoungWalkV6",  clipName = "Final_Young_Walk",  display = "青年·行走", fps = 24f, steps = 2 },
            new AnimSpec { frameDir = "MiddleIdle",   clipName = "Final_Middle_Idle", display = "中年·待机", fps = 12f, steps = 0 },
            // 中年 21fps(略低于原生24):步频 2×21/46 = 0.91 步/秒,沉稳
            new AnimSpec { frameDir = "MiddleWalkV2", clipName = "Final_Middle_Walk", display = "中年·行走", fps = 21f, steps = 2 },
            new AnimSpec { frameDir = "OldIdle",      clipName = "Final_Old_Idle",    display = "老年·待机", fps = 12f, steps = 0 },
            // 老年行走:44 帧(22 源帧 + 相邻帧 50% 插值补帧),仍 24fps 播放。
            //   关键:插值把帧密度翻倍后,同一段动作在 24fps 下从 0.92s 拉长到 1.83s
            //   = 自然减速一半(步频约 0.55 步/秒,最慢),而每帧只停 42ms、单帧腿部仅动 5px
            //   —— 慢但完全不卡顿。这解决了"降 fps 压慢必然顿挫"(旧 12帧@8fps 每帧125ms)的矛盾。
            new AnimSpec { frameDir = "OldWalk",      clipName = "Final_Old_Walk",    display = "老年·行走", fps = 24f, steps = 1 },
        };

        [MenuItem("失落的女神/AI 动画/⭐ 生成定稿三形态动画场景(待机+行走)", priority = 90)]
        public static void BuildFinal3FormsSandbox()
        {
            if (!EnsureNotPlaying()) return;

            var built = new List<BuiltAnim>();
            foreach (var spec in FinalAnims)
            {
                var b = BuildClipAndController(spec.frameDir, spec.clipName, spec.fps);
                if (b.controller == null)
                {
                    Debug.LogWarning($"[定稿场景] 跳过 {spec.display}:找不到目录 {spec.frameDir} 或无帧");
                    continue;
                }
                b.label = spec.display;
                b.fps = spec.fps;
                b.steps = spec.steps;
                built.Add(b);
            }

            if (built.Count == 0)
            {
                EditorUtility.DisplayDialog("错误",
                    "AIAnimations 下没有找到任何定稿帧目录。", "好");
                return;
            }

            BuildRowSandboxScene(built, FinalScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = new System.Text.StringBuilder();
            msg.AppendLine("定稿三形态动画场景已生成:");
            msg.AppendLine(FinalScenePath);
            msg.AppendLine();
            msg.AppendLine("从左到右(每形态 待机 → 行走):");
            foreach (var b in built)
            {
                var cadence = b.steps > 0 ? $"{b.StepsPerSecond:F2} 步/秒" : "待机";
                msg.AppendLine($"  {b.label}  {b.frameCount} 帧 @{b.fps:F0}fps  {cadence}");
            }
            msg.AppendLine();
            msg.AppendLine("全部套用 CharacterCutout 材质治白边,脚底贴地面参考线。");
            msg.AppendLine("打开场景按 Play 即可查看。");
            EditorUtility.DisplayDialog("定稿三形态动画", msg.ToString(), "好");
        }

        [MenuItem("失落的女神/AI 动画/生成青年行走对比沙盒", priority = 96)]
        public static void BuildYoungWalkCompareSandbox()
        {
            BuildWalkCompareSandbox(YoungWalkVariants, "青年行走");
        }

        [MenuItem("失落的女神/AI 动画/生成中年行走对比沙盒", priority = 97)]
        public static void BuildMiddleWalkCompareSandbox()
        {
            BuildWalkCompareSandbox(MiddleWalkVariants, "中年行走");
        }

        /// <summary>青年+中年全部版本一屏,能直接看出两个形态的身高差有没有保住。</summary>
        [MenuItem("失落的女神/AI 动画/生成青年+中年行走全版对比沙盒", priority = 98)]
        public static void BuildAllWalkCompareSandbox()
        {
            var all = new List<AnimSpec>();
            all.AddRange(YoungWalkVariants);
            all.AddRange(MiddleWalkVariants);
            BuildWalkCompareSandbox(all.ToArray(), "青年+中年行走");
        }

        /// <summary>
        /// 把一组行走变体并排放进沙盒。
        /// ⚠️ 注意各版 fps **故意不同**:这一轮要对比的正是播放速度对眼珠的影响,
        /// 所以不再把步频强行配成一致(那正是上一轮 30fps 快放 1.25 倍的错误来源)。
        /// </summary>
        static void BuildWalkCompareSandbox(AnimSpec[] variants, string title)
        {
            if (!EnsureNotPlaying()) return;

            var built = new List<BuiltAnim>();
            foreach (var spec in variants)
            {
                var b = BuildClipAndController(spec.frameDir, spec.clipName, spec.fps);
                if (b.controller == null) continue;      // 目录不存在就跳过
                b.label = spec.display;
                b.fps = spec.fps;
                b.steps = spec.steps;
                built.Add(b);
            }

            if (built.Count == 0)
            {
                EditorUtility.DisplayDialog("错误",
                    $"找不到 {title} 的任何帧目录,请确认 AIAnimations 下的子目录名。", "好");
                return;
            }

            BuildRowSandboxScene(built);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = new System.Text.StringBuilder();
            msg.AppendLine($"{title}对比沙盒已生成:");
            msg.AppendLine(ScenePath);
            msg.AppendLine();
            msg.AppendLine("从左到右:");
            foreach (var b in built)
            {
                var cadence = b.steps > 0 ? $"{b.StepsPerSecond:F2} 步/秒" : "-";
                msg.AppendLine($"  {b.label}  {b.frameCount} 帧 @{b.fps:F0}fps " +
                               $"({b.frameCount / b.fps:F2}s)  {cadence}");
            }
            msg.AppendLine();
            msg.AppendLine("本轮重点:");
            msg.AppendLine("  1. 30fps/27fps 版是快放(上一轮的坑),眼珠自然快 25%");
            msg.AppendLine("  2. 24fps 原速版眼珠慢了,但源素材本身眼珠还是在扫视");
            msg.AppendLine("  3. ⭐ v10眼稳版 = 换窗口 + 眼区时间中值稳态化,");
            msg.AppendLine("     眼区变化量已经比中年版还小(8.31 vs 8.78)");
            msg.Append("打开场景按 Play 即可预览,重点看最右边那版眼睛还快不快。");
            EditorUtility.DisplayDialog($"{title}对比", msg.ToString(), "好");
        }

        /// <summary>按目录名从 AllAnims 里查 fps,查不到用 fallback。</summary>
        static float FpsOf(string frameDir, float fallback)
        {
            foreach (var a in AllAnims)
                if (a.frameDir == frameDir) return a.fps;
            return fallback;
        }

        [MenuItem("失落的女神/AI 动画/生成全形态对比沙盒", priority = 98)]
        public static void BuildAllFormsSandbox()
        {
            if (!EnsureNotPlaying()) return;

            var built = new List<BuiltAnim>();
            foreach (var spec in AllAnims)
            {
                var b = BuildClipAndController(spec.frameDir, spec.clipName, spec.fps);
                if (b.controller == null) continue;      // 目录不存在/没帧,跳过
                b.label = spec.display;
                b.fps = spec.fps;
                b.steps = spec.steps;
                built.Add(b);
            }

            if (built.Count == 0)
            {
                EditorUtility.DisplayDialog("错误",
                    "AIAnimations 下没有找到任何可用的帧目录。", "好");
                return;
            }

            BuildRowSandboxScene(built);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = new System.Text.StringBuilder();
            msg.AppendLine("对比沙盒已生成:");
            msg.AppendLine(ScenePath);
            msg.AppendLine();
            msg.AppendLine("从左到右:");
            foreach (var b in built)
            {
                var cadence = b.steps > 0
                    ? $"  |  {b.steps} 步 → {b.StepsPerSecond:F2} 步/秒"
                    : "";
                msg.AppendLine($"  {b.label}  {b.frameCount} 帧 @{b.fps:F0}fps " +
                               $"({b.frameCount / b.fps:F2}s){cadence}");
            }
            msg.AppendLine();
            msg.AppendLine("步频应为 青年 > 中年 > 老年。");
            msg.Append("打开场景按 Play 即可预览。");
            EditorUtility.DisplayDialog("AI 动画对比沙盒", msg.ToString(), "好");
        }

        [MenuItem("失落的女神/AI 动画/生成青年 Idle+Walk 对比沙盒", priority = 99)]
        public static void BuildYoungIdleWalkSandbox()
        {
            // EditorSceneManager.NewScene 在 Play 模式下会抛 InvalidOperationException,
            // 所以先挡住,给个能看懂的提示而不是一串堆栈。
            if (!EnsureNotPlaying()) return;

            // 两个动画并排放,方便直接对比 Idle 与 Walk 的抠图质量和幅度。
            // fps 从 AllAnims 取,避免两个菜单各写一套数字、改了一处忘另一处。
            float idleFps = FpsOf("YoungIdle", 12f);
            float walkFps = FpsOf("YoungWalk", 24f);
            var idle = BuildClipAndController("YoungIdle", "Young_Idle_AI", idleFps);
            var walk = BuildClipAndController("YoungWalk", "Young_Walk_AI", walkFps);
            if (idle.controller == null && walk.controller == null)
            {
                EditorUtility.DisplayDialog("错误",
                    "YoungIdle 和 YoungWalk 目录都没有可用的 PNG Sprite。", "好");
                return;
            }

            BuildDualSandboxScene(idle, walk);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = "对比沙盒已生成:\n" + ScenePath + "\n\n";
            msg += idle.controller != null
                ? $"左: Idle  {idle.frameCount} 帧 @{idleFps:F0}fps ({idle.frameCount / idleFps:F2}s)\n"
                : "左: Idle  (未找到帧)\n";
            msg += walk.controller != null
                ? $"右: Walk  {walk.frameCount} 帧 @{walkFps:F0}fps ({walk.frameCount / walkFps:F2}s)\n"
                : "右: Walk  (未找到帧)\n";
            msg += "\n打开场景按 Play 即可预览。";
            EditorUtility.DisplayDialog("AI 动画对比沙盒", msg, "好");
        }

        struct BuiltAnim
        {
            public AnimatorController controller;
            public Sprite firstFrame;
            public int frameCount;
            public string label;
            public float fps;
            public int steps;

            /// <summary>每秒步数。待机(steps=0)返回 0,不参与步频比较。</summary>
            public float StepsPerSecond =>
                steps > 0 && frameCount > 0 ? steps / (frameCount / fps) : 0f;
        }

        /// <summary>
        /// 这些构建函数会新建/覆盖场景,而 EditorSceneManager.NewScene 在 Play 模式下
        /// 直接抛 InvalidOperationException。返回 false 表示当前不能构建。
        /// </summary>
        static bool EnsureNotPlaying()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("请先退出播放模式",
                "生成沙盒场景需要新建场景,不能在 Play 模式下进行。\n\n" +
                "请按 Stop 停止播放,然后重新点这个菜单。", "好");
            return false;
        }

        /// <summary>扫一个帧目录,生成 clip + 单状态 controller。目录不存在则返回空。</summary>
        static BuiltAnim BuildClipAndController(string frameSubDir, string clipName, float fps)
        {
            var result = new BuiltAnim { label = clipName };
            string framesDir = AIAnimDir + "/" + frameSubDir;
            if (!AssetDatabase.IsValidFolder(framesDir))
            {
                Debug.LogWarning($"[AIAnimationBuilder] 跳过:找不到 {framesDir}");
                return result;
            }

            EnsureDir(ControllerDir);
            AssetDatabase.ImportAsset(framesDir,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            var sprites = LoadSpritesSorted(framesDir);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[AIAnimationBuilder] 跳过:{framesDir} 里没有 PNG Sprite");
                return result;
            }
            Debug.Log($"[AIAnimationBuilder] {frameSubDir}: 找到 {sprites.Count} 帧");

            string clipPath = ControllerDir + "/" + clipName + ".anim";
            string ctrlPath = ControllerDir + "/" + clipName + ".controller";
            var clip = BuildFrameClip(sprites, fps, loop: true, clipPath);
            result.controller = BuildSingleStateController(clip, ctrlPath);
            result.firstFrame = sprites[0];
            result.frameCount = sprites.Count;
            return result;
        }

        [MenuItem("失落的女神/AI 动画/生成青年 Idle 沙盒场景", priority = 100)]
        public static void BuildYoungIdleSandbox()
        {
            if (!EnsureNotPlaying()) return;

            string framesDir = AIAnimDir + "/YoungIdle";
            string clipName = "Young_Idle_AI";
            string clipPath = ControllerDir + "/" + clipName + ".anim";
            string ctrlPath = ControllerDir + "/Young_Idle_AI.controller";

            if (!AssetDatabase.IsValidFolder(framesDir))
            {
                EditorUtility.DisplayDialog("错误",
                    "找不到 " + framesDir + "\n请先把 AI 生成的 PNG 序列帧放到该目录。", "好");
                return;
            }

            EnsureDir(ControllerDir);
            EnsureDir(SceneDir);

            // 1) 强制重新导入(触发 AISpriteImporter 设为 Sprite)
            AssetDatabase.ImportAsset(framesDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            // 2) 收集 Sprite(按文件名排序)
            var sprites = LoadSpritesSorted(framesDir);
            if (sprites.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "目录里没有找到 PNG Sprite。", "好");
                return;
            }
            Debug.Log($"[AIAnimationBuilder] 找到 {sprites.Count} 帧 Sprite");

            // 3) 生成 AnimationClip
            var clip = BuildFrameClip(sprites, 12f, loop: true, clipPath);

            // 4) 生成 AnimatorController(单 Idle 状态)
            var controller = BuildSingleStateController(clip, ctrlPath);

            // 5) 生成沙盒场景
            BuildSandboxScene(controller, sprites[0]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AIAnimationBuilder] 完成!\n  Clip: {clipPath}\n  Controller: {ctrlPath}\n  Scene: {ScenePath}");
            EditorUtility.DisplayDialog("AI 动画沙盒",
                "沙盒场景已生成:\n" + ScenePath +
                "\n\n共 " + sprites.Count + " 帧 @ 12fps,循环播放。\n" +
                "打开场景按 Play 即可预览。", "好");

            // 打开场景
            EditorSceneManager.OpenScene(ScenePath);
        }

        // --------------------------------------------------------------------
        //  把一目录 PNG 变成按名排序的 Sprite 列表
        // --------------------------------------------------------------------
        static List<Sprite> LoadSpritesSorted(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var list = new List<(string name, Sprite s)>();
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) list.Add((Path.GetFileName(path), sprite));
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var result = new List<Sprite>();
            foreach (var x in list) result.Add(x.s);
            return result;
        }

        // --------------------------------------------------------------------
        //  用 Sprite 序列生成一个逐帧 AnimationClip
        // --------------------------------------------------------------------
        static AnimationClip BuildFrameClip(List<Sprite> sprites, float fps, bool loop, string path)
        {
            var clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(path);
            clip.frameRate = fps;

            float interval = 1f / fps;
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * interval,
                    value = sprites[i]
                };
            }

            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // 已存在就删了重建
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // --------------------------------------------------------------------
        //  单状态 Controller(直接播放,没有参数/过渡)
        // --------------------------------------------------------------------
        static AnimatorController BuildSingleStateController(AnimationClip clip, string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            // 清默认状态
            foreach (var s in sm.states) sm.RemoveState(s.state);

            var state = sm.AddState("Idle");
            state.motion = clip;
            state.writeDefaultValues = true;
            sm.defaultState = state;

            return controller;
        }

        // --------------------------------------------------------------------
        //  生成最简沙盒场景:Orthographic 相机 + 一个 SpriteRenderer + Animator
        // --------------------------------------------------------------------
        static void BuildSandboxScene(RuntimeAnimatorController controller, Sprite firstFrame)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 相机
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;      // 与游戏一致(黑边上下各 1,可见区高 10)
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            // 角色
            var player = new GameObject("Player");
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = firstFrame;
            sr.sortingOrder = 10;

            // 用 CharacterCutout 材质啃白边
            var cutoutMat = AssetDatabase.LoadAssetAtPath<Material>(CutoutMatPath);
            if (cutoutMat != null)
            {
                sr.sharedMaterial = cutoutMat;
                Debug.Log("[AIAnimationBuilder] 已应用 CharacterCutout 材质");
            }
            else
            {
                Debug.LogWarning("[AIAnimationBuilder] 找不到 CharacterCutout.mat,用默认 Sprite 材质");
            }

            // 锚点在脚底,角色中心大概在 y=2.5 处(高 5 单位)
            player.transform.position = new Vector3(0, 0, 0);
            // 朝左(sprite 默认朝左,与立绘一致)——不翻转,直接看原始方向
            player.transform.localScale = Vector3.one;

            var anim = player.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            if (ok) AddSceneToBuildSettings(ScenePath);
        }

        /// <summary>Idle 与 Walk 并排的对比场景。左 Idle、右 Walk,各带一个文字标签。</summary>
        static void BuildDualSandboxScene(BuiltAnim left, BuiltAnim right)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;      // 比单人沙盒拉近些,两个角色刚好占满
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 角色锚点在脚底(y=0),所以相机要抬到半身高才居中
            cam.transform.position = new Vector3(0, 2.6f, -10);
            camGo.AddComponent<AudioListener>();

            var cutoutMat = AssetDatabase.LoadAssetAtPath<Material>(CutoutMatPath);
            if (cutoutMat == null)
                Debug.LogWarning("[AIAnimationBuilder] 找不到 CharacterCutout.mat,用默认 Sprite 材质");

            SpawnOne(left, new Vector3(-2.2f, 0, 0), "Player_Idle", cutoutMat);
            SpawnOne(right, new Vector3(2.2f, 0, 0), "Player_Walk", cutoutMat);

            // 地面参考线 —— 没有它就看不出角色是否在上下抖
            var line = new GameObject("GroundLine");
            var lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-6, 0, 1));
            lr.SetPosition(1, new Vector3(6, 0, 1));
            lr.widthMultiplier = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.4f, 0.45f, 1f);

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            if (ok) AddSceneToBuildSettings(ScenePath);
        }

        /// <summary>
        /// 任意数量的动画横排一行。相机的视野宽度按数量自动放大,
        /// 这样加了中年形态(变成 6 个)也不会挤出画面。
        /// </summary>
        static void BuildRowSandboxScene(List<BuiltAnim> anims, string scenePath = null)
        {
            if (string.IsNullOrEmpty(scenePath)) scenePath = ScenePath;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            const float spacing = 4.4f;                  // 相邻角色的间距(世界单位)
            float totalW = spacing * anims.Count;
            // 正交相机的可见宽度 = orthographicSize * 2 * aspect。按 16:9 反推需要的 size,
            // 并与角色高度(约 5 单位)取较大者,保证竖向也装得下。
            float sizeForWidth = totalW / 2f / (16f / 9f);
            float orthoSize = Mathf.Max(3.2f, sizeForWidth);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            // 角色锚点在脚底(y=0),所以相机要抬到半身高才让人物居中
            cam.transform.position = new Vector3(0, orthoSize * 0.72f, -10);
            camGo.AddComponent<AudioListener>();

            var cutoutMat = AssetDatabase.LoadAssetAtPath<Material>(CutoutMatPath);
            if (cutoutMat == null)
                Debug.LogWarning("[AIAnimationBuilder] 找不到 CharacterCutout.mat,用默认 Sprite 材质");

            // 从左到右均匀排开,整体居中
            float startX = -(anims.Count - 1) * spacing / 2f;
            for (int i = 0; i < anims.Count; i++)
            {
                var pos = new Vector3(startX + i * spacing, 0, 0);
                SpawnOne(anims[i], pos, anims[i].label, cutoutMat);
            }

            // 地面参考线 —— 没有它就看不出角色是否在上下抖
            var line = new GameObject("GroundLine");
            var lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            float half = totalW / 2f + 1f;
            lr.SetPosition(0, new Vector3(-half, 0, 1));
            lr.SetPosition(1, new Vector3(half, 0, 1));
            lr.widthMultiplier = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.4f, 0.45f, 1f);

            bool ok = EditorSceneManager.SaveScene(scene, scenePath);
            if (ok) AddSceneToBuildSettings(scenePath);
        }

        static void SpawnOne(BuiltAnim a, Vector3 pos, string goName, Material mat)
        {
            if (a.controller == null) return;
            var go = new GameObject(goName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = a.firstFrame;
            sr.sortingOrder = 10;
            if (mat != null) sr.sharedMaterial = mat;
            go.transform.position = pos;

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = a.controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parts = dir.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
