// ============================================================================
//  CoffinDialCloseup.cs —— 石棺表盘指针谜题特写(2026-07-27 v3)
//   玩家拖动长/短指针都指到 12 点(正上方)→ 闪白 → 解锁中年形态 → 组装投影仪。
//   三层 UI:表盘背景 + 长指针 + 短指针,全部 3400×1200 同尺寸图层,
//   StretchFill 填满 1920×678 容器(与展台特写尺寸一致,铺满中间场景条带)。
//   短指针在上层(叠在长指针上面,符合真实钟表)。
//
//   指针旋转中心 = 指针根部 = 表盘圆心,不是图像几何中心。
//   每个指针有独立的 pivot 和 baseAngle(默认指向的基准角度偏移)。
//   每个指针配一个全屏透明 hitbox 接收拖拽,解决指针图形太难点中的问题。
//
//   v3 改动:
//     - 修正 pivot/baseAngle(基于 PIL 像素精确检测,表盘圆心≈(1588, 489))
//     - 添加透明 hitbox 扩大拖拽热区
//     - 短指针在上、长指针在下
//
//   使用方式: CoffinDialCloseup.Show(() => { /* 解谜成功回调 */ });
// ============================================================================

using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class CoffinDialCloseup
    {
        static Action _onSolved;

        // 2026-07-27 v3: 基于 PIL 像素精确检测结果(表盘圆心≈(1588, 489))
        //   两指针根部几乎重合于圆心,pivot 非常接近。
        //   坐标系:UGUI 左下=(0,0),右上=(1,1);角度:12点=0°,顺时针为正。
        const float kShortPivotX = 0.4671f;
        const float kShortPivotY = 0.5929f;
        const float kLongPivotX  = 0.4669f;
        const float kLongPivotY  = 0.5934f;

        // 指针图默认方向对应的 baseAngle(0°=12点,顺时针为正)。
        //   短指针:指向右下方≈3:38 → 109.3°
        //   长指针:指向左上方≈10:55 → 327.1°(等价于 -32.9°)
        const float kShortBaseAngle = 109.3f;
        const float kLongBaseAngle  = 327.1f;

        public static void Show(Action onSolved = null)
        {
            _onSolved = onSolved;

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            var template = BuildDialTemplate();
            CloseupView.Open(template);
            CloseupView.SetCanClose(false);
        }

        public static void NotifySolved()
        {
            var runnerGo = new GameObject("~CoffinDialVideoRunner");
            UnityEngine.Object.DontDestroyOnLoad(runnerGo);
            runnerGo.AddComponent<FlashRunner>().StartCoroutine(PlayCutsceneThenCallback(_onSolved));
        }

        /// <summary>播放中年解锁过场视频 → 关闭表盘特写 → 回调。
        /// 视频文件不存在或准备失败时退化为白闪过渡。
        /// 注意:VideoPlayer 通过反射调用,避免编译时引用 UnityEngine.VideoModule。</summary>
        static IEnumerator PlayCutsceneThenCallback(Action cb)
        {
            // 先尝试播放视频(反射版 VideoPlayer)
            var vp = VideoPlayerProxy.Create();
            if (vp != null)
            {
                // 找视频文件
                string videoPath = FindVideoFile();
                if (videoPath != null)
                {
                    Debug.Log("[CoffinDialCloseup] 找到视频文件: " + videoPath);

                    if (vp.Setup(videoPath))
                    {
                        Debug.Log("[CoffinDialCloseup] 视频播放器已创建,开始准备...");
                        vp.Prepare();

                        // 等视频准备好
                        float timeout = 10f;
                        while (!vp.IsPrepared && !vp.HasError && timeout > 0f)
                        {
                            timeout -= Time.unscaledDeltaTime;
                            yield return null;
                        }

                        if (vp.IsPrepared)
                        {
                            Debug.Log("[CoffinDialCloseup] 视频准备完成,开始播放");
                            CloseupView.Close();
                            vp.Play();

                            // 等视频播完
                            while (!vp.IsFinished && !vp.HasError)
                                yield return null;

                            Debug.Log("[CoffinDialCloseup] 视频播放完成");
                            vp.Destroy();
                            cb?.Invoke();
                            yield break;
                        }
                        else
                        {
                            Debug.LogWarning("[CoffinDialCloseup] 视频准备失败/超时,退化为白闪");
                        }
                    }
                    vp.Destroy();
                }
                else
                {
                    Debug.LogWarning("[CoffinDialCloseup] 找不到视频文件,退化为白闪");
                    vp.Destroy();
                }
            }
            else
            {
                Debug.LogWarning("[CoffinDialCloseup] VideoPlayer 创建失败(Video 模块可能不可用),退化为白闪");
            }

            // 退化方案:白闪过渡(注意:必须 FadeToClear 否则白屏卡死)
            Debug.Log("[CoffinDialCloseup] 使用白闪过渡代替视频");
            var overlay = FadeOverlayColored.Get();
            yield return overlay.FadeToColor(Color.white, 0.25f);
            CloseupView.Close();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return overlay.FadeToClear(0.25f);

            cb?.Invoke();
        }

        /// <summary>在常见路径中查找视频文件,返回完整路径或 null。</summary>
        static string FindVideoFile()
        {
            string[] candidates = new string[]
            {
                System.IO.Path.Combine(Application.streamingAssetsPath,
                    "Video", "middle_unlock_cutscene.mp4"),
                System.IO.Path.Combine(Application.dataPath,
                    "_Project", "Resources", "Video", "middle_unlock_cutscene.mp4"),
            };
            foreach (var p in candidates)
            {
                if (System.IO.File.Exists(p)) return p;
            }
            return null;
        }

        /// <summary>反射版 VideoPlayer 封装。
        /// 用反射操作 UnityEngine.Video.VideoPlayer,避免编译时强引用。
        /// 如果 Video 模块不可用,Create() 返回 null。</summary>
        class VideoPlayerProxy
        {
            GameObject _go;
            Component _vp;  // VideoPlayer 组件
            Type _vpType;
            RenderTexture _rt;
            bool _prepared;
            bool _finished;
            bool _error;

            public bool IsPrepared => _prepared;
            public bool IsFinished => _finished;
            public bool HasError => _error;

            public static VideoPlayerProxy Create()
            {
                // 反射加载 VideoPlayer 类型
                Assembly videoAsm = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "UnityEngine.VideoModule")
                    {
                        videoAsm = asm;
                        break;
                    }
                }
                if (videoAsm == null)
                {
                    // 再试试 UnityEngine.dll 里有没有(老版本)
                    try
                    {
                        var t = typeof(Component).Assembly.GetType("UnityEngine.Video.VideoPlayer");
                        if (t != null) videoAsm = t.Assembly;
                    }
                    catch { }
                }
                if (videoAsm == null) return null;

                var vpType = videoAsm.GetType("UnityEngine.Video.VideoPlayer");
                if (vpType == null) return null;

                var proxy = new VideoPlayerProxy();
                proxy._vpType = vpType;

                // 创建 GameObject 和 UI 画布
                proxy._go = new GameObject("~MiddleUnlockVideo");
                UnityEngine.Object.DontDestroyOnLoad(proxy._go);

                var canvasGo = new GameObject("VideoCanvas");
                canvasGo.transform.SetParent(proxy._go.transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var rawGo = new GameObject("RawImage");
                rawGo.transform.SetParent(canvasGo.transform, false);
                var rawImg = rawGo.AddComponent<RawImage>();
                var rt = rawImg.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

                proxy._rt = new RenderTexture(1920, 1080, 24);
                proxy._rt.Create();
                rawImg.texture = proxy._rt;

                // 添加 VideoPlayer 组件
                proxy._vp = (Component)proxy._go.AddComponent(vpType);

                return proxy;
            }

            public bool Setup(string videoUrl)
            {
                if (_vp == null) return false;
                try
                {
                    // playOnAwake = false
                    _vpType.GetProperty("playOnAwake").SetValue(_vp, false);
                    // waitForFirstFrame = true
                    _vpType.GetProperty("waitForFirstFrame").SetValue(_vp, true);
                    // source = Url (VideoSource.Url = 1)
                    _vpType.GetProperty("source").SetValue(_vp, 1);
                    // url
                    string url = "file:///" + videoUrl.Replace('\\', '/');
                    _vpType.GetProperty("url").SetValue(_vp, url);
                    // renderMode = RenderTexture (VideoRenderMode.RenderTexture = 2)
                    _vpType.GetProperty("renderMode").SetValue(_vp, 2);
                    // targetTexture
                    _vpType.GetProperty("targetTexture").SetValue(_vp, _rt);
                    // isLooping = false
                    _vpType.GetProperty("isLooping").SetValue(_vp, false);
                    // audioOutputMode = Direct (VideoAudioOutputMode.Direct = 2)
                    _vpType.GetProperty("audioOutputMode").SetValue(_vp, 2);

                    // 注册 prepareCompleted 事件(用动态方法,避免强类型引用)
                    var prepareEvt = _vpType.GetEvent("prepareCompleted");
                    if (prepareEvt != null)
                    {
                        var handler = CreateVoidHandler(prepareEvt.EventHandlerType, () => _prepared = true);
                        prepareEvt.AddEventHandler(_vp, handler);
                    }

                    // 注册 loopPointReached 事件
                    var loopEvt = _vpType.GetEvent("loopPointReached");
                    if (loopEvt != null)
                    {
                        var handler = CreateVoidHandler(loopEvt.EventHandlerType, () => _finished = true);
                        loopEvt.AddEventHandler(_vp, handler);
                    }

                    // 注册 errorReceived 事件
                    var errorEvt = _vpType.GetEvent("errorReceived");
                    if (errorEvt != null)
                    {
                        var handler = CreateStringHandler(errorEvt.EventHandlerType, msg =>
                        {
                            _error = true;
                            Debug.LogError("[VideoPlayerProxy] 错误: " + msg);
                        });
                        errorEvt.AddEventHandler(_vp, handler);
                    }

                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[VideoPlayerProxy] Setup 失败: " + e.Message);
                    return false;
                }
            }

            /// <summary>创建一个 void 委托(用于 VideoPlayer 事件),忽略参数,调用 action。</summary>
            static Delegate CreateVoidHandler(Type delegateType, Action action)
            {
                // VideoPlayer 事件签名通常是 void(VideoPlayer) 或 void(VideoPlayer, string)
                // 用表达式树动态生成适配方法,忽略参数直接调用 action。
                var paramExprs = GetDelegateParameterTypes(delegateType)
                    .Select((t, i) => Expression.Parameter(t, "p" + i)).ToArray();
                var callExpr = Expression.Call(
                    Expression.Constant(action), typeof(Action).GetMethod("Invoke"));
                var lambda = Expression.Lambda(delegateType, callExpr, paramExprs);
                return lambda.Compile();
            }

            /// <summary>创建带 string 参数的委托处理器(用于 errorReceived 事件)。</summary>
            static Delegate CreateStringHandler(Type delegateType, Action<string> action)
            {
                var paramTypes = GetDelegateParameterTypes(delegateType);
                var paramExprs = paramTypes.Select((t, i) => Expression.Parameter(t, "p" + i)).ToArray();
                // 找到 string 类型的参数
                Expression stringArg = null;
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    if (paramTypes[i] == typeof(string))
                    {
                        stringArg = paramExprs[i];
                        break;
                    }
                }
                if (stringArg == null) stringArg = Expression.Constant("unknown");
                var callExpr = Expression.Call(
                    Expression.Constant(action), typeof(Action<string>).GetMethod("Invoke"),
                    stringArg);
                var lambda = Expression.Lambda(delegateType, callExpr, paramExprs);
                return lambda.Compile();
            }

            static Type[] GetDelegateParameterTypes(Type delegateType)
            {
                var invoke = delegateType.GetMethod("Invoke");
                return invoke.GetParameters().Select(p => p.ParameterType).ToArray();
            }

            public void Play()
            {
                _vpType.GetMethod("Play").Invoke(_vp, null);
            }

            public void Prepare()
            {
                _vpType.GetMethod("Prepare").Invoke(_vp, null);
            }

            public void Destroy()
            {
                if (_go != null)
                {
                    UnityEngine.Object.Destroy(_go);
                    _go = null;
                }
                if (_rt != null)
                {
                    _rt.Release();
                    UnityEngine.Object.Destroy(_rt);
                    _rt = null;
                }
            }
        }

        static GameObject BuildDialTemplate()
        {
            var root = new GameObject("CoffinDial");
            var rt = root.AddComponent<RectTransform>();
            // 与展台特写一致:铺满中间场景条带(1920×678,比例 2.833:1 = 3400:1200)
            rt.sizeDelta = new Vector2(1920f, 678f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 表盘背景
            var faceGo = new GameObject("DialFace");
            faceGo.transform.SetParent(root.transform, false);
            var faceImg = faceGo.AddComponent<Image>();
            var faceTex = Resources.Load<Texture2D>("Closeups/coffin_dial_face");
            if (faceTex != null)
                faceImg.sprite = Sprite.Create(faceTex, new Rect(0, 0, faceTex.width, faceTex.height), new Vector2(0.5f, 0.5f));
            faceImg.preserveAspect = false;
            faceImg.raycastTarget = false;
            StretchFill(faceImg.rectTransform);

            // 长指针(下层,先创建) + 短指针(上层,后创建)
            // 真实钟表长针(分针)在短针(时针)上面——但我们的"长/短"指长度,
            // 这里短针在上层(视觉上压在长针上),符合常见钟表。
            // 注意:两个指针都不接收点击,由全局 GlobalDragHandler 统一处理拖拽。
            BuildHandVisual(root.transform, "LongHand",  "Closeups/coffin_dial_long",
                kLongPivotX,  kLongPivotY,  kLongBaseAngle);
            BuildHandVisual(root.transform, "ShortHand", "Closeups/coffin_dial_short",
                kShortPivotX, kShortPivotY, kShortBaseAngle);

            // 全局透明 hitbox — 统一接收所有点击,自动判断拖哪个指针
            // 这样两个指针不会互相遮挡,点哪里都能正确选择要拖动的指针
            var hitboxGo = new GameObject("GlobalHitbox");
            hitboxGo.transform.SetParent(root.transform, false);
            var hitboxImg = hitboxGo.AddComponent<Image>();
            hitboxImg.color = new Color(0, 0, 0, 0.001f);
            hitboxImg.raycastTarget = true;
            StretchFill(hitboxImg.rectTransform);

            var handler = hitboxGo.AddComponent<DialDragHandler>();
            handler.dialRoot = rt;

            root.AddComponent<DialBehaviour>();
            return root;
        }

        /// <summary>仅构建指针视觉图(不带交互,交互由全局 hitbox 处理)。</summary>
        static GameObject BuildHandVisual(Transform parent, string name, string texPath,
            float pivotX, float pivotY, float baseAngle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            StretchFill(rt);
            rt.pivot = new Vector2(pivotX, pivotY); // 旋转中心 = 指针根部 = 表盘圆心

            var img = go.AddComponent<Image>();
            var tex = Resources.Load<Texture2D>(texPath);
            if (tex != null)
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            img.preserveAspect = false;
            img.raycastTarget = false; // 视觉图不接收点击

            // 存一下 baseAngle,供 DialBehaviour 读取
            var angleComp = go.AddComponent<HandAngleInfo>();
            angleComp.baseAngle = baseAngle;
            angleComp.pivotX = pivotX;
            angleComp.pivotY = pivotY;

            return go;
        }

        static void StretchFill(RectTransform child)
        {
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

        /// <summary>挂在每个指针视觉图上,保存角度和 pivot 信息。</summary>
        class HandAngleInfo : MonoBehaviour
        {
            public float baseAngle;
            public float pivotX;
            public float pivotY;
            public float currentAngle; // 当前角度(12点=0°,顺时针+)
        }

        class DialBehaviour : MonoBehaviour
        {
            RectTransform _shortRt;
            RectTransform _longRt;
            HandAngleInfo _shortInfo;
            HandAngleInfo _longInfo;
            bool _solved;

            void Awake()
            {
                var shortGo = transform.Find("ShortHand").gameObject;
                var longGo  = transform.Find("LongHand").gameObject;
                _shortRt = shortGo.GetComponent<RectTransform>();
                _longRt  = longGo.GetComponent<RectTransform>();
                _shortInfo = shortGo.GetComponent<HandAngleInfo>();
                _longInfo  = longGo.GetComponent<HandAngleInfo>();

                // 初始角度随机偏开 12 点,避免一开始就命中
                // 短指针:随机到左下区域(约 5~9 点方向)
                SetHandAngle(_shortRt, _shortInfo, UnityEngine.Random.Range(150f, 270f));
                // 长指针:随机到右下区域(约 2~6 点方向)
                SetHandAngle(_longRt, _longInfo, UnityEngine.Random.Range(60f, 180f));
            }

            /// <summary>设置指定指针的当前角度。</summary>
            public static void SetHandAngle(RectTransform rt, HandAngleInfo info, float angle)
            {
                info.currentAngle = angle;
                // 需要旋转的角度 = currentAngle - baseAngle(顺时针)
                // Z rotation: 顺时针 = -Z → rotation.z = -(currentAngle - baseAngle) = baseAngle - currentAngle
                rt.localRotation = Quaternion.Euler(0, 0, info.baseAngle - angle);
            }

            /// <summary>获取指定指针的当前角度。</summary>
            public static float GetHandAngle(HandAngleInfo info)
            {
                return info.currentAngle;
            }

            /// <summary>根据点击点(表盘本地坐标)找最近的指针。
            /// 返回 "Short" 或 "Long"。</summary>
            public HandAngleInfo FindClosestHand(Vector2 localPoint, out RectTransform outRt)
            {
                float distShort = DistanceToHand(localPoint, _shortRt, _shortInfo);
                float distLong  = DistanceToHand(localPoint, _longRt,  _longInfo);
                if (distShort <= distLong)
                {
                    outRt = _shortRt;
                    return _shortInfo;
                }
                else
                {
                    outRt = _longRt;
                    return _longInfo;
                }
            }

            /// <summary>计算点到指针线段的最短距离(用于判断点哪个指针)。</summary>
            float DistanceToHand(Vector2 p, RectTransform rt, HandAngleInfo info)
            {
                // 指针根部(旋转中心)在本地坐标系中的位置
                Vector2 root = new Vector2(
                    (info.pivotX - 0.5f) * rt.rect.width,
                    (info.pivotY - 0.5f) * rt.rect.height);

                // 指针尖端方向(单位向量)
                // 12点方向 = +y → angle=0° 对应向量 (0, 1)
                // 顺时针 angle 度 → 方向向量 = (sin(angle), cos(angle))
                float rad = info.currentAngle * Mathf.Deg2Rad;
                Vector2 tipDir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                // 估算指针长度(用图片像素长度 / 图片宽度 * 容器宽度 近似)
                // 短指针 ≈ 112px / 3400px * 容器宽; 长指针 ≈ 89px / 3400px * 容器宽
                // 这里用一个相对值:指针长度约为 0.035 * 容器宽
                float handLen = 0.04f * rt.rect.width;

                // 计算点 p 到线段 [root, root + tipDir * handLen] 的最短距离
                Vector2 ap = p - root;
                float t = Vector2.Dot(ap, tipDir);
                t = Mathf.Clamp(t, 0f, handLen);
                Vector2 closest = root + tipDir * t;
                return Vector2.Distance(p, closest);
            }

            void Update()
            {
                if (_solved) return;
                const float kTol = 8f;
                float sa = Mathf.Abs(Normalize180(_shortInfo.currentAngle));
                float la = Mathf.Abs(Normalize180(_longInfo.currentAngle));
                if (sa <= kTol && la <= kTol)
                {
                    _solved = true;
                    CoffinDialCloseup.NotifySolved();
                }
            }

            float Normalize180(float a)
            {
                while (a > 180f) a -= 360f;
                while (a < -180f) a += 360f;
                return a;
            }
        }

        /// <summary>全局拖拽处理器:挂在全屏 hitbox 上,自动判断拖哪个指针。</summary>
        class DialDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public RectTransform dialRoot;  // 表盘容器根
            DialBehaviour _dial;
            HandAngleInfo _draggingInfo;    // 当前正在拖的指针
            RectTransform _draggingRt;

            void Awake()
            {
                _dial = GetComponentInParent<DialBehaviour>();
            }

            public void OnBeginDrag(PointerEventData e)
            {
                if (_dial == null || dialRoot == null) return;

                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dialRoot, e.position, e.pressEventCamera, out localPoint);

                // 找到最近的指针
                RectTransform rt;
                _draggingInfo = _dial.FindClosestHand(localPoint, out rt);
                _draggingRt = rt;
            }

            public void OnDrag(PointerEventData e)
            {
                if (_draggingInfo == null || _draggingRt == null) return;
                if (dialRoot == null) return;

                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dialRoot, e.position, e.pressEventCamera, out localPoint);

                // 旋转中心(根部)在 dialRoot 本地坐标系中的位置
                Vector2 rootPos = new Vector2(
                    (_draggingInfo.pivotX - 0.5f) * dialRoot.rect.width,
                    (_draggingInfo.pivotY - 0.5f) * dialRoot.rect.height);

                // 鼠标位置相对于根部的向量
                float dx = localPoint.x - rootPos.x;
                float dy = localPoint.y - rootPos.y;
                if (dx == 0f && dy == 0f) return;

                // atan2(y, x): +x 轴为 0°,逆时针为正
                // 12 点方向 = +y = atan2 中 90°
                // 钟表角度(12点=0, 顺时针+): angle = 90° - atan2(y,x)
                float rad = Mathf.Atan2(dy, dx);
                float deg = rad * Mathf.Rad2Deg;
                float newAngle = 90f - deg;

                DialBehaviour.SetHandAngle(_draggingRt, _draggingInfo, newAngle);
            }

            public void OnEndDrag(PointerEventData e)
            {
                _draggingInfo = null;
                _draggingRt = null;
            }
        }

        class FlashRunner : MonoBehaviour { }
    }
}
