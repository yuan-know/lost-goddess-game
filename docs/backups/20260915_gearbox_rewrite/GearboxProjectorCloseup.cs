// ============================================================================
//  GearboxProjectorCloseup.cs —— PZ_04z 齿轮箱「精密操作」特写(S05 千眼回廊·中年线)
//
//  ★ 2026-09-15 重写:旧版 GearboxProjectorPuzzle 是**世界空间**的假特写
//    (自建正交相机 + 缩放根节点 + 手算"条带世界等价"),跟真实渲染路径完全不是
//    一回事。真实特写是 CloseupView 的 **ScreenSpaceOverlay UI 层**(1920×678 容器 +
//    70% 黑底),参照 GearDialCloseup 的写法。本文件按那条路重做。
//
//  玩法(用户 2026-09-14/15 口述为准):
//    ① 调整底座仰角(水平 → 上倾 15°):按住顶部竖轴做圆周拖拽。
//       ★ 只让**内部机构**倾(齿轮组 + 竖轴 + 阀件),木箱外框稳稳不动。
//       齿轮组联动旋转;到 15° 时先"轻微卡顿"再"咔哒"过冲吸附到位。
//       水平(0°)时光束打在左侧墙**低处**,随仰角抬升爬到**高处**。
//    ② 调整摆动幅度(左右全幅 → 集中左侧):拖箱右缘凸出的黄铜旋钮。
//       ★ 旋钮做**沿轴推拉**(往里推 = 收拢),它有独立图层,会真的动。
//    ③ 锁定角度(固定投射在左侧墙高处):点击中间 S 形弯管阀件。
//    ✗ 顺序错误(仰角未锁就先碰旋钮)→ S 形弯管喷蒸汽 + 刻痕
//      「偏之一线,灼身千里」+ 进度回滚。
//
//  坐标系:素材是 3400×1200 全画布,容器 1920×678 → k = 1920/3400 = 0.564706。
//    画布像素 (x,y) → 容器局部 (x*k - 960, 339 - y*k)。
//    所有锚点/轴心 = docs/gearbox/gearbox_measure.json 的实测值。
//
//  ⚠ 世界空间的特效在 ScreenSpaceOverlay 里**看不见**(被 70% 黑底压住):
//    蒸汽改用 UI 逐帧换 sprite(tools/gearbox_export.py 打的 steam_sheet.png)。
//    音效走项目自己的 AudioManager.PlaySfx。
//
//  使用方式:GearboxProjectorCloseup.Show(() => { /* 解谜成功回调 */ });
//  调试场景:Scenes/GearboxDebug.unity + Utils/GearboxDebugBootstrap.cs
//  离线核对:tools/gearbox_sim.py --verify-only
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class GearboxProjectorCloseup
    {
        static Action _onSolved;

        // ── 容器 / 画布 ───────────────────────────────────────────────────
        public const float kCanvasW = 3400f;
        public const float kCanvasH = 1200f;
        public const float kContainerW = 1920f;
        public const float kContainerH = 678f;
        /// <summary>画布中心(3400×1200 的正中)—— 显示变换的原点。</summary>
        public const float kCanvasCenterX = 1700f, kCanvasCenterY = 600f;
        /// <summary>画布像素 → 容器像素。
        /// ★ 2026-09-15:这个值不是"1920/3400 刚好塞满",而是**拟合项目里那张成品面板**
        ///   `Resources/Closeups/gear_mechanism_closeup.png`(1920×677,用户 2026-09-11 给的、
        ///   并已在 SmokeSprayInScene 里摆好的那张)得到的:把本文件这五层按 scale 缩放后
        ///   与该面板比对,scale=0.635 时形状 IoU = **0.9929**(0.5647 只有 0.785)。
        ///   也就是说面板 = 本套图放大 12.4% 后的画面;照抄它才是"复现现成的",
        ///   别再用"刚好塞满容器"去推。复核脚本见 tools/gearbox_sim.py --verify-only。</summary>
        public const float kScale = 0.635f;

        // ── 图层锚点(画布像素)+ 轴心(贴图内归一化)────────────────────────
        public const float RootPxX = 1732.5f, RootPxY = 1015f;   // 箱底中心 = 内部机构旋转轴
        const float RectX0 = 1310f, RectY0 = 120f, RectW = 835f, RectH = 988f;

        static readonly Vector2 ValvePx = new Vector2(1691f, 720f);
        static readonly Vector2 GearSmallPx = new Vector2(1550f, 522f);
        static readonly Vector2 GearBigPx = new Vector2(1787f, 350f);
        static readonly Vector2 ShaftPx = new Vector2(1781f, 704f);
        /// <summary>光束发射点(大齿轮上缘,画布像素)。</summary>
        static readonly Vector2 BeamEmitPx = new Vector2(1787f, 300f);
        /// <summary>蒸汽喷口(画布像素)。
        /// ★ 用**用户定稿值**:SmokeSprayDebugBuilder 里 `EmitPxX=903, EmitPxY=393`
        ///   是 2026-09-11 用户在 SmokeSprayInScene 里手动拖出来的,注释写着"直接照抄,别再改"。
        ///   按面板↔画布变换反算回画布 = (1610, 686)。
        ///   (我 2026-09-15 自己放大 4 倍量的 (1583,688) 差 27px —— 以定稿值为准。)</summary>
        static readonly Vector2 SpoutPx = new Vector2(1610f, 686f);
        /// <summary>右缘旋钮:裁切框左缘中点(推拉的锚点)。</summary>
        static readonly Vector2 KnobPivotPx = new Vector2(2042f, 605f);
        static readonly Vector2 KnobCenterPx = new Vector2(2092f, 605f);
        const float KnobRectW = 98f, KnobRectH = 146f;

        /// <summary>图层显示尺寸(835×988 裁切 × k)。</summary>
        static readonly Vector2 LayerSize = new Vector2(RectW * kScale, RectH * kScale);
        static readonly Vector2 KnobSize = new Vector2(KnobRectW * kScale, KnobRectH * kScale);

        static Vector2 PivotFor(float ax, float ay)
        {
            return new Vector2((ax - RectX0) / RectW, 1f - (ay - RectY0) / RectH);
        }

        /// <summary>画布像素 → 容器局部坐标(原点 = 画布中心)。</summary>
        public static Vector2 CanvasToLocal(float x, float y)
        {
            return new Vector2((x - kCanvasCenterX) * kScale,
                               (kCanvasCenterY - y) * kScale);
        }
        public static Vector2 CanvasToLocal(Vector2 p) { return CanvasToLocal(p.x, p.y); }

        /// <summary>绕箱底中心(画布像素空间)旋转一点。+deg = 逆时针 = 上倾。</summary>
        public static Vector2 RotateAroundRoot(Vector2 canvasPx, float deg)
        {
            float ax = canvasPx.x - RootPxX, ay = RootPxY - canvasPx.y;   // 画布 y 翻转成"上为正"
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(RootPxX + ax * c - ay * s, RootPxY - (ax * s + ay * c));
        }

        // ── 靶点:左侧墙高处(**容器局部坐标**,不是世界坐标)───────────────
        //  UI 特写里没有世界墙;可见区就是 1920×678 容器。靶点取容器左上,
        //  对应画布像素约 (354,228) —— 在木箱左侧那片压暗的走廊墙上。
        //  调试场景方向键可实时挪,P 打印定稿值。
        public static Vector2 BeamHitLocal = new Vector2(-762f, 208f);
        /// <summary>左侧墙平面(光束落点都在这条竖线上)。</summary>
        static float WallX { get { return BeamHitLocal.x; } }

        // ── 手感参数(调试场景可实时调,定稿后回填默认值)─────────────────────
        public static float DragDegPerFullTilt = 270f;   // 圆周拖拽满 270° = 0→15°
        public static float DragSign = 1f;
        public static float TiltFollowSmoothTime = 0.08f;
        public static float DetentHitchTilt = 14.35f;    // 卡顿点(略低于 15°)
        public static float DetentHitchTime = 0.13f;
        public static float DetentSnapTime = 0.20f;
        public static float SnapOvershootDeg = 0.7f;     // 吸附过冲的绝对角度
        public static float GearBigRatio = -2.2f;
        public static float GearSmallRatio = 3.1f;
        public static float GearFollowSmoothTime = 0.06f;
        public static float ShaftLeanMaxDeg = 5f;
        public static float RatchetDeg = 22.5f;
        public static float ShaftMinRadiusPx = 14f;      // 圆周拖拽的圆心死区(避角度奇点)
        public static float ShaftMaxDeltaPerFrame = 120f;

        public static float KnobPushTravel = 13f;        // 旋钮推进/拔出的行程(容器 px)
        public static float KnobPushScale = 0.90f;       // 推到底时的横向缩放
        public static float AmplitudePerUnit = 0.55f;    // 每拖 1 容器 px 的幅度变化
        public static float AmplitudeThreshold = 0.35f;
        public static float AmplitudeSweepMax = 26f;     // 满幅摆动半角(度)
        public static float SweepPeriod = 2.6f;

        /// <summary>烟雾朝向(度)。定稿值,见 SmokeSprayDebugBuilder.EmitAngleDeg。</summary>
        public const float kSteamAngleDeg = -8.9f;
        /// <summary>烟雾精灵尺寸与缩放(定稿值,见 SmokeSprayDebugBuilder)。</summary>
        public const float kSmokeSpriteW = 480f, kSmokeSpriteH = 204f;
        public const float kSmokeScale = 0.45f;
        public static float SteamFps = 24f;
        public static float ScarFadeTime = 0.8f;
        public static float RollbackTime = 0.7f;
        public static float LongPressHintTime = 0.55f;
        public static bool ShowGuide = true;

        public static string ClickSfx = "gearbox_click";
        public static string SteamSfx = "gearbox_steam_hiss";

        public static void Show(Action onSolved = null)
        {
            _onSolved = onSolved;
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);
            var template = BuildTemplate();
            CloseupView.OpenExisting(template);
            CloseupView.SetCanClose(true);
        }

        public static void NotifySolved()
        {
            var cb = _onSolved;
            _onSolved = null;
            cb?.Invoke();
        }

        // ── 构建 UI ───────────────────────────────────────────────────────
        static GameObject BuildTemplate()
        {
            var root = new GameObject("GearboxProjector");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(kContainerW, kContainerH);
            rootRT.pivot = new Vector2(0.5f, 0.5f);

            // 1) 木箱底图(整块 835×988 裁切,轴心 = 箱底中心)
            var baseRT = MakeLayer(root.transform, "Base", "Closeups/GearboxPuzzle/gearbox_base",
                                   RootPxX, RootPxY, PivotFor(RootPxX, RootPxY));

            // 2) 光束(放在内部机构**下面**,看起来是从齿轮后面射出来的)
            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(root.transform, false);
            var beamRT = beamGo.AddComponent<RectTransform>();
            beamRT.anchorMin = beamRT.anchorMax = new Vector2(0.5f, 0.5f);
            beamRT.pivot = new Vector2(0.5f, 0.5f);
            var beamImg = beamGo.AddComponent<Image>();
            beamImg.raycastTarget = false;
            beamImg.color = new Color(1f, 0.96f, 0.82f, 0.26f);

            // 3) 内部机构(★ 只有这一坨会随仰角旋转;木箱外框不动)
            var cradleGo = new GameObject("InnerCradle");
            cradleGo.transform.SetParent(root.transform, false);
            var cradleRT = cradleGo.AddComponent<RectTransform>();
            cradleRT.anchorMin = cradleRT.anchorMax = new Vector2(0.5f, 0.5f);
            cradleRT.pivot = new Vector2(0.5f, 0.5f);
            cradleRT.sizeDelta = Vector2.zero;                 // 只当旋转轴用
            cradleRT.anchoredPosition = CanvasToLocal(RootPxX, RootPxY);

            var cradle = cradleRT;
            MakeCradleLayer(cradle, "GearSmall", "Closeups/GearboxPuzzle/gearbox_gear_small", GearSmallPx);
            MakeCradleLayer(cradle, "GearBig", "Closeups/GearboxPuzzle/gearbox_gear_big", GearBigPx);
            MakeCradleLayer(cradle, "Shaft", "Closeups/GearboxPuzzle/gearbox_shaft", ShaftPx);
            var valveRT = MakeCradleLayer(cradle, "Valve", "Closeups/GearboxPuzzle/gearbox_valve", ValvePx);

            // 3b) 蒸汽(UI 逐帧;挂在机构下 → 跟着仰角走,喷口永远对得上)
            var steamGo = new GameObject("Steam");
            steamGo.transform.SetParent(cradle, false);
            var steamRT = steamGo.AddComponent<RectTransform>();
            steamRT.anchorMin = steamRT.anchorMax = new Vector2(0.5f, 0.5f);
            steamRT.pivot = new Vector2(1f, 1f);       // 贴图右上角 ≈ 烟雾的"喷出源"
            // ★ 定稿 SmokeScale = 0.45:喉部 53px*0.45≈24px = 喷嘴开口高。
            //   面板像素 = 480*0.45 = 216 宽 / 204*0.45 ≈ 92 高。
            steamRT.sizeDelta = new Vector2(kSmokeSpriteW * kSmokeScale,
                                            kSmokeSpriteH * kSmokeScale);
            steamRT.anchoredPosition = CanvasToLocal(SpoutPx) - CanvasToLocal(RootPxX, RootPxY);
            // ★ 定稿 -8.9°(SmokeSprayDebugBuilder.EmitAngleDeg,用户拖出来的)。
            //   精灵喷出方向是局部 -X;Unity Z 正角逆时针 → **正角往下**,所以要**负角**才往左上翘。
            //   我一开始写成 +8,方向是反的。
            steamRT.localRotation = Quaternion.Euler(0f, 0f, kSteamAngleDeg);
            var steamImg = steamGo.AddComponent<Image>();
            steamImg.raycastTarget = false;
            steamImg.enabled = false;

            // 4) 右缘旋钮(独立图层;推拉)
            var knobRT = MakeLayer(root.transform, "Knob", "Closeups/GearboxPuzzle/gearbox_knob",
                                   KnobPivotPx.x, KnobPivotPx.y, new Vector2(0f, 0.5f), KnobSize);

            // 5) 靶环 + 十字准星
            var reticleGo = new GameObject("Reticle");
            reticleGo.transform.SetParent(root.transform, false);
            var reticleRT = reticleGo.AddComponent<RectTransform>();
            reticleRT.anchorMin = reticleRT.anchorMax = new Vector2(0.5f, 0.5f);
            reticleRT.pivot = new Vector2(0.5f, 0.5f);
            reticleRT.sizeDelta = new Vector2(132f, 132f);
            reticleRT.anchoredPosition = BeamHitLocal;
            var reticleImg = reticleGo.AddComponent<Image>();
            reticleImg.raycastTarget = false;
            reticleImg.sprite = MakeReticleSprite(128, 52f, 3.2f, new Color(1f, 0.88f, 0.55f, 0.5f));

            // 6) 刻痕(暗色错位副本垫底,亮红字压在上面)
            var scarShadowRT = MakeScarLayer(root.transform, "ScarShadow",
                new Color(0.02f, 0.01f, 0.01f, 0f), new Vector2(3f, -193f));
            var scarRT = MakeScarLayer(root.transform, "Scar",
                new Color(1f, 1f, 1f, 0f), new Vector2(0f, -190f));

            // 7) 红闪
            var flashGo = new GameObject("RedFlash");
            flashGo.transform.SetParent(root.transform, false);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(0.55f, 0.05f, 0.04f, 0f);
            var flashRT = flashImg.rectTransform;
            flashRT.anchorMin = new Vector2(0f, 0f);
            flashRT.anchorMax = new Vector2(1f, 1f);
            flashRT.offsetMin = new Vector2(-960f, -201f);   // 铺满整屏(含上下黑边)
            flashRT.offsetMax = new Vector2(960f, 201f);

            // 8) 提示条(容器上沿之外,压在压暗的房间里)
            var toastGo = new GameObject("Toast");
            toastGo.transform.SetParent(root.transform, false);
            var toast = toastGo.AddComponent<Text>();
            toast.font = GameFonts.Primary;
            toast.fontSize = 40;
            toast.alignment = TextAnchor.MiddleCenter;
            toast.raycastTarget = false;
            toast.horizontalOverflow = HorizontalWrapMode.Overflow;
            var to = toastGo.AddComponent<Outline>();
            to.effectColor = Color.black;
            to.effectDistance = new Vector2(2.5f, 2.5f);
            // 提示条放在**箱体正上方那条空档**里(容器内 y 264..339,木箱顶边之上),
            // 不跟顶部的流程指导抢位置。
            var toastRT = toast.rectTransform;
            toastRT.anchorMin = toastRT.anchorMax = new Vector2(0.5f, 1f);
            toastRT.pivot = new Vector2(0.5f, 1f);
            toastRT.anchoredPosition = new Vector2(0f, -4f);
            toastRT.sizeDelta = new Vector2(1500f, 58f);
            var toastGroup = toastGo.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;

            // 9) 流程指导(容器下沿之外)
            var guide = BuildGuide(root.transform);

            // 10) 全局 hitbox
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
            var beh = root.AddComponent<GearboxBehaviour>();
            beh.cradle = cradleRT;
            beh.beamImg = beamImg;
            beh.reticleImg = reticleImg;
            beh.reticleRT = reticleRT;
            beh.knobRT = knobRT;
            beh.valveRT = valveRT;
            beh.shaftRT = cradle.Find("Shaft") != null
                        ? cradle.Find("Shaft").GetComponent<RectTransform>() : null;
            beh.steamImg = steamImg;
            beh.steamRT = steamRT;
            beh.scarImg = scarRT.GetComponent<Image>();
            beh.scarShadowImg = scarShadowRT.GetComponent<Image>();
            beh.flashImg = flashImg;
            beh.toastText = toast;
            beh.toastGroup = toastGroup;
            beh.guide = guide;

            var drag = hitboxGo.AddComponent<GearboxDragHandler>();
            drag.container = rootRT;
            drag.behaviour = beh;
            beh.dragHandler = drag;

            return root;
        }

        static RectTransform MakeLayer(Transform parent, string name, string texPath,
                                       float ax, float ay, Vector2 pivot,
                                       Vector2? sizeOverride = null)
        {
            // ⚠ 不要 SetSiblingIndex:显式下标会随兄弟数量漂移,反而打乱层级。
            //   这里一律"按创建顺序"决定绘制顺序(我本来就是从下往上建的)。
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.sizeDelta = sizeOverride ?? LayerSize;
            rt.anchoredPosition = CanvasToLocal(ax, ay);
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(texPath);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>内部机构的零件:anchoredPosition 要相对机构轴心(箱底中心)算。</summary>
        static RectTransform MakeCradleLayer(Transform cradle, string name, string texPath,
                                             Vector2 anchorPx)
        {
            var go = new GameObject(name);
            go.transform.SetParent(cradle, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = PivotFor(anchorPx.x, anchorPx.y);
            rt.sizeDelta = LayerSize;
            rt.anchoredPosition = CanvasToLocal(anchorPx) - CanvasToLocal(RootPxX, RootPxY);
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite(texPath);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        static RectTransform MakeScarLayer(Transform parent, string name, Color tint, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(880f, 311f);
            rt.anchoredPosition = pos;
            rt.localEulerAngles = new Vector3(0f, 0f, -2.5f);
            var img = go.AddComponent<Image>();
            img.sprite = LoadSprite("Closeups/GearboxPuzzle/scar_offset_line");
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint;
            return rt;
        }

        static GearboxGuide BuildGuide(Transform parent)
        {
            var go = new GameObject("FlowGuide");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            // ★ 挂在**容器上沿之外**(顶部黑边)。底部黑边在真实游戏里是**对白条**的
            //   地盘(EyeCorridorLaserPuzzle 明确"走底部对白条"),流程指导放那儿会撞车。
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 8f);
            rt.sizeDelta = new Vector2(1240f, 140f);
            var g = go.AddComponent<GearboxGuide>();

            var panel = go.AddComponent<Image>();
            panel.raycastTarget = false;
            panel.color = new Color(0.06f, 0.05f, 0.045f, 0.62f);

            var edgeGo = new GameObject("Edge");
            edgeGo.transform.SetParent(go.transform, false);
            var edge = edgeGo.AddComponent<Image>();
            edge.raycastTarget = false;
            edge.color = new Color(0.72f, 0.58f, 0.30f, 0.5f);
            var ert = edge.rectTransform;
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 1f);
            ert.anchoredPosition = Vector2.zero;
            ert.sizeDelta = new Vector2(0f, 2f);

            g.step1 = GuideText(go.transform, "Step1", new Vector2(28f, -18f), 26, 396f);
            g.step2 = GuideText(go.transform, "Step2", new Vector2(424f, -18f), 26, 396f);
            g.step3 = GuideText(go.transform, "Step3", new Vector2(820f, -18f), 26, 396f);
            g.hint = GuideText(go.transform, "Hint", new Vector2(28f, -72f), 22, 1184f);
            g.hint.color = new Color(0.85f, 0.80f, 0.68f, 0.9f);
            g.panel = panel;
            return g;
        }

        static Text GuideText(Transform parent, string name, Vector2 pos, int size, float w)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = GameFonts.Primary;
            t.fontSize = size;
            t.alignment = TextAnchor.UpperLeft;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, 50f);
            return t;
        }

        static Sprite LoadSprite(string resPath)
        {
            var tex = Resources.Load<Texture2D>(resPath);
            if (tex == null)
            {
                Debug.LogError("[GearboxProjector] 找不到贴图:" + resPath);
                return null;
            }
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>程序生成靶环贴图(项目里没有环形素材,没必要为它加美术)。</summary>
        static Sprite MakeReticleSprite(int size, float radius, float stroke, Color c)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float cx = size * 0.5f - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - radius) / stroke);
                    // 环内四个正方向的短准星(不要画到环外去)
                    float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cx);
                    bool onAxis = (dx < stroke * 0.7f && dy > radius * 0.42f && dy < radius * 0.86f)
                               || (dy < stroke * 0.7f && dx > radius * 0.42f && dx < radius * 0.86f);
                    if (onAxis) a = Mathf.Max(a, 0.9f);
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, a * c.a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ====================================================================
        //  GearboxBehaviour —— 主逻辑
        // ====================================================================
        public class GearboxBehaviour : MonoBehaviour
        {
            public RectTransform cradle, knobRT, valveRT, shaftRT, reticleRT, steamRT;
            public Image beamImg, reticleImg, steamImg, scarImg, scarShadowImg, flashImg;
            public Text toastText;
            public CanvasGroup toastGroup;
            public GearboxGuide guide;
            public GearboxDragHandler dragHandler;

            public enum Phase { Idle, Penalizing, Solved }
            public Phase State { get; private set; }

            public float ElevationTarget { get; private set; }
            public float ElevationVisual { get; private set; }
            public bool ElevationLocked { get; private set; }
            public float Amplitude { get; private set; } = 1f;
            public bool AmplitudeDone { get; private set; }
            public int PenaltyCount { get; private set; }

            public int CurrentStep
            {
                get
                {
                    if (State == Phase.Solved) return 4;
                    if (!ElevationLocked) return 1;
                    if (!AmplitudeDone) return 2;
                    return 3;
                }
            }
            public bool AmplitudeArmed { get { return ElevationLocked; } }

            float _dragAccum, _gearBig, _gearSmall, _gearBigTgt, _gearSmallTgt;
            float _ratchetAccum, _shaftLean, _detentT, _hitchT, _hitchFrom, _sweepPhase;
            float _holdT, _pulseT, _knobVisual = 1f, _steamT;
            bool _detenting, _hitching, _rollingBack, _holdHinted, _steamOn;
            Coroutine _toastCo;

            Sprite[] _steamFrames;
            int _steamFrame;
            int _pressedZone;    // 0=无 1=竖轴 2=旋钮 3=阀件

            float KnobRestX { get { return GearboxProjectorCloseup.CanvasToLocal(KnobPivotPx).x; } }

            void Awake()
            {
                // ⚠ AddComponent 会立刻跑 Awake,此时序列化字段(beamImg/reticleImg…)还没赋值,
                //   所以这里只做"不依赖外部赋值"的初始化;其余交给 Update 每帧设。
                LoadSteamFrames();
            }

            void LoadSteamFrames()
            {
                var tex = Resources.Load<Texture2D>("Closeups/GearboxPuzzle/steam_sheet");
                if (tex == null)
                {
                    Debug.LogWarning("[GearboxProjector] 找不到 steam_sheet,蒸汽将不可见。");
                    return;
                }
                const int fw = 288, fh = 122, cols = 13;
                int total = (tex.width / fw) * (tex.height / fh);
                _steamFrames = new Sprite[total];
                for (int i = 0; i < total; i++)
                {
                    int c = i % cols, r = i / cols;
                    var rect = new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh);
                    _steamFrames[i] = Sprite.Create(tex, rect, new Vector2(1f, 1f), 100f);
                }
            }

            // ── 对外状态(调试 HUD)─────────────────────────────────────────
            public float KnobVisual { get { return _knobVisual; } }
            public Vector2 EmitLocalDebug { get { return EmitLocal(ElevationVisual); } }

            // ── 输入入口(由 DragHandler 调)─────────────────────────────────
            public void OnPress(Vector2 screenPos)
            {
                _holdT = 0f; _holdHinted = false;
                if (State != Phase.Idle) { _pressedZone = 0; return; }
                _pressedZone = HitZone(screenPos);
                // ★ 顺序错误:仰角没锁就先碰旋钮 → 蒸汽惩罚(按下去就算)
                if (_pressedZone == 2 && !ElevationLocked)
                {
                    _pressedZone = 0;
                    TriggerPenalty("顺序错误:先抬起底座仰角");
                }
            }

            public void OnBeginDrag(Vector2 screenPos)
            {
                if (State != Phase.Idle) return;
                _pressedZone = HitZone(screenPos);
                if (_pressedZone == 2 && !ElevationLocked)
                {
                    TriggerPenalty("顺序错误:先抬起底座仰角");
                    _pressedZone = 0;
                }
                dragHandler.ResetDragState(screenPos, _pressedZone);
            }

            public void OnDrag(Vector2 screenPos)
            {
                if (State != Phase.Idle || _pressedZone == 0) return;
                if (_pressedZone == 1) DragShaft(screenPos);
                else if (_pressedZone == 2) DragKnob(screenPos);
            }

            public void OnEndDrag() { _pressedZone = 0; }

            public void OnClick(Vector2 screenPos)
            {
                if (State != Phase.Idle) return;
                if (HitZone(screenPos) == 3) TryLock();
            }

            /// <summary>0=无 1=竖轴 2=旋钮 3=阀件。热区都在各自的 RectTransform 局部空间里判。</summary>
            public int HitZone(Vector2 screenPos)
            {
                if (knobRT != null)
                {
                    Vector2 lp;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(knobRT, screenPos, null, out lp))
                        if (lp.sqrMagnitude <= 62f * 62f) return 2;
                }
                if (valveRT != null)
                {
                    Vector2 lp;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(valveRT, screenPos, null, out lp))
                    {
                        Vector2 c = ValveGrabLocal();
                        if ((lp - c).sqrMagnitude <= 78f * 78f) return 3;
                    }
                }
                if (shaftRT != null)
                {
                    Vector2 lp;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(shaftRT, screenPos, null, out lp))
                    {
                        Vector2 c = ShaftGrabLocal();
                        if ((lp - c).sqrMagnitude <= 118f * 118f) return 1;
                    }
                }
                return 0;
            }

            Vector2 ShaftGrabLocal()
            {
                // 竖轴抓取点取轴的上段(画布 1781,470 附近)
                return GearboxProjectorCloseup.CanvasToLocal(1781f, 470f)
                       - GearboxProjectorCloseup.CanvasToLocal(ShaftPx);
            }

            /// <summary>阀件热区中心:图层轴心(1691,720)本来就落在管身中部,偏移为 0。</summary>
            static Vector2 ValveGrabLocal() { return Vector2.zero; }

            // ── ① 竖轴:圆周拖拽 ───────────────────────────────────────────
            void DragShaft(Vector2 screenPos)
            {
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(shaftRT, screenPos, null, out lp))
                    return;
                Vector2 c = ShaftGrabLocal();
                float delta = dragHandler.ComputeShaftDelta(lp - c, ShaftMinRadiusPx, ShaftMaxDeltaPerFrame);
                if (Mathf.Abs(delta) > 0.001f) AddElevationDrag(delta * DragSign);
            }

            void AddElevationDrag(float deltaDeg)
            {
                if (ElevationLocked || _detenting || _hitching || _rollingBack) return;
                _holdT = 0f;
                float before = _dragAccum;
                _dragAccum = Mathf.Clamp(_dragAccum + deltaDeg, 0f, DragDegPerFullTilt);
                float target = _dragAccum / DragDegPerFullTilt * 15f;
                float d = target - ElevationTarget;
                _gearBigTgt += d * GearBigRatio;
                _gearSmallTgt += d * GearSmallRatio;
                ElevationTarget = target;

                _ratchetAccum += Mathf.Abs(_dragAccum - before);
                if (_ratchetAccum >= RatchetDeg)
                {
                    _ratchetAccum -= RatchetDeg;
                    PlaySfx(ClickSfx, 0.4f, UnityEngine.Random.Range(0.94f, 1.08f));
                }
                if (ElevationTarget >= 15f - 0.01f) BeginDetent();
            }

            void BeginDetent()
            {
                if (ElevationLocked) return;
                ElevationLocked = true;
                ElevationTarget = 15f;
                _hitching = true;
                _hitchT = 0f;
                _hitchFrom = DetentHitchTilt;
                ElevationVisual = _hitchFrom;
                PlaySfx(ClickSfx, 1f, 1f);
                ShowToast("底座仰角 15° 已到位", 1.1f);
            }

            // ── ② 旋钮:推拉 ───────────────────────────────────────────────
            void DragKnob(Vector2 screenPos)
            {
                Vector2 lp;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(knobRT, screenPos, null, out lp))
                    return;
                float dx = dragHandler.KnobDelta(lp);
                SetAmplitude(Amplitude + dx * AmplitudePerUnit);
            }

            public void SetAmplitude(float v)
            {
                if (State != Phase.Idle) return;
                Amplitude = Mathf.Clamp01(v);
                if (!ElevationLocked) return;
                if (!AmplitudeDone && Amplitude <= AmplitudeThreshold)
                {
                    AmplitudeDone = true;
                    PlaySfx(ClickSfx, 0.85f, 0.9f);
                    ShowToast("摆幅已集中到左侧", 1.1f);
                }
                else if (AmplitudeDone && Amplitude > AmplitudeThreshold + 0.08f)
                {
                    AmplitudeDone = false;
                }
            }

            // ── ③ 阀件:锁定 ───────────────────────────────────────────────
            public void TryLock()
            {
                if (State != Phase.Idle || _rollingBack) return;
                if (!ElevationLocked) { ShowToast("顺序:先把底座仰角抬到 15°", 1.6f); return; }
                if (!AmplitudeDone) { ShowToast("先把摆幅收到左侧", 1.6f); return; }
                State = Phase.Solved;
                PlaySfx(ClickSfx, 1f, 0.82f);
                ShowToast("角度已锁定:光束固定投射在左侧墙高处", 2.4f);
                GearboxProjectorCloseup.NotifySolved();
            }

            // ── 惩罚 ───────────────────────────────────────────────────────
            void TriggerPenalty(string why)
            {
                if (State != Phase.Idle) return;
                State = Phase.Penalizing;
                PenaltyCount++;
                _penaltyWhy = why;
                StartCoroutine(PenaltyRoutine());
            }

            string _penaltyWhy = "";

            IEnumerator PenaltyRoutine()
            {
                _steamOn = true;
                _steamFrame = 0;
                _steamT = 0f;
                if (steamImg != null) steamImg.enabled = true;
                PlaySfx(SteamSfx, 1f, 1f);
                ShowToast(_penaltyWhy, 1.2f);

                float t = 0f;
                while (t < 0.5f)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(t / 0.5f);
                    var c = flashImg.color;
                    c.a = Mathf.Sin(k * Mathf.PI) * 0.45f;
                    flashImg.color = c;
                    yield return null;
                }
                var c2 = flashImg.color; c2.a = 0f; flashImg.color = c2;

                t = 0f;
                while (t < ScarFadeTime)
                {
                    t += Time.unscaledDeltaTime;
                    float a = Mathf.Clamp01(t / ScarFadeTime);
                    var c = scarImg.color; c.a = a; scarImg.color = c;
                    var s = scarShadowImg.color; s.a = a * 0.85f; scarShadowImg.color = s;
                    yield return null;
                }

                _steamOn = false;
                if (steamImg != null) steamImg.enabled = false;

                _rollingBack = true;
                float fromTilt = ElevationVisual, fromAmp = Amplitude;
                _dragAccum = 0f;
                ElevationTarget = 0f;
                ElevationLocked = false;
                AmplitudeDone = false;
                t = 0f;
                while (t < RollbackTime)
                {
                    t += Time.unscaledDeltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / RollbackTime));
                    ElevationVisual = Mathf.Lerp(fromTilt, 0f, k);
                    Amplitude = Mathf.Lerp(fromAmp, 1f, k);
                    yield return null;
                }
                ElevationVisual = 0f;
                Amplitude = 1f;
                _gearBigTgt = _gearSmallTgt = 0f;
                _rollingBack = false;
                State = Phase.Idle;
                ShowToast("进度已回滚,从头再来", 1.6f);
            }

            // ── 每帧 ───────────────────────────────────────────────────────
            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                _pulseT += dt;
                UpdateElevation(dt);
                UpdateGears(dt);
                UpdateKnobVisual(dt);
                UpdateSteam(dt);
                UpdateBeam(dt);
                UpdateHoldHint(dt);
                if (guide != null) guide.Refresh(this);
            }

            void UpdateElevation(float dt)
            {
                if (_hitching)
                {
                    _hitchT += dt;
                    ElevationVisual = _hitchFrom;
                    if (_hitchT >= DetentHitchTime) { _hitching = false; _detenting = true; _detentT = 0f; }
                    ApplyCradle();
                    return;
                }
                if (_detenting)
                {
                    // 过冲吸附:先用绝对角度冲过 15.7°,再坐回 15°。
                    // ⚠ 别写成 Lerp(_hitchFrom, 15, EaseOutBack(t)):过冲量正比于 0.65°,只有 0.03°,看不见。
                    _detentT += dt / Mathf.Max(0.0001f, DetentSnapTime);
                    float t = Mathf.Clamp01(_detentT);
                    float over = 15f + SnapOvershootDeg;
                    if (t < 0.45f)
                    {
                        float rise = t / 0.45f;
                        ElevationVisual = Mathf.Lerp(_hitchFrom, over, 1f - (1f - rise) * (1f - rise));
                    }
                    else
                    {
                        float settle = (t - 0.45f) / 0.55f;
                        ElevationVisual = Mathf.Lerp(over, 15f, Mathf.SmoothStep(0f, 1f, settle));
                    }
                    if (t >= 1f) { _detenting = false; ElevationVisual = 15f; }
                    ApplyCradle();
                    return;
                }
                float target = _rollingBack ? ElevationVisual : ElevationTarget;
                float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, TiltFollowSmoothTime));
                ElevationVisual = Mathf.Lerp(ElevationVisual, target, k);
                if (Mathf.Abs(ElevationVisual - target) < 0.002f) ElevationVisual = target;
                ApplyCradle();
            }

            void ApplyCradle()
            {
                if (cradle != null) cradle.localRotation = Quaternion.Euler(0f, 0f, ElevationVisual);
            }

            void UpdateGears(float dt)
            {
                float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, GearFollowSmoothTime));
                _gearBig = Mathf.Lerp(_gearBig, _gearBigTgt, k);
                _gearSmall = Mathf.Lerp(_gearSmall, _gearSmallTgt, k);
                if (cradle != null)
                {
                    var gb = cradle.Find("GearBig");
                    if (gb != null) gb.localRotation = Quaternion.Euler(0f, 0f, _gearBig);
                    var gs = cradle.Find("GearSmall");
                    if (gs != null) gs.localRotation = Quaternion.Euler(0f, 0f, _gearSmall);
                }
                _shaftLean = Mathf.Lerp(_shaftLean, 0f, 1f - Mathf.Exp(-10f * dt));
                if (shaftRT != null)
                    shaftRT.localRotation = Quaternion.Euler(0f, 0f, ElevationVisual + _shaftLean);
            }

            /// <summary>旋钮推拉:往里推 = 收拢摆幅。缩放 + 微位移,读作"沿轴进出"。</summary>
            void UpdateKnobVisual(float dt)
            {
                if (knobRT == null) return;
                _knobVisual = Mathf.Lerp(_knobVisual, Amplitude,
                                         1f - Mathf.Exp(-14f * dt));
                float push = 1f - _knobVisual;                       // 0=全拔出 1=推到底
                float sx = Mathf.Lerp(1f, KnobPushScale, push);
                knobRT.localScale = new Vector3(sx, 1f, 1f);
                knobRT.anchoredPosition = new Vector2(
                    KnobRestX - push * KnobPushTravel, knobRT.anchoredPosition.y);
            }

            void UpdateSteam(float dt)
            {
                if (!_steamOn || steamImg == null || _steamFrames == null || _steamFrames.Length == 0) return;
                _steamT += dt * SteamFps;
                while (_steamT >= 1f)
                {
                    _steamT -= 1f;
                    _steamFrame = (_steamFrame + 1) % _steamFrames.Length;
                }
                steamImg.sprite = _steamFrames[_steamFrame];
            }

            // ── 光束 / 靶环 ────────────────────────────────────────────────
            Vector2 EmitLocal(float deg)
            {
                return GearboxProjectorCloseup.CanvasToLocal(
                    GearboxProjectorCloseup.RotateAroundRoot(BeamEmitPx, deg));
            }

            void UpdateBeam(float dt)
            {
                _sweepPhase += dt / Mathf.Max(0.05f, SweepPeriod) * Mathf.PI * 2f;
                if (_sweepPhase > Mathf.PI * 2f) _sweepPhase -= Mathf.PI * 2f;

                Vector2 e15 = EmitLocal(15f);
                Vector2 d15 = (BeamHitLocal - e15).normalized;
                float a15 = Mathf.Atan2(d15.y, d15.x) * Mathf.Rad2Deg;
                float center = a15 + (15f - ElevationVisual);
                float pan = State == Phase.Solved ? 0f
                          : Amplitude * AmplitudeSweepMax * Mathf.Sin(_sweepPhase);
                float ang = (center + pan) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 emit = EmitLocal(ElevationVisual);

                Vector2 end;
                if (dir.x < -0.02f)
                {
                    float t = (WallX - emit.x) / dir.x;
                    end = new Vector2(WallX, emit.y + dir.y * t);
                    end.y = Mathf.Clamp(end.y, -kContainerH * 0.5f, kContainerH * 0.5f);
                }
                else
                {
                    end = RayExitRect(emit, dir);
                }
                if (State == Phase.Solved) end = BeamHitLocal;

                Vector2 mid = (emit + end) * 0.5f;
                float len = (end - emit).magnitude;
                float rot = Mathf.Atan2(end.y - emit.y, end.x - emit.x) * Mathf.Rad2Deg;
                var rt = beamImg.rectTransform;
                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(len, 9f);
                rt.localRotation = Quaternion.Euler(0f, 0f, rot);
                float a = State == Phase.Solved ? 1f : (ElevationLocked ? 0.9f : 0.26f);
                beamImg.color = new Color(1f, 0.96f, 0.82f, a);

                bool armed = ElevationLocked;
                float baseA = State == Phase.Solved ? 0.95f : (armed ? 0.7f : 0.42f);
                float pulse = armed && State != Phase.Solved
                            ? 1f + 0.18f * Mathf.Sin(_pulseT * 5.2f) : 1f;
                SetReticleAlpha(baseA * pulse);
                reticleRT.sizeDelta = Vector2.one * (State == Phase.Solved ? 88f : 132f);
            }

            static Vector2 RayExitRect(Vector2 o, Vector2 d)
            {
                float hw = kContainerW * 0.5f, hh = kContainerH * 0.5f;
                float best = float.MaxValue;
                if (d.x > 1e-4f) best = Mathf.Min(best, (hw - o.x) / d.x);
                if (d.x < -1e-4f) best = Mathf.Min(best, (-hw - o.x) / d.x);
                if (d.y > 1e-4f) best = Mathf.Min(best, (hh - o.y) / d.y);
                if (d.y < -1e-4f) best = Mathf.Min(best, (-hh - o.y) / d.y);
                if (best == float.MaxValue || best < 0f || best > 6000f) best = 4000f;
                return o + d * best;
            }

            void SetReticleAlpha(float a)
            {
                if (reticleImg == null) return;
                var c = reticleImg.color;
                reticleImg.color = new Color(c.r, c.g, c.b, a);
            }

            void UpdateHoldHint(float dt)
            {
                if (State != Phase.Idle || ElevationLocked || _pressedZone != 0)
                { _holdT = 0f; _holdHinted = false; return; }
                if (HitZone(Input.mousePosition) != 1 || !Input.GetMouseButton(0))
                { _holdT = 0f; _holdHinted = false; return; }
                _holdT += dt;
                if (!_holdHinted && _holdT >= LongPressHintTime)
                {
                    _holdHinted = true;
                    ShowToast("按住竖轴,绕它画圈 → 抬起底座", 1.8f);
                }
            }

            // ── 提示条 / 音效 ──────────────────────────────────────────────
            public void ShowToast(string msg, float seconds)
            {
                // ⚠ 必须存句柄:StopCoroutine("名字") 停不掉 StartCoroutine(IEnumerator)
                if (_toastCo != null) StopCoroutine(_toastCo);
                _toastCo = StartCoroutine(ToastRoutine(msg, seconds));
            }

            IEnumerator ToastRoutine(string msg, float seconds)
            {
                toastText.text = msg;
                float t = 0f;
                while (t < 0.15f) { t += Time.unscaledDeltaTime; toastGroup.alpha = t / 0.15f; yield return null; }
                toastGroup.alpha = 1f;
                yield return new WaitForSecondsRealtime(seconds);
                t = 0f;
                while (t < 0.3f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - t / 0.3f; yield return null; }
                toastGroup.alpha = 0f;
                _toastCo = null;
            }

            void PlaySfx(string name, float vol, float pitch)
            {
                if (string.IsNullOrEmpty(name)) return;
                AudioManager.PlaySfx(name);
            }

            // ── 调试 API(供 GearboxDebugBootstrap)───────────────────────
            public void DebugNudgeTilt(float d)
            {
                if (State != Phase.Idle || ElevationLocked || _detenting || _hitching) return;
                _dragAccum = Mathf.Clamp(_dragAccum + d / 15f * DragDegPerFullTilt, 0f, DragDegPerFullTilt);
                float target = _dragAccum / DragDegPerFullTilt * 15f;
                float dd = target - ElevationTarget;
                _gearBigTgt += dd * GearBigRatio;
                _gearSmallTgt += dd * GearSmallRatio;
                ElevationTarget = target;
            }
            public void DebugCompleteElevation()
            {
                if (State != Phase.Idle || ElevationLocked) return;
                _dragAccum = DragDegPerFullTilt;
                ElevationTarget = 15f;
                _gearBigTgt += (15f - ElevationVisual) * GearBigRatio;
                _gearSmallTgt += (15f - ElevationVisual) * GearSmallRatio;
                BeginDetent();
            }
            public void DebugSetAmplitude(float v)
            {
                if (State != Phase.Idle) return;
                if (!ElevationLocked) { TriggerPenalty("顺序错误:先抬起底座仰角"); return; }
                SetAmplitude(v);
            }
            public void DebugTriggerPenalty() { TriggerPenalty("手动触发:蒸汽惩罚"); }
            public void DebugNudgeTarget(float dx, float dy)
            {
                GearboxProjectorCloseup.BeamHitLocal += new Vector2(dx, dy);
                if (reticleRT != null) reticleRT.anchoredPosition = GearboxProjectorCloseup.BeamHitLocal;
            }
            public void DebugToggleGuide() { if (guide != null) guide.SetVisible(!guide.IsVisible); }
            public void DebugReset(bool keepScar)
            {
                StopAllCoroutines();
                _toastCo = null;
                State = Phase.Idle;
                _dragAccum = 0f; ElevationTarget = 0f; ElevationVisual = 0f;
                ElevationLocked = false; Amplitude = 1f; AmplitudeDone = false;
                _detenting = _hitching = _rollingBack = false;
                _gearBig = _gearBigTgt = _gearSmall = _gearSmallTgt = 0f;
                _ratchetAccum = 0f; _shaftLean = 0f; _holdT = 0f; _holdHinted = false;
                _pressedZone = 0; _steamOn = false;
                _knobVisual = 1f;
                if (cradle != null) cradle.localRotation = Quaternion.identity;
                if (steamImg != null) steamImg.enabled = false;
                if (toastGroup != null) toastGroup.alpha = 0f;
                if (!keepScar)
                {
                    var c = scarImg.color; c.a = 0f; scarImg.color = c;
                    var s = scarShadowImg.color; s.a = 0f; scarShadowImg.color = s;
                    PenaltyCount = 0;
                }
                var f = flashImg.color; f.a = 0f; flashImg.color = f;
                if (dragHandler != null) dragHandler.ResetDragState(Vector2.zero, 0);
            }

            public void ResetRun()
            {
                DebugReset(false);
            }
        }

        // ====================================================================
        //  GearboxGuide —— 三步流程指导(容器下沿之外的一条横条)
        // ====================================================================
        public class GearboxGuide : MonoBehaviour
        {
            public Text step1, step2, step3, hint;
            public Image panel;
            bool _visible = true;
            int _lastStep = -1;
            bool _lastPenalizing;

            static readonly Color Done = new Color(0.62f, 0.82f, 0.55f, 1f);
            static readonly Color Active = new Color(1f, 0.86f, 0.45f, 1f);
            static readonly Color Idle = new Color(0.62f, 0.60f, 0.56f, 0.8f);

            public bool IsVisible { get { return _visible; } }

            public void SetVisible(bool v)
            {
                _visible = v;
                _lastStep = -1;          // 重新显示时强制刷一次文本
                gameObject.SetActive(v);
            }

            public void Refresh(GearboxBehaviour p)
            {
                if (!_visible || p == null || step1 == null) return;
                int step = p.CurrentStep;
                bool pen = p.State == GearboxBehaviour.Phase.Penalizing;
                if (step == _lastStep && pen == _lastPenalizing) return;   // 只在状态变化时改文本
                _lastStep = step;
                _lastPenalizing = pen;

                step1.text = (step > 1 ? "✓ " : "① ") + "抬起底座 15°";
                step2.text = (step > 2 ? "✓ " : "② ") + "收拢摆幅";
                step3.text = (step > 3 ? "✓ " : "③ ") + "锁定角度";
                step1.color = step == 1 ? Active : (step > 1 ? Done : Idle);
                step2.color = step == 2 ? Active : (step > 2 ? Done : Idle);
                step3.color = step == 3 ? Active : (step > 3 ? Done : Idle);

                if (pen) hint.text = "⚠ 顺序错了:蒸汽惩罚,进度回滚";
                else if (step == 1) hint.text = "按住顶部竖轴,绕着它画圈(只按不动会提示你)";
                else if (step == 2) hint.text = "把箱体右缘的黄铜旋钮往里推 —— 摆幅越窄,光束越靠近左侧靶点";
                else if (step == 3) hint.text = "点击中间那根 S 形弯管,把角度钉死";
                else hint.text = "角度已锁定,光束固定投射在左侧墙高处。";
                hint.color = pen ? new Color(0.95f, 0.42f, 0.32f, 1f)
                                 : new Color(0.85f, 0.80f, 0.68f, 0.9f);
            }
        }

        // ====================================================================
        //  GearboxDragHandler —— 全屏 hitbox,把拖拽路由给 Behaviour
        // ====================================================================
        public class GearboxDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler,
                                          IEndDragHandler, IPointerDownHandler, IPointerClickHandler
        {
            public RectTransform container;
            public GearboxBehaviour behaviour;

            Vector2 _lastKnobLocal;
            Vector2 _lastDir;
            bool _haveDir;
            float _pressMove;
            Vector2 _pressPos;

            public void ResetDragState(Vector2 screenPos, int zone)
            {
                _haveDir = false;
                _pressMove = 0f;
                _pressPos = screenPos;
                if (zone == 2 && behaviour != null && behaviour.knobRT != null)
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        behaviour.knobRT, screenPos, null, out _lastKnobLocal);
            }

            /// <summary>旋钮的横向位移(容器 px);往里推(向左)= 正值收拢。</summary>
            public float KnobDelta(Vector2 local)
            {
                float dx = _lastKnobLocal.x - local.x;    // 往左拖 = 正值
                _lastKnobLocal = local;
                return dx;
            }

            /// <summary>圆周拖拽的角度增量(度,顺时针为正);带死区与单帧爆转钳制。</summary>
            public float ComputeShaftDelta(Vector2 fromCenter, float minRadiusPx, float maxDelta)
            {
                if (fromCenter.magnitude < minRadiusPx)
                {
                    _haveDir = false;
                    return 0f;
                }
                Vector2 dir = fromCenter.normalized;
                if (!_haveDir) { _lastDir = dir; _haveDir = true; return 0f; }
                float a0 = Mathf.Atan2(_lastDir.y, _lastDir.x) * Mathf.Rad2Deg;
                float a1 = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(a0, a1) * -1f;   // 容器 y 向上;顺时针为正
                _lastDir = dir;
                if (Mathf.Abs(delta) > maxDelta) return 0f;
                return delta;
            }

            public void OnPointerDown(PointerEventData e)
            {
                if (behaviour != null) behaviour.OnPress(e.position);
            }
            public void OnBeginDrag(PointerEventData e)
            {
                if (behaviour != null) behaviour.OnBeginDrag(e.position);
            }
            public void OnDrag(PointerEventData e)
            {
                _pressMove += Mathf.Abs(e.position.x - _pressPos.x) + Mathf.Abs(e.position.y - _pressPos.y);
                if (behaviour != null) behaviour.OnDrag(e.position);
            }
            public void OnEndDrag(PointerEventData e)
            {
                if (behaviour != null) behaviour.OnEndDrag();
            }
            public void OnPointerClick(PointerEventData e)
            {
                if (_pressMove > 14f) return;
                if (behaviour != null) behaviour.OnClick(e.position);
            }
        }
    }
}
