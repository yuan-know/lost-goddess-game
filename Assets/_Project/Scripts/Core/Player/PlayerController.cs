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

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (_moving) MoveStep();
            UpdateSorting();
        }

        // ── 公开 API(契约 §7)──

        public void WalkTo(Vector2 worldPos, Action onArrive = null)
        {
            if (!_controllable) { onArrive?.Invoke(); return; }

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
