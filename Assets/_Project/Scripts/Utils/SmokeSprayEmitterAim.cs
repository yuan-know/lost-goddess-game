// ============================================================================
//  SmokeSprayEmitterAim.cs —— 烟雾喷口的"可手动摆放"组件
//
//  设计:把烟雾精灵做成 SmokeEmitter 的**子物体**,并且让精灵里的"喷出源"
//  正好落在 SmokeEmitter 的原点上。于是:
//      · 拖动 SmokeEmitter   = 整体挪烟雾(喷口跟着走)
//      · 旋转 SmokeEmitter   = 绕喷口转(喷出方向变,喷口位置不跑)
//      · 改 scale            = 烟雾整体大小(仍以喷口为锚)
//
//  ⚠️ 旋转符号:精灵的喷出方向是**局部 -X**(指向 180°)。Unity 的 Z 正角是
//  逆时针,会把 180° 转到 192° —— 那是**偏下**!所以"想往左上喷"要用**负角**。
//
//  想拖?选中 SmokeEmitter(或直接点场景里的烟雾),场景视图里会出现:
//      · 三向位置手柄   —— 拖动 = 挪位置
//      · 橙色箭头+圆点  —— 拖箭头尖 = 调喷出方向
//      · Inspector scale —— 调大小
//  调完 Ctrl+S 保存场景即可。正式接入时把本组件删掉,直接用 Transform。
// ============================================================================

using UnityEngine;

namespace LostGoddess.VFX
{
    [SelectionBase]                 // 点场景里的烟雾时,优先选中 SmokeEmitter 而不是精灵
    [ExecuteAlways]
    public class SmokeSprayEmitterAim : MonoBehaviour
    {
        [Tooltip("喷出方向角(度)。0=水平向左;⚠️负=偏上,正=偏下 " +
                 "(精灵喷出方向是局部 -X=180°,Unity Z 正角是逆时针,会把它转向下)。")]
        public float angleDeg = -13f;

        [Range(0.1f, 1.5f)]
        [Tooltip("烟雾整体缩放。")]
        public float scale = 0.45f;

        [Tooltip("精灵里\"喷出源\"相对 pivot 的本地偏移(世界单位,PPU=100)。构建时写入,不用手改。")]
        public Vector2 emitLocalOffset = new Vector2(2.39f, 1.07f);

        /// <summary>当前喷出方向(世界坐标,已归一化)。</summary>
        public Vector3 SprayDir => Quaternion.Euler(0f, 0f, angleDeg) * Vector3.left;

        public Transform SmokeChild => transform.childCount > 0 ? transform.GetChild(0) : null;

        /// <summary>只摆子物体(缩放 + 让喷出源钉在轴心上)。**不动自己的旋转**,
        /// 旋转交给编辑器手柄/Inspector,免得跟用户的 E 键旋转打架。</summary>
        public void ApplyChild()
        {
            var c = SmokeChild;
            if (c == null) return;
            c.localScale = new Vector3(scale, scale, 1f);
            c.localPosition = -(Vector3)emitLocalOffset * scale;
        }

        /// <summary>把 angleDeg 应用到自己的旋转。</summary>
        public void ApplyAngle()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        /// <summary>把自己的旋转读回 angleDeg(用户用 E 键手动转了之后同步字段)。</summary>
        public void SyncAngleFromTransform()
        {
            angleDeg = transform.localEulerAngles.z;
            if (angleDeg > 180f) angleDeg -= 360f;
        }

        void OnValidate() { ApplyChild(); }
        void OnEnable()   { ApplyChild(); ApplyAngle(); }
        void Update()     { ApplyChild(); }
    }
}
