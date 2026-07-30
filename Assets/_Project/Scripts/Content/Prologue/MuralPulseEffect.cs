// ============================================================================
//  MuralPulseEffect.cs —— 壁画图层脉冲效果
//  直接操作 SpriteRenderer 的颜色做呼吸发光效果
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public class MuralPulseEffect : MonoBehaviour
    {
        public SpriteRenderer targetRenderer;
        public float pulseCycle = 1.0f;      // 脉冲周期（秒）
        public float minBrightness = 0.5f;   // 最低亮度（变暗）
        public float maxBrightness = 2.5f;   // 最高亮度（发光）

        private bool _isActive = false;
        private float _time = 0f;

        void Update()
        {
            if (!_isActive || targetRenderer == null)
                return;

            _time += Time.deltaTime;
            float t = Mathf.PingPong(_time / pulseCycle, 1f);
            float brightness = Mathf.Lerp(minBrightness, maxBrightness, t);

            // 纯白色，通过亮度值实现发光
            targetRenderer.color = new Color(brightness, brightness, brightness, 1f);
        }

        /// <summary>启动脉冲效果</summary>
        public void StartPulse()
        {
            _isActive = true;
            _time = 0f;
            Debug.Log("[MuralPulseEffect] 启动脉冲效果 - 纯白色脉冲，亮度: 0.5 到 2.5");
        }

        /// <summary>停止脉冲效果，恢复原始颜色</summary>
        public void StopPulse()
        {
            _isActive = false;
            if (targetRenderer != null)
            {
                targetRenderer.color = Color.white;
            }
            Debug.Log("[MuralPulseEffect] 停止脉冲效果");
        }

        /// <summary>检查脉冲是否激活</summary>
        public bool IsActive()
        {
            return _isActive;
        }
    }
}
