using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    /// <summary>
    /// 一次性把 old.controller 的参数从 Speed(float) 改成 isWalking(bool),
    /// 让它对得上 PlayerController.walkParam ("isWalking", SetBool).
    /// 过渡条件同步:Idle→Walk = isWalking==true, Walk→Idle = isWalking==false.
    /// </summary>
    public static class RewriteOldControllerToBool
    {
        [MenuItem("失落的女神/把 old Controller 改成 isWalking(bool)")]
        public static void Rewrite()
        {
            const string path = "Assets/_Project/Art/Animations/old.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null)
            {
                EditorUtility.DisplayDialog("找不到", path, "好");
                return;
            }

            // 1. 参数:移除 Speed,添加 isWalking(如已存在则跳过)
            for (int i = ctrl.parameters.Length - 1; i >= 0; i--)
            {
                if (ctrl.parameters[i].name == "Speed")
                    ctrl.RemoveParameter(i);
            }
            bool hasIsWalking = false;
            foreach (var p in ctrl.parameters)
                if (p.name == "isWalking") { hasIsWalking = true; break; }
            if (!hasIsWalking)
                ctrl.AddParameter("isWalking", AnimatorControllerParameterType.Bool);

            // 2. 遍历所有 state 的 transition,重写条件
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states)
            {
                var stateName = cs.state.name;
                foreach (var tr in cs.state.transitions)
                {
                    // 清掉旧条件
                    while (tr.conditions.Length > 0) tr.RemoveCondition(tr.conditions[0]);

                    // Idle→Walk => isWalking==true
                    // Walk→Idle => isWalking==false
                    if (stateName == "Idle" && tr.destinationState != null && tr.destinationState.name == "Walk")
                        tr.AddCondition(AnimatorConditionMode.If, 0f, "isWalking");
                    else if (stateName == "Walk" && tr.destinationState != null && tr.destinationState.name == "Idle")
                        tr.AddCondition(AnimatorConditionMode.IfNot, 0f, "isWalking");
                }
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                "old.controller 参数已改为 isWalking(bool)。\n" +
                "Idle→Walk 条件:isWalking = true\n" +
                "Walk→Idle 条件:isWalking = false\n\n" +
                "PlayerController.walkParam='isWalking' 已经匹配,可以进沙盒测试了。",
                "好");
            Selection.activeObject = ctrl;
        }
    }
}
