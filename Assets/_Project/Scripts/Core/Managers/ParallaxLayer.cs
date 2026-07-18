// ============================================================================
//  ParallaxLayer.cs —— 视差背景层
//  挂在三张背景 SpriteRenderer 上,给不同 factor:
//    远景 factor 0.20(几乎不动)/ 中景 0.50 / 前景 1.00(与相机同速)
//
//  数学:记 dx = 相机相对起点位移,则 layer.x = 层起点X + dx * factor
//         factor=0 → 层完全不动(视觉相对相机反向滚 = 极远)
//         factor=1 → 层与相机同速 → 视觉相对屏幕不动 = 极近/绑在相机上
//         中间值 = 视差层次
//  相机跟随人物,人物走过场景时:远层滚得慢=显得深、近层滚得快=显得近。
//
//  【前景 factor 建议 1.0 而非 >1】>1 会让前景反向"甩",玩家看着头晕。
//  遮挡类前景(树干挡人)常用 1.0 或 0.9-0.95(轻微视差)。
// ============================================================================
using UnityEngine;

namespace LostGoddess
{
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("视差系数:0=完全不动(极远),1=跟相机同速(极近/贴屏),中间=层次")]
        [Range(0f, 1f)] public float factor = 0.5f;

        Transform _cam;
        float _layerX0;
        float _camX0;
        bool _init;

        void OnEnable() { _init = false; }

        void LateUpdate()
        {
            if (!_init)
            {
                _cam = Camera.main != null ? Camera.main.transform : null;
                if (_cam == null) return;
                _layerX0 = transform.position.x;
                _camX0 = _cam.position.x;
                _init = true;
            }
            float dx = _cam.position.x - _camX0;
            var p = transform.position;
            p.x = _layerX0 + dx * factor;
            transform.position = p;
        }
    }
}
