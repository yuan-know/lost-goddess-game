// ============================================================================
//  PuzzleBase.cs —— 谜题基类(契约 §3)  🟢
//  子类判定成功后调 Solve();内部 SetFlag + 触发 OnSolved(可在 Inspector 连开门/播剧情)。
// ============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace LostGoddess
{
    public abstract class PuzzleBase : MonoBehaviour
    {
        [Header("谜题(契约 §3)")]
        [Tooltip("解开时设置的 flag(留空则不写 flag)。用 Keys.Flags 里的常量")]
        public string solveFlag = "";

        [Tooltip("解开时触发,可在 Inspector 连'开门/播剧情'")]
        public UnityEvent OnSolved;

        public bool IsSolved { get; private set; }

        protected virtual void Start()
        {
            // 若存档里已标记解开,进场即恢复为已解状态(不重复触发 OnSolved)
            if (!string.IsNullOrEmpty(solveFlag) && GameState.GetFlag(solveFlag))
            {
                IsSolved = true;
                OnAlreadySolved();
            }
        }

        /// <summary>子类判定成功后调用。</summary>
        protected void Solve()
        {
            if (IsSolved) return;
            IsSolved = true;

            if (!string.IsNullOrEmpty(solveFlag))
                GameState.SetFlag(solveFlag, true);

            OnSolved?.Invoke();
            Debug.Log($"[Puzzle] {name} 已解开。");
        }

        /// <summary>进场时发现存档已解开(子类可覆盖:直接把门画成开着等)。</summary>
        protected virtual void OnAlreadySolved() { }
    }
}
