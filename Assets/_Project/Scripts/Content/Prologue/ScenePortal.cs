// ============================================================================
//  ScenePortal.cs —— 通用"点它跳房间"/"走过去自动跳房间"交互物(内容层 B 复用组件)
//  用法:场景里建一个 GameObject → 加 Collider2D(isTrigger)+ SpriteRenderer(可为空)→
//        挂本组件 → Inspector 或代码里填 targetRoom / requireItem / requireFlag / allowedEras。
//  两种触发模式:
//    · 点击模式(默认):玩家点它 → 走近 → 触发 OnClick → 跳场景
//    · 走入触发模式(triggerOnEnter=true):玩家走到触发区(碰到 Collider2D) → 自动跳场景
//        —— 用于"走到场景最右端自动切换"这种无需点击的边界过渡
//  逻辑:
//    · Era 不匹配 → InteractableBase 会自动播 wrongEraDialogueId(在基类里处理)
//    · requireItem 非空 && 未持有 → 播 lackItemDialogueId
//    · requireFlag 非空 && flag 未 true → 播 lackFlagDialogueId
//    · 全通过 → 可选播 successDialogueId(默认无) → SceneLoader.GoToRoom(targetRoom)
//  失败对白全部为空时 → 默认走 wrongEraDialogueId(基类)或 Debug.Log 一句。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class ScenePortal : InteractableBase
    {
        [Header("目标房间")]
        [Tooltip("SceneLoader.GoToRoom 会用这个名字")]
        public string targetRoom = "";

        [Tooltip("场景切换淡入淡出时长(0=用 SceneLoader.DefaultFade)")]
        public float fadeTime = 0f;

        [Header("触发模式")]
        [Tooltip("true=玩家走入 Collider2D 自动跳场景;false=点击触发(默认)")]
        public bool triggerOnEnter = false;

        [Header("前置条件")]
        [Tooltip("需要持有的道具 id(空=不需要)")]
        public string requireItem = "";
        [Tooltip("道具不足时播的对白 id(空=沉默)")]
        public string lackItemDialogueId = "";

        [Tooltip("需要为 true 的 flag(空=不需要)")]
        public string requireFlag = "";
        [Tooltip("flag 未满足时播的对白 id(空=沉默)")]
        public string lackFlagDialogueId = "";

        [Header("成功过场")]
        [Tooltip("跳场景前播的对白 id(空=不播,直接跳)")]
        public string successDialogueId = "";

        [Header("进入目标房间的方向")]
        [Tooltip("空=保持 SceneLoader.EnterDirection 不变;'left'=在目标场景左侧出生,'right'=右侧,'up'/'down'=中央爬梯")]
        public string enterDirection = "";

        bool _triggered;   // 防止走入触发被重复调用(碰撞抖动/淡出期间仍在区域内)

        public override void OnClick()
        {
            // 2026-07-21 走入触发模式下,点击不再算数(用户明确要求"走到边缘才切场景,不允许点击触发")
            if (triggerOnEnter) return;
            TryGoToRoom();
        }

        protected override void Awake()
        {
            base.Awake();
            // 注意:AddComponent 会立刻同步调 Awake,那时外部还没来得及给 triggerOnEnter 赋值。
            //   collider 禁用/walkToBeforeInteract 覆写等只能延后到 Start(此时字段已就绪)。
        }

        void Start()
        {
            // 走入触发模式:让点击"穿透"Portal 直达 WalkableArea,老人才能走进 Portal 区域触发切场景。
            //   ClickInputManager 用 Physics2D.OverlapPoint(mask=~0) 找 hit,LayerMask 无法排除掉走入模式的 Portal
            //   (IgnoreRaycast 层对 OverlapPoint 没特殊语义)——干脆运行时把 collider 关掉,自己在 Update 里做矩形包含检测。
            if (triggerOnEnter)
            {
                walkToBeforeInteract = false;
                var c = GetComponent<Collider2D>();
                if (c != null) c.enabled = false;
            }
        }

        // 走入触发:老人在触发区内 → 尝试跳场景
        //   collider 在走入模式下会被 Awake 里禁用(为了让点击穿透 → 老人可以走过去),
        //   所以这里不能再用 col.OverlapPoint;改成用 Portal transform 位置 + localScale 手算矩形。
        //   SandboxBootstrap / PrologueWoodsScene 建 Portal 时 localScale = (w, h, 1) 直接当宽高,
        //   于是矩形 = center ± scale/2。
        //
        //  2026-07-23 只判 X,不判 Y:
        //    走入触发本来就是"到达场景边缘"的语义,Y 由 WalkableArea 锁死,
        //    玩家不可能上下移动。之前判 Y 会因为不同 Era 的 yOffset (老年=0/中年=2.5/青年=2.5)
        //    让高偏移形态永远进不了触发区(旧 `pos.y + 1` 补丁对偏移>1.5 的形态也不够)。
        //    干脆去掉 Y 判定,对所有形态都成立。
        void Update()
        {
            if (!triggerOnEnter || _triggered) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;

            Vector2 center = transform.position;
            float halfW = Mathf.Abs(transform.lossyScale.x) * 0.5f;
            float px = pc.transform.position.x;

            if (px >= center.x - halfW && px <= center.x + halfW)
            {
                // 走入模式下不受 Era gate 拦截(纯边界过渡),条件由 requireItem/requireFlag 控制
                TryGoToRoom();
            }
        }

        void TryGoToRoom()
        {
            if (_triggered) return;

            // Item 检查
            if (!string.IsNullOrEmpty(requireItem) && !InventorySystem.Has(requireItem))
            {
                if (!string.IsNullOrEmpty(lackItemDialogueId))
                    DialogueSystem.Show(lackItemDialogueId);
                return;
            }
            // Flag 检查
            if (!string.IsNullOrEmpty(requireFlag) && !GameState.GetFlag(requireFlag))
            {
                if (!string.IsNullOrEmpty(lackFlagDialogueId))
                    DialogueSystem.Show(lackFlagDialogueId);
                return;
            }
            // 通过 → 走过场对白(如有)然后跳场景
            _triggered = true;
            if (!string.IsNullOrEmpty(successDialogueId))
            {
                DialogueSystem.Show(successDialogueId, DoGoToRoom);
            }
            else
            {
                DoGoToRoom();
            }
        }

        void DoGoToRoom()
        {
            if (string.IsNullOrEmpty(targetRoom))
            {
                Debug.LogWarning($"[ScenePortal] {name}: targetRoom 未填,不跳场景。");
                return;
            }
            if (!string.IsNullOrEmpty(enterDirection))
            {
                SceneLoader.EnterDirection = enterDirection;
                // 2026-07-24 标记为"外部显式指定",防止 SceneLoader.Transition 里的
                //   InferEnterDirection 把 Portal 意图覆盖成默认 "right"。
                SceneLoader.EnterDirectionOverridden = true;
            }
            if (fadeTime > 0f) SceneLoader.GoToRoom(targetRoom, fadeTime);
            else SceneLoader.GoToRoom(targetRoom);
        }
    }
}
