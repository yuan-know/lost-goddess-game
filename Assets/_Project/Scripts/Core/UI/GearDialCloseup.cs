// ============================================================================
//  GearDialCloseup.cs —— 开门齿轮旋钮谜题特写(2026-09-13 v3 手感重构)
//
//  玩法:转动下方大齿轮在 5 个「呈现道具」之间切换,再用右下角小旋钮旋转当前道具,
//        让 5 个道具的指针都指向右上角(45°)时全部定住 → 解谜完成 → 开门过场。
//
//  背景/道具图层 3400×1200,StretchFill 填满 1920×678 容器。
//  齿轮/旋钮是 500×500 小图,固定位置放置。
//  全局 hitbox 接收所有拖拽,自动判断拖齿轮还是拖旋钮。
//
//  角度系统:每个道具的「实际指向角度」 = baseAngle + rotation
//     baseAngle = 图默认的指向角度(PIL 精确检测;调试场景可现场校准)
//     rotation  = 玩家旋钮转动的量(可正可负)
//     胜利条件:Normalize(baseAngle + rotation) 接近 45°
//
//  ── v3 手感改动(2026-09-13)─────────────────────────────────────────────
//   齿轮(切换指针界面):
//     · 原来 `Mathf.Round` 直接跳档 —— 视觉是硬切,且在圆心附近一直"打架"。
//     · 现在改为「连续跟手 + 磁吸顿挫 + 过冲吸附」:
//         齿轮平滑跟随手指;靠近某一档位时被磁吸拉住(形成越档阻力);
//         越过档位中线立刻"咔哒"并切换指针界面(二者同一时刻发生);
//         松手时用 ease-out-back 过冲一点再回到档位,做出机械咬合感。
//     · 指针界面切换不再是硬切,新道具 0.1s 快速淡入。
//   旋钮(旋转指针):
//     · 修复靠近圆心时 atan2 角度爆跳/抖动 —— 最小半径钳制 + 单帧角度钳制。
//       ⚠ 最小半径只能取很小(默认 12px):旋钮 500 图内容半径仅 194,显示 160px 时
//         可见半径只有约 62px;死区一旦设到 40px 级别就会吃掉大半个旋钮面,
//         表现为"按在旋钮上扭不动"(2026-09-13 踩过这个坑)。
//       单帧角度变化 >KnobMaxDeltaPerFrame 一律当伪信号重新取基准,不再靠大死区兜底。
//     · 旋钮视觉角度平滑跟随,并且与「当前道具的偏移量」保持一致
//       (锁定/换道具时旋钮同步到位,不再空转)。
//     · 对齐目标时指针过冲锁定,给"咬合"确认感;悬停放大 / 按住回缩的反馈。
//   调试:
//     · 新增可实时调的手感参数(public static)与 DebugXxx API,
//       配合 GearDialDebugBootstrap 可脱离正式流程单独 Play 调手感。
//
//  使用方式: GearDialCloseup.Show(() => { /* 解谜成功回调 */ });
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class GearDialCloseup
    {
        static Action _onSolved;

        // ── 常量 ──────────────────────────────────────────────────────────
        const float kWinAngle = 45f;           // 目标角度:右上角 = 45°
        const float kWinTolerance = 8f;        // 胜利容差 ±8°
        const int   kPropCount = 5;            // 5 个呈现道具
        const float kGearStep = 72f;           // 齿轮每档 72°(360/5)

        // 容器尺寸(与表盘/展台特写一致,铺满中间场景条带)
        const float kContainerW = 1920f;
        const float kContainerH = 678f;

        // 齿轮:500×500 小图,底部中央放置(只露出上半部分)
        const float kGearSize = 520f;          // 齿轮显示尺寸
        const float kGearPivotX = 0.498f;      // PIL 检测: 0.4978
        const float kGearPivotY = 0.504f;      // PIL 检测: 0.5040
        const float kGearOffsetY = -180f;      // 齿轮中心 y 偏移(负值=往下,让它只露上半)

        // 旋钮:500×500 小图,右下角放置
        const float kKnobSize = 160f;
        const float kKnobPivotX = 0.498f;      // PIL 检测: 0.4980
        const float kKnobPivotY = 0.501f;      // PIL 检测: 0.5012
        const float kKnobMarginRight = 100f;
        const float kKnobMarginBottom = 100f;

        // 道具旋转中心(光圈中心,PIL 检测都在 0.5 附近)
        const float kPropPivotX = 0.500f;
        const float kPropPivotY = 0.500f;

        // 小罗盘指针 pivot(PIL 检测: 指针质心 ≈ 整图中心)
        const float kSmallNeedlePivotX = 0.5005f;
        const float kSmallNeedlePivotY = 0.5019f;

        // ── 各道具的 baseAngle(视觉校准 v1) ──
        //   图默认指向的角度(12点=0°,顺时针为正)
        //   2026-07-27: PIL 检测值偏差较大,根据图片视觉分析重测。
        //   ⚠ 这是「近似值」,最可靠的做法是在调试场景里用 T 打开目标线,
        //     把某道具的指针调成视觉正对 45°,读 HUD 反推真实 baseAngle 再回填。
        //   改成了非 readonly:调试场景可以现场校准(AdjustBaseAngle)。
        public static float[] kPropBaseAngles = new float[kPropCount] {
            48f,     // 0:Button 按钮 → 默认指向右上(约1点半方向)
            45f,     // 1:Dial 表盘 → 默认指向右上(约1点半方向)
            60f,     // 2:Gramophone 唱片机 → 默认指向右上偏右(唱臂方向)
            30f,     // 3:LongCompass 长指针罗盘 → 默认指向右上偏上
            45f,     // 4:SmallCompass 小罗盘指针 → 默认指向正右上
        };

        // ── 手感参数(调试场景可实时调,改完把数值回填默认值即可)──────────
        /// <summary>齿轮视觉跟随手指的平滑时间(秒)。越小越"硬跟手",越大越"沉"。</summary>
        public static float GearFollowSmoothTime = 0.055f;
        /// <summary>磁吸强度 0~1。0=完全顺滑无顿挫,1=强吸附(几乎跳档)。</summary>
        public static float GearMagnetStrength = 0.50f;
        /// <summary>磁吸生效范围(度)。距离最近档位小于此值才开始被吸。</summary>
        public static float GearMagnetRange = 40f;
        /// <summary>松手后吸附到档位的时长(秒)。</summary>
        public static float GearSnapTime = 0.16f;
        /// <summary>松手吸附的过冲量(0=不过冲,1≈10% 过冲)。</summary>
        public static float GearSnapOvershoot = 0.55f;
        /// <summary>单帧最大允许角度变化(度),防止圆心附近角度爆跳。</summary>
        public static float GearMaxDeltaPerFrame = 150f;

        /// <summary>指针界面切换时新道具的淡入时长(秒)。</summary>
        public static float PropFadeTime = 0.10f;
        /// <summary>对齐目标时指针锁定动画时长(秒)。</summary>
        public static float PropLockTime = 0.20f;
        /// <summary>锁定的过冲量(0=不过冲,1≈10% 过冲)。</summary>
        public static float PropLockOvershoot = 0.60f;

        /// <summary>旋钮视觉平滑时间(秒)。</summary>
        public static float KnobSmoothTime = 0.05f;
        /// <summary>旋钮拖拽的最小计算半径(px)。只用来避开圆心那一点的角度奇点。
        /// ⚠ 别调大:旋钮 500 图内容半径仅 194/500,显示 160px 时可见半径只有约 62px,
        ///   死区一旦超过 ~20px,按在旋钮面上就会觉得"转不动"。</summary>
        public static float KnobMinRadiusPx = 12f;
        /// <summary>旋钮单帧最大允许角度变化(度);超过就当伪信号重新取基准。</summary>
        public static float KnobMaxDeltaPerFrame = 90f;

        /// <summary>悬停时旋钮放大倍率。</summary>
        public static float HoverScaleKnob = 1.10f;
        /// <summary>悬停时齿轮放大倍率。</summary>
        public static float HoverScaleGear = 1.015f;
        /// <summary>是否开启悬停高亮/放大提示。</summary>
        public static bool HoverHighlight = true;
        /// <summary>锁定后旋钮的暗化系数(1=不暗化)。</summary>
        public static float KnobLockedDim = 0.62f;

        /// <summary>咔哒音效名(Resources/Audio 下;没有音频文件时静默)。</summary>
        public static string ClickSfx = "gear_click";
        /// <summary>指针咬合音效名。</summary>
        public static string LockSfx = "gear_lock";

        /// <summary>调试用:为 true 时 NotifySolved 不播开门过场,只回调。</summary>
        public static bool SuppressCutscene = false;

        public enum PropSlot
        {
            Button = 0,
            Dial = 1,
            Gramophone = 2,
            LongCompass = 3,
            SmallCompass = 4,
        }

        public static void Show(Action onSolved = null)
        {
            _onSolved = onSolved;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var template = BuildGearDialTemplate();
            CloseupView.OpenExisting(template);
            CloseupView.SetCanClose(false);
        }

        public static void NotifySolved()
        {
            Debug.Log("[GearDialCloseup] NotifySolved() 被调用");

            if (SuppressCutscene)
            {
                Debug.Log("[GearDialCloseup] 调试模式:跳过开门过场,直接回调。");
                _onSolved?.Invoke();
                return;
            }

            var runnerGo = new GameObject("~GearDialVideoRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerGo);
            Debug.Log("[GearDialCloseup] 开始播放开门过场，回调是否存在: " + (_onSolved != null ? "是" : "否"));
            runnerGo.AddComponent<FlashRunner>().StartCoroutine(PlayDoorCutsceneThenCallback(_onSolved));
        }

        // ── 构建 UI ───────────────────────────────────────────────────────
        static GameObject BuildGearDialTemplate()
        {
            var root = new GameObject("GearDial");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(kContainerW, kContainerH);
            rootRT.pivot = new Vector2(0.5f, 0.5f);

            // 1) 背景:砖墙 + 光圈(3400×1200,StretchFill)
            BuildBgLayer(root.transform, "BgAperture", "Closeups/d6/d6_bg_aperture");

            // 2) 道具容器(5 个道具叠放,都是整屏图 StretchFill)
            var propContainerGo = new GameObject("PropContainer");
            propContainerGo.transform.SetParent(root.transform, false);
            var propContainerRT = propContainerGo.AddComponent<RectTransform>();
            StretchFill(propContainerRT);

            // 5 个道具(顺序从下到上,当前槽位 SetActive(true),其他 false)
            BuildPropLayer(propContainerGo.transform, "Prop_Button",       "Closeups/d6/d6_prop_button",       0);
            BuildPropLayer(propContainerGo.transform, "Prop_Dial",         "Closeups/d6/d6_prop_dial",         1);
            BuildPropLayer(propContainerGo.transform, "Prop_Gramophone",   "Closeups/d6/d6_prop_gramophone",   2);
            BuildPropLayer(propContainerGo.transform, "Prop_LongCompass",  "Closeups/d6/d6_prop_longcompass",  3);

            // 小罗盘特殊:底盘 + 指针(指针单独旋转)
            BuildSmallCompassProp(propContainerGo.transform);

            // 3) 下方齿轮(500×500 小图,底部中央)
            var gearGo = new GameObject("GearWheel");
            gearGo.transform.SetParent(root.transform, false);
            var gearRT = gearGo.AddComponent<RectTransform>();
            gearRT.anchorMin = new Vector2(0.5f, 0f);
            gearRT.anchorMax = new Vector2(0.5f, 0f);
            gearRT.pivot = new Vector2(kGearPivotX, kGearPivotY);
            gearRT.sizeDelta = new Vector2(kGearSize, kGearSize);
            gearRT.anchoredPosition = new Vector2(0f, kGearOffsetY);
            var gearImg = gearGo.AddComponent<Image>();
            var gearTex = Resources.Load<Texture2D>("Closeups/d6/d6_gear_wheel");
            if (gearTex != null)
            gearImg.sprite = Sprite.Create(gearTex, new Rect(0, 0, gearTex.width, gearTex.height), new Vector2(0.5f, 0.5f));
            gearImg.preserveAspect = true;
            gearImg.raycastTarget = false;

            // 4) 右下角旋钮(500×500 小图)
            var knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(root.transform, false);
            var knobRT = knobGo.AddComponent<RectTransform>();
            knobRT.anchorMin = new Vector2(1f, 0f);
            knobRT.anchorMax = new Vector2(1f, 0f);
            knobRT.pivot = new Vector2(kKnobPivotX, kKnobPivotY);
            knobRT.sizeDelta = new Vector2(kKnobSize, kKnobSize);
            knobRT.anchoredPosition = new Vector2(-kKnobMarginRight, kKnobMarginBottom);
            var knobImg = knobGo.AddComponent<Image>();
            var knobTex = Resources.Load<Texture2D>("Closeups/d6/d6_knob");
            if (knobTex != null)
            knobImg.sprite = Sprite.Create(knobTex, new Rect(0, 0, knobTex.width, knobTex.height), new Vector2(0.5f, 0.5f));
            knobImg.preserveAspect = true;
            knobImg.raycastTarget = false;

            // 5) 全局透明 hitbox —— 统一接收所有拖拽
            // 注意:不能只 StretchFill root,因为齿轮下半部分伸出了 root(伸到下方黑边里),
            //     必须把 hitbox 向下扩展,否则齿轮下半部分收不到点击。
            var hitboxGo = new GameObject("GlobalHitbox");
            hitboxGo.transform.SetParent(root.transform, false);
            var hitboxImg = hitboxGo.AddComponent<Image>();
            hitboxImg.color = new Color(0, 0, 0, 0.001f);
            hitboxImg.raycastTarget = true;
            var hitboxRT = hitboxImg.rectTransform;
            hitboxRT.anchorMin = new Vector2(0f, 0f);
            hitboxRT.anchorMax = new Vector2(1f, 1f);
            hitboxRT.offsetMin = new Vector2(0f, -600f);  // 向下多伸 600px,确保齿轮/旋钮全在 hitbox 内
            hitboxRT.offsetMax = Vector2.zero;

            // 主逻辑组件(先创建,这样 DragHandler.Awake 时就能 GetComponentInParent 能找到)
            var behaviour = root.AddComponent<GearDialBehaviour>();
            behaviour.propContainer = propContainerGo.transform;
            behaviour.gearRT = gearRT;
            behaviour.knobRT = knobRT;

            var dragHandler = hitboxGo.AddComponent<GearDialDragHandler>();
            dragHandler.dialRoot = rootRT;
            dragHandler.gearRT = gearRT;
            dragHandler.knobRT = knobRT;

            return root;
        }

        static GameObject BuildBgLayer(Transform parent, string name, string texPath)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            StretchFill(rt);
            var img = go.AddComponent<Image>();
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex != null)
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.preserveAspect = false;
            img.raycastTarget = false;
            return go;
        }

        static GameObject BuildPropLayer(Transform parent, string name, string texPath, int propIndex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            StretchFill(rt);
            rt.pivot = new Vector2(kPropPivotX, kPropPivotY);
            var img = go.AddComponent<Image>();
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex != null)
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.preserveAspect = false;
            img.raycastTarget = false;
            go.SetActive(false);
            return go;
        }

        static void BuildSmallCompassProp(Transform parent)
        {
            var go = new GameObject("Prop_SmallCompass");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            StretchFill(rt);
            rt.pivot = new Vector2(kPropPivotX, kPropPivotY);
            // 小罗盘整体不旋转(只有指针转),pivot 不影响

            // 底盘
            var baseGo = new GameObject("Base");
            baseGo.transform.SetParent(go.transform, false);
            var baseRT = baseGo.AddComponent<RectTransform>();
            StretchFill(baseRT);
            var baseImg = baseGo.AddComponent<Image>();
            var baseTex = Resources.Load<Texture2D>("Closeups/d6/d6_prop_smallcompass_base");
            if (baseTex != null)
            baseImg.sprite = Sprite.Create(baseTex, new Rect(0, 0, baseTex.width, baseTex.height), new Vector2(0.5f, 0.5f));
            baseImg.preserveAspect = false;
            baseImg.raycastTarget = false;

            // 指针(单独旋转,pivot 在指针根部/中心)
            var needleGo = new GameObject("Needle");
            needleGo.transform.SetParent(go.transform, false);
            var needleRT = needleGo.AddComponent<RectTransform>();
            StretchFill(needleRT);
            needleRT.pivot = new Vector2(kSmallNeedlePivotX, kSmallNeedlePivotY);
            var needleImg = needleGo.AddComponent<Image>();
            var needleTex = Resources.Load<Texture2D>("Closeups/d6/d6_prop_smallcompass_needle");
            if (needleTex != null)
            needleImg.sprite = Sprite.Create(needleTex, new Rect(0, 0, needleTex.width, needleTex.height), new Vector2(0.5f, 0.5f));
            needleImg.preserveAspect = false;
            needleImg.raycastTarget = false;

            go.SetActive(false);
        }

        static void StretchFill(RectTransform child)
        {
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        // ── 过场视频/白闪 ─────────────────────────────────────────────────
        /// <summary>解谜成功后的收尾。
        /// v3 修复:第一件事就是白幕遮屏 + 关掉特写层,然后再去准备/播放开门视频。
        /// 旧版是先 Prepare 视频(最长 5 秒)才 Close 特写 → 齿轮会一直挂在原地。</summary>
        static IEnumerator PlayDoorCutsceneThenCallback(Action cb)
        {
            // ① 第一时间白幕遮屏,把齿轮盖掉
            var overlay = FadeOverlayColored.Get();
            if (overlay != null)
                yield return overlay.FadeToColor(Color.white, 0.2f);

            // ② 立刻关特写层(齿轮/旋钮/道具在这里销毁,不会留在原地)
            Debug.Log("[GearDialCloseup] 解谜完成,先关闭特写层再走后续流程");
            CloseupView.SetCanClose(true);
            CloseupView.Close();

            // ③ 准备开门视频(此时屏幕已被白幕盖住,准备多久都看不见齿轮)
            var vp = VideoPlayerProxy.Create("~DoorOpenVideo");
            bool videoOk = false;
            if (vp != null)
            {
                string videoPath = FindDoorVideoFile();
                if (videoPath != null && vp.Setup(videoPath))
                {
                    Debug.Log($"[GearDialCloseup] 找到开门视频: {videoPath},开始准备");
                    vp.Prepare();
                    float timeout = 5f;
                    while (!vp.IsPrepared && !vp.HasError && timeout > 0f)
                    {
                        timeout -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                    videoOk = vp.IsPrepared && !vp.HasError;
                    if (!videoOk)
                        Debug.LogWarning("[GearDialCloseup] 视频准备失败或超时,退化为白闪");
                }
                else
                {
                    Debug.LogWarning("[GearDialCloseup] 视频 Setup 失败或未找到视频文件,退化为白闪");
                }
                if (!videoOk) vp.Destroy();
            }
            else
            {
                Debug.LogWarning("[GearDialCloseup] VideoPlayer 不可用,退化为白闪");
            }

            // ④ 有视频:先揭开白幕再播;没视频:白幕上停一下再揭开
            if (videoOk)
            {
                Debug.Log("[GearDialCloseup] 视频准备完成,开始播放");
                vp.Play();
                if (overlay != null)
                    yield return overlay.FadeToClear(0.3f);
                while (!vp.IsFinished && !vp.HasError)
                    yield return null;
                Debug.Log("[GearDialCloseup] 视频播放完成");
                vp.Destroy();
            }
            else if (overlay != null)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                yield return overlay.FadeToClear(0.3f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            Debug.Log("[GearDialCloseup] 收尾完成,调用回调 cb?.Invoke()");
            cb?.Invoke();
            Debug.Log("[GearDialCloseup] 回调函数已调用完成");
        }

        static string FindDoorVideoFile()
        {
            string fileName = "door_open_cutscene.mp4";
            string p1 = System.IO.Path.Combine(Application.streamingAssetsPath, "Video", fileName);
            if (System.IO.File.Exists(p1)) return p1;
            string p2 = System.IO.Path.Combine(Application.dataPath, "_Project", "Resources", "Video", fileName);
            if (System.IO.File.Exists(p2)) return p2;
            return null;
        }

        // 对外暴露 baseAngle 数组(供 Behaviour 读取)
        public static float GetBaseAngle(int index)
        {
            if (index < 0 || index >= kPropCount) return 0f;
            return kPropBaseAngles[index];
        }

        /// <summary>调试用:现场校准某个道具的 baseAngle(单位:度,增量)。</summary>
        public static void AdjustBaseAngle(int index, float deltaDeg)
        {
            if (index < 0 || index >= kPropCount) return;
            kPropBaseAngles[index] = Mathf.Repeat(kPropBaseAngles[index] + deltaDeg, 360f);
        }

        /// <summary>ease-out-back:t=0→0, t=1→1,中途有轻微过冲。</summary>
        internal static float EaseOutBack(float t, float overshoot)
        {
            float s = Mathf.Max(0.0001f, overshoot);
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }

        /// <summary>ease-out-cubic。</summary>
        internal static float EaseOutCubic(float t)
        {
            float u = 1f - Mathf.Clamp01(t);
            return 1f - u * u * u;
        }

        // ========================================================================
        //  GearDialBehaviour —— 主逻辑组件
        //   维护 5 个道具的 rotation/解出状态、当前槽位、胜负判定。
        //   实际角度 = baseAngle + rotation;胜利时过冲吸附到精确角度并锁定。
        // ========================================================================
        public class GearDialBehaviour : MonoBehaviour
        {
            public Transform propContainer;
            public RectTransform gearRT;
            public RectTransform knobRT;

            float[] _propRotations;   // 每个道具的旋转量(叠加在 baseAngle 上)
            bool[] _propSolved;
            bool[] _propLocking;      // 正在播"咬合"锁定动画
            float[] _lockFrom, _lockTo, _lockT;

            Image[][] _propFadeImgs;  // 每个道具需要淡入淡出的 Image(小罗盘有 2 个)

            int _currentSlot;
            bool _propFading;
            float _propFadeT;

            // ── 齿轮状态 ──
            float _gearAngle;         // 逻辑角度(拖动累加,归一到 [0,360))
            float _gearVisual;        // 当前显示角度
            float _gearVel;           // SmoothDamp 速度
            bool _gearSnapping;       // 松手吸附动画中
            float _gearSnapFrom, _gearSnapTo, _gearSnapT;

            // ── 旋钮状态 ──
            float _knobVisual;
            float _knobVel;

            // ── 悬停 / 按住反馈 ──
            int _dragMode;            // 0=无 1=齿轮 2=旋钮
            float _knobScale = 1f, _gearScale = 1f;

            GameObject[] _propGos;
            RectTransform[] _propRTs;
            RectTransform _smallNeedleRT;

            bool _notifiedSolved;

            Image _knobImg;

            // ── 对外只读状态(调试 HUD 用)────────────────────────────────
            public int CurrentSlot => _currentSlot;
            public float GearAngle => _gearAngle;
            public float GearVisual => _gearVisual;
            public float KnobAngle => _knobVisual;
            public int PropCount => kPropCount;
            public float GetRotation(int i) => (i >= 0 && i < kPropCount) ? _propRotations[i] : 0f;
            public bool IsSolved(int i) => (i >= 0 && i < kPropCount) && _propSolved[i];
            public bool IsLocking(int i) => (i >= 0 && i < kPropCount) && _propLocking[i];
            public bool AllSolved()
            {
                for (int i = 0; i < kPropCount; i++) if (!_propSolved[i]) return false;
                return true;
            }
            public float GetActualAngle(int i)
            {
                if (i < 0 || i >= kPropCount) return 0f;
                return Mathf.Repeat(GearDialCloseup.GetBaseAngle(i) + _propRotations[i], 360f);
            }
            public float AngleErrorToWin(int i)
            {
                return Mathf.DeltaAngle(GetActualAngle(i), kWinAngle);
            }

            void Awake()
            {
                _propRotations = new float[kPropCount];
                _propSolved = new bool[kPropCount];
                _propLocking = new bool[kPropCount];
                _lockFrom = new float[kPropCount];
                _lockTo = new float[kPropCount];
                _lockT = new float[kPropCount];
                _propGos = new GameObject[kPropCount];
                _propRTs = new RectTransform[kPropCount];
                _propFadeImgs = new Image[kPropCount][];

                // 自己查找子物体,不依赖外部字段赋值(避免 AddComponent → Awake 时序问题)
                var propContainerTf = transform.Find("PropContainer");
                gearRT = transform.Find("GearWheel")?.GetComponent<RectTransform>();
                knobRT = transform.Find("Knob")?.GetComponent<RectTransform>();

                if (knobRT != null) _knobImg = knobRT.GetComponent<Image>();

                string[] names = { "Prop_Button", "Prop_Dial", "Prop_Gramophone", "Prop_LongCompass", "Prop_SmallCompass" };
                for (int i = 0; i < kPropCount; i++)
                {
                    var go = propContainerTf != null ? propContainerTf.Find(names[i])?.gameObject : null;
                    _propGos[i] = go;
                    if (go != null)
                    {
                        _propRTs[i] = go.GetComponent<RectTransform>();
                        _propFadeImgs[i] = go.GetComponentsInChildren<Image>(true);
                    }
                }

                // 小罗盘指针
                var sc = propContainerTf != null ? propContainerTf.Find("Prop_SmallCompass") : null;
                if (sc != null)
                {
                    var needle = sc.Find("Needle");
                    if (needle != null) _smallNeedleRT = needle.GetComponent<RectTransform>();
                }

                // 随机初始 rotation,保证结果角度不在目标 ±20° 内
                for (int i = 0; i < kPropCount; i++)
                {
                    _propRotations[i] = RandomStartRotation(i);
                    _propSolved[i] = false;
                    _propLocking[i] = false;
                    ApplyPropRotation(i);
                }

                // 初始齿轮角度 0 → 正上方孔位里画的道具
                _currentSlot = DetentToSlot(0f);
                _gearAngle = 0f;
                _gearVisual = 0f;
                _gearSnapping = false;
                _knobVisual = _propRotations[_currentSlot];
                ApplyGearVisual(0f);
                CommitSlot(_currentSlot, instant: true);

                _notifiedSolved = false;

                Debug.Log("[GearDialCloseup] 初始化完成,5个道具初始角度:");
                for (int i = 0; i < kPropCount; i++)
                {
                    Debug.Log($"  道具{i} ({(GearDialCloseup.PropSlot)i}): base={GearDialCloseup.GetBaseAngle(i):F1}° "
                    + $"rot={_propRotations[i]:F1}° → 实际={GetActualAngle(i):F1}°");
                }
            }

            /// <summary>随机一个初始旋转量,保证 (baseAngle + rotation) mod 360 不在目标±20°内。</summary>
            float RandomStartRotation(int index)
            {
                float baseAngle = GearDialCloseup.GetBaseAngle(index);
                float target = kWinAngle;

                // 先随机一个 0~360 的旋转量
                float rot = UnityEngine.Random.Range(0f, 360f);
                float actual = Mathf.Repeat(baseAngle + rot, 360f);

                // 检查是否落在禁区(目标±20°)
                float diff = Mathf.DeltaAngle(actual, target);
                if (Mathf.Abs(diff) < 20f)
                {
                    // 偏移 180°,确保不在禁区
                    rot += 180f;
                }

                return Mathf.Repeat(rot + 180f, 360f) - 180f;  // 归一到 [-180,180)
            }

            void Update()
            {
                float dt = Time.unscaledDeltaTime;

                UpdateGear(dt);
                UpdateKnob(dt);
                UpdateLockAnimations(dt);
                UpdatePropFade(dt);
                UpdateHover(dt);

                if (!_notifiedSolved && AllSolved())
                {
                    _notifiedSolved = true;
                    Debug.Log("[GearDialCloseup] 全部 5 个道具对齐,解谜完成!");
                    GearDialCloseup.NotifySolved();
                }
            }

            // ── 齿轮:连续跟手 + 磁吸顿挫 + 松手过冲吸附 ────────────────────────

            // ── 齿轮孔位与道具的对应(素材实测,见 docs/d6/识别_齿轮5孔图案.png)──
            // 盘面上 5 个圆窗各画着一个道具的小图标,孔位按顺时针编号:
            //   0=正上 1=右上 2=右下 3=左下 4=左上
            // 实测图案: 孔0=唱片机 孔1=长罗盘 孔2=小罗盘 孔3=表盘 孔4=按钮
            // 转到「正上方」的孔 = 当前正在解密的道具 → 图案必须和显示的道具对上。
            static readonly int[] kSlotByWindow = new int[kPropCount] {
                (int)PropSlot.Gramophone,    // 孔0 正上:黑胶唱片 + 右上唱臂
                (int)PropSlot.LongCompass,   // 孔1 右上:长针贯穿直径的罗盘
                (int)PropSlot.SmallCompass,  // 孔2 右下:顶部带环的怀表
                (int)PropSlot.Dial,          // 孔3 左下:带刻度钟面 + 细针
                (int)PropSlot.Button,        // 孔4 左上:带刻度 + 粗针的按钮盘
            };

            /// <summary>齿轮角度(顺时针,度) → 此刻转到正上方的孔位编号(0~4)。
            /// 齿轮顺时针转 1 档,原正上方的孔顺时针走开一格,
            /// 所以补到正上方的是编号小一格的孔。</summary>
            static int AngleToWindow(float angle)
            {
                int step = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / kGearStep) % kPropCount;
                return (kPropCount - step) % kPropCount;
            }

            /// <summary>齿轮角度 → 当前档位 = 正上方孔位里画的道具。</summary>
            static int DetentToSlot(float angle)
            {
                return kSlotByWindow[AngleToWindow(angle)];
            }

            /// <summary>槽位 → 把该道具的图案转到正上方所需的齿轮角度(顺时针,度)。</summary>
            static float SlotToAngle(int slot)
            {
                int w = 0;
                for (int i = 0; i < kPropCount; i++)
                    if (kSlotByWindow[i] == slot) { w = i; break; }
                return Mathf.Repeat(kGearStep * ((kPropCount - w) % kPropCount), 360f);
            }

            /// <summary>玩家拖动齿轮时调用(增量角度)。</summary>
            public void RotateGear(float deltaDeg)
            {
                if (gearRT == null) return;
                deltaDeg = Mathf.Clamp(deltaDeg, -GearDialCloseup.GearMaxDeltaPerFrame, GearDialCloseup.GearMaxDeltaPerFrame);
                if (Mathf.Approximately(deltaDeg, 0f)) return;

                _gearSnapping = false;   // 继续拖 → 取消松手吸附
                _gearAngle = Mathf.Repeat(_gearAngle + deltaDeg, 360f);

                // 越过档位中线 → 立刻"咔哒",并同步切换指针界面
                int slot = DetentToSlot(_gearAngle);
                if (slot != _currentSlot)
                {
                    CommitSlot(slot, instant: false);
                    PlayClick();
                }
            }

            /// <summary>松手:把齿轮过冲吸附到最近档位。</summary>
            public void EndGearDrag()
            {
                float detent = Mathf.Round(_gearAngle / kGearStep) * kGearStep;
                _gearAngle = Mathf.Repeat(detent, 360f);

                int slot = DetentToSlot(_gearAngle);
                if (slot != _currentSlot) { CommitSlot(slot, instant: false); PlayClick(); }

                // 从当前位置过冲到档位
                _gearSnapFrom = _gearVisual;
                _gearSnapTo = detent;
                _gearSnapT = 0f;
                _gearSnapping = true;
            }

            void UpdateGear(float dt)
            {
                if (gearRT == null) return;

                if (_gearSnapping)
                {
                    _gearSnapT += dt / Mathf.Max(0.0001f, GearDialCloseup.GearSnapTime);
                    float t = Mathf.Clamp01(_gearSnapT);
                    float e = GearDialCloseup.EaseOutBack(t, GearDialCloseup.GearSnapOvershoot);
                    _gearVisual = Mathf.LerpAngle(_gearSnapFrom, _gearSnapTo, e);
                    if (t >= 1f)
                    {
                        _gearSnapping = false;
                        _gearVisual = Mathf.Repeat(_gearSnapTo, 360f);
                    }
                }
                else
                {
                    // 磁吸目标:离最近档位越近,越被拉住(形成越档阻力)
                    float detent = Mathf.Round(_gearAngle / kGearStep) * kGearStep;
                    float dist = Mathf.Abs(Mathf.DeltaAngle(_gearAngle, detent));
                    float w = Mathf.Clamp01(1f - dist / Mathf.Max(1f, GearDialCloseup.GearMagnetRange));
                    w = w * w * (3f - 2f * w);   // smoothstep
                    float target = Mathf.LerpAngle(_gearAngle, detent, GearDialCloseup.GearMagnetStrength * w);

                    _gearVisual = Mathf.SmoothDampAngle(_gearVisual, target, ref _gearVel,
                        GearDialCloseup.GearFollowSmoothTime, Mathf.Infinity, dt);
                }

                ApplyGearVisual(_gearVisual);
            }

            void ApplyGearVisual(float angle)
            {
                if (gearRT == null) return;
                gearRT.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Repeat(angle, 360f));
            }

            // ── 旋钮:旋转当前道具 ────────────────────────────────────────────

            public void RotateCurrentProp(float deltaDeg)
            {
                int slot = _currentSlot;
                if (_propSolved[slot] || _propLocking[slot]) return;   // 锁定/咬合中不接受输入

                deltaDeg = Mathf.Clamp(deltaDeg, -GearDialCloseup.KnobMaxDeltaPerFrame, GearDialCloseup.KnobMaxDeltaPerFrame);
                if (Mathf.Approximately(deltaDeg, 0f)) return;

                // 归一到 [-180,180),避免长时间旋转后数值无限增长
                float r = _propRotations[slot] + deltaDeg;
                _propRotations[slot] = Mathf.Repeat(r + 180f, 360f) - 180f;
                ApplyPropRotation(slot);

                // 检查是否对齐目标
                float diff = Mathf.DeltaAngle(GetActualAngle(slot), kWinAngle);
                if (Mathf.Abs(diff) <= kWinTolerance)
                    BeginPropLock(slot);
            }

            /// <summary>开始"咬合"锁定动画:指针过冲到精确 45°。</summary>
            void BeginPropLock(int slot)
            {
                _propLocking[slot] = true;
                _lockFrom[slot] = _propRotations[slot];
                // 目标旋转量,取与当前位置之间最短路径(避免绕远路)
                float targetRot = kWinAngle - GearDialCloseup.GetBaseAngle(slot);
                _lockTo[slot] = _lockFrom[slot] + Mathf.DeltaAngle(_lockFrom[slot], targetRot);
                _lockT[slot] = 0f;
                PlayLock();
            }

            void UpdateLockAnimations(float dt)
            {
                for (int i = 0; i < kPropCount; i++)
                {
                    if (!_propLocking[i]) continue;

                    _lockT[i] += dt / Mathf.Max(0.0001f, GearDialCloseup.PropLockTime);
                    float t = Mathf.Clamp01(_lockT[i]);
                    float e = GearDialCloseup.EaseOutBack(t, GearDialCloseup.PropLockOvershoot);
                    _propRotations[i] = Mathf.LerpAngle(_lockFrom[i], _lockTo[i], e);
                    ApplyPropRotation(i);

                    if (t >= 1f)
                    {
                        _propLocking[i] = false;
                        _propRotations[i] = Mathf.Repeat(_lockTo[i] + 180f, 360f) - 180f;
                        _propSolved[i] = true;
                        ApplyPropRotation(i);
                        Debug.Log($"[GearDialCloseup] 道具 {i} ({(GearDialCloseup.PropSlot)i}) 对齐定住!");
                    }
                }
            }

            void ApplyPropRotation(int index)
            {
                if (index < 0 || index >= kPropCount) return;
                if (index == 4) // SmallCompass
                {
                    if (_smallNeedleRT != null)
                    _smallNeedleRT.localRotation = Quaternion.Euler(0, 0, -_propRotations[index]);
                }
                else
                {
                    if (_propRTs[index] != null)
                    _propRTs[index].localRotation = Quaternion.Euler(0, 0, -_propRotations[index]);
                }
            }

            // ── 槽位切换 ─────────────────────────────────────────────────────

            void CommitSlot(int slot, bool instant)
            {
                _currentSlot = slot;

                for (int i = 0; i < kPropCount; i++)
                {
                    var go = _propGos[i];
                    if (go == null) continue;
                    if (i == slot)
                    {
                        go.SetActive(true);
                        SetPropAlpha(i, instant ? 1f : 0f);
                    }
                    else
                    {
                        go.SetActive(false);
                    }
                }

                _propFadeT = instant ? 1f : 0f;
                _propFading = !instant;

                Debug.Log($"[GearDialCloseup] 切换到槽位 {slot}: {(GearDialCloseup.PropSlot)slot}");
            }

            void SetPropAlpha(int i, float a)
            {
                var imgs = _propFadeImgs[i];
                if (imgs == null) return;
                for (int k = 0; k < imgs.Length; k++)
                {
                    var im = imgs[k];
                    if (im == null) continue;
                    var c = im.color;
                    c.a = a;
                    im.color = c;
                }
            }

            void UpdatePropFade(float dt)
            {
                if (!_propFading) return;
                _propFadeT += dt / Mathf.Max(0.0001f, GearDialCloseup.PropFadeTime);
                float t = Mathf.Clamp01(_propFadeT);
                SetPropAlpha(_currentSlot, GearDialCloseup.EaseOutCubic(t));
                if (t >= 1f)
                {
                    _propFading = false;
                    SetPropAlpha(_currentSlot, 1f);
                }
            }

            // ── 旋钮视觉 / 悬停反馈 ──────────────────────────────────────────

            void UpdateKnob(float dt)
            {
                if (knobRT == null) return;

                // 旋钮角度 = 当前道具的偏移量(换道具 / 锁定时自动同步到位)
                float target = _propRotations[_currentSlot];
                _knobVisual = Mathf.SmoothDampAngle(_knobVisual, target, ref _knobVel,
                    GearDialCloseup.KnobSmoothTime, Mathf.Infinity, dt);
                knobRT.localRotation = Quaternion.Euler(0f, 0f, -_knobVisual);

                // 锁定后旋钮压暗,表示"这一格已经咬合,不可再转"
                if (_knobImg != null)
                {
                    bool locked = _propSolved[_currentSlot] || _propLocking[_currentSlot];
                    float k = locked ? GearDialCloseup.KnobLockedDim : 1f;
                    var c = _knobImg.color;
                    float blend = 1f - Mathf.Exp(-12f * dt);
                    _knobImg.color = Color.Lerp(c, new Color(k, k, k, c.a), blend);
                }
            }

            void UpdateHover(float dt)
            {
                float knobTarget = 1f, gearTarget = 1f;

                if (GearDialCloseup.HoverHighlight)
                {
                    Vector2 mp = Input.mousePosition;
                    bool hk = _dragMode == 2 || IsInKnobArea(mp, null);
                    bool hg = !hk && (_dragMode == 1 || IsInGearArea(mp, null));
                    if (hk) knobTarget = GearDialCloseup.HoverScaleKnob;
                    else if (hg) gearTarget = GearDialCloseup.HoverScaleGear;

                    // 按住时轻微回缩,做出"按下"的反馈
                    if (_dragMode == 2) knobTarget = Mathf.Lerp(knobTarget, 1f, 0.55f);
                    if (_dragMode == 1) gearTarget = Mathf.Lerp(gearTarget, 1f, 0.55f);
                }

                float blend = 1f - Mathf.Exp(-18f * dt);
                _knobScale = Mathf.Lerp(_knobScale, knobTarget, blend);
                _gearScale = Mathf.Lerp(_gearScale, gearTarget, blend);

                if (knobRT != null) knobRT.localScale = new Vector3(_knobScale, _knobScale, 1f);
                if (gearRT != null) gearRT.localScale = new Vector3(_gearScale, _gearScale, 1f);
            }

            /// <summary>拖拽处理器同步拖拽状态(0=无 1=齿轮 2=旋钮)。</summary>
            public void SetDragMode(int mode) { _dragMode = mode; }

            // ── 命中检测 ─────────────────────────────────────────────────────

            public bool IsInKnobArea(Vector2 screenPos, Camera cam)
            {
                if (knobRT == null) return false;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    knobRT, screenPos, cam, out localPos);
                float r = 120f; // 放大热区
                return localPos.magnitude <= r;
            }

            public bool IsInGearArea(Vector2 screenPos, Camera cam)
            {
                if (gearRT == null) return false;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    gearRT, screenPos, cam, out localPos);
                // 齿轮显示区域:半径 kGearSize/2
                float r = GearDialCloseup.kGearSize * 0.55f;
                return localPos.magnitude <= r;
            }

            // ── 音效(无音频文件时 AudioManager 静默)──────────────────────────
            void PlayClick()
            {
                if (!string.IsNullOrEmpty(GearDialCloseup.ClickSfx))
                    AudioManager.PlaySfx(GearDialCloseup.ClickSfx);
            }
            void PlayLock()
            {
                if (!string.IsNullOrEmpty(GearDialCloseup.LockSfx))
                    AudioManager.PlaySfx(GearDialCloseup.LockSfx);
            }

            // ── 调试 API(供 GearDialDebugBootstrap 使用)────────────────────

            /// <summary>直接跳到某个槽位(把该道具的图案转到正上方,带齿轮旋转动画)。</summary>
            public void DebugJumpToSlot(int slot)
            {
                slot = ((slot % kPropCount) + kPropCount) % kPropCount;
                float ang = SlotToAngle(slot);
                _gearSnapping = false;
                _gearAngle = ang;
                if (slot != _currentSlot) { CommitSlot(slot, instant: false); PlayClick(); }
                _gearSnapFrom = _gearVisual;
                _gearSnapTo = ang;
                _gearSnapT = 0f;
                _gearSnapping = true;
            }

            /// <summary>调试:直接旋转齿轮(度)。</summary>
            public void DebugNudgeGear(float deltaDeg) => RotateGear(deltaDeg);

            /// <summary>调试:旋转当前道具指针(度)。</summary>
            public void DebugNudgeCurrent(float deltaDeg) => RotateCurrentProp(deltaDeg);

            /// <summary>调试:把当前道具对齐到 45°(先退 20°,好看清咬合动画)。</summary>
            public void DebugSolveCurrent()
            {
                int slot = _currentSlot;
                if (_propSolved[slot] || _propLocking[slot]) return;
                float target = kWinAngle - GearDialCloseup.GetBaseAngle(slot);
                _propRotations[slot] = Mathf.Repeat(target - 20f + 180f, 360f) - 180f;
                ApplyPropRotation(slot);
                BeginPropLock(slot);
            }

            /// <summary>调试:重置整个谜题(重新随机 + 清空进度)。</summary>
            public void DebugReset()
            {
                for (int i = 0; i < kPropCount; i++)
                {
                    _propRotations[i] = RandomStartRotation(i);
                    _propSolved[i] = false;
                    _propLocking[i] = false;
                    _lockT[i] = 0f;
                    ApplyPropRotation(i);
                }
                _gearAngle = 0f;
                _gearVisual = 0f;
                _gearVel = 0f;
                _gearSnapping = false;
                _knobVel = 0f;
                _knobVisual = _propRotations[0];
                ApplyGearVisual(0f);
                CommitSlot(0, instant: true);
                _notifiedSolved = false;
                Debug.Log("[GearDialCloseup] 谜题已重置。");
            }
        }

        // ========================================================================
        //  GearDialDragHandler —— 全局拖拽处理器
        // ========================================================================
        public class GearDialDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public RectTransform dialRoot;
            public RectTransform gearRT;
            public RectTransform knobRT;

            enum DragMode { None, Gear, Knob }
            DragMode _mode;
            float _lastAngle;
            bool _rebaseNext;          // 死区内跟丢了角度 → 出死区后重新取基准,不产生跳变
            GearDialBehaviour _behaviour;

            void Awake()
            {
                _behaviour = GetComponentInParent<GearDialBehaviour>();
                _mode = DragMode.None;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (_behaviour == null) return;
                Vector2 screenPos = eventData.position;

                var cam = eventData.pressEventCamera;
                if (_behaviour.IsInKnobArea(screenPos, cam))
                {
                    _mode = DragMode.Knob;
                    bool dead;
                    _lastAngle = ComputeAngle(knobRT, screenPos, cam, GearDialCloseup.KnobMinRadiusPx, out dead);
                    _rebaseNext = dead;
                }
                else if (_behaviour.IsInGearArea(screenPos, cam))
                {
                    _mode = DragMode.Gear;
                    _lastAngle = ComputeAngle(gearRT, screenPos, cam, 0f, out _);
                    _rebaseNext = false;
                }
                else
                {
                    _mode = DragMode.None;
                    _rebaseNext = false;
                }

                _behaviour.SetDragMode(_mode == DragMode.None ? 0 : (_mode == DragMode.Gear ? 1 : 2));
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_mode == DragMode.None || _behaviour == null) return;
                Vector2 screenPos = eventData.position;

                var cam = eventData.pressEventCamera;
                if (_mode == DragMode.Gear)
                {
                    float angle = ComputeAngle(gearRT, screenPos, cam, 0f, out _);
                    float delta = Mathf.DeltaAngle(_lastAngle, angle);
                    _behaviour.RotateGear(delta);
                    _lastAngle = angle;
                }
                else if (_mode == DragMode.Knob)
                {
                    bool dead;
                    float angle = ComputeAngle(knobRT, screenPos, cam, GearDialCloseup.KnobMinRadiusPx, out dead);

                    // 手指进到圆心死区:不旋转,等出死区后重新取基准(避免角度翻转导致的跳变)
                    if (dead) { _rebaseNext = true; return; }
                    if (_rebaseNext) { _rebaseNext = false; _lastAngle = angle; return; }

                    float delta = Mathf.DeltaAngle(_lastAngle, angle);
                    // 单帧跳变(急速划过圆心 / 丢帧)一律当伪信号:只重新取基准,不产生爆转
                    if (Mathf.Abs(delta) > GearDialCloseup.KnobMaxDeltaPerFrame) { _lastAngle = angle; return; }
                    _behaviour.RotateCurrentProp(delta);
                    _lastAngle = angle;
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_mode == DragMode.Gear && _behaviour != null)
                _behaviour.EndGearDrag();
                _mode = DragMode.None;
                if (_behaviour != null) _behaviour.SetDragMode(0);
            }

            /// <summary>计算屏幕点相对于 pivot 的角度(0°=12点,顺时针为正)。
            /// minRadiusPx>0 时把点推到最小半径外计算,并通过 deadZone 告知调用方
            /// "手指已进圆心死区"(此时角度不可信,应当冻结旋转)。</summary>
            float ComputeAngle(RectTransform target, Vector2 screenPos, Camera cam, float minRadiusPx, out bool deadZone)
            {
                deadZone = false;
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, screenPos, cam, out localPos);

                if (minRadiusPx > 0f && localPos.magnitude < minRadiusPx)
                {
                    deadZone = true;
                    float m = localPos.magnitude;
                    if (m < 0.0001f) localPos = new Vector2(0f, minRadiusPx);
                    else localPos = localPos / m * minRadiusPx;
                }

                float deg = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;
                // atan2: 右=0,上=90,左=±180,下=-90
                // 目标: 上=0°,顺时针增加 → 右=90,下=180,左=270
                float result = 90f - deg;
                if (result < 0) result += 360f;
                return result;
            }
        }

        // ========================================================================
        //  FlashRunner —— 空 MonoBehaviour,仅用于启动协程
        // ========================================================================
        public class FlashRunner : MonoBehaviour { }
    }
}
