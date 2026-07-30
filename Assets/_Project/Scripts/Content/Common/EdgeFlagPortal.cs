// ============================================================================
//  EdgeFlagPortal.cs —— 场景边缘的旗帜指引切换场景
//   玩家走到旗帜附近 → 自动切换到目标房间,旗帜销毁。
//   带上下呼吸浮动动画,直接挂在旗帜 GameObject 上。
//   触发一次后记住 flag,再次进入场景不再生成旗帜。
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class EdgeFlagPortal : MonoBehaviour
    {
        [Tooltip("触发半径:玩家x与旗帜x差 ≤ 这个值时触发切场景")]
        public float triggerRadius = 1.2f;
        [Tooltip("目标房间")]
        public string targetRoom = Rooms.Prologue_Foyer;
        [Tooltip("进入方向(SceneLoader.EnterDirection),决定出生位置")]
        public string enterDirection = "left";
        [Tooltip("触发/持久化用的 flag 名。留空=用 (targetRoom,enterDirection) 自动生成。" +
                 "多个引导阶段共用同一条边缘时,用它区分,避免老年探索踩过一次后新阶段的旗帜被误销毁。")]
        public string triggerFlagOverride = "";

        bool _triggered;

        void Start()
        {
            // 检查是否已经触发过，如果触发过直接销毁，不再显示
            string flagName = GetTriggerFlagName();
            if (GameState.GetFlag(flagName))
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            if (_triggered) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;

            float dx = Mathf.Abs(pc.transform.position.x - transform.position.x);
            if (dx <= triggerRadius)
            {
                _triggered = true;
                // 记住触发状态，下次进入场景不再显示
                string flagName = GetTriggerFlagName();
                GameState.SetFlag(flagName, true);
                SceneLoader.EnterDirection = enterDirection;
                SceneLoader.EnterDirectionOverridden = true;   // 2026-07-24 见 SceneLoader.EnterDirectionOverridden 注释
                SceneLoader.GoToRoom(targetRoom);
                Destroy(gameObject);
            }
        }

        string GetTriggerFlagName()
        {
            if (!string.IsNullOrEmpty(triggerFlagOverride)) return triggerFlagOverride;
            // 用目标房间和位置生成唯一的 flag 名称
            return $"edge_flag_triggered_{targetRoom}_{enterDirection}";
        }
    }
}
