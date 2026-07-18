// ============================================================================
//  InsightHighlights.cs —— 岁月洞察下的两种显影效果(内容层 B)
//   1) InsightPhantom 已在 InsightMode.cs 中,负责"淡入到 alpha=N,退出淡出到 0"
//      → 用于墙壁幻影图腾(mural_prologue.png)。挂 SpriteRenderer 上即用,零参数。
//
//   2) InsightPulseHighlight(本文件):高亮"可爬点"用,除了 alpha 淡入,
//      还要来回脉冲发光(模拟"闪烁的幽光提示")。
//      挂 SpriteRenderer 上 → 洞察打开时 alpha 从 0 → visibleAlpha 淡入,并进入脉冲循环;
//      洞察关闭时 → 淡出到 0,停止脉冲。
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class InsightPulseHighlight : MonoBehaviour
    {
        [Tooltip("洞察时的目标 alpha 基准值")]
        [Range(0f, 1f)] public float visibleAlpha = 0.85f;
        [Tooltip("脉冲上下浮动幅度(0 = 无脉冲,静态发光)")]
        [Range(0f, 0.5f)] public float pulseAmplitude = 0.25f;
        [Tooltip("脉冲一个周期的秒数")]
        public float pulseCycle = 1.3f;
        [Tooltip("淡入/淡出时长")]
        public float fadeDuration = 0.4f;

        SpriteRenderer _sr;
        Coroutine _loop;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            SetAlpha(InsightMode.IsActive ? visibleAlpha : 0f);
        }

        void OnEnable()  { InsightMode.OnToggled += OnInsight; }
        void OnDisable() { InsightMode.OnToggled -= OnInsight; }

        void OnInsight(bool on)
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(on ? EnterAndPulse() : FadeOut());
        }

        IEnumerator EnterAndPulse()
        {
            // 淡入到 visibleAlpha
            yield return FadeAlpha(_sr.color.a, visibleAlpha, fadeDuration);
            // 无限脉冲循环
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(t * Mathf.PI * 2f / pulseCycle);  // -1..+1
                SetAlpha(visibleAlpha + k * pulseAmplitude);
                yield return null;
            }
        }

        IEnumerator FadeOut()
        {
            yield return FadeAlpha(_sr.color.a, 0f, fadeDuration);
            _loop = null;
        }

        IEnumerator FadeAlpha(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float a)
        {
            var c = _sr.color;
            c.a = Mathf.Clamp01(a);
            _sr.color = c;
        }
    }
}
