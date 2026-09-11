// ============================================================================
//  SmokeSprayTrigger.cs —— 烟雾喷出的**运行时触发接口**
//
//  给谁用:内容层(谜题 / 剧情 / 按钮)想在任意时机"喷一口烟",调这里。
//  挂在哪:挂在 SmokeEmitter 上(和 SmokeSprayEmitterAim 同一个物体)。
//          smokeAnimator 留空会自动找子物体上的 Animator(默认摆法就是)。
//
//  怎么用(代码):
//      GetComponent<SmokeSprayTrigger>().PlayOnce();          // 喷一次,播完自动隐藏
//      GetComponent<SmokeSprayTrigger>().PlayLooped();        // 持续喷,直到 Stop()
//      GetComponent<SmokeSprayTrigger>().Stop();              // 停
//      trigger.onSprayFinished.AddListener(OnSprayDone);      // 播完回调(如解锁机关)
//
//  怎么用(不写代码):Inspector 里把别的物体的事件拖到
//      onSprayStart / onSprayFinished 两个 UnityEvent 上即可。
//
//  参数说明:
//      mode            OneShot=喷一次(默认,机关味);Loop=持续喷
//      autoPlayOnStart 启用即播(调试场景用;正式玩法一律关掉)
//      hideWhenIdle    没在播时把烟雾精灵隐藏(默认开,免得首帧空图挂在那里)
//
//  注:clip 的状态名固定为 "Spray"(与 SmokeSprayDebugBuilder.StateName 一致)。
// ============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LostGoddess.VFX
{
    public class SmokeSprayTrigger : MonoBehaviour
    {
        public enum SprayMode { OneShot = 0, Loop = 1 }

        [Tooltip("OneShot=喷一次(播完自动隐藏);Loop=持续喷,直到 Stop()。")]
        public SprayMode mode = SprayMode.OneShot;

        [Tooltip("启用时自动开始播放(调试用;正式玩法关掉,由谜题/剧情调 Play)。")]
        public bool autoPlayOnStart = false;

        [Tooltip("未播放时隐藏烟雾精灵。")]
        public bool hideWhenIdle = true;

        [Tooltip("烟雾动画所在 Animator。留空自动找子物体。")]
        public Animator smokeAnimator;

        [Tooltip("开始喷烟时调用。")]
        public UnityEvent onSprayStart;

        [Tooltip("OneShot 播完时调用(Loop 模式不会触发)。")]
        public UnityEvent onSprayFinished;

        public const string StateName = "Spray";   // 与 SmokeSprayDebugBuilder.StateName 一致

        /// <summary>当前是否正在喷烟。</summary>
        public bool IsPlaying { get; private set; }

        Coroutine m_Finish;

        void Awake()
        {
            if (smokeAnimator == null) smokeAnimator = GetComponentInChildren<Animator>();
        }

        void OnEnable()
        {
            if (autoPlayOnStart) Play();
            else if (hideWhenIdle) SetVisible(false);
        }

        void OnDisable() { StopNow(); }

        /// <summary>按当前 mode 播放(总是从头开始)。</summary>
        public void Play()
        {
            if (mode == SprayMode.Loop) PlayLooped();
            else PlayOnce();
        }

        /// <summary>喷一次:播完自动停并隐藏,然后回调 onSprayFinished。</summary>
        public void PlayOnce()
        {
            if (!Begin()) return;
            float len = ClipLength();
            if (len <= 0f) len = 1.5f;   // 找不到 clip 长度时的兜底(34帧@24fps≈1.42s)
            m_Finish = StartCoroutine(FinishAfter(len));
        }

        /// <summary>持续喷(clip 自循环),直到 Stop()。</summary>
        public void PlayLooped()
        {
            if (!Begin()) return;
            // clip 本身 loopTime=true,交给它自己循环即可
        }

        /// <summary>停止播放。只隐藏,不触发 onSprayFinished。</summary>
        public void Stop()
        {
            if (m_Finish != null) { StopCoroutine(m_Finish); m_Finish = null; }
            StopNow();
        }

        bool Begin()
        {
            if (smokeAnimator == null) Awake();
            if (smokeAnimator == null)
            {
                Debug.LogWarning("[SmokeSprayTrigger] 找不到烟雾 Animator,无法播放。", this);
                return false;
            }
            if (m_Finish != null) { StopCoroutine(m_Finish); m_Finish = null; }
            SetVisible(true);
            smokeAnimator.enabled = true;
            smokeAnimator.Play(StateName, 0, 0f);
            IsPlaying = true;
            onSprayStart?.Invoke();
            return true;
        }

        void StopNow()
        {
            IsPlaying = false;
            if (hideWhenIdle) SetVisible(false);
        }

        IEnumerator FinishAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            m_Finish = null;
            StopNow();
            onSprayFinished?.Invoke();
        }

        float ClipLength()
        {
            var ctrl = smokeAnimator != null ? smokeAnimator.runtimeAnimatorController : null;
            if (ctrl == null || ctrl.animationClips == null) return 0f;
            foreach (var c in ctrl.animationClips)
                if (c != null && c.name == "Smoke_Spray") return c.length;
            return ctrl.animationClips.Length > 0 && ctrl.animationClips[0] != null
                ? ctrl.animationClips[0].length : 0f;
        }

        void SetVisible(bool visible)
        {
            var sr = smokeAnimator
                ? smokeAnimator.GetComponent<SpriteRenderer>()
                : GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = visible;
        }
    }
}
