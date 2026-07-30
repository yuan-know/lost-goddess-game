// ============================================================================
//  MuralInsightController.cs —— 监听Q键控制壁画脉冲
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class MuralInsightController : MonoBehaviour
    {
        public MuralPulseEffect pulseEffect;

        void Update()
        {
            // 监听Q键（岁月洞察技能键）
            if (Input.GetKeyDown(KeyCode.Q))
            {
                Debug.Log("[MuralInsightController] 检测到Q键按下");

                if (pulseEffect != null)
                {
                    if (!pulseEffect.IsActive())
                    {
                        pulseEffect.StartPulse();
                        Debug.Log("[MuralInsightController] 启动壁画脉冲效果");
                    }
                    else
                    {
                        Debug.Log("[MuralInsightController] 脉冲效果已经在运行中");
                    }
                }
                else
                {
                    Debug.LogError("[MuralInsightController] pulseEffect 为空！");
                }
            }
        }

        void Start()
        {
            if (pulseEffect == null)
            {
                Debug.LogError("[MuralInsightController] Start: pulseEffect 未设置！");
            }
            else
            {
                Debug.Log("[MuralInsightController] Start: pulseEffect 已设置");
            }
        }
    }
}
