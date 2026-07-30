// ============================================================================
//  FloatingIndicator.cs —— 指引图标(旗帜/放大镜)的上下呼吸浮动动画
//   挂在任何 GameObject 上,自动执行:pos.y += amplitude * sin(time * speed)
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    public class FloatingIndicator : MonoBehaviour
    {
        [Tooltip("浮动振幅(世界单位,推荐 0.15~0.25)")]
        public float amplitude = 0.2f;
        [Tooltip("浮动速度(推荐 2~3,越大越快)")]
        public float speed = 2.5f;

        float _phase;
        float _baseY;

        void Start()
        {
            _baseY = transform.position.y;
        }

        void Update()
        {
            _phase += Time.deltaTime * speed;
            float offset = amplitude * Mathf.Sin(_phase);
            Vector3 p = transform.position;
            p.y = _baseY + offset;
            transform.position = p;
        }
    }
}
