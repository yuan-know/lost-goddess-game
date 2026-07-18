// ============================================================================
//  CameraFollow.cs —— 相机水平跟随老人(只跟 X)
//  烟火式横版:老人在地平线上左右走,相机跟着 X 平移,但 Y/Z 固定,可视范围锁定
//  Y 上下不变。X 被 clamp 在 [minX, maxX] 内(通常= WalkableArea 边界),防超出背景
//  两端露出裁剪外的黑边。
//
//  用法:挂到 Main Camera 上,或由 SandboxBootstrap/房间构建器代码 AddComponent。
//  target 未设时自动找 Tag=Player 或名叫 "Player" 的 GameObject。
// ============================================================================
using UnityEngine;

namespace LostGoddess
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [Tooltip("相机 X 的可移动区间(通常按背景宽度算)")]
        public float minX = -10f;
        public float maxX = 10f;
        [Tooltip("平滑跟随时间(0=瞬移)")]
        public float smoothTime = 0.15f;
        [Tooltip("相机固定 Y(通常 0);相机高度不随人变")]
        public float fixedY = 0f;

        float _vx;

        void LateUpdate()
        {
            if (target == null)
            {
                var g = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                if (g == null) return;
                target = g.transform;
            }
            float wantX = Mathf.Clamp(target.position.x, minX, maxX);
            var p = transform.position;
            p.x = (smoothTime > 0f)
                ? Mathf.SmoothDamp(p.x, wantX, ref _vx, smoothTime)
                : wantX;
            p.y = fixedY;
            transform.position = p;
        }
    }
}
