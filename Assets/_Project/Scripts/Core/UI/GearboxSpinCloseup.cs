// ============================================================================
//  GearboxSpinCloseup.cs —— 齿轮箱「内部旋转」特写(S05 千眼回廊 · 中年线)
//
//  ★ 2026-09-15 从零重做。旧 GearboxProjectorCloseup.cs(仰角/摆幅/锁定 三段式)
//    整包弃用 —— 玩法已被用户推翻。本文件只做一件简单的事。
//
//  玩法(用户 2026-09-15 口述,原文):
//    "玩家只需要点击右侧大传动轴就开始播放齿轮箱内部旋转动画游戏也就通关结束"
//    "如果玩家点击的不是大传动轴而是齿轮箱内其他零部件那就播放烟雾喷出特效后弹出失败刻痕"
//
//  实现:
//    · 打开方式 = 真实特写层通道 CloseupView.OpenExisting(ScreenSpaceOverlay,
//      sortingOrder=1050,自带 DimBackground 0.7)。**不是**世界空间假特写。
//    · 命中判定 = 屏幕点 → 容器局部坐标 → 与各零件热区比形状。
//    · 通关 = 把 gearspin_sheet{,_2,_3,_4}.png(161 帧,7 列 × 6 行 × 4 张)
//      当 UI Image 逐帧播。播完 → 关特写(CloseOnSolved)→ 触发 onSolved 续剧情。
//      ⚠ 播放期间**常驻图是藏起来的**(见七修),播完也不回落 —— 末帧与首帧并不相同。
//    · 失败 = ① 烟雾一遍(steam_sheet.png 13 列 × 4 行)逐帧喷,开头伴随一次红闪
//      → ② 刻痕(scar_offset_line.png)弹出/渐显 → ③ 停留 → ④ 淡出消失
//      → ⑤ 重新开始小游戏。★ 刻痕**不再常驻**,烟雾**只播一遍**不循环。
//    · ★ 2026-09-15 用户要求删掉齿轮箱上方的系统提示语句(Toast),已整块移除;
//      通关/失败的反馈全部由画面本身承担。
//
//  坐标系:素材按 3400×1200 全画布定义,容器 1920×678。
//    画布像素 (x,y) → 容器局部 (x*k - 960, 339 - y*k),k = 0.635。
//
//  ★★★ 2026-09-15 四修(用户口述,本轮)★★★★★★★★★★★★★★★★★★★★★★★★★★★★
//    "我重新做了一版齿轮箱转动的序列帧图,你同样先把边缘蓝色像素块腐蚀干净然后
//     接入进去,这次直接使用这套序列帧图的初始帧作为齿轮箱的常驻图,这样就不会
//     出现动画播放前后的形变了,然后你然烟雾特效不断重播并给它加一个可拖动和缩放
//     的组件(我亲自来摆放位置和调整尺寸),交互逻辑不变但是等我调整好烟雾特效后
//     你再让小游戏交互生效"
//
//    → 四条改造:
//      ① **常驻图 = 序列帧首帧**(gearbox_static.png)。旧的"五层静态拼装"
//         (base/gear_big/gear_small/shaft/valve) + knob 全部删除。因为静态拼装
//         与序列帧是两套不同的画法,切换时必然看到形变 —— 用首帧当静态图,
//         静态态和动画第一帧**逐像素相同**,过渡零抖动。
//      ② 旋转帧表换成新规格 575×620 / 7 列 / 41 帧每张 / 4 张。
//      ③ 烟雾**常驻循环重播**(不再是点错才喷一次)。
//      ④ 烟雾加**运行时拖动 + 缩放**组件 SteamLayoutHandle(鼠标左键拖、滚轮缩放),
//         用户亲自摆位;摆好后把控制台里打印的 anchoredPosition / sizeDelta /
//         rotation 抄进本文件顶部常量即为定稿。
//
//  ★★★ 2026-09-15 五修 —— 摆位定稿 + 玩法正式生效 ★★★★★★★★★★★★★★★★★★★★★★★
//    用户:"我调整好位置和尺寸了,你固定一下,然后进入正常的小游戏交互"
//    · 蒸汽摆位从 Unity Console 的 `[SteamLayout]` 日志抄回并**固化**为常量:
//        anchoredPosition = (-48.5, 0.6)
//        sizeDelta        = (249.0, 105.5)   ← 相对素材 288×122 是 0.865 倍(无损)
//        rotZ             = -8.90            ← 用户没动旋转,保持初值
//    · `SteamLayoutEditable = false` → 不再创建全屏拖拽手柄(它会吃掉所有点击),
//      需要再调时按 **F 键**切回摆位模式。
//    · `InteractionEnabled = true` → **玩法正式生效**。
//    · 蒸汽显示时机随模式走:摆位模式常驻循环(方便看效果);
//      正常玩法 = 点错零件才喷一整轮(素材末 5 帧是空的,自然收尾)。
//
//  ★★★ 2026-09-15 六修 ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
//    用户:"把齿轮箱上方的系统提示语句删去,玩家点击错误先播放一遍烟雾特效
//          然后弹出刻痕并淡出消失重新开始小游戏"
//          补充:"红色闪光可以保留。但是一次失败只播放一遍烟雾特效"
//    · **删除 Toast**:原先 BuildTemplate 第 6 步建的 "Toast" 节点
//      (Text + Outline + CanvasGroup,挂在箱体正上方)连同 ShowToast()/ToastRoutine()
//      与 _toastCo 字段**整块移除**。4 处调用点(开始旋转/已启动/零件错了/再来一次)全删。
//    · **失败流程重排**(原先是"红闪 + 刻痕与蒸汽同时进行,刻痕常驻不消失"):
//        ① 烟雾一遍(2.17 s),开头 0.45 s 红闪(保留)
//        ② 烟雾播完 → 刻痕渐显弹出 0.35 s
//        ③ 停留 0.7 s(让玩家看清)
//        ④ 刻痕淡出 0.6 s
//        ⑤ State = Idle,重新开始小游戏
//      新增 `FadeScar(from,to,dur)` 协程统一处理刻痕亮度过渡
//      (亮层 alpha=k,暗影层 alpha=k*0.85)。
//    · 烟雾**只播一遍** —— `_steamOn` 在 steamDur 结束后置 false,绝不循环重复。
//    · 失败流程总时长 ≈ 2.17 + 0.35 + 0.7 + 0.6 ≈ **3.8 s**,之后回到 Idle 可再次尝试。
//      想调节奏就改 PenaltyRoutine 里那四个数(flashDur / FadeScar / 停留 / FadeScar)。
//
//  ★★★ 2026-09-15 七修 —— 动画期间隐藏常驻图 + 通关关特写 + 交互接口 ★★★★★★★★★★★★
//    用户:"在播放齿轮箱旋转动画的时候需要把齿轮箱的初始帧隐藏(因为现在注意到动画在
//          播放过程中还能看见初始帧的留影),在动画播放结束后立刻关闭齿轮箱特写就行了
//          (或者播放结束前一两帧恢复初始帧)。注意在真实游戏场景中小游戏通过后需要在
//          播放完齿轮箱转动动画后关闭齿轮箱特写并继续接下来的剧情,而齿轮箱也需要
//          留一个交互接口用于触发弹出齿轮箱特写并开始小游戏"
//    · **动画期间隐藏常驻图** —— `BeginSpin()` 里 `staticImg.enabled = false`。
//      (实测叠加时"序列帧透明 & 常驻图不透明"有 1323 px ≈ 0.45% 的透出通道,且序列帧
//       与常驻图有 43% 的可见像素不同 —— 藏掉最干净,不再依赖"两层完全重合"的假设。)
//    · **播完不回落到常驻图** —— 序列帧末帧与首帧并不逐像素相同(实测差 126k px),
//      回落会跳。真实游戏直接关特写;调试场景停在末帧,按 R 重置才回常驻图。
//    · **通关后自动关特写** = `CloseOnSolved`(默认 **true**)。关闭**之后**才触发 onSolved,
//      所以剧情可以直接接在回调里,不会有特写挡在上面;同时 `SetCanClose(false)`,
//      玩家不能点背景跳过小游戏。
//    · **交互接口** = `Content/EyeCorridor/Interact_Gearbox.cs`(继承 InteractableBase,
//      照 Interact_EyeConsole 的范式):挂 Collider2D 物体 + Inspector 填 onSolved 即可接关卡。
//
//  ★★★ 2026-09-15 八修 —— 热区**实测重定**(用户:"严重错位了!")★★★★★★★★★★★★★★
//    · **错位根因**:四修时的热区是凭用户文字描述**估**的,从没对照素材 —— 画出来一看,
//      "①竖杆"整个落在右侧木框木纹上,完全避开了真实零件(面板上所有竖直结构其实都
//      集中在 u -0.26..0.29)。**教训:方位词("右侧")不能当坐标用,必须量。**
//    · **正解**:旧版素材有**独立零件图层**(shaft / gear_big / gear_small / valve),
//      `docs/gearbox/gearbox_measure.json` 记着各自的 pivot 与画布落点。做法:
//        量各层 alpha bbox → 旧画布坐标 → 用"齿轮箱整体"在旧(813×969)/新(571×616)
//        两套素材里的 alpha bbox 做线性映射(sx=0.7023, sy=0.6357)→ 零件在新面板的 u/v。
//      交叉验证:霍夫圆检出的大齿轮圆心 (0.117,0.584) ≈ 图层 bbox 算出的 (0.142,0.537) ✓
//      ⚠ 坑:`gearbox_measure.json` 的 `rect` 是 **[x0,y0,x1,y1]** 不是 [x,y,w,h]。
//    · **1 号【大传动轴】**(红)= 顶部大齿轮(椭圆 u0.142 v0.537 半轴 0.502/0.259)
//      + 竖轴(矩形 u -0.045..0.287, v -0.994..0.409)。两者同轴,合为一个热区。
//    · **2 号【失败】** 拆成 **7 个明确零件区**(F1 左上齿轮组 / F2 左中大齿轮 /
//      F3 左下大齿轮 / F4 阀件 / F5 右中齿轮 / F6 右下齿轮组 / F7 右缘旋钮)+ **面板兜底**。
//      兜底让失败区功能上冗余,它的价值是**显式化 + 可单独调**(以后给某零件换反馈只改一行)。
//      新增 `InEllipse()` 辅助(归一化椭圆判定,参数与 viz 脚本一一对应)。
//    · **可视化**:`tools/gearbox_hitzone_viz.py` → `docs/gearbox/_hitzones.png`
//      (热区叠加 + 0.1 归一化网格 + 编号 + 图例,图例画在面板外不遮挡)。
//      ★ **改热区后必须重跑这张图复核** —— 这次就是靠它才发现错位的。
//
//  ⚠⚠ 画质事故复盘(三次踩坑,务必记住):
//    ① 一修把**整帧 1470×630 缩到 340×146**(缩 4.3 倍)打帧表 → 齿轮箱掉到 100×117,
//       Unity 再放大到屏幕 526×609 = **净放大 5.2 倍 → 糊**,与静态图层差 8.2 倍精细度。
//       用户原话:"明显和齿轮箱图层不是一个量级的精细度"。
//    ② 二修只改了"坐标系"(按 340×146 算尺寸),**素材还是缩过的**,画质没救。
//    ③ 三修正解 = 只裁齿轮箱区域(575×620)原生分辨率 + 多图集。
//    ★ 教训:遇到"画质糊",先量 **素材像素 → 屏幕像素的比值**;
//      只要 >2 倍就要怀疑素材被缩过,而不是调 filterMode。
//
//  ★★★ 2026-09-15 九修 —— **交付版:调试脚手架已清理** ★★★★★★★★★★★★★★★★★★★★
//    用户:"把调试用的部分删去,然后把这一整个小游戏实现的资源打包推送到 github"。
//    已删(备份在 docs/backups/20260915_gearbox_debug_scaffold/):
//      · Scenes/GearboxDebug.unity + Utils/GearboxDebugBootstrap.cs(调试场景与启动器)
//        同时从 ProjectSettings/EditorBuildSettings.asset 移除了场景条目
//      · 本文件里的:InteractionEnabled / SteamLayoutEditable / HitZoneCalibration
//        三个调试开关、SteamLayoutHandle 类(烟雾摆位手柄)、SteamLayoutText()、
//        ResetRun() / ResetSolvedFlag()
//    ⚠ 下面各"修"的历史段落里仍会提到那些开关 —— 它们是**事故复盘记录**,保留。
//    ★ 正式接入方式:在关卡里挂 `Content/EyeCorridor/Interact_Gearbox`(InteractableBase),
//      Inspector 填 onSolved 接剧情即可。
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class GearboxSpinCloseup
    {
        static Action _onSolved;
        static bool _solvedOnce;

        /// <summary>★ 通关(旋转动画播完)后是否**自动关闭特写**。
        /// 正式游戏 = **true** —— 关掉特写后触发 <see cref="Show"/> 的 onSolved 回调,
        /// 由它继续接下来的剧情(保证剧情开始时特写已经收掉)。
        /// 同时决定"玩家能否点背景关掉特写":true→禁止(不能中途跳过小游戏)。
        /// (调试时若想让特写留着反复看,把它设成 false 并自行处理回调。)</summary>
        public static bool CloseOnSolved = true;

        // ── 容器 / 画布 ─────────────────────────────────────────────────
        public const float kCanvasW = 3400f;
        public const float kCanvasH = 1200f;
        public const float kContainerW = 1920f;
        public const float kContainerH = 678f;
        public const float kCanvasCenterX = 1700f, kCanvasCenterY = 600f;
        /// <summary>画布像素 → 容器像素(见文件头注释:拟合现成面板得到)。</summary>
        public const float kScale = 0.635f;

        // ── 常驻图(2026-09-15 四修:改用序列帧首帧)────────────────────────
        //  ⚠ 旧版是 5 层静态拼装(gearbox_base + gear_big + gear_small + shaft + valve)
        //    再叠一个 knob。它的画法和序列帧**不一样**(描边/明暗/透视都有差),
        //    所以点开大传动轴、序列帧接上的那一瞬间会看到明显形变 —— 用户报的就是这个。
        //  ★ 现在直接用序列帧的**第 0 帧**导出成 gearbox_static.png 当常驻图,
        //    静态态 = 动画第 0 帧,像素级一致,切换零抖动。
        const string StaticTexPath = "Closeups/GearboxPuzzle/gearbox_static";
        /// <summary>常驻图原生尺寸 = 序列帧单格尺寸(同一裁切框)。</summary>
        const int StaticTexW = 575, StaticTexH = 620;

        /// <summary>常驻图在容器里的显示尺寸。设定原则:**素材像素→屏幕像素 ≈ 1:1**。
        /// 显示高度 610px(素材 620)→ 只缩到 0.984 倍,画质无损。
        /// 摆位与序列帧完全共用一套(见 SpinAnchored / SpinSize),保证两者重合。</summary>
        static readonly Vector2 StaticSize = new Vector2(566f, 610f);

        // ── 序列帧表(2026-09-15 四修:新素材 575×620)──────────────────────
        //  源帧 1470×630,齿轮箱裁切框 575×620(x 446..1021 y 12..632)。
        //  **不缩放**,原生像素打进帧表 → 素材→屏幕只缩 0.98 倍,画质与常驻图同源。
        //  161 帧在 4096 单边上限下装不进一张图集 → 拆 **4 张**
        //  栅格 7 列 × 6 行 = 4025×3720(≤4096 ✓),每张 41 帧,末张 38 帧。
        const int SpinFrameW = 575, SpinFrameH = 620, SpinCols = 7;
        /// <summary>每张图集的帧数(最后一张可能不满)。</summary>
        const int SpinPerSheet = 41;
        /// <summary>图集张数。</summary>
        const int SpinSheetCount = 4;
        static readonly string[] SpinSheetPaths = {
            "Closeups/GearboxPuzzle/gearspin_sheet",
            "Closeups/GearboxPuzzle/gearspin_sheet_2",
            "Closeups/GearboxPuzzle/gearspin_sheet_3",
            "Closeups/GearboxPuzzle/gearspin_sheet_4",
        };
        /// <summary>真实帧数(源 161 帧)。改素材时同步改这里或读 gearbox_spin.json。</summary>
        const int SpinFrameCount = 161;
        public static float SpinFps = 26f;          // 161 帧 @26fps ≈ 6.2 秒

        // ── 蒸汽帧表 ────────────────────────────────────────────────────
        const int SteamFrameW = 288, SteamFrameH = 122, SteamCols = 13;
        public static float SteamFps = 24f;

        // ── ★ 序列帧 / 常驻图 摆位(2026-09-15 四修实测)────────────────────
        //  序列帧 = **整块齿轮箱面板**(木框 + 全部齿轮 + 右缘旋钮),常驻图就是它的首帧,
        //  两者共用同一 anchoredPosition / sizeDelta —— 这样"播放前后"绝无位移。
        //
        //  量测(tools/gearbox_measure_frames.py):
        //    · 新素材 union alpha bbox = 帧内 x 2..573 y 2..618 → 基本铺满整格
        //    · 常驻图即首帧,同尺寸
        //  摆位:让面板在 1920×678 容器里居中略偏右(原五层合成也在这个位置),
        //       底边压在容器下沿之上一点,顶部留出提示条空档。
        //  ★ 素材 575 → 屏幕 566 = **缩到 0.98 倍**(无损);旧版是放大 5.2 倍 → 糊。
        static readonly Vector2 SpinSize = new Vector2(566f, 610f);
        /// <summary>面板中心(容器局部坐标,原点 = 容器中心)。</summary>
        static readonly Vector2 SpinAnchored = new Vector2(22f, -4f);

        // ── ★★ 蒸汽摆位(2026-09-15 用户亲手拖动/缩放定稿)★★★★★★★★★★★★★★
        //  ★ 用户原话:"我调整好位置和尺寸了,你固定一下,然后进入正常的小游戏交互"。
        //  定稿值从 Unity Console 的 `[SteamLayout]` 日志抄回(347 次拖动 + 12 次缩放,
        //  **0 次旋转** —— rotZ 保持初始 -8.90)。
        //
        //  ⚠ 蒸汽 rect 的 pivot = (1,1)(右上角 = 喷出源),所以 anchoredPosition 就是
        //    喷口锚点在容器局部坐标里的位置;贴图从其右上角往左下展开。
        //  ⚠ 改这三个值后,可用 tools/gearbox_screen_sim.py 复刻屏幕观感复核。
        /// <summary>蒸汽锚点(容器局部坐标,原点 = 容器中心)。= Console 日志的 anchoredPosition。</summary>
        static readonly Vector2 SteamAnchored = new Vector2(-48.5f, 0.6f);
        /// <summary>蒸汽显示尺寸。=(249.0, 105.5) → 相对素材 288×122 是 0.865 倍等比缩小。</summary>
        static readonly Vector2 SteamSize = new Vector2(249.0f, 105.5f);
        /// <summary>蒸汽旋转角(度)。精灵喷向局部 -X,负角 = 往左上翘。</summary>
        const float SteamRotZ = -8.9f;

        public static string ClickSfx = "gearbox_click";
        public static string SteamSfx = "gearbox_steam_hiss";

        /// <summary>弹出齿轮箱特写并开始小游戏。真实游戏里由交互物
        /// <c>Interact_Gearbox.OnClick()</c>(继承 InteractableBase)调用。</summary>
        /// <param name="onSolved">通关回调。★ 在**特写关闭之后**触发(CloseOnSolved=true 时),
        /// 所以可以直接在里面接剧情/切场景,不会有特写挡在上面。</param>
        public static void Show(Action onSolved = null)
        {
            _onSolved = onSolved;
            _solvedOnce = false;
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);
            var template = BuildTemplate();
            // ★ 六修:把"特写关闭"作为通关回调的触发时机 —— 保证续剧情时特写已收掉。
            CloseupView.OpenExisting(template, OnCloseupClosed);
            // ★ 真实游戏禁止玩家点背景跳过小游戏;调试场景允许(方便随时退)。
            CloseupView.SetCanClose(!CloseOnSolved);
        }

        /// <summary>特写关闭后:只有"因通关而关闭"才把回调放出去;玩家自己关的不算。</summary>
        static void OnCloseupClosed()
        {
            if (!_solvedOnce) return;
            FireSolved();
        }

        static void FireSolved()
        {
            var cb = _onSolved;
            _onSolved = null;
            if (cb != null) cb.Invoke();
        }

        /// <summary>动画播完时由 <c>SpinBehaviour</c> 调用。
        /// CloseOnSolved=true → 关闭特写(关闭完成后再触发回调)。</summary>
        public static void NotifySolved()
        {
            if (_solvedOnce) return;
            _solvedOnce = true;
            if (CloseOnSolved) CloseupView.Close();   // → OnCloseupClosed → FireSolved
            else FireSolved();
        }

        // ── 构建 UI ─────────────────────────────────────────────────────
        static GameObject BuildTemplate()
        {
            var root = new GameObject("GearboxSpin");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(kContainerW, kContainerH);
            rootRT.pivot = new Vector2(0.5f, 0.5f);

            // 1) ★ 常驻图 = 序列帧首帧(gearbox_static.png)
            //    旧的 5 层静态拼装 + knob 已删除:与序列帧画法不同 → 切换时形变。
            var staticGo = new GameObject("Static");
            staticGo.transform.SetParent(root.transform, false);
            var staticRT = staticGo.AddComponent<RectTransform>();
            staticRT.anchorMin = staticRT.anchorMax = new Vector2(0.5f, 0.5f);
            staticRT.pivot = new Vector2(0.5f, 0.5f);
            staticRT.sizeDelta = StaticSize;
            staticRT.anchoredPosition = SpinAnchored;
            var staticImg = staticGo.AddComponent<Image>();
            staticImg.sprite = LoadSprite(StaticTexPath);
            staticImg.preserveAspect = true;
            staticImg.raycastTarget = false;

            // 2) 旋转序列帧(初始隐藏;命中大传动轴后逐帧播)
            //    ★ 与常驻图**同尺寸同位置** → 首帧接上的瞬间画面完全不动。
            var spinGo = new GameObject("SpinAnim");
            spinGo.transform.SetParent(root.transform, false);
            var spinRT = spinGo.AddComponent<RectTransform>();
            spinRT.anchorMin = spinRT.anchorMax = new Vector2(0.5f, 0.5f);
            spinRT.pivot = new Vector2(0.5f, 0.5f);
            spinRT.sizeDelta = SpinSize;
            spinRT.anchoredPosition = SpinAnchored;
            var spinImg = spinGo.AddComponent<Image>();
            spinImg.raycastTarget = false;
            spinImg.preserveAspect = true;
            spinImg.enabled = false;

            // 3) 蒸汽(UI 逐帧)
            //    ★★ 摆位 = 用户 2026-09-15 亲手拖动/缩放定稿值(见文件上方常量区)。
            //    ★ 显示时机:摆位模式下**常驻循环**(方便看效果);正常玩法下**点错零件才喷**。
            var steamGo = new GameObject("Steam");
            steamGo.transform.SetParent(root.transform, false);
            var steamRT = steamGo.AddComponent<RectTransform>();
            steamRT.anchorMin = steamRT.anchorMax = new Vector2(0.5f, 0.5f);
            steamRT.pivot = new Vector2(1f, 1f);            // 贴图右上角 ≈ 喷出源
            steamRT.sizeDelta = SteamSize;
            steamRT.anchoredPosition = SteamAnchored;
            // 精灵喷出方向 = 局部 -X;Unity Z 正角逆时针 → 负角才往左上翘。
            steamRT.localRotation = Quaternion.Euler(0f, 0f, SteamRotZ);
            var steamImg = steamGo.AddComponent<Image>();
            steamImg.raycastTarget = false;
            steamImg.enabled = false;                       // 开局不喷,点错零件才播一遍

            // 4) 失败刻痕(暗影副本垫底 + 亮红字压上)
            var scarShadowRT = MakeScar(root.transform, "ScarShadow",
                new Vector2(3f, -28f), new Color(0.02f, 0.01f, 0.01f, 0f));
            var scarRT = MakeScar(root.transform, "Scar",
                new Vector2(0f, -25f), new Color(1f, 1f, 1f, 0f));

            // 5) 红闪(铺满整屏,含上下黑边)
            var flashGo = new GameObject("RedFlash");
            flashGo.transform.SetParent(root.transform, false);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(0.55f, 0.05f, 0.04f, 0f);
            var flashRT = flashImg.rectTransform;
            flashRT.anchorMin = new Vector2(0f, 0f);
            flashRT.anchorMax = new Vector2(1f, 1f);
            flashRT.offsetMin = new Vector2(-960f, -201f);
            flashRT.offsetMax = new Vector2(960f, 201f);

            // 6) 全屏 hitbox(接收点击)
            //    ★ 2026-09-15 用户要求:"把齿轮箱上方的系统提示语句删去" ——
            //      原先这里有一个 Toast(Text + Outline + CanvasGroup,挂在箱体正上方),
            //      连 ShowToast()/ToastRoutine() 一起**整块删除**。通关/失败的反馈
            //      改由画面本身承担(旋转动画 = 成功;蒸汽 + 刻痕 = 失败)。
            var hitboxGo = new GameObject("GlobalHitbox");
            hitboxGo.transform.SetParent(root.transform, false);
            var hitboxImg = hitboxGo.AddComponent<Image>();
            hitboxImg.color = new Color(0f, 0f, 0f, 0.001f);
            hitboxImg.raycastTarget = true;
            var hrt = hitboxImg.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.offsetMin = new Vector2(-960f, -201f);
            hrt.offsetMax = new Vector2(960f, 201f);

            // 主逻辑
            var beh = root.AddComponent<SpinBehaviour>();
            beh.spinImg = spinImg;
            beh.spinRT = spinRT;
            beh.staticImg = staticImg;
            beh.staticRT = staticRT;
            beh.steamImg = steamImg;
            beh.steamRT = steamRT;
            beh.scarImg = scarRT.GetComponent<Image>();
            beh.scarShadowImg = scarShadowRT.GetComponent<Image>();
            beh.flashImg = flashImg;
            beh.hitbox = hitboxGo;

            hitboxGo.AddComponent<SpinClickRouter>().behaviour = beh;
            return root;
        }

        static RectTransform MakeScar(Transform parent, string name, Vector2 offset, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // ★ 刻痕素材 3400×1200,按**画布**缩放(和面板同一套 kScale),
            //   直接铺满 1920×678 容器 —— 字会自然落在箱体下方那条空档上。
            rt.sizeDelta = new Vector2(kCanvasW * kScale, kCanvasH * kScale);
            rt.anchoredPosition = new Vector2(0f, 0f) + offset;
            rt.localEulerAngles = new Vector3(0f, 0f, -2.5f);
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite("Closeups/GearboxPuzzle/scar_offset_line");
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint;
            return rt;
        }

        static Sprite LoadSprite(string resPath)
        {
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex == null)
            {
                Debug.LogError("[GearboxSpin] 找不到贴图:" + resPath);
                return null;
            }
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f);
        }

        // ====================================================================
        //  SpinBehaviour —— 主逻辑
        // ====================================================================
        public class SpinBehaviour : MonoBehaviour
        {
            public Image spinImg, steamImg, scarImg, scarShadowImg, flashImg;
            public Image staticImg;
            public RectTransform spinRT, steamRT, staticRT;
            public GameObject hitbox;

            public enum Phase { Idle, Spinning, Penalizing, Solved }
            public Phase State { get; private set; }
            public int PenaltyCount { get; private set; }

            Sprite[] _spinFrames, _steamFrames;
            int _spinFrame, _steamFrame;
            float _frameT, _steamT;
            bool _spinOn, _steamOn;

            void Awake()
            {
                _spinFrames = SliceSpinFrames();
                _steamFrames = SliceFrames("Closeups/GearboxPuzzle/steam_sheet",
                                           SteamFrameW, SteamFrameH, SteamCols, 0);
                if (_spinFrames == null || _spinFrames.Length == 0)
                    Debug.LogError("[GearboxSpin] gearspin 图集没切开,旋转动画将不可见。");
            }

            /// <summary>把**多张**旋转图集按顺序拼成一个 Sprite 数组。
            /// 每张图集 uniform 栅格(7 列 × 6 行),帧序 = 图集序号 × 每张帧数 + 格内序号。
            /// 只取前 SpinFrameCount 帧(最后一张图集尾部不满)。</summary>
            static Sprite[] SliceSpinFrames()
            {
                var list = new System.Collections.Generic.List<Sprite>(SpinFrameCount);
                for (int s = 0; s < SpinSheetCount && list.Count < SpinFrameCount; s++)
                {
                    var tex = Resources.Load<Texture2D>(SpinSheetPaths[s]);
                    if (tex == null)
                    {
                        Debug.LogWarning("[GearboxSpin] 找不到图集 " + SpinSheetPaths[s]);
                        continue;
                    }
                    int rows = Mathf.CeilToInt((float)SpinPerSheet / SpinCols);
                    for (int i = 0; i < SpinPerSheet && list.Count < SpinFrameCount; i++)
                    {
                        int c = i % SpinCols, r = i / SpinCols;
                        if (r >= rows) break;
                        // Unity 纹理 y 向上,帧表按"左→右、上→下"排 → 要翻
                        int y = tex.height - (r + 1) * SpinFrameH;
                        if (y < 0) continue;
                        var rect = new Rect(c * SpinFrameW, y, SpinFrameW, SpinFrameH);
                        list.Add(Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f));
                    }
                }
                return list.ToArray();
            }

            /// <summary>把帧表切成 Sprite 数组。帧表按"左→右、上→下"排(Unity 纹理 y 向上,要翻)。
            /// wantCount &gt; 0 时只取前 wantCount 帧(帧表尾部可能是空栅格)。</summary>
            static Sprite[] SliceFrames(string resPath, int fw, int fh, int cols, int wantCount)
            {
                var tex = Resources.Load<Texture2D>(resPath);
                if (tex == null) { Debug.LogWarning("[GearboxSpin] 找不到 " + resPath); return null; }
                int total = (tex.width / fw) * (tex.height / fh);
                if (wantCount > 0) total = Mathf.Min(total, wantCount);
                var arr = new Sprite[total];
                for (int i = 0; i < total; i++)
                {
                    int c = i % cols, r = i / cols;
                    var rect = new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh);
                    arr[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
                return arr;
            }

            // ── 命中判定 ──────────────────────────────────────────────
            /// <summary>屏幕点落在哪:0=空 1=大传动轴(通关) 2=其他零件(失败)。
            /// 用**容器局部坐标**判形状,比逐 Image 射线更稳、更好调。
            ///
            /// ★★ 热区坐标的来历(2026-09-15 八修 —— 别再靠肉眼估)★★★★★★★★
            ///   旧版五层素材有**独立零件图层**(gearbox_shaft / gear_big / gear_small /
            ///   valve),位置记在 docs/gearbox/gearbox_measure.json。做法:
            ///     量每层 alpha bbox → 旧画布坐标 → 用"齿轮箱整体"在旧(813×969)与
            ///     新(571×616)两套素材里的 bbox 做线性映射 → 零件在**新面板**里的 u/v。
            ///   交叉验证:霍夫圆检出的大齿轮圆心 (0.117,0.584) ≈ 图层 bbox 算出的
            ///   (0.142,0.537) —— 两条独立路径吻合,数据可信。
            ///   ★ 可视化核对:tools/gearbox_hitzone_viz.py → docs/gearbox/_hitzones.png
            ///
            /// ★ 1 号【大传动轴】= 顶部大齿轮(椭圆)+ 竖轴(矩形),两者同轴合为一个热区。
            ///   ⚠ 竖轴是**细长矩形**,不能用圆兜。
            /// ★ 2 号【其他零件】= 7 个明确的零件失败区 + **面板兜底**
            ///   (面板范围内其余位置一律算点错)。
            ///   ⚠ 兜底那一条让失败区在功能上冗余 —— 它们的作用是**显式化 + 可单独调**,
            ///     以后要给某个零件换反馈时直接改它那一行。
            ///
            /// ⚠ 坐标是**归一化**的(u 分母 = 面板半宽,v 分母 = 面板半高),所以归一化
            ///   空间里的"圆"在像素空间是椭圆 —— 这正是我们要的:直接对着 _hitzones.png
            ///   的网格读数调参,不用换算像素。</summary>
            public int HitZone(Vector2 screenPos)
            {
                var rt = (RectTransform)transform;
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rt, screenPos, null, out lp))
                    return 0;

                Vector2 c = SpinAnchored;
                Vector2 h = SpinSize * 0.5f;             // 半宽半高

                // 归一化坐标 u,v ∈ [-1,1](相对面板中心;v 向上为正)
                float u = (lp.x - c.x) / h.x;
                float v = (lp.y - c.y) / h.y;

                // ── 1 号:大传动轴 → 通关 ──────────────────────────────
                // ① 顶部大齿轮(椭圆)—— 实测来源:旧图层 gear_big 的 alpha bbox
                //    u -0.360..0.644  v 0.278..0.797
                if (InEllipse(u, v, 0.142f, 0.537f, 0.502f, 0.259f)) return 1;
                // ② 竖轴(矩形)—— 实测来源:旧图层 gearbox_shaft 的 alpha bbox
                //    u -0.045..0.287  v -0.994..0.409
                if (u >= -0.045f && u <= 0.287f && v >= -0.994f && v <= 0.409f) return 1;

                // ── 2 号:其他零件 → 失败 ──────────────────────────────
                //    F2/F4 是实测值(对应旧图层 gear_small / valve),其余按图上位置量
                if (InEllipse(u, v, -0.660f, 0.740f, 0.270f, 0.210f)) return 2;   // F1 左上齿轮组
                if (InEllipse(u, v, -0.430f, 0.190f, 0.400f, 0.190f)) return 2;   // F2 左中大齿轮
                if (InEllipse(u, v, -0.450f, -0.780f, 0.350f, 0.220f)) return 2;  // F3 左下大齿轮
                if (InEllipse(u, v, -0.100f, -0.240f, 0.170f, 0.240f)) return 2;  // F4 阀件
                if (InEllipse(u, v, 0.590f, 0.200f, 0.190f, 0.170f)) return 2;    // F5 右中齿轮
                if (InEllipse(u, v, 0.550f, -0.450f, 0.250f, 0.380f)) return 2;   // F6 右下齿轮组
                if (InEllipse(u, v, 1.060f, 0.200f, 0.170f, 0.180f)) return 2;    // F7 右缘旋钮

                // 兜底:面板范围内其余位置(木框 / 缝隙)也算点错零件
                if (u >= -1f && u <= 1f && v >= -1f && v <= 1f) return 2;

                return 0;
            }

            /// <summary>归一化椭圆内判定:(u,v) 是否落在中心 (cu,cv)、半轴 (ru,rv) 的椭圆内。
            /// ⚠ 半轴是**归一化**单位(u 用面板半宽、v 用面板半高作分母),所以参数直接对应
            ///   tools/gearbox_hitzone_viz.py 里画出来的形状,两边永远一致。</summary>
            static bool InEllipse(float u, float v, float cu, float cv, float ru, float rv)
            {
                float du = (u - cu) / ru, dv = (v - cv) / rv;
                return du * du + dv * dv <= 1f;
            }

            public void OnClickAt(Vector2 screenPos)
            {
                // 动画 / 惩罚进行中不接受新输入
                if (State == Phase.Spinning || State == Phase.Penalizing) return;
                int z = HitZone(screenPos);
                if (z == 1) BeginSpin();
                else if (z == 2) TriggerPenalty();
            }

            // ── 通关:播放内部旋转 ─────────────────────────────────────
            void BeginSpin()
            {
                if (State == Phase.Solved) return;
                State = Phase.Spinning;
                _spinOn = true;
                _spinFrame = 0;
                _frameT = 0f;
                if (spinImg != null)
                {
                    spinImg.enabled = true;
                    if (_spinFrames != null && _spinFrames.Length > 0)
                        spinImg.sprite = _spinFrames[0];
                }
                // ★★ 六修(用户要求):动画期间**把常驻图藏掉**。
                //    常驻图就是首帧,和序列帧同尺寸同位置叠着 —— 但序列帧的齿轮在转、
                //    常驻图的齿轮不动,两者叠加会互相透出(用户报的"初始帧留影")。
                //    藏掉之后动画期间只有序列帧一层,最干净。
                //    动画播完不再回落到常驻图(序列帧末帧与首帧并不逐像素相同,回落会跳)——
                //    真实游戏直接把特写关掉;调试场景就停在末帧,按 R 重置才回到常驻图。
                if (staticImg != null) staticImg.enabled = false;
                PlaySfx(ClickSfx, 1f, 1f);
            }

            // ── 失败:蒸汽 → 刻痕弹出 → 淡出 → 重开 ────────────────────
            void TriggerPenalty()
            {
                if (State == Phase.Penalizing || State == Phase.Solved) return;
                State = Phase.Penalizing;
                PenaltyCount++;
                StartCoroutine(PenaltyRoutine());
            }

            IEnumerator PenaltyRoutine()
            {
                // ★ 2026-09-15 用户定序:"玩家点击错误先播放一遍烟雾特效然后弹出刻痕
                //   并淡出消失重新开始小游戏"。红闪保留(用户确认)。
                //   ★ 一次失败**只播一遍**烟雾,绝不循环重复。

                // ① 烟雾特效(一遍)+ 开头一次红闪
                _steamOn = true;
                _steamFrame = 0;
                _steamT = 0f;
                PlaySfx(SteamSfx, 1f, 1f);

                const float flashDur = 0.45f;
                float steamDur = (_steamFrames != null && _steamFrames.Length > 0)
                    ? _steamFrames.Length / Mathf.Max(1f, SteamFps) : 2.2f;

                float t = 0f;
                while (t < steamDur)
                {
                    t += Time.unscaledDeltaTime;
                    float fa = t < flashDur
                        ? Mathf.Sin(t / flashDur * Mathf.PI) * 0.45f : 0f;
                    var fc = flashImg.color; fc.a = fa; flashImg.color = fc;
                    yield return null;
                }
                var fe = flashImg.color; fe.a = 0f; flashImg.color = fe;
                _steamOn = false;                       // ★ 只播一遍,到此熄火

                // ② 弹出刻痕(渐显)
                yield return FadeScar(0f, 1f, 0.35f);

                // ③ 停留,让玩家看清
                yield return new WaitForSecondsRealtime(0.7f);

                // ④ 淡出消失
                yield return FadeScar(1f, 0f, 0.6f);

                // ⑤ 重新开始小游戏(回到可再次点击的状态)
                State = Phase.Idle;
                _spinFrame = 0;
                _frameT = 0f;
            }

            /// <summary>刻痕 alpha 过渡(亮层 1.0,暗影层 0.85 倍)。</summary>
            IEnumerator FadeScar(float from, float to, float dur)
            {
                float t = 0f;
                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                    var c = scarImg.color;
                    scarImg.color = new Color(c.r, c.g, c.b, k);
                    var s = scarShadowImg.color;
                    scarShadowImg.color = new Color(s.r, s.g, s.b, k * 0.85f);
                    yield return null;
                }
            }

            // ── 每帧 ──────────────────────────────────────────────────
            void Update()
            {
                float dt = Time.unscaledDeltaTime;

                if (_spinOn)
                {
                    _frameT += dt * SpinFps;
                    while (_frameT >= 1f)
                    {
                        _frameT -= 1f;
                        _spinFrame++;
                        if (_spinFrames == null || _spinFrame >= _spinFrames.Length)
                        {
                            // 播完一轮 = 通关
                            _spinOn = false;
                            _spinFrame = Mathf.Max(0, (_spinFrames?.Length ?? 1) - 1);
                            State = Phase.Solved;
                            if (spinImg != null) spinImg.sprite = _spinFrames[_spinFrame];
                            StartCoroutine(FinishSolved());
                            break;
                        }
                        if (spinImg != null) spinImg.sprite = _spinFrames[_spinFrame];
                    }
                }

                // 蒸汽显隐:由失败流程 _steamOn 控制(点错零件才播一遍)
                bool steamActive = _steamOn;
                if (steamImg != null)
                {
                    if (steamImg.enabled != steamActive) steamImg.enabled = steamActive;
                    if (steamActive && _steamFrames != null && _steamFrames.Length > 0)
                    {
                        _steamT += dt * SteamFps;
                        while (_steamT >= 1f)
                        {
                            _steamT -= 1f;
                            _steamFrame = (_steamFrame + 1) % _steamFrames.Length;
                        }
                        steamImg.sprite = _steamFrames[_steamFrame];
                    }
                }
            }

            /// <summary>动画播完 → 等一帧(确保最后一帧已经呈现出来)→ 交给 CloseupView。
            /// ★ 必须走协程:NotifySolved() 在 CloseOnSolved=true 时会 Destroy 本对象,
            ///   直接在 Update 里调虽然也能用(销毁延迟到帧末),但等一帧更稳。</summary>
            IEnumerator FinishSolved()
            {
                yield return null;
                GearboxSpinCloseup.NotifySolved();
            }

            // ── 音效 ──────────────────────────────────────────────────
            void PlaySfx(string name, float vol, float pitch)
            {
                if (string.IsNullOrEmpty(name)) return;
                AudioManager.PlaySfx(name);
            }
        }

        // ====================================================================
        //  SpinClickRouter —— 全屏 hitbox,把点击路由给 Behaviour
        // ====================================================================
        public class SpinClickRouter : MonoBehaviour, IPointerClickHandler
        {
            public SpinBehaviour behaviour;
            public void OnPointerClick(PointerEventData e)
            {
                if (behaviour != null) behaviour.OnClickAt(e.position);
            }
        }
    }
}
