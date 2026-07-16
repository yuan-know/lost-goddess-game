// ============================================================================
//  WalkableArea.cs —— 房间可走区(契约 §8)  🟢
//  【烟火式横版】老人只在一条固定的水平地平线上左右行走(Y 锁定),
//  可走范围由 [minX, maxX] 限定。点击任意处,只取 X,老人不上下移动。
//  保留契约方法签名 ClampToArea / Contains,B 无感知。
// ============================================================================

using UnityEngine;

namespace LostGoddess
{
    public class WalkableArea : MonoBehaviour
    {
        public static WalkableArea Current { get; private set; }

        [Header("烟火式横版可走线")]
        [Tooltip("地平线 Y(老人脚底固定在这个高度);默认取本物体 Y")]
        public bool useTransformY = true;
        public float groundY = -2.4f;

        [Tooltip("可走的左右边界(世界 X)")]
        public float minX = -8f;
        public float maxX = 8f;

        public float GroundY => useTransformY ? transform.position.y : groundY;

        void OnEnable()  { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        /// <summary>限制 X 在 [minX,maxX]。</summary>
        public float ClampX(float x) => Mathf.Clamp(x, minX, maxX);

        /// <summary>把任意点投影到地平线并 clamp X(Y 强制为地平线)。</summary>
        public Vector2 ClampToArea(Vector2 worldPos)
        {
            return new Vector2(ClampX(worldPos.x), GroundY);
        }

        /// <summary>是否在可走 X 范围内(Y 不参与,横版只看左右)。</summary>
        public bool Contains(Vector2 worldPos)
        {
            return worldPos.x >= minX && worldPos.x <= maxX;
        }

        // 在 Scene 视图画出地平线,方便美术/策划摆位
        void OnDrawGizmos()
        {
            float y = GroundY;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(minX, y, 0), new Vector3(maxX, y, 0));
            Gizmos.DrawWireSphere(new Vector3(minX, y, 0), 0.15f);
            Gizmos.DrawWireSphere(new Vector3(maxX, y, 0), 0.15f);
        }
    }
}
