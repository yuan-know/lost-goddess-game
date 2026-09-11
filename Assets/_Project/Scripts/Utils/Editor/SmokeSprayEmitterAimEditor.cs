// ============================================================================
//  SmokeSprayEmitterAimEditor.cs —— 烟雾喷口的场景内拖拽手柄
//
//  给 SmokeSprayEmitterAim 加三样东西(都在 Scene 视图里直接拖):
//    1. 三向位置手柄   —— 拖 = 整体挪烟雾(喷口跟着走)
//    2. 橙色箭头 + 箭头尖的圆点 —— 拖圆点 = 调喷出方向(绕喷口转,喷口不跑)
//    3. 一个半透明圆环,标出喷口轴心在哪
//  大小用 Inspector 里的 scale 滑条(精确),不另加缩放手柄免得跟位置手柄叠一起。
//
//  ⚠️ 旋转符号:精灵的喷出方向是局部 -X(180°),Z 正角逆时针会把它转向下,
//  所以"偏上"= 负角。箭头拖拽时用 atan2(-y,-x) 直接把拖点换算成这个约定下的角度。
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    [CustomEditor(typeof(LostGoddess.VFX.SmokeSprayEmitterAim))]
    public class SmokeSprayEmitterAimEditor : Editor
    {
        SerializedProperty pAngle, pScale, pOffset;

        void OnEnable()
        {
            pAngle = serializedObject.FindProperty("angleDeg");
            pScale = serializedObject.FindProperty("scale");
            pOffset = serializedObject.FindProperty("emitLocalOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(pAngle);
            EditorGUILayout.PropertyField(pScale);
            EditorGUILayout.PropertyField(pOffset);
            serializedObject.ApplyModifiedProperties();

            var t = (LostGoddess.VFX.SmokeSprayEmitterAim)target;
            t.ApplyAngle();
            t.ApplyChild();

            EditorGUILayout.HelpBox(
                "场景视图里:\n" +
                "  · 三向箭头手柄 —— 拖动 = 挪烟雾\n" +
                "  · 橙色箭头尖的圆点 —— 拖 = 调喷出方向\n" +
                "  · 上面的 scale 滑条 —— 调大小\n" +
                "调完 Ctrl+S 保存场景。", MessageType.Info);
        }

        void OnSceneGUI()
        {
            var t = (LostGoddess.VFX.SmokeSprayEmitterAim)target;
            Vector3 pos = t.transform.position;
            float r = HandleUtility.GetHandleSize(pos);

            // ---- 1) 位置:拖 = 整体挪 ----
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(t.transform, "移动烟雾喷口");
                t.transform.position = newPos;
            }

            // ---- 2) 朝向:拖箭头尖的圆点 ----
            Vector3 tip = pos + t.SprayDir * (r * 1.15f);
            Handles.color = new Color(1f, 0.55f, 0.15f, 0.9f);
            Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(t.SprayDir),
                                   r * 0.75f, EventType.Repaint);

            EditorGUI.BeginChangeCheck();
            var fmh_73_58_639247186882498911 = Quaternion.identity; Vector3 newTip = Handles.FreeMoveHandle(tip,
                               r * 0.13f, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 v = newTip - pos;
                if (v.sqrMagnitude > 1e-5f)
                {
                    Undo.RecordObject(t, "调整烟雾喷出方向");
                    t.angleDeg = Mathf.Atan2(-v.y, -v.x) * Mathf.Rad2Deg;
                    t.ApplyAngle();
                }
            }

            // ---- 3) 标出喷口轴心 ----
            Handles.color = new Color(1f, 0.8f, 0.25f, 0.55f);
            Handles.DrawWireDisc(pos, Vector3.forward, r * 0.16f);
            Handles.DotHandleCap(0, pos, Quaternion.identity, r * 0.05f, EventType.Repaint);
        }
    }
}
