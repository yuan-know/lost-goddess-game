// ============================================================================
//  EyeConsoleCloseup.cs —— 眼睛控制台特写（黄金瞳 · 正式版）  v4 (2026-09-13)
//
//  需求:
//    左旋钮 旋转 → 眼球 左右转
//    中旋钮 旋转 → 眼球 上下转
//    右拉杆 拉动 → 拉杆旋转序列帧 + 眼睑开合序列帧（同一进度联动）
//
//  ── 素材从哪来（美术给的） ──────────────────────────────────────────
//    黄金瞳.psd     → 空眼窝层 / 单独眼球层
//    三个按钮.png   → 左旋钮 / 中旋钮 / 拉杆
//  跑 tools/eye_console_layered_assets.py 拆出全画布分层，
//  再跑 tools/eye_console_pack.py 按 alpha 包围盒裁小 + 生成 manifest.json。
//
//  ── 旋钮为什么拆成「外壳 + 顶面」两层 ───────────────────────────────
//  旋钮是 3/4 视角的圆柱。整张图做 2D 旋转的话，转到 ±135° 会看起来
//  「躺倒」（侧壁甩到一边）。所以：外壳不动，只让顶面绕自己的圆心转。
//  顶面在 3D 里本来就是个圆，投影成椭圆，绕中心转是对的。
//
//  ── 摆放公式（源图像素 → 容器局部坐标）─────────────────────────────
//    容器中心为 (0,0)，尺寸 1920x678
//    SrcToContainer(p) = ((p.x/3400 - 0.5)*1920, (0.5 - p.y/1200)*678)
//    每层的 pivot 取「源图上这个动作的基准点」（旋钮轴心 / 眼球球心 / 拉杆球头）
//    → 该点正好落在 SrcToContainer(pivot) 上，旋转/平移时图形位置天然正确
//
//  ── 图层顺序（自下而上）────────────────────────────────────────────
//    0 Base            控制台本体（**拉杆旋转序列帧 ec_base_00..20**）
//                      —— 拉杆已经画在这一层里了，不再有单独的拉杆图层。
//    1 EyeFrameBack    铜框 + 眼窝内壁（静态）
//    2 EyeOpeningMask  眼窝开口遮罩（Mask 组件，showMaskGraphic=false）
//    2.1 Eyeball       眼球（在遮罩内平移，左右 + 上下）
//    2.5 EyeFrameFront 眼睑/眼眶前景（**压在眼球上面**）
//    2.6 Eyelid        眼睑开合序列帧（**和拉杆同一 leverT 联动**）
//    3 KnobLeftShell   左旋钮外壳（静态）
//    4 KnobLeftTop     左旋钮顶面（绕轴自转）
//    5 KnobMidShell    中旋钮外壳（静态）
//    6 KnobMidTop      中旋钮顶面（绕轴自转）
//    7 三个拖拽手柄（近乎全透明，只负责接收拖拽）
//
//  ── 素材侧做过的一次性处理（别再重做，脚本都在 tools/）──────────────
//  · 底层 ec_base_00..20.png：AI 视频抽帧 → 腐蚀轮廓蓝色污染 + 仿射对齐到
//    3400x1200（对齐矩阵是「量底板矩形」解出来的，所以**位置大小和旧
//    ec_bg.png 完全一致**，旋钮/眼球的图层坐标一个都不用改）→
//    再逐帧光度配平 + 冻结拉杆活动区之外，消掉面板亮度漂移。
//    → tools/eye_lever_frames_clean.py + tools/eye_base_stabilize.py
//  · 眼睑 ec_eyelid_00..13.png：AI 视频抽帧的 alpha 是一整只眼睛（81% 溢出到
//    铜框上，播放时会反复重刷铜色）。已裁进眼窝开口。
//    → tools/eye_build_eyelid.py + tools/eye_eyelid_clip_opening.py
//  · ec_lever_handle.png 已废弃归档到 docs/eye_console/_unused/；
//    ec_bg.png 保留，只作为「底层序列帧缺失时」的兜底底图。
//
//  ── 使用 ────────────────────────────────────────────────────────────
//    // 纯交互（不做判定）
//    EyeConsoleCloseup.Show(() => { /* 关掉后的回调 */ });
//
//    // 当谜题用：给目标值，解出后回调
//    EyeConsoleCloseup.Show(onClosed,
//        new EyeConsoleSolution { lookX01 = 0.5f, leverT = 1f, tolerance = 0.1f },
//        () => GameState.SetFlag(...));
//
//    房间里用 EyeConsoleInteractable 组件挂上去即可（点击 → 开特写）。
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostGoddess
{
    // manifest.json 的数据结构（由 tools/eye_console_pack.py 生成）
    // 坐标一律是「源图像素，左上原点」
    [Serializable]
    public class EcLayerEntry
    {
        public string key;
        public string file;
        public string[] frames;
        public float[] rect;      // [x0,y0,x1,y1]
        public float[] pivot;     // [px,py]
    }

    [Serializable]
    public class EcManifest
    {
        public float[] canvas;
        public float[] container;
        public EcLayerEntry[] layers;

        public EcLayerEntry Find(string key)
        {
            if (layers == null) return null;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != null && layers[i].key == key) return layers[i];
            return null;
        }
    }

    /// <summary>
    /// 谜题目标值。三个字段都是 0~1 的归一化值，NaN = 该项不参与判定。
    /// 用 <see cref="EyeConsoleCloseup.Show(System.Action, EyeConsoleSolution, System.Action)"/> 传进去。
    /// </summary>
    [Serializable]
    public class EyeConsoleSolution
    {
        [Tooltip("左旋钮 → 眼球左右。0 = 看最左，0.5 = 中位，1 = 看最右")]
        public float lookX01 = float.NaN;
        [Tooltip("中旋钮 → 眼球上下。0 = 看最上，0.5 = 中位，1 = 看最下")]
        public float lookY01 = float.NaN;
        [Tooltip("拉杆进度。0 = 静止位（眼睑全开），1 = 拉满（眼睑全闭）")]
        public float leverT = float.NaN;
        [Tooltip("容差（归一化）。判定 |当前值 - 目标值| <= tolerance")]
        public float tolerance = 0.1f;
    }

    public static class EyeConsoleCloseup
    {
        static Action _onClosed;
        static EyeConsoleSolution _solution;
        static Action _onSolved;

        /// <summary>三个可拖拽的控件。</summary>
        public enum ControlId
        {
            LeftKnob,
            MidKnob,
            Lever
        }

        public const float kContW = 1920f;
        public const float kContH = 678f;
        public const float kSrcW = 3400f;
        public const float kSrcH = 1200f;

        const string kResDir = "Closeups/eye_console_layered/";
        const string kManifestPath = kResDir + "manifest";

        // ── 源图 3400x1200 下的关键点 ────────────────────────────────────
        public static readonly Vector2 kEyeCenterSrc = new Vector2(1699.5f, 302f);
        public static readonly Vector2 kEyeballSrc = new Vector2(1689.5f, 286.5f);
        // 运行时控件已经按旧控制台面板复位到这些源图坐标。
        public static readonly Vector2 kKnobLeftSrc = new Vector2(1390.2f, 758.6f);
        public static readonly Vector2 kKnobMidSrc = new Vector2(1712.8f, 785.3f);

        // 拉杆球头：静止位 / 拉满位（源图坐标）。
        // 由 tools/eye_lever_frames_clean.py 生成 ec_base_manifest.json 时量得：
        //   静止帧(第 0 帧)  球心 帧(982.8, 282.1) → 画布(2108, 675)
        //   拉满帧(第 20 帧) 球心 帧(936.6, 169.3) → 画布(2046, 528)
        // 球头是绕底座转上去的，竖直行程 ≈147 源图像素。
        public static readonly Vector2 kLeverRestSrc = new Vector2(2108f, 675f);
        public static readonly Vector2 kLeverPulledSrc = new Vector2(2046f, 528f);
        // 拖拽手柄摆在行程中点，整段行程都能抓住
        public static readonly Vector2 kLeverBallSrc =
            (kLeverRestSrc + kLeverPulledSrc) * 0.5f;
        public const float kKnobLeftRadiusSrc = 112f;
        public const float kKnobMidRadiusSrc = 118f;

        // ── 运动参数 ─────────────────────────────────────────────────────
        public const float kKnobMaxAngle = 135f;    // 旋钮可转范围 ±135°
        // 眼球行程（源图像素，单边）。
        //
        // ⚠ 这里有个绕不过去的几何约束：
        //   眼窝开口 ≈ 320×100（扁杏仁），眼球 ≈ 82×76。
        //   球竖向占开口 76%、横向只占 26% —— 比例差 3 倍。
        //   所以球一上下动就撞到开口上下缘（轻微移动即被切），
        //   而左右要滑很远才碰到边缘，等碰到时已经在杏仁尖上、直接被切成小块。
        //   → 左右「适度被切」这个状态，在这套素材下不存在。
        //
        // 实测可见比例（球面积占比）：
        //   (150,30) → 中位 100% / 纯水平 50% / 纯垂直 70% / 斜向 16%
        //   (140,32) → 中位 100% / 纯水平 61% / 纯垂直 60% / 斜向 20%
        //   (138,36) → 中位 100% / 纯水平 66% / 纯垂直 59% / 斜向 16%
        // 取 (140,32)：左右比原来明显，斜向也还能看到球。
        // 想让左右更明显就把 X 加大（但斜向会更快消失）。
        public const float kEyeTravelXSrc = 140f;
        public const float kEyeTravelYSrc = 32f;
        // 拉杆行程 = 球头「静止位 → 拉满位」的竖直距离（源图像素）
        // = kLeverRestSrc.y - kLeverPulledSrc.y
        public const float kLeverTravelSrc = 147f;

        public static float EyeTravelX => kEyeTravelXSrc * (kContW / kSrcW);
        public static float EyeTravelY => kEyeTravelYSrc * (kContH / kSrcH);
        public static float LeverTravel => kLeverTravelSrc * (kContH / kSrcH);

        /// <summary>源图像素(左上原点) → 容器局部坐标(容器中心 = 0,0)</summary>
        public static Vector2 SrcToContainer(Vector2 p) =>
            new Vector2((p.x / kSrcW - 0.5f) * kContW, (0.5f - p.y / kSrcH) * kContH);

        static EcManifest _mf;
        static bool _loaded;

        public static bool HasManifest { get { Load(); return _mf != null; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var ta = Resources.Load<TextAsset>(kManifestPath);
            if (ta == null)
            {
                Debug.LogError("[EyeConsole] 缺 " + kManifestPath + ".json，" +
                               "跑 tools/eye_console_pack.py 生成。");
                return;
            }
            try { _mf = JsonUtility.FromJson<EcManifest>(ta.text); }
            catch (Exception e) { Debug.LogError("[EyeConsole] manifest 解析失败: " + e.Message); }
        }

        // ── 入口 ─────────────────────────────────────────────────────────
        /// <summary>纯交互：不开谜题判定，只是让玩家摆弄控制台。</summary>
        public static void Show(Action onClosed = null)
        {
            Show(onClosed, null, null);
        }

        /// <summary>
        /// 谜题模式：solution 里非 NaN 的项参与判定，玩家把三个控件都摆到位
        /// 并稳定保持约 0.35 秒后回调 onSolved，然后自动收起特写
        /// （收起时会再触发 onClosed）。
        /// </summary>
        public static void Show(Action onClosed, EyeConsoleSolution solution, Action onSolved)
        {
            _onClosed = onClosed;
            _solution = solution;
            _onSolved = onSolved;
            var go = Build();
            CloseupView.OpenExisting(go, () =>
            {
                var cb = _onClosed;
                _onClosed = null;
                _solution = null;
                _onSolved = null;
                cb?.Invoke();
            });
            CloseupView.SetCanClose(true);
        }

        /// <summary>立刻收起特写。会走 OpenExisting 的收尾回调：清理状态并触发 onClosed。</summary>
        public static void Close()
        {
            CloseupView.Close();
        }

        // ── 构建 ─────────────────────────────────────────────────────────
        static GameObject Build()
        {
            Load();
            Vector2 canvasCenter = new Vector2(kSrcW * 0.5f, kSrcH * 0.5f);

            var root = new GameObject("EyeConsole");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(kContW, kContH);
            rootRT.pivot = new Vector2(0.5f, 0.5f);

            // 0) 最底层：控制台本体（拉杆旋转序列帧）
            //    拉杆已经画在这一层里，不再有单独的拉杆图层。
            //    位置/大小由 manifest 的 base.rect / base.pivot 决定，与旧
            //    ec_bg.png 的落点完全一致 —— 上面的旋钮图层不用挪。
            var baseFrames = LoadLayerFrames("base");
            Image baseImg = null;
            if (baseFrames.Length > 0)
            {
                var baseGo = new GameObject("Base");
                baseGo.transform.SetParent(root.transform, false);
                var baseRT = baseGo.AddComponent<RectTransform>();
                PlaceLayer(baseRT, "base", canvasCenter);
                baseImg = baseGo.AddComponent<Image>();
                baseImg.sprite = baseFrames[0];
                baseImg.preserveAspect = false;
                baseImg.raycastTarget = false;
            }
            else
            {
                // 兜底：序列帧缺失时退回旧静态底图 ec_bg.png（整容器拉伸层）
                Debug.LogWarning("[EyeConsole] 缺 base 序列帧（ec_base_00..N），" +
                                 "退回静态底图 ec_bg.png，拉杆不会转动。");
                var bgGo = new GameObject("Bg");
                bgGo.transform.SetParent(root.transform, false);
                var bgRT = bgGo.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.sprite = LoadSprite(kResDir + "ec_bg");
                bgImg.preserveAspect = false;
                bgImg.raycastTarget = false;
                if (bgImg.sprite == null) bgImg.enabled = false;
            }

            // 1) 铜框 + 眼窝内壁（静态）
            AddLayer(root.transform, "EyeFrameBack", "eye_frame_back", canvasCenter);

            // 2) 眼窝开口遮罩 → 眼球在遮罩内平移
            // 层级顺序很重要：前景眼睑在后面单独盖上去，不能把球画在眼睑上。
            var maskGo = new GameObject("EyeOpeningMask");
            maskGo.transform.SetParent(root.transform, false);
            var maskRT = maskGo.AddComponent<RectTransform>();
            Vector2 maskCenter = PlaceLayer(maskRT, "eye_mask", canvasCenter);
            var maskImg = maskGo.AddComponent<Image>();
            var maskSp = LoadSprite(ResPathOf("eye_mask"));
            maskImg.sprite = maskSp;
            maskImg.raycastTarget = false;
            RectTransform eyeRT = null;
            if (maskSp != null)
            {
                maskGo.AddComponent<Mask>().showMaskGraphic = false;
                eyeRT = AddLayer(maskGo.transform, "Eyeball", "eyeball", maskCenter);
            }
            else
            {
                maskImg.enabled = false;
                Debug.LogWarning("[EyeConsole] 缺眼窝遮罩，眼球不做裁剪（会露出眼窝边界）。");
                eyeRT = AddLayer(root.transform, "Eyeball", "eyeball", canvasCenter);
            }

            // 2.5) 眼睑 / 眼眶前景：**最后画，压在眼球上面**
            // 这是用户指出的关键层级：正常状态下眼球必须被眼睑边缘遮挡。
            AddLayer(root.transform, "EyeFrameFront", "eye_frame_front", canvasCenter);

            // 2.6) 眼睑开合（多帧覆盖层，压在眼眶前景之上）
            // 素材是整眼渲染，已经用「与最开帧的差异」抠成纯眼睑覆盖层，
            // 所以只盖住落下的眼睑，眼窝里由旋钮控制的眼球仍然可见。
            var lidFrames = LoadLayerFrames("eyelid");
            Image lidImg = null;
            if (lidFrames.Length > 0)
            {
                var lidGo = new GameObject("Eyelid");
                lidGo.transform.SetParent(root.transform, false);
                var lidRT = lidGo.AddComponent<RectTransform>();
                PlaceLayer(lidRT, "eyelid", canvasCenter);
                lidImg = lidGo.AddComponent<Image>();
                lidImg.preserveAspect = false;
                lidImg.raycastTarget = false;
                lidImg.sprite = lidFrames[0];
                lidImg.enabled = false;   // 第 0 帧是「全开」，本来就是空的
            }

            // 3~6) 两个旋钮：外壳静态 + 顶面绕轴自转（pivot 相同，所以能对齐）
            var knobLeftShell = AddLayer(root.transform, "KnobLeftShell", "knob_left_shell", canvasCenter);
            var knobLeftTop = AddLayer(root.transform, "KnobLeftTop", "knob_left_top", canvasCenter);
            var knobMidShell = AddLayer(root.transform, "KnobMidShell", "knob_mid_shell", canvasCenter);
            var knobMidTop = AddLayer(root.transform, "KnobMidTop", "knob_mid_top", canvasCenter);
            // 外壳和顶面的锚点必须一致
            if (knobLeftShell != null && knobLeftTop != null) knobLeftTop.anchoredPosition = knobLeftShell.anchoredPosition;
            if (knobMidShell != null && knobMidTop != null) knobMidTop.anchoredPosition = knobMidShell.anchoredPosition;

            // 7) 拉杆：**不再单独成层**。拉杆的旋转已经烘进 Base 序列帧里，
            //    这里只留一个不可见的「拉杆位置锚点」，给拖拽手柄当坐标用
            //    （手柄本身也是近乎全透明，只负责接收拖拽）。
            RectTransform leverRT = null;
            {
                var anchorGo = new GameObject("LeverAnchor");
                anchorGo.transform.SetParent(root.transform, false);
                leverRT = anchorGo.AddComponent<RectTransform>();
                leverRT.anchorMin = leverRT.anchorMax = new Vector2(0.5f, 0.5f);
                leverRT.pivot = new Vector2(0.5f, 0.5f);
                leverRT.sizeDelta = Vector2.zero;
                leverRT.anchoredPosition =
                    SrcToContainer(kLeverBallSrc) - SrcToContainer(canvasCenter);
            }

            // 8) 全局 hitbox
            var hitGo = new GameObject("GlobalHitbox");
            hitGo.transform.SetParent(root.transform, false);
            var hitImg = hitGo.AddComponent<Image>();
            hitImg.color = new Color(0, 0, 0, 0.001f);
            // 不能让全屏 hitbox 参与射线：它会盖住左旋钮和拉杆，
            // 导致点击任何位置都落进同一个默认分支。交互改用三个独立手柄。
            hitImg.raycastTarget = false;
            var hitRT = hitImg.rectTransform;
            hitRT.anchorMin = Vector2.zero; hitRT.anchorMax = Vector2.one;
            hitRT.offsetMin = Vector2.zero; hitRT.offsetMax = Vector2.zero;

            var bh = root.AddComponent<EyeConsoleBehaviour>();
            bh.eyeRT = eyeRT;
            bh.eyeBasePos = eyeRT != null ? eyeRT.anchoredPosition : Vector2.zero;
            bh.knobLeftShellRT = knobLeftShell;
            bh.knobLeftTopRT = knobLeftTop;
            bh.knobMidShellRT = knobMidShell;
            bh.knobMidTopRT = knobMidTop;
            bh.leverAnchorRT = leverRT;
            bh.baseImg = baseImg;
            bh.baseFrames = baseFrames;
            bh.lidImg = lidImg;
            bh.lidFrames = lidFrames;
            bh.solution = _solution;
            bh.onSolved = _onSolved;

            // 三个互不重叠的透明手柄：每个只负责拖自己的控件。
            // 空白区域不触发任何控件，避免「点哪里都操控中旋钮」。
            AddControlHandle(root.transform, "LeftKnobHit", knobLeftShell,
                              new Vector2(150f, 150f), ControlId.LeftKnob, bh);
            AddControlHandle(root.transform, "MidKnobHit", knobMidShell,
                              new Vector2(150f, 150f), ControlId.MidKnob, bh);
            // 拉杆是绕底座往左上转的，热区要盖住整段行程
            AddControlHandle(root.transform, "LeverHit", leverRT,
                              new Vector2(170f, 330f), ControlId.Lever, bh);

            return root;
        }

        // ── 图层摆放 ─────────────────────────────────────────────────────
        /// <summary>按 manifest 摆好 rt：让图层上 pivot 那个点落在源图 pivot 的位置。
        /// 返回该层裁剪框中心（源图像素），给子层当坐标基准用。</summary>
        static Vector2 PlaceLayer(RectTransform rt, string key, Vector2 parentCenterSrc)
        {
            Load();
            var L = _mf != null ? _mf.Find(key) : null;
            if (L == null || L.rect == null || L.rect.Length != 4)
            {
                Debug.LogError("[EyeConsole] manifest 里没有图层 " + key);
                return parentCenterSrc;
            }
            var rectPx = new Rect(L.rect[0], L.rect[1], L.rect[2] - L.rect[0], L.rect[3] - L.rect[1]);
            var pivPx = new Vector2(L.pivot[0], L.pivot[1]);

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2((pivPx.x - rectPx.x) / rectPx.width,
                                   1f - (pivPx.y - rectPx.y) / rectPx.height);
            rt.sizeDelta = new Vector2(rectPx.width * (kContW / kSrcW),
                                       rectPx.height * (kContH / kSrcH));
            rt.anchoredPosition = SrcToContainer(pivPx) - SrcToContainer(parentCenterSrc);
            return new Vector2(rectPx.x + rectPx.width * 0.5f, rectPx.y + rectPx.height * 0.5f);
        }

        /// <summary>给一个控件挂近乎全透明的拖拽手柄（只负责接收拖拽）。</summary>
        static void AddControlHandle(Transform parent, string name, RectTransform target,
                                     Vector2 size, ControlId control, EyeConsoleBehaviour behaviour)
        {
            if (target == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = target.anchoredPosition;

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.001f);
            img.raycastTarget = true;
            go.transform.SetAsLastSibling();

            var handle = go.AddComponent<EyeConsoleControlDrag>();
            handle.rootRT = parent.GetComponent<RectTransform>();
            handle.behaviour = behaviour;
            handle.control = control;
        }

        static RectTransform AddLayer(Transform parent, string name, string key, Vector2 parentCenterSrc)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            PlaceLayer(rt, key, parentCenterSrc);
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(ResPathOf(key));
            if (img.sprite == null) img.enabled = false;
            img.preserveAspect = false;
            img.raycastTarget = false;
            return rt;
        }

        static string ResPathOf(string key)
        {
            Load();
            var L = _mf != null ? _mf.Find(key) : null;
            string file = (L != null && !string.IsNullOrEmpty(L.file)) ? L.file : ("ec_" + key);
            return kResDir + file;
        }

        /// <summary>读某个多帧图层的全部帧（manifest 里带 frames[]）。</summary>
        static Sprite[] LoadLayerFrames(string key)
        {
            Load();
            var L = _mf != null ? _mf.Find(key) : null;
            var list = new System.Collections.Generic.List<Sprite>();
            if (L == null) return list.ToArray();
            if (L.frames != null && L.frames.Length > 0)
            {
                for (int i = 0; i < L.frames.Length; i++)
                {
                    var sp = LoadSprite(kResDir + L.frames[i]);
                    if (sp != null) list.Add(sp);
                }
            }
            else if (!string.IsNullOrEmpty(L.file))
            {
                var sp = LoadSprite(kResDir + L.file);
                if (sp != null) list.Add(sp);
            }
            return list.ToArray();
        }

        static Sprite LoadSprite(string resPath)
        {
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex == null) return null;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        // ====================================================================
        //  EyeConsoleBehaviour —— 主逻辑
        // ====================================================================
        public class EyeConsoleBehaviour : MonoBehaviour
        {
            public RectTransform eyeRT;
            public Vector2 eyeBasePos;
            public RectTransform knobLeftShellRT;
            public RectTransform knobLeftTopRT;
            public RectTransform knobMidShellRT;
            public RectTransform knobMidTopRT;
            /// <summary>不可见的拉杆位置锚点（只给拖拽手柄当坐标基准，不参与绘制）。</summary>
            public RectTransform leverAnchorRT;
            /// <summary>最底层控制台本体（拉杆旋转序列帧）。</summary>
            public Image baseImg;
            public Sprite[] baseFrames;
            public Image lidImg;
            public Sprite[] lidFrames;

            /// <summary>谜题目标值（null = 纯交互不判定）。</summary>
            public EyeConsoleSolution solution;
            /// <summary>谜题解开后回调（在自动收起特写之前触发）。</summary>
            public Action onSolved;

            [Header("左旋钮角度(-135~135)：负=看左，正=看右")]
            public float knobLeftAngle;
            [Header("中旋钮角度(-135~135)：负=看上，正=看下")]
            public float knobMidAngle;
            [Header("拉杆进度(0=全开/杆在静止位, 1=全闭/杆已拉满)")]
            public float leverT;

            public float smooth = 22f;

            // 谜题判定：目标值需稳定保持这么久才算解开（防止拖过头路过的瞬间误判）
            const float kHoldTime = 0.35f;
            float _holdTimer;
            bool _solved;

            float _shownLeft, _shownMid, _shownLever;
            int _shownBaseIdx = -1, _shownLidIdx = -1;

            void Awake()
            {
                knobLeftAngle = 0f;
                knobMidAngle = 0f;
                leverT = 0f;
                _shownLeft = _shownMid = _shownLever = 0f;
                ApplyKnobs();
                ApplyEye();
                ApplyLever();
            }

            public float LookX01 => (knobLeftAngle / kKnobMaxAngle + 1f) * 0.5f;
            public float LookY01 => (knobMidAngle / kKnobMaxAngle + 1f) * 0.5f;

            public void TurnKnob(bool isLeft, float deltaDeg)
            {
                if (isLeft)
                    knobLeftAngle = Mathf.Clamp(knobLeftAngle + deltaDeg, -kKnobMaxAngle, kKnobMaxAngle);
                else
                    knobMidAngle = Mathf.Clamp(knobMidAngle + deltaDeg, -kKnobMaxAngle, kKnobMaxAngle);
                ApplyKnobs();
            }

            public void SetLever(float t)
            {
                leverT = Mathf.Clamp01(t);
                _shownLever = leverT;
                ApplyLever();
            }

            void Update()
            {
                float k = 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime);
                _shownLeft = Mathf.Lerp(_shownLeft, knobLeftAngle, k);
                _shownMid = Mathf.Lerp(_shownMid, knobMidAngle, k);
                _shownLever = Mathf.Lerp(_shownLever, leverT, k);
                if (Mathf.Abs(_shownLeft - knobLeftAngle) < 0.15f) _shownLeft = knobLeftAngle;
                if (Mathf.Abs(_shownMid - knobMidAngle) < 0.15f) _shownMid = knobMidAngle;
                if (Mathf.Abs(_shownLever - leverT) < 0.002f) _shownLever = leverT;

                ApplyKnobs();
                ApplyEye();
                ApplyLever();
                CheckSolution();
            }

            // ── 谜题判定 ─────────────────────────────────────────────────
            //  solution 里非 NaN 的项都满足 |当前值-目标值| <= tolerance，
            //  且稳定保持 kHoldTime 秒 → 算解开：回调 onSolved 后自动收起特写。
            void CheckSolution()
            {
                if (_solved || solution == null || onSolved == null) return;
                if (!Within(solution.lookX01, LookX01, solution.tolerance) ||
                    !Within(solution.lookY01, LookY01, solution.tolerance) ||
                    !Within(solution.leverT, leverT, solution.tolerance))
                {
                    _holdTimer = 0f;
                    return;
                }
                _holdTimer += Time.unscaledDeltaTime;
                if (_holdTimer < kHoldTime) return;
                _solved = true;
                var cb = onSolved;
                onSolved = null;
                cb?.Invoke();
                Close();   // 解开即收起（会触发外部的 onClosed 回调）
            }

            static bool Within(float target, float value, float tol)
            {
                if (float.IsNaN(target)) return true;   // 该项不参与判定
                return Mathf.Abs(value - Mathf.Clamp01(target)) <= Mathf.Max(0.0001f, tol);
            }

            void ApplyKnobs()
            {
                // 拖拽算出来的角度是「屏幕逆时针为正」，Unity 的 Z 旋转也是逆时针为正，
                // 所以直接用，不要取负。
                if (knobLeftTopRT != null) knobLeftTopRT.localRotation = Quaternion.Euler(0f, 0f, _shownLeft);
                if (knobMidTopRT != null) knobMidTopRT.localRotation = Quaternion.Euler(0f, 0f, _shownMid);
            }

            void ApplyEye()
            {
                if (eyeRT == null) return;
                // 眼球在眼窝遮罩内平移：左旋钮管左右，中旋钮管上下
                float x = _shownLeft / kKnobMaxAngle * EyeTravelX;
                float y = -_shownMid / kKnobMaxAngle * EyeTravelY;
                eyeRT.anchoredPosition = eyeBasePos + new Vector2(x, y);
            }

            void ApplyLever()
            {
                // ── 拉杆与眼睑是「同一根拉杆」的两个表现，联动方式 ──────────
                //    leverT = 0  → 拉杆停在静止位(倾斜靠右) + 眼睑全开
                //    leverT = 1  → 拉杆转到拉满位(竖直偏左) + 眼睑全闭
                //  两者都从同一个 _shownLever 取帧索引，所以拖到一半松手时
                //  杆和眼皮一定在同一进度上，不会各走各的。
                ApplyLeverFrame(_shownLever);
                ApplyEyelidFrame(_shownLever);
            }

            /// <summary>拉杆旋转序列帧：按进度定格（不要按时间播，否则拖到一半松手会不同步）。</summary>
            void ApplyLeverFrame(float t)
            {
                if (baseImg == null || baseFrames == null || baseFrames.Length == 0) return;
                int i = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * (baseFrames.Length - 1)),
                                    0, baseFrames.Length - 1);
                if (i == _shownBaseIdx) return;
                _shownBaseIdx = i;
                baseImg.sprite = baseFrames[i];
            }

            /// <summary>眼睑开合序列帧：0 = 全开，末帧 = 全闭。</summary>
            void ApplyEyelidFrame(float t)
            {
                if (lidImg == null || lidFrames == null || lidFrames.Length == 0) return;
                if (t <= 0.002f)
                {
                    // 第 0 帧本来就是「全开」（空图），直接整层关掉省一次绘制
                    if (lidImg.enabled) lidImg.enabled = false;
                    _shownLidIdx = -1;
                    return;
                }
                if (!lidImg.enabled) lidImg.enabled = true;
                int i = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * (lidFrames.Length - 1)),
                                    0, lidFrames.Length - 1);
                if (i == _shownLidIdx) return;
                _shownLidIdx = i;
                lidImg.sprite = lidFrames[i];
            }

            public static Vector2 ScreenToLocal(RectTransform rootRT, Vector2 screenPos, Camera cam)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRT, screenPos, cam, out var local);
                return local;
            }
        }

        // ====================================================================
        //  EyeConsoleControlDrag —— 控件拖拽（旋钮绕轴转 / 拉杆上下行程）
        // ====================================================================
        public class EyeConsoleControlDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public RectTransform rootRT;
            public EyeConsoleBehaviour behaviour;
            public ControlId control;
            float _lastAngle;
            float _startY;

            public void OnBeginDrag(PointerEventData e)
            {
                if (rootRT == null) rootRT = GetComponentInParent<RectTransform>();
                if (rootRT == null || behaviour == null) return;
                Vector2 local = EyeConsoleBehaviour.ScreenToLocal(rootRT, e.position, e.pressEventCamera);
                if (control == ControlId.LeftKnob)
                    _lastAngle = AngleAround(SrcToContainer(kKnobLeftSrc), local);
                else if (control == ControlId.MidKnob)
                    _lastAngle = AngleAround(SrcToContainer(kKnobMidSrc), local);
                else
                    _startY = local.y;
            }

            public void OnDrag(PointerEventData e)
            {
                if (rootRT == null || behaviour == null) return;
                Vector2 local = EyeConsoleBehaviour.ScreenToLocal(rootRT, e.position, e.pressEventCamera);
                if (control == ControlId.LeftKnob || control == ControlId.MidKnob)
                {
                    Vector2 center = control == ControlId.LeftKnob
                        ? SrcToContainer(kKnobLeftSrc) : SrcToContainer(kKnobMidSrc);
                    float angle = AngleAround(center, local);
                    float delta = Mathf.DeltaAngle(_lastAngle, angle);
                    _lastAngle = angle;
                    behaviour.TurnKnob(control == ControlId.LeftKnob, delta);
                }
                else
                {
                    // 新序列帧里拉杆是「绕底座往左上转上去」的，所以跟着往上拖
                    // 才是把杆拉起来。
                    float dy = local.y - _startY;
                    behaviour.SetLever(behaviour.leverT + dy / LeverTravel);
                    _startY = local.y;
                }
            }

            public void OnEndDrag(PointerEventData e) { }

            static float AngleAround(Vector2 center, Vector2 p)
            {
                Vector2 d = p - center;
                return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
        }
    }
}
