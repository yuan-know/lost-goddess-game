// ============================================================================
//  PlayerController.cs —— 老人行走控制(契约 §7)  🟢
//  单屏点击寻路:点空地→走过去;交互系统调 WalkTo(interactPoint)→到位回调。
//  动画方案无关:只操作 Animator 参数(isWalking)+ localScale 翻转朝向;
//  底层是骨骼还是逐帧,本脚本不关心。美术出图前可用纯色占位方块驱动同一逻辑。
//  深度排序:按脚底 Y 值动态设 sortingOrder(单屏近大远小的前后遮挡)。
// ============================================================================

using System;
using UnityEngine;

namespace LostGoddess
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("移动")]
        [Tooltip("移动速度(单位/秒)。Young 快 / Middle 中 / Old 慢")]
        public float moveSpeed = 3f;
        [Tooltip("到达判定阈值")]
        public float arriveThreshold = 0.05f;

        [Header("动画(可空,占位期无 Animator 也能跑)")]
        public Animator animator;
        [Tooltip("Animator 中 bool 参数名:是否行走")]
        public string walkParam = "isWalking";

        [Header("朝向")]
        [Tooltip("角色贴图默认朝向是否朝右")]
        public bool spriteFacesRight = true;

        [Header("深度排序")]
        public SpriteRenderer sortingTarget; // 或 SortingGroup;这里用 SpriteRenderer 简化
        [Tooltip("脚底 Y 越小越靠前;乘一个系数转成 sortingOrder")]
        public float sortingScale = 100f;

        // ── 状态 ──
        bool _controllable = true;
        bool _moving;
        Vector2 _target;
        Action _onArrive;
        int _walkHash;

        public bool IsMoving => _moving;

        void Awake()
        {
            Instance = this;
            if (animator != null && !string.IsNullOrEmpty(walkParam))
                _walkHash = Animator.StringToHash(walkParam);
        }

        void Start()
        {
            // 确保初始状态同步到 Animator：_moving 初始是 false，
            // 如果 Animator Controller 默认 isWalking = true，
            // 没有这一步会导致一直播行走动画即使人物不动。
            SetMoving(_moving);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (_moving) MoveStep();
            UpdateSorting();

            // W 键循环切换已解锁形态
            if (_controllable && Input.GetKeyDown(KeyCode.W))
            {
                TrySwitchEra();
            }
        }

        /// <summary>尝试按 W 键切换到下一个已解锁形态</summary>
        void TrySwitchEra()
        {
            // 收集所有已解锁的 Era
            var unlockedEras = new System.Collections.Generic.List<Era>();
            foreach (Era era in System.Enum.GetValues(typeof(Era)))
            {
                if (GameState.IsEraUnlocked(era))
                    unlockedEras.Add(era);
            }

            // 少于 2 个解锁 → 还没通过剧情解释切换功能，不响应
            if (unlockedEras.Count < 2)
                return;

            // 找到当前 Era 在列表中的索引
            Era current = GameState.CurrentEra;
            int index = unlockedEras.IndexOf(current);
            if (index < 0) index = 0;

            // 取下一个（循环）
            int nextIndex = (index + 1) % unlockedEras.Count;
            Era nextEra = unlockedEras[nextIndex];

            // 闪白切换，PlayerBuilder 会自动重建角色
            var overlay = FadeOverlayColored.Get();
            StartCoroutine(DoSwitchEra(nextEra, overlay));
        }

        System.Collections.IEnumerator DoSwitchEra(Era nextEra, FadeOverlayColored overlay)
        {
            // 锁定控制，防止切换过程中误操作
            SetControllable(false);

            // 闪白
            yield return overlay.FadeToColor(Color.white, 0.25f);

            Debug.Log("[PlayerController.DoSwitchEra] 闪白完成，准备切换形态");

            // 关键：在销毁Player之前，先让overlay开始执行淡出
            overlay.StartCoroutine(FadeOutAfterSwitch(overlay, nextEra));

            Debug.Log("[PlayerController.DoSwitchEra] 已启动淡出协程");

            // 等待一帧确保协程开始运行
            yield return null;

            Debug.Log("[PlayerController.DoSwitchEra] 协程已提交，DoSwitchEra结束");
        }

        // 独立的静态协程，在overlay上运行，不依赖PlayerController
        static System.Collections.IEnumerator FadeOutAfterSwitch(FadeOverlayColored overlay, Era nextEra)
        {
            Debug.Log($"[FadeOutAfterSwitch] 开始执行，目标形态={nextEra}");

            // 切换形态
            GameState.SetEra(nextEra);

            Debug.Log("[FadeOutAfterSwitch] SetEra完成，等待0.1秒");

            // 等待新Player创建完成
            yield return new WaitForSeconds(0.1f);

            Debug.Log("[FadeOutAfterSwitch] 开始淡出");

            // 淡出
            yield return overlay.FadeToClear(0.4f);

            Debug.Log("[FadeOutAfterSwitch] 淡出完成");
        }

        // ── 公开 API(契约 §7)──

        /// <param name="force">为 true 时无视 _controllable 锁,用于 Cutscene 强制移动。</param>
        public void WalkTo(Vector2 worldPos, Action onArrive = null, bool force = false)
        {
            if (!_controllable && !force) { onArrive?.Invoke(); return; }

            // 烟火式横版:只取 X,clamp 到可走线;Y 锁定为老人当前 Y(纯左右移动,不上下)
            float targetX = worldPos.x;
            if (WalkableArea.Current != null)
                targetX = WalkableArea.Current.ClampX(targetX);

            _target = new Vector2(targetX, transform.position.y);
            _onArrive = onArrive;

            if (((Vector2)transform.position - _target).sqrMagnitude <= arriveThreshold * arriveThreshold)
            {
                Arrive();
                return;
            }
            SetMoving(true);
        }

        public void WalkTo(Transform target, Action onArrive = null)
        {
            if (target == null) { onArrive?.Invoke(); return; }
            WalkTo((Vector2)target.position, onArrive);
        }

        /// <summary>Cutscene/剧情专用:强制角色走到目标,不受玩家控制状态影响。</summary>
        public void ForceWalkTo(Vector2 worldPos, Action onArrive = null)
        {
            WalkTo(worldPos, onArrive, force: true);
        }

        public void StopMoving()
        {
            SetMoving(false);
            _onArrive = null;
        }

        public void SetControllable(bool on)
        {
            _controllable = on;
            if (!on) SetMoving(false);
        }

        /// <summary>是否可控(2026-07-20 供 PlayerTalkPrompt 等临时锁场景保存/恢复状态)。</summary>
        public bool IsControllable() => _controllable;

        /// <summary>把角色瞬移到某点(进房间时定位到出生点用)。</summary>
        public void Teleport(Vector2 worldPos)
        {
            transform.position = worldPos;
            SetMoving(false);
        }

        // ── 内部 ──

        void MoveStep()
        {
            Vector2 pos = transform.position;
            Vector2 dir = _target - pos;
            float dist = dir.magnitude;

            if (dist <= arriveThreshold)
            {
                transform.position = _target;
                Arrive();
                return;
            }

            Vector2 step = dir.normalized * moveSpeed * Time.deltaTime;
            if (step.magnitude >= dist) { transform.position = _target; Arrive(); return; }

            transform.position = pos + step;
            FaceTowards(dir.x);
        }

        void Arrive()
        {
            SetMoving(false);
            var cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();
        }

        void SetMoving(bool on)
        {
            _moving = on;
            // 延迟算 hash:Awake 时 animator 还没被 Bootstrap 赋值,那时 hash 是 0。这里补上。
            if (_walkHash == 0 && !string.IsNullOrEmpty(walkParam))
                _walkHash = Animator.StringToHash(walkParam);
            if (animator != null && _walkHash != 0)
                animator.SetBool(_walkHash, on);
        }

        void FaceTowards(float dirX)
        {
            if (Mathf.Abs(dirX) < 0.001f) return;
            bool goingRight = dirX > 0f;
            // 贴图默认朝右:朝右 scale.x 为正,朝左为负
            float sign = (goingRight == spriteFacesRight) ? 1f : -1f;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * sign;
            transform.localScale = s;
        }

        void UpdateSorting()
        {
            if (sortingTarget == null) return;
            // 脚底 Y 越小(越靠下/靠近镜头)→ sortingOrder 越大(越靠前)
            sortingTarget.sortingOrder = Mathf.RoundToInt(-transform.position.y * sortingScale);
        }
    }
}
