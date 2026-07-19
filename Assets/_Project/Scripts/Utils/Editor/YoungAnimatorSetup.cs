// ============================================================================
//  YoungAnimatorSetup.cs —— 青年形态 Animator Controller 脚手架生成(Editor)
//
//  用途:一键生成青年形态的动画状态机,与 PlayerController 的接口对齐:
//        · 参数:isWalking (bool)   —— PlayerController.SetMoving 会 SetBool 它
//        · 状态:Idle(默认) / Walk
//        · 过渡:Idle → Walk (isWalking == true)
//                Walk → Idle (isWalking == false)
//        · 附带生成两个占位 AnimationClip(young_Idle / young_Walk)并挂进对应状态,
//          Walk 设为 loop。美术在 Animation 窗口对这两个 clip 摆关键帧即可,
//          无需再手动连状态机。
//
//  产物目录:Assets/_Project/Art/Animations/
//        · young.controller
//        · young_Idle.anim(空,待摆帧)
//        · young_Walk.anim(空,loop,待摆帧)
//        (与 old/middle controller/clip 平级并存)
//
//  接线(手工,见指南):把青年角色 GameObject 的 Animator.controller 指向 young.controller,
//        PlayerController.animator 指向同一个 Animator。walkParam 保持默认 "isWalking"。
//
//  可重复执行:已存在则覆盖 Controller 的参数/状态(clip 若已摆帧不会被清空,只在缺失时新建)。
//
//  此脚本照 MiddleAnimatorSetup.cs 复制修改,仅改 Dir/文件名/菜单名/dialog 文本。
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class YoungAnimatorSetup
    {
        const string Dir = "Assets/_Project/Art/Animations";
        const string ControllerPath = Dir + "/young.controller";
        const string IdleClipPath = Dir + "/young_Idle.anim";
        const string WalkClipPath = Dir + "/young_Walk.anim";
        const string WalkParam = "isWalking"; // 必须与 PlayerController.walkParam 一致

        [MenuItem("失落的女神/生成青年Animator脚手架")]
        public static void Generate()
        {
            EnsureDir(Dir);

            // ── 1) 占位 clip(不存在才建,避免覆盖美术已摆的帧)──
            var idleClip = GetOrCreateClip(IdleClipPath, loop: false);
            var walkClip = GetOrCreateClip(WalkClipPath, loop: true);

            // ── 2) Controller(重建状态机,保证结构正确)──
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // 参数:isWalking(先清可能重复的再加)
            RemoveParamIfExists(controller, WalkParam);
            controller.AddParameter(WalkParam, AnimatorControllerParameterType.Bool);

            // 状态机:清空默认层的 states 重建
            var sm = controller.layers[0].stateMachine;
            foreach (var cs in sm.states) sm.RemoveState(cs.state);

            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            var walkState = sm.AddState("Walk");
            walkState.motion = walkClip;

            sm.defaultState = idleState;

            // 过渡 Idle → Walk (isWalking == true)
            var toWalk = idleState.AddTransition(walkState);
            toWalk.AddCondition(AnimatorConditionMode.If, 0, WalkParam);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.05f;

            // 过渡 Walk → Idle (isWalking == false)
            var toIdle = walkState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, WalkParam);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.05f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 自动把 young.controller 挂到 young.prefab 的 Animator 上
            int linked = LinkControllerToPrefab(controller);

            Debug.Log("[YoungAnimatorSetup] 已生成 " + ControllerPath +
                      "\n参数 isWalking + 状态 Idle/Walk + 双向过渡。占位 clip:young_Idle / young_Walk(待摆帧)。" +
                      (linked > 0 ? "\n已自动挂到 young.prefab 的 Animator。" : ""));
            EditorUtility.DisplayDialog("完成",
                "青年 Animator 脚手架已生成:\n" + ControllerPath +
                "\n\n· 参数 isWalking(bool),PlayerController 会自动驱动它\n" +
                "· 状态 Idle(默认)/ Walk,已连好双向过渡\n" +
                "· 占位 clip young_Idle / young_Walk —— 可用 Animation 窗口摆关键帧,或运行 `生成青年Idle/Walk动画` 菜单自动生成第一版\n\n" +
                (linked > 0 ? "· 已自动把 young.controller 挂到 young.prefab\n" : "· 请手动把 young.controller 拖到 young.prefab 的 Animator 上\n") +
                "PlayerController.animator 指向该 Animator。", "好");

            // 选中 controller 方便查看
            Selection.activeObject = controller;
        }

        static AnimationClip GetOrCreateClip(string path, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;

            clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
            if (loop)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void RemoveParamIfExists(AnimatorController c, string name)
        {
            var ps = c.parameters;
            for (int i = ps.Length - 1; i >= 0; i--)
                if (ps[i].name == name) c.RemoveParameter(ps[i]);
        }

        static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            // 逐级创建
            var parts = dir.Split('/');
            string cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static int LinkControllerToPrefab(AnimatorController controller)
        {
            const string prefabPath = "Assets/_Project/Resources/Characters_Rigged/young.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return 0;

            var anim = root.GetComponent<Animator>();
            int linked = 0;
            if (anim != null && anim.runtimeAnimatorController != controller)
            {
                anim.runtimeAnimatorController = controller;
                linked = 1;
            }

            if (linked > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            PrefabUtility.UnloadPrefabContents(root);
            return linked;
        }
    }
}
