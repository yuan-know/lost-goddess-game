// ============================================================================
//  InsightMode.cs —— 岁月洞察(契约 §11)  🟡→🟢
//  老年按 Q 键触发:全屏变暗滤镜 + OnToggled 事件(墙壁密语/二楼高亮点等 SpriteRenderer 监听淡入)。
//  非老年按 Q → 播 wrong_era_need_old 提示对白,不进入。
//  程序侧:提供 Toggle/SetActive 与 IsActive 三件套;运行时自动创建 UI 全屏滤镜面板与按键监听。
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class InsightMode
    {
        public static event Action<bool> OnToggled;  // true=进入洞察,false=退出;UI/后处理/幻影 Sprite 都监听
        public static bool IsActive { get; private set; }

        static InsightRuntime _runtime;

        static void EnsureRuntime()
        {
            if (_runtime != null) return;
            _runtime = InsightRuntime.CreateAttached();
            // 场景切换时不销毁(DontDestroyOnLoad 在 CreateAttached 内做了)
            // 场景切换时若之前是激活状态,自动关闭(避免残留滤镜)
            SceneLoader.OnAfterLoad += (_) => { if (IsActive) SetActive(false); };
        }

        public static void Toggle()
        {
            EnsureRuntime();
            // 非老年按 Q → 提示
            if (GameState.CurrentEra != Era.Old)
            {
                DialogueSystem.Show(Dialogues.wrong_era_need_old);
                return;
            }
            SetActive(!IsActive);
        }

        public static void SetActive(bool on)
        {
            EnsureRuntime();
            if (IsActive == on) return;
            IsActive = on;
            _runtime.Fade(on);
            OnToggled?.Invoke(on);
            if (on) GameState.SetFlag(Flags.Prologue_InsightUsed, true);
        }
    }

    /// <summary>InsightMode 的运行时载体:全屏滤镜 + 按键监听。</summary>
    public class InsightRuntime : MonoBehaviour
    {
        Image _overlay;
        CanvasGroup _cg;

        public static InsightRuntime CreateAttached()
        {
            var go = new GameObject("~InsightRuntime");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 350;  // 在 DialogueUI(400) 之下,不遮对白
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            var imgGo = new GameObject("Overlay");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.1f, 0f);  // 深蓝黑色,alpha 由 fade 控
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var rt2 = go.AddComponent<InsightRuntime>();
            rt2._overlay = img;
            rt2._cg = go.AddComponent<CanvasGroup>();
            rt2._cg.alpha = 0f;
            rt2._cg.blocksRaycasts = false;
            return rt2;
        }

        void Update()
        {
            // Q 键切换洞察(全局监听)。避免 UI 输入框抢焦点情况:检查 Input.GetKeyDown 即可
            if (Input.GetKeyDown(KeyCode.Q))
            {
                InsightMode.Toggle();
            }
        }

        public void Fade(bool on)
        {
            StopAllCoroutines();
            StartCoroutine(FadeCoroutine(on));
        }

        IEnumerator FadeCoroutine(bool on)
        {
            float duration = 0.35f;
            float startAlpha = _overlay.color.a;
            float endAlpha = on ? 0.55f : 0f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                Color c = _overlay.color; c.a = Mathf.Lerp(startAlpha, endAlpha, k);
                _overlay.color = c;
                yield return null;
            }
        }
    }

    /// <summary>挂在场景里 SpriteRenderer 上的"洞察时才显影"组件。
    /// 用于:墙壁幻影图腾、二楼坍塌处高亮、隐藏线索等。默认 alpha=0,
    /// 监听 InsightMode.OnToggled 淡入/淡出。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class InsightPhantom : MonoBehaviour
    {
        [Tooltip("洞察时的目标 alpha")]
        [Range(0f, 1f)] public float visibleAlpha = 1f;
        [Tooltip("淡入淡出时长(秒)")]
        public float fadeDuration = 0.4f;

        SpriteRenderer _sr;
        Coroutine _fade;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            SetAlpha(InsightMode.IsActive ? visibleAlpha : 0f);
        }

        void OnEnable()  { InsightMode.OnToggled += OnInsight; }
        void OnDisable() { InsightMode.OnToggled -= OnInsight; }

        void OnInsight(bool on)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeTo(on ? visibleAlpha : 0f));
        }

        IEnumerator FadeTo(float target)
        {
            float start = _sr.color.a;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeDuration);
                SetAlpha(Mathf.Lerp(start, target, k));
                yield return null;
            }
            _fade = null;
        }

        void SetAlpha(float a)
        {
            var c = _sr.color; c.a = a; _sr.color = c;
        }
    }
}
