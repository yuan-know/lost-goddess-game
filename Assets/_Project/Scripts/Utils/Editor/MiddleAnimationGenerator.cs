// ============================================================================
//  MiddleAnimationGenerator.cs —— 基于 Rest Pose 一键生成中年 Idle/Walk 动画
//
//  用途:避免手工在 Animation 窗口逐骨头 K 帧的重复劳动,先按当前绑定姿势生成
//        第一版可播放动画,美术再在 Animation 窗口里微调即可。
//
//  产物:
//        · Assets/_Project/Art/Animations/middle_Idle.anim  (1 秒呼吸,不循环)
//        · Assets/_Project/Art/Animations/middle_Walk.anim  (0.75 秒步行,循环)
//
//  运行方式:Unity 菜单 `失落的女神/生成中年Idle/Walk动画(基于RestPose)`
//  注意:会覆盖同名 clip 的曲线(如果之前已经手 K 过,请先备份)。
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class MiddleAnimationGenerator
    {
        const string PrefabPath = "Assets/_Project/Resources/Characters_Rigged/middle.prefab";
        const string IdlePath   = "Assets/_Project/Art/Animations/middle_Idle.anim";
        const string WalkPath   = "Assets/_Project/Art/Animations/middle_Walk.anim";

        // 中年骨骼层级路径(从根骨骼 bone_1 开始,与 old 类似)
        static readonly string[] BonePaths = new[]
        {
            "bone_1",
            "bone_1/bone_4",
            "bone_1/bone_4/bone_5",
            "bone_1/bone_6",
            "bone_1/bone_6/bone_9",
            "bone_1/bone_6/bone_9/bone_15",
            "bone_1/bone_7",
            "bone_1/bone_7/bone_8",
            "bone_1/bone_7/bone_8/bone_16",
            "bone_1/bone_10",
            "bone_1/bone_10/bone_12",
            "bone_1/bone_13",
            "bone_1/bone_13/bone_14",
        };

        // 四肢摆动倍率(Walk 用;Idle 保持原幅度)
        const float ArmSwingScale = 0.5f; // 双臂 bone_4/5/13/14,中年比青年稍稳重,用 0.5
        const float LegSwingScale = 1.0f; // 双腿 bone_6/7/8/9/15/16,当前幅度不变

        [MenuItem("失落的女神/生成中年Idle/Walk动画(基于RestPose)")]
        public static void Generate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[MiddleAnimationGenerator] 找不到 " + PrefabPath);
                return;
            }

            // 实例化以采样 Rest Pose
            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.hideFlags = HideFlags.HideAndDontSave;

            var restPose = new Dictionary<string, Vector3>();
            foreach (var path in BonePaths)
            {
                var t = FindBone(instance.transform, path);
                if (t == null)
                {
                    Debug.LogWarning("[MiddleAnimationGenerator] 找不到骨骼: " + path);
                    continue;
                }
                restPose[path] = t.localEulerAngles;
            }

            Object.DestroyImmediate(instance);

            if (restPose.Count == 0)
            {
                Debug.LogError("[MiddleAnimationGenerator] 未采样到任何骨骼,请检查 middle.prefab 是否包含 bone_* 层级。");
                return;
            }

            // 生成 / 覆盖 clip(Idle 也设 loop,保持呼吸连续)
            var idleClip = CreateOrReplaceClip(IdlePath, loop: true);
            var walkClip = CreateOrReplaceClip(WalkPath, loop: true);

            BuildIdle(idleClip, restPose);
            BuildWalk(walkClip, restPose);

            EditorUtility.SetDirty(idleClip);
            EditorUtility.SetDirty(walkClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MiddleAnimationGenerator] 完成:\n" +
                      "  Idle: " + IdlePath + "\n" +
                      "  Walk: " + WalkPath + "\n" +
                      "请在 Animation 窗口选中 middle 角色进行微调。");
            EditorUtility.DisplayDialog("完成", "中年 Idle/Walk 动画已生成\n" +
                "· Idle 为 1 秒呼吸微动\n" +
                "· Walk 为 0.75 秒循环步态\n\n" +
                "建议下一步:在 Animation 窗口打开 clip 检查/微调,然后 Play Sandbox 验证。", "好");
        }

        static Transform FindBone(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            // 根节点路径直接按名查找
            var parts = path.Split('/');
            var cur = root.Find(parts[0]);
            if (cur == null) return null;
            for (int i = 1; i < parts.Length; i++)
            {
                cur = cur.Find(parts[i]);
                if (cur == null) return null;
            }
            return cur;
        }

        static AnimationClip CreateOrReplaceClip(string path, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                // 清空旧曲线,避免残留
                AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
                {
                    loopTime = loop,
                    loopBlend = false,
                });
                // 无法直接清空所有 curve,下面会覆盖用到的路径;未用到的旧曲线影响不大
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        // ------------------------------------------------------------------
        // Idle: 1 秒,轻微呼吸
        // ------------------------------------------------------------------
        static void BuildIdle(AnimationClip clip, Dictionary<string, Vector3> rest)
        {
            float[] times = { 0f, 0.5f, 1f };

            // 根骨骼:轻微上下浮动 + 轻微左右摆动
            SetEulerCurves(clip, "bone_1", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 0.5f), Vector3.zero },
                new Vector3?[] { Vector3.zero, new Vector3(0f, -0.003f, 0f), Vector3.zero });

            // 脖子/头:轻微点头
            SetEulerCurves(clip, "bone_1/bone_10", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 0.8f), Vector3.zero });
            SetEulerCurves(clip, "bone_1/bone_10/bone_12", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 1.2f), Vector3.zero });

            // 上臂:轻微张开
            SetEulerCurves(clip, "bone_1/bone_4", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.zero });
            SetEulerCurves(clip, "bone_1/bone_13", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.zero });

            // 小臂/手:轻微下垂摆动
            SetEulerCurves(clip, "bone_1/bone_4/bone_5", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -0.8f), Vector3.zero });
            SetEulerCurves(clip, "bone_1/bone_13/bone_14", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 0.8f), Vector3.zero });
        }

        // ------------------------------------------------------------------
        // Walk: 0.75 秒循环
        // 最后一帧(t=0.75)与第一帧(t=0)数值相同,保证循环衔接
        // ------------------------------------------------------------------
        static void BuildWalk(AnimationClip clip, Dictionary<string, Vector3> rest)
        {
            float[] times = { 0f, 0.1875f, 0.375f, 0.5625f, 0.75f };

            // 根骨骼:身体随步伐上下起伏 + 轻微扭动(保持原幅度);最后一帧还原到 t=0
            SetEulerCurves(clip, "bone_1", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.zero },
                new Vector3?[] { Vector3.zero, new Vector3(0f, -0.008f, 0f), Vector3.zero, new Vector3(0f, -0.008f, 0f), Vector3.zero });

            // 左腿 (bone_6 大腿, bone_9 小腿, bone_15 脚)
            SetEulerCurves(clip, "bone_1/bone_6", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-12f), new Vector3(0f,0f,12f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,12f), new Vector3(0f,0f,-12f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_6/bone_9", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,8f), new Vector3(0f,0f,-15f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-15f), new Vector3(0f,0f,8f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_6/bone_9/bone_15", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-5f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-5f) }, LegSwingScale));

            // 右腿 (bone_7 大腿, bone_8 小腿, bone_16 脚) —— 与左腿相位差半个周期
            SetEulerCurves(clip, "bone_1/bone_7", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,12f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,12f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,12f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_7/bone_8", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-15f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-15f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-15f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_7/bone_8/bone_16", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,8f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,8f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,8f) }, LegSwingScale));

            // 左臂 (bone_13 大臂, bone_14 小臂) —— 与左腿反向摆动
            SetEulerCurves(clip, "bone_1/bone_13", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f) }, ArmSwingScale));
            SetEulerCurves(clip, "bone_1/bone_13/bone_14", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-5f), new Vector3(0f,0f,5f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,5f), new Vector3(0f,0f,-5f) }, ArmSwingScale));

            // 右臂 (bone_4 大臂, bone_5 小臂) —— 与右腿反向摆动
            SetEulerCurves(clip, "bone_1/bone_4", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f) }, ArmSwingScale));
            SetEulerCurves(clip, "bone_1/bone_4/bone_5", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,5f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,5f), new Vector3(0f,0f,-5f), new Vector3(0f,0f,5f) }, ArmSwingScale));
        }

        // ------------------------------------------------------------------
        // 工具:为某骨骼写 Euler 旋转曲线(可选同时写位置曲线)
        // ------------------------------------------------------------------
        static void SetEulerCurves(AnimationClip clip, string path,
            Dictionary<string, Vector3> rest, float[] times,
            Vector3[] eulerOffsets)
        {
            SetEulerCurves(clip, path, rest, times, eulerOffsets, null);
        }

        static void SetEulerCurves(AnimationClip clip, string path,
            Dictionary<string, Vector3> rest, float[] times,
            Vector3[] eulerOffsets, Vector3?[] posOffsets)
        {
            if (!rest.TryGetValue(path, out var restEuler)) return;

            // 旋转
            if (eulerOffsets != null)
            {
                SetFloatCurve(clip, path, "localEulerAnglesRaw.x", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.x + o.x)).ToArray());
                SetFloatCurve(clip, path, "localEulerAnglesRaw.y", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.y + o.y)).ToArray());
                SetFloatCurve(clip, path, "localEulerAnglesRaw.z", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.z + o.z)).ToArray());
            }

            // 位置(仅当提供了位置偏移时)
            if (posOffsets != null)
            {
                // 需要先采样当前位置,这里暂时只在根骨骼使用,且 prefab 根在 (0,0,0)
                var restPos = Vector3.zero;
                SetFloatCurve(clip, path, "m_LocalPosition.x", times,
                    posOffsets.Select(o => restPos.x + (o?.x ?? 0f)).ToArray());
                SetFloatCurve(clip, path, "m_LocalPosition.y", times,
                    posOffsets.Select(o => restPos.y + (o?.y ?? 0f)).ToArray());
                SetFloatCurve(clip, path, "m_LocalPosition.z", times,
                    posOffsets.Select(o => restPos.z + (o?.z ?? 0f)).ToArray());
            }
        }

        static Vector3[] ScaleOffsets(Vector3[] offsets, float scale)
        {
            if (offsets == null) return null;
            var result = new Vector3[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                result[i] = offsets[i] * scale;
            return result;
        }

        static float[] UnwrapAngles(float[] values)
        {
            if (values == null || values.Length < 2) return values;
            var result = new float[values.Length];
            result[0] = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                float v = values[i];
                float diff = v - result[i - 1];
                while (diff > 180f)  { v -= 360f; diff -= 360f; }
                while (diff < -180f) { v += 360f; diff += 360f; }
                result[i] = v;
            }
            return result;
        }

        static void SetFloatCurve(AnimationClip clip, string path, string propertyName, float[] times, float[] values)
        {
            if (times.Length != values.Length)
            {
                Debug.LogError($"[MiddleAnimationGenerator] 时间/数值长度不一致: {path}/{propertyName}");
                return;
            }

            // Euler 角度需要 unwrap，防止 ±180° 边界导致插值走大圈
            if (propertyName.StartsWith("localEulerAnglesRaw"))
                values = UnwrapAngles(values);

            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++)
                keys[i] = new Keyframe(times[i], values[i], 0f, 0f);

            var curve = new AnimationCurve(keys);
            clip.SetCurve(path, typeof(Transform), propertyName, curve);
        }

        static float NormalizeAngle(float a)
        {
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }
    }
}
