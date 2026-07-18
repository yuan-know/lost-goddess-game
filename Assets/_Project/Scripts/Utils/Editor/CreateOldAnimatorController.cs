using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    /// <summary>
    /// 为老年角色新建 Animator Controller,内含 Idle / Walk 空 clip 和过渡。
    /// 参数:Speed (float) —— PlayerController 已经在写这个参数。
    /// Speed > 0.01 → Walk;<= 0.01 → Idle。
    /// clip 内容留空,先跑通 Controller,再进 Animation 窗口 K 关键帧。
    /// </summary>
    public static class CreateOldAnimatorController
    {
        const string ControllerPath = "Assets/_Project/Art/Animations/old.controller";
        const string IdleClipPath   = "Assets/_Project/Art/Animations/old_Idle.anim";
        const string WalkClipPath   = "Assets/_Project/Art/Animations/old_Walk.anim";

        [MenuItem("失落的女神/新建老年 Animator Controller")]
        public static void Create()
        {
            // 目录
            var dir = Path.GetDirectoryName(ControllerPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 1. 建两个空 clip
            var idleClip = CreateOrLoadClip(IdleClipPath, "Idle", loop: true);
            var walkClip = CreateOrLoadClip(WalkClipPath, "Walk", loop: true);

            // 2. 建 Controller
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller != null)
            {
                if (!EditorUtility.DisplayDialog("Controller 已存在",
                    $"{ControllerPath} 已存在,是否覆盖?", "覆盖", "取消"))
                    return;
                AssetDatabase.DeleteAsset(ControllerPath);
            }
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // 3. 参数
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            // 4. 状态
            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;
            sm.defaultState = idleState;

            // 5. 过渡: Idle -> Walk (Speed > 0.01)
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.1f;
            toWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");

            // 6. 过渡: Walk -> Idle (Speed < 0.01)
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                $"已创建:\n{ControllerPath}\n{IdleClipPath}\n{WalkClipPath}\n\n" +
                "下一步:\n" +
                "1. 选中场景里的 old 根节点\n" +
                "2. Inspector 里给 Animator 组件的 Controller 字段拖入 old.controller\n" +
                "3. Window → Animation → Animation 窗口里选 Idle,K 呼吸关键帧;选 Walk,K 走路关键帧",
                "好");

            // 高亮 Controller
            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
        }

        static AnimationClip CreateOrLoadClip(string path, string name, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;

            clip = new AnimationClip { name = name };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
    }
}
