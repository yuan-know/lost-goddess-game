// ============================================================================
//  AIAnimPrefabBuilder.cs —— 把定稿的 AI 逐帧动画做成可被 PlayerBuilder 加载的
//  正式角色 Prefab(单 SpriteRenderer 换图 + Idle/Walk 双状态 Animator)。
//
//  与骨骼 prefab(Characters_Rigged,SpriteSkin 逐部位)的区别:
//    AI 逐帧动画每帧是一整张 260×512 精灵,只有一个 SpriteRenderer,
//    靠 AnimationClip 切换 m_Sprite 播放。结构简单、天然没有骨骼白线/权重问题。
//
//  产物:
//    Assets/_Project/Art/AIAnimations/_Controllers/{Era}_AI_Idle.anim / _Walk.anim
//    Assets/_Project/Art/AIAnimations/_Controllers/{Era}_AI.controller  (Idle/Walk + isWalking)
//    Assets/_Project/Resources/Characters_AI/{era}.prefab
//
//  菜单:失落的女神 ▸ AI 动画 ▸ 生成三形态正式 Prefab(Idle/Walk 双状态)
// ============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class AIAnimPrefabBuilder
    {
        const string AIAnimDir = "Assets/_Project/Art/AIAnimations";
        const string ControllerDir = AIAnimDir + "/_Controllers";
        const string PrefabDir = "Assets/_Project/Resources/Characters_AI";
        const string CutoutMatPath = "Assets/_Project/Art/Materials/CharacterCutout.mat";

        struct EraAnim
        {
            public string prefabName;   // young / middle / old
            public string idleDir;      // AIAnimations 下的待机帧目录
            public string walkDir;      // 行走帧目录
            public float idleFps;
            public float walkFps;
        }

        // 与 AIAnimationBuilder.FinalAnims 的定稿选择保持一致。
        static readonly EraAnim[] Eras = {
            new EraAnim { prefabName = "young",  idleDir = "YoungIdle",    walkDir = "YoungWalkV6",  idleFps = 12f, walkFps = 24f },
            new EraAnim { prefabName = "middle", idleDir = "MiddleIdle",   walkDir = "MiddleWalkV2", idleFps = 12f, walkFps = 21f },
            new EraAnim { prefabName = "old",    idleDir = "OldIdle",      walkDir = "OldWalk",      idleFps = 12f, walkFps = 24f },
        };

        [MenuItem("失落的女神/AI 动画/生成三形态正式 Prefab(Idle/Walk 双状态)", priority = 91)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("请先退出播放模式", "生成 Prefab 不能在 Play 模式下进行。", "好");
                return;
            }

            EnsureFolder(ControllerDir);
            EnsureFolder(PrefabDir);

            var cutoutMat = AssetDatabase.LoadAssetAtPath<Material>(CutoutMatPath);
            if (cutoutMat == null)
                Debug.LogWarning("[AIAnimPrefabBuilder] 找不到 CharacterCutout.mat,白边需另行处理");

            var report = new System.Text.StringBuilder();
            report.AppendLine("生成三形态逐帧 Prefab:");

            foreach (var e in Eras)
            {
                var idleSprites = LoadSprites(e.idleDir);
                var walkSprites = LoadSprites(e.walkDir);
                if (idleSprites.Count == 0 && walkSprites.Count == 0)
                {
                    report.AppendLine($"  [跳过] {e.prefabName}:{e.idleDir}/{e.walkDir} 都没有帧");
                    continue;
                }

                string idleClipPath = $"{ControllerDir}/{Cap(e.prefabName)}_AI_Idle.anim";
                string walkClipPath = $"{ControllerDir}/{Cap(e.prefabName)}_AI_Walk.anim";
                string ctrlPath = $"{ControllerDir}/{Cap(e.prefabName)}_AI.controller";

                var idleClip = BuildFrameClip(idleSprites, e.idleFps, idleClipPath);
                var walkClip = BuildFrameClip(walkSprites, e.walkFps, walkClipPath);
                var controller = BuildIdleWalkController(idleClip, walkClip, ctrlPath);

                // 组装 prefab:根 GameObject + SpriteRenderer + Animator
                var go = new GameObject(e.prefabName);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = walkSprites.Count > 0 ? walkSprites[0] :
                            (idleSprites.Count > 0 ? idleSprites[0] : null);
                // 与骨骼版一致:角色身体固定在 sortingOrder 29~41 区间(取 35)。
                // 道具按此摆放(应挡住角色的道具/前景用 50/60/70,背景用 -20/-30)。
                // 不用动态排序(骨骼版 sortingTarget 也是 null),否则会破坏既有道具遮挡关系。
                sr.sortingOrder = 35;
                if (cutoutMat != null) sr.sharedMaterial = cutoutMat;

                var anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                string prefabPath = $"{PrefabDir}/{e.prefabName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);

                report.AppendLine($"  {e.prefabName}: idle {idleSprites.Count}帧@{e.idleFps:F0}fps, " +
                                  $"walk {walkSprites.Count}帧@{e.walkFps:F0}fps → {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("三形态 Prefab 完成", report.ToString(), "好");
        }

        // ------------------------------------------------------------------
        static AnimatorController BuildIdleWalkController(AnimationClip idle, AnimationClip walk, string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("isWalking", AnimatorControllerParameterType.Bool);

            var sm = ac.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);

            var idleState = sm.AddState("Idle");
            idleState.motion = idle;
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            sm.defaultState = idleState;

            // Idle → Walk:isWalking=true
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0, "isWalking");

            // Walk → Idle:isWalking=false
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");

            EditorUtility.SetDirty(ac);
            return ac;
        }

        static AnimationClip BuildFrameClip(List<Sprite> sprites, float fps, string path)
        {
            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };
            clip.frameRate = fps;

            if (sprites.Count > 0)
            {
                float interval = 1f / fps;
                var keys = new ObjectReferenceKeyframe[sprites.Count];
                for (int i = 0; i < sprites.Count; i++)
                    keys[i] = new ObjectReferenceKeyframe { time = i * interval, value = sprites[i] };

                var binding = new EditorCurveBinding
                {
                    path = "",
                    type = typeof(SpriteRenderer),
                    propertyName = "m_Sprite"
                };
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static List<Sprite> LoadSprites(string subDir)
        {
            string folder = AIAnimDir + "/" + subDir;
            var list = new List<(string name, Sprite s)>();
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (s != null) list.Add((Path.GetFileName(p), s));
                }
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var result = new List<Sprite>();
            foreach (var x in list) result.Add(x.s);
            return result;
        }

        static string Cap(string s) => char.ToUpper(s[0]) + s.Substring(1);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
