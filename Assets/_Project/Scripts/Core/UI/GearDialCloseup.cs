// ============================================================================
//  GearDialCloseup.cs —— 开门齿轮旋钮谜题特写(2026-07-27 v2)
//   玩家转动下方齿轮切换 5 个呈现道具,再用右下角旋钮旋转当前道具,
//   让 5 个道具的指针都指向右上角(45°)时全部定住 → 解谜完成 → 开门过场。
//
//   背景/道具图层 3400×1200,StretchFill 填满 1920×678 容器。
//   齿轮/旋钮是 500×500 小图,固定位置放置。
//   全局 hitbox 接收所有拖拽,自动判断拖齿轮还是拖旋钮。
//   齿轮是分段旋转(五档卡嗒),旋钮是连续旋转。
//
//   角度系统:每个道具的"实际指向角度" = baseAngle + rotation
//     baseAngle = 图默认的指向角度(PIL 精确检测)
//     rotation  = 玩家旋钮转动的量(可正可负)
//     胜利条件:Normalize(baseAngle + rotation) 接近 45°
//
//   使用方式: GearDialCloseup.Show(() => { /* 解谜成功回调 */ });
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
        //   2026-07-27: PIL 检测值偏差较大,根据图片视觉分析重测:
        //     Button≈48°, Dial≈45°, Gramophone≈60°, LongCompass≈30°, SmallCompass≈45°
        //   TODO: 在游戏内精细校准(让指针视觉上指向45°时记下rotation,反推baseAngle)
        static readonly float[] kPropBaseAngles = new float[kPropCount] {
            48f,     // 0:Button 按钮 → 默认指向右上(约1点半方向)
            45f,     // 1:Dial 表盘 → 默认指向右上(约1点半方向)
            60f,     // 2:Gramophone 唱片机 → 默认指向右上偏右(唱臂方向)
            30f,     // 3:LongCompass 长指针罗盘 → 默认指向右上偏上
            45f,     // 4:SmallCompass 小罗盘指针 → 默认指向正右上
        };

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
        static IEnumerator PlayDoorCutsceneThenCallback(Action cb)
        {
            Debug.Log("[GearDialCloseup] PlayDoorCutsceneThenCallback 协程开始");
            // 优先路径:尝试播放开门过场视频
            var vp = VideoPlayerProxy.Create("~DoorOpenVideo");
            if (vp != null)
            {
                string videoPath = FindDoorVideoFile();
                if (videoPath != null)
                {
                    Debug.Log($"[GearDialCloseup] 找到开门视频: {videoPath}");
                    bool setupOk = vp.Setup(videoPath);
                    if (setupOk)
                    {
                        vp.Prepare();
                        float timeout = 5f;
                        while (!vp.IsPrepared && !vp.HasError && timeout > 0)
                        {
                            timeout -= Time.unscaledDeltaTime;
                            yield return null;
                        }
                        if (vp.IsPrepared && !vp.HasError)
                        {
                            Debug.Log("[GearDialCloseup] 视频准备完成,开始播放");
                            CloseupView.SetCanClose(true);  // 允许关闭
                            CloseupView.Close();
                            vp.Play();
                            while (!vp.IsFinished && !vp.HasError)
                            yield return null;
                            Debug.Log("[GearDialCloseup] 视频播放完成");
                            vp.Destroy();
                            Debug.Log("[GearDialCloseup] 准备调用回调函数 cb?.Invoke()");
                            cb?.Invoke();
                            Debug.Log("[GearDialCloseup] 回调函数已调用完成");
                            yield break;
                        }
                        else
                        {
                            Debug.LogWarning("[GearDialCloseup] 视频准备失败或超时,退化为白闪");
                            vp.Destroy();
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[GearDialCloseup] 视频 Setup 失败,退化为白闪");
                        vp.Destroy();
                    }
                }
                else
                {
                    Debug.LogWarning("[GearDialCloseup] 未找到开门视频文件,退化为白闪");
                    vp.Destroy();
                }
            }

            // 退化路径:白闪
            Debug.Log("[GearDialCloseup] 进入白闪/兜底路径");
            CloseupView.SetCanClose(true);  // 允许关闭
            var overlay = FadeOverlayColored.Get();
            if (overlay != null)
            {
                yield return overlay.FadeToColor(Color.white, 0.3f);
                CloseupView.Close();
                yield return new WaitForSecondsRealtime(0.5f);
                yield return overlay.FadeToClear(0.3f);
            }
            else
            {
                CloseupView.Close();
                yield return new WaitForSecondsRealtime(0.8f);
            }
            Debug.Log("[GearDialCloseup] 白闪/兜底路径完成，准备调用回调函数");
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

        // ========================================================================
        //  GearDialBehaviour —— 主逻辑组件
        //   维护 5 个道具的 rotation/解出状态、当前槽位、胜负判定。
        //   实际角度 = baseAngle + rotation;胜利时吸附到精确角度并锁定。
        // ========================================================================
        public class GearDialBehaviour : MonoBehaviour
        {
            public Transform propContainer;
            public RectTransform gearRT;
            public RectTransform knobRT;

            float[] _propRotations;  // 每个道具的旋转量(叠加在 baseAngle 上)
            bool[] _propSolved;

            int _currentSlot;
            float _gearAngle;        // 齿轮累计旋转角度

            GameObject[] _propGos;
            RectTransform[] _propRTs;
            RectTransform _smallNeedleRT;

            bool _notifiedSolved;

            void Awake()
            {
                _propRotations = new float[5];
                _propSolved = new bool[5];
                _propGos = new GameObject[5];
                _propRTs = new RectTransform[5];

                // 自己查找子物体,不依赖外部字段赋值(避免 AddComponent → Awake 时序问题)
                var propContainerTf = transform.Find("PropContainer");
                gearRT = transform.Find("GearWheel")?.GetComponent<RectTransform>();
                knobRT = transform.Find("Knob")?.GetComponent<RectTransform>();

                string[] names = { "Prop_Button", "Prop_Dial", "Prop_Gramophone", "Prop_LongCompass", "Prop_SmallCompass" };
                for (int i = 0; i < 5; i++)
                {
                    var go = propContainerTf != null ? propContainerTf.Find(names[i])?.gameObject : null;
                    _propGos[i] = go;
                    if (go != null) _propRTs[i] = go.GetComponent<RectTransform>();
                }

                // 小罗盘指针
                var sc = propContainerTf != null ? propContainerTf.Find("Prop_SmallCompass") : null;
                if (sc != null)
                {
                    var needle = sc.Find("Needle");
                    if (needle != null) _smallNeedleRT = needle.GetComponent<RectTransform>();
                }

                // 随机初始 rotation,保证结果角度不在目标 ±20° 内
                for (int i = 0; i < 5; i++)
                {
                    _propRotations[i] = RandomStartRotation(i);
                    _propSolved[i] = false;
                    ApplyPropRotation(i);
                }

                // 初始槽位 0,齿轮角度 0
                _currentSlot = 0;
                _gearAngle = 0f;
                ApplyGearRotation();
                UpdatePropVisibility();

                _notifiedSolved = false;

                Debug.Log("[GearDialCloseup] 初始化完成,5个道具初始角度:");
                for (int i = 0; i < 5; i++)
                {
                    float actual = Mathf.Repeat(GearDialCloseup.GetBaseAngle(i) + _propRotations[i], 360f);
                    Debug.Log($"  道具{i} ({(GearDialCloseup.PropSlot)i}): base={GearDialCloseup.GetBaseAngle(i):F1}° "
                    + $"rot={_propRotations[i]:F1}° → 实际={actual:F1}°");
                }
            }

            /// <summary>随机一个初始旋转量,保证 (baseAngle + rotation) mod 360 不在目标±20°内。</summary>
            float RandomStartRotation(int index)
            {
                float baseAngle = GearDialCloseup.GetBaseAngle(index);
                float target = GearDialCloseup.kWinAngle;

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

                return rot;
            }

            void Update()
            {
                if (_notifiedSolved) return;

                bool allSolved = true;
                for (int i = 0; i < 5; i++)
                {
                    if (!_propSolved[i]) { allSolved = false; break; }
                }

                if (allSolved)
                {
                    _notifiedSolved = true;
                    Debug.Log("[GearDialCloseup] 全部 5 个道具对齐,解谜完成!");
                    GearDialCloseup.NotifySolved();
                }
            }

            // ── 齿轮操作 ──────────────────────────────────────────────────────

            public void RotateGear(float deltaDeg)
            {
                _gearAngle += deltaDeg;
                ApplyGearRotation();

                // 计算当前槽位(连续实时计算,不卡档时也能看到切换)
                int slot = Mathf.RoundToInt(_gearAngle / 72f);
                slot = ((slot % 5) + 5) % 5;

                if (slot != _currentSlot)
                {
                    _currentSlot = slot;
                    UpdatePropVisibility();
                    Debug.Log($"[GearDialCloseup] 切换到槽位 {_currentSlot}: {(GearDialCloseup.PropSlot)_currentSlot}");
                }
            }

            public void EndGearDrag()
            {
                // 吸附到最近档位
                float snapped = Mathf.Round(_gearAngle / 72f) * 72f;
                _gearAngle = snapped;
                ApplyGearRotation();
            }

            void ApplyGearRotation()
            {
                if (gearRT == null) return;
                // 分段显示:只在 0/72/144/216/288 五档停留(咔哒感)
                float stepped = Mathf.Round(_gearAngle / 72f) * 72f;
                gearRT.localRotation = Quaternion.Euler(0, 0, -stepped);
            }

            // ── 旋钮操作(旋转当前道具) ───────────────────────────────────────

            public void RotateCurrentProp(float deltaDeg)
            {
                int slot = _currentSlot;
                if (_propSolved[slot]) return;

                _propRotations[slot] += deltaDeg;
                ApplyPropRotation(slot);

                // 检查是否对齐目标
                float actual = Mathf.Repeat(GearDialCloseup.GetBaseAngle(slot) + _propRotations[slot], 360f);
                float diff = Mathf.DeltaAngle(actual, GearDialCloseup.kWinAngle);
                if (Mathf.Abs(diff) <= GearDialCloseup.kWinTolerance)
                {
                    // 吸附到精确角度 + 锁定
                    float targetRot = GearDialCloseup.kWinAngle - GearDialCloseup.GetBaseAngle(slot);
                    _propRotations[slot] = targetRot;
                    _propSolved[slot] = true;
                    ApplyPropRotation(slot);
                    Debug.Log($"[GearDialCloseup] 道具 {slot} ({(GearDialCloseup.PropSlot)slot}) 对齐定住!");
                }
            }

            void ApplyPropRotation(int index)
            {
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

            void UpdatePropVisibility()
            {
                for (int i = 0; i < 5; i++)
                {
                    if (_propGos[i] != null)
                    _propGos[i].SetActive(i == _currentSlot);
                }
            }

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
                    _lastAngle = ComputeAngle(knobRT, screenPos, cam);
                }
                else if (_behaviour.IsInGearArea(screenPos, cam))
                {
                    _mode = DragMode.Gear;
                    _lastAngle = ComputeAngle(gearRT, screenPos, cam);
                }
                else
                {
                    _mode = DragMode.None;
                }
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_mode == DragMode.None || _behaviour == null) return;
                Vector2 screenPos = eventData.position;

                var cam = eventData.pressEventCamera;
                if (_mode == DragMode.Gear)
                {
                    float angle = ComputeAngle(gearRT, screenPos, cam);
                    float delta = Mathf.DeltaAngle(_lastAngle, angle);
                    _behaviour.RotateGear(delta);
                    _lastAngle = angle;
                }
                else if (_mode == DragMode.Knob)
                {
                    float angle = ComputeAngle(knobRT, screenPos, cam);
                    float delta = Mathf.DeltaAngle(_lastAngle, angle);
                    _behaviour.RotateCurrentProp(delta);
                    // 旋钮视觉跟随
                    if (knobRT != null)
                    {
                        float z = knobRT.localRotation.eulerAngles.z;
                        knobRT.localRotation = Quaternion.Euler(0, 0, z - delta);
                    }
                    _lastAngle = angle;
                }
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_mode == DragMode.Gear && _behaviour != null)
                _behaviour.EndGearDrag();
                _mode = DragMode.None;
            }

            /// <summary>计算屏幕点相对于 pivot 的角度(0°=12点,顺时针为正)。</summary>
            float ComputeAngle(RectTransform target, Vector2 screenPos, Camera cam)
            {
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    target, screenPos, cam, out localPos);
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
