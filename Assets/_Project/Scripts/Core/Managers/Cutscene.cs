// ============================================================================
//  Cutscene.cs —— 剧情脚本步骤执行器(契约 §12)  🟡→🟢
//  用于第 0/4 幕纯剧情段:走到 X → 播对白 → 屏幕变暗 → 剧情杀 等顺序动作。
//
//  用法(内容层 B):
//    在场景根节点挂 Cutscene 组件,Inspector 里往 steps[] 拖各种 XxxStep(见下方 CutsceneSteps.cs)。
//    或用代码 API 组装(推荐 Cutscene 类下的 Builder,B 一行一步):
//        cs.Add(new SayStep(Dialogues.prologue_0_01))
//          .Add(new WalkPlayerToStep(15f))
//          .Add(new SwitchEraStep(Era.Young))
//          .Add(new GoToSceneStep(Rooms.Chapter1_Hall));
//        cs.Play();
//
//  执行期间 PlayerController.SetControllable(false),点击不驱动老人;完成后恢复。
//  单实例:场景里挂多个 Cutscene 只有一个能 Play(其它 Play 请求排队/警告)。
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LostGoddess
{
    public class Cutscene : MonoBehaviour
    {
        [Tooltip("是否 Awake 时自动播(第 0 幕这种一进场景就演的用)")]
        public bool playOnAwake = false;

        [Tooltip("步骤列表(代码 Add 或运行时组装)")]
        public List<CutsceneStep> steps = new List<CutsceneStep>();

        public event Action OnFinished;

        Coroutine _routine;
        bool _playing;

        public bool IsPlaying => _playing;

        void Start()
        {
            if (playOnAwake) Play();
        }

        public Cutscene Add(CutsceneStep step)
        {
            steps.Add(step);
            return this;
        }

        public void Play()
        {
            if (_playing) { Debug.LogWarning("[Cutscene] 已在播放中,忽略重复 Play。"); return; }
            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            _playing = true;
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(false);

            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                yield return s.Execute(this);
            }

            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);
            _playing = false;
            _routine = null;
            OnFinished?.Invoke();
        }

        public void Stop()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _playing = false;
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(true);
        }
    }

    /// <summary>Cutscene 单步基类。子类实现 Execute 返回一个 IEnumerator,Cutscene 逐步 yield。</summary>
    public abstract class CutsceneStep
    {
        public abstract IEnumerator Execute(Cutscene owner);
    }
}
