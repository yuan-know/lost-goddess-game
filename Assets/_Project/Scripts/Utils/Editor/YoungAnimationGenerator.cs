// ============================================================================
//  YoungAnimationGenerator.cs —— 基于 Rest Pose 一键生成青年 Idle/Walk 动画
//
//  用途:避免手工在 Animation 窗口逐骨头 K 帧的重复劳动,先按当前绑定姿势生成
//        第一版可播放动画,美术再在 Animation 窗口里微调即可。
//
//  产物:
//        · Assets/_Project/Art/Animations/young_Idle.anim  (1 秒呼吸,loop)
//        · Assets/_Project/Art/Animations/young_Walk.anim  (0.6 秒步行,loop)
//
//  运行方式:Unity 菜单 `失落的女神/生成青年Idle/Walk动画(基于RestPose)`
//  注意:会覆盖同名 clip 的曲线(如果之前已经手 K 过,请先备份)。
// ============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class YoungAnimationGenerator
    {
        const string PrefabPath = "Assets/_Project/Resources/Characters_Rigged/young.prefab";
        const string IdlePath   = "Assets/_Project/Art/Animations/young_Idle.anim";
        const string WalkPath   = "Assets/_Project/Art/Animations/young_Walk.anim";

        // 青年骨骼层级路径(9 根骨头;bone_1 同时承担根/头颈,无单独 head/feet)
        static readonly string[] BonePaths = new[]
        {
            "bone_1",
            "bone_1/bone_2",
            "bone_1/bone_2/bone_3",
            "bone_1/bone_4",
            "bone_1/bone_4/bone_5",
            "bone_1/bone_6",
            "bone_1/bone_6/bone_7",
            "bone_1/bone_8",
            "bone_1/bone_8/bone_9",
        };

        // 四肢摆动倍率(Walk 用;Idle 保持原幅度)
        const float ArmSwingScale = 0.4f; // 双臂 bone_2/3/4/5,当前手臂晃得太厉害,先压到 0.4
        const float LegSwingScale = 1.0f; // 双腿 bone_6/7/8/9,当前已 OK,不额外压缩

        [MenuItem("失落的女神/生成青年Idle/Walk动画(基于RestPose)")]
        public static void Generate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[YoungAnimationGenerator] 找不到 " + PrefabPath);
                return;
            }

            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            instance.hideFlags = HideFlags.HideAndDontSave;

            var restPose = new Dictionary<string, Vector3>();
            foreach (var path in BonePaths)
            {
                var t = FindBone(instance.transform, path);
                if (t == null)
                {
                    Debug.LogWarning("[YoungAnimationGenerator] 找不到骨骼: " + path);
                    continue;
                }
                restPose[path] = t.localEulerAngles;
            }

            Object.DestroyImmediate(instance);

            if (restPose.Count == 0)
            {
                Debug.LogError("[YoungAnimationGenerator] 未采样到任何骨骼,请检查 young.prefab 是否包含 bone_* 层级。");
                return;
            }

            var idleClip = CreateOrReplaceClip(IdlePath, loop: true);
            var walkClip = CreateOrReplaceClip(WalkPath, loop: true);

            BuildIdle(idleClip, restPose);
            BuildWalk(walkClip, restPose);

            EditorUtility.SetDirty(idleClip);
            EditorUtility.SetDirty(walkClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[YoungAnimationGenerator] 完成:\n" +
                      "  Idle: " + IdlePath + "\n" +
                      "  Walk: " + WalkPath + "\n" +
                      "请在 Animation 窗口选中青年角色进行微调。");
            EditorUtility.DisplayDialog("完成", "青年 Idle/Walk 动画已生成\n" +
                "· Idle 为 1 秒呼吸微动\n" +
                "· Walk 为 0.6 秒循环步态(青年更轻快)\n\n" +
                "建议下一步:Play Sandbox 按 1 切青年验证,然后在 Animation 窗口微调。", "好");
        }

        static Transform FindBone(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
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
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            return clip;
        }

        // ------------------------------------------------------------------
        // Idle: 1 秒,轻微呼吸
        // ------------------------------------------------------------------
        static void BuildIdle(AnimationClip clip, Dictionary<string, Vector3> rest)
        {
            float[] times = { 0f, 0.5f, 1f };

            // 根骨骼(兼头颈):轻微上下 + 轻微摆动
            SetEulerCurves(clip, "bone_1", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 0.6f), Vector3.zero },
                new Vector3?[] { Vector3.zero, new Vector3(0f, -0.004f, 0f), Vector3.zero });

            // 双臂:轻微张开
            SetEulerCurves(clip, "bone_1/bone_2", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 1.2f), Vector3.zero });
            SetEulerCurves(clip, "bone_1/bone_4", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -1.2f), Vector3.zero });

            // 双小臂:轻微下垂
            SetEulerCurves(clip, "bone_1/bone_2/bone_3", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -1f), Vector3.zero });
            SetEulerCurves(clip, "bone_1/bone_4/bone_5", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.zero });
        }

        // ------------------------------------------------------------------
        // Walk: 0.6 秒循环,青年更轻快
        // 最后一帧(t=0.6)与第一帧(t=0)数值相同,保证循环衔接
        // ------------------------------------------------------------------
        static void BuildWalk(AnimationClip clip, Dictionary<string, Vector3> rest)
        {
            float[] times = { 0f, 0.15f, 0.3f, 0.45f, 0.6f };

            // 根骨骼:轻快起伏 + 扭动(保持原幅度);最后一帧还原到 t=0
            SetEulerCurves(clip, "bone_1", rest, times,
                new[] { Vector3.zero, new Vector3(0f, 0f, -1.2f), Vector3.zero, new Vector3(0f, 0f, 1.2f), Vector3.zero },
                new Vector3?[] { Vector3.zero, new Vector3(0f, -0.006f, 0f), Vector3.zero, new Vector3(0f, -0.006f, 0f), Vector3.zero });

            // 左腿 (bone_6 大腿, bone_7 小腿)
            SetEulerCurves(clip, "bone_1/bone_6", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_6/bone_7", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,6f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,6f) }, LegSwingScale));

            // 右腿 (bone_8 大腿, bone_9 小腿) —— 与左腿相位差半个周期
            SetEulerCurves(clip, "bone_1/bone_8", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f), new Vector3(0f,0f,-10f), new Vector3(0f,0f,10f) }, LegSwingScale));
            SetEulerCurves(clip, "bone_1/bone_8/bone_9", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-12f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-12f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-12f) }, LegSwingScale));

            // 左臂 (bone_2 大臂, bone_3 小臂) —— 与左腿反向摆动
            SetEulerCurves(clip, "bone_1/bone_2", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,14f), new Vector3(0f,0f,-14f), new Vector3(0f,0f,14f), new Vector3(0f,0f,-14f), new Vector3(0f,0f,14f) }, ArmSwingScale));
            SetEulerCurves(clip, "bone_1/bone_2/bone_3", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-6f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-6f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-6f) }, ArmSwingScale));

            // 右臂 (bone_4 大臂, bone_5 小臂) —— 与右腿反向摆动
            SetEulerCurves(clip, "bone_1/bone_4", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,-14f), new Vector3(0f,0f,14f), new Vector3(0f,0f,-14f), new Vector3(0f,0f,14f), new Vector3(0f,0f,-14f) }, ArmSwingScale));
            SetEulerCurves(clip, "bone_1/bone_4/bone_5", rest, times,
                ScaleOffsets(new[] { new Vector3(0f,0f,6f), new Vector3(0f,0f,-6f), new Vector3(0f,0f,6f), new Vector3(0f,0f,-6f), new Vector3(0f,0f,6f) }, ArmSwingScale));
        }

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

            if (eulerOffsets != null)
            {
                SetFloatCurve(clip, path, "localEulerAnglesRaw.x", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.x + o.x)).ToArray());
                SetFloatCurve(clip, path, "localEulerAnglesRaw.y", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.y + o.y)).ToArray());
                SetFloatCurve(clip, path, "localEulerAnglesRaw.z", times,
                    eulerOffsets.Select(o => NormalizeAngle(restEuler.z + o.z)).ToArray());
            }

            if (posOffsets != null)
            {
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
                Debug.LogError($"[YoungAnimationGenerator] 时间/数值长度不一致: {path}/{propertyName}");
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
