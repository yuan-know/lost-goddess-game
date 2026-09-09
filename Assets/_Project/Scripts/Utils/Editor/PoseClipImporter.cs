// ============================================================================
//  PoseClipImporter.cs —— 读 tools/pose/out/clip_*.json 生成骨骼 AnimationClip
//
//  菜单:失落的女神 ▸ 动作捕捉 ▸ 导入姿态 Clip (JSON)
//
//  这是 "参考视频 → MediaPipe → 骨骼曲线" 管线的最后一步。前三步在 Python:
//      tools/pose/extract_skeleton.py   psb.meta -> 骨架 rest pose
//      tools/pose/extract_pose.py       参考视频 -> 关节世界角序列
//      tools/pose/retarget.py           世界角 -> 骨骼局部角 + 循环闭合
//  本脚本只负责把 JSON 的曲线写成 .anim,不做任何角度换算。
//
//  与 YoungAnimationGenerator 的关键区别:
//      · 关键帧密度来自真人运动数据(~12 keys/秒),不是手写的 5 帧
//      · 切线用 JSON 里的中心差分值,不是全 0 —— 骨头速度不再在每个关键帧归零
//        (那正是旧动画"每帧顿一下"的根因)
//      · 覆盖脚踝/躯干等次级运动
//
//  安全性:写入前会把已存在的同名 clip 备份成 <name>.bak.anim,手 K 过的曲线不会丢。
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class PoseClipImporter
    {
        const string JsonDir = "tools/pose/out";                     // 工程根下,不在 Assets 里
        const string AnimDir = "Assets/_Project/Art/Animations";

        [MenuItem("失落的女神/动作捕捉/导入姿态 Clip (JSON)", priority = 200)]
        public static void ImportClip()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string startDir = Path.Combine(projectRoot, JsonDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(startDir)) startDir = projectRoot;

            string json = EditorUtility.OpenFilePanel("选择 retarget.py 产出的 clip_*.json", startDir, "json");
            if (string.IsNullOrEmpty(json)) return;

            PoseClipData data;
            try
            {
                data = JsonUtility.FromJson<PoseClipData>(File.ReadAllText(json));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("解析失败", "读不动这个 JSON:\n" + e.Message, "好");
                return;
            }

            if (data == null || data.curves == null || data.curves.Length == 0)
            {
                EditorUtility.DisplayDialog("内容为空",
                    "JSON 里没有 curves。确认跑过 retarget.py 且它报告了 curves 数量。", "好");
                return;
            }
            if (string.IsNullOrEmpty(data.clipName))
            {
                EditorUtility.DisplayDialog("缺字段", "JSON 缺 clipName。", "好");
                return;
            }

            string clipPath = AnimDir + "/" + data.clipName + ".anim";

            // 手 K 过的曲线很贵,覆盖前先备份 + 明确告知
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                string backup = AnimDir + "/" + data.clipName + ".bak.anim";
                AssetDatabase.DeleteAsset(backup);
                if (!AssetDatabase.CopyAsset(clipPath, backup))
                {
                    Debug.LogWarning("[PoseClipImporter] 备份失败,已中止,未改动 " + clipPath);
                    EditorUtility.DisplayDialog("中止", "无法备份现有 clip,未做任何改动。", "好");
                    return;
                }
                if (!EditorUtility.DisplayDialog("覆盖确认",
                        data.clipName + ".anim 已存在。\n\n已备份为 " + data.clipName +
                        ".bak.anim,继续会用视频数据覆盖它的曲线。",
                        "覆盖", "取消"))
                    return;
            }

            var clip = existing;
            if (clip == null)
            {
                clip = new AnimationClip { name = data.clipName };
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                clip.ClearCurves();   // 只清曲线,保留 clip 的 GUID,Animator 引用不断
            }

            clip.frameRate = data.frameRate > 0f ? data.frameRate : 24f;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = data.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            int written = 0;
            foreach (var c in data.curves)
            {
                if (c.keys == null || c.keys.Length < 2) continue;

                var keys = new Keyframe[c.keys.Length];
                for (int i = 0; i < c.keys.Length; i++)
                {
                    var k = c.keys[i];
                    // inT/outT 是 retarget.py 算的中心差分斜率 —— 不要清零
                    keys[i] = new Keyframe(k.t, k.v, k.inT, k.outT);
                }

                var curve = new AnimationCurve(keys);
                clip.SetCurve(c.path, typeof(Transform), c.property, curve);
                written++;
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PoseClipImporter] " + clipPath);
            sb.AppendLine("  形态=" + data.form + " loop=" + data.loop +
                          " 时长=" + data.duration.ToString("0.00", CultureInfo.InvariantCulture) + "s" +
                          " 曲线=" + written + " 每曲线关键帧=" + data.keyCount);
            sb.AppendLine("  源视频: " + data.sourceVideo);
            if (data.notes != null)
                foreach (var n in data.notes) sb.AppendLine("  ! " + n);
            Debug.Log(sb.ToString());

            string noteMsg = (data.notes != null && data.notes.Length > 0)
                ? "\n\n注意:\n· " + string.Join("\n· ", data.notes)
                : "";
            EditorUtility.DisplayDialog("导入完成",
                data.clipName + " 已写入 " + written + " 条骨骼曲线。" + noteMsg +
                "\n\n下一步:在 Animation 窗口选中角色播放确认," +
                "再决定是否接进 young.controller。", "好");

            Selection.activeObject = clip;
        }

        // ---- JSON 结构(对应 retarget.py 的输出)----
        [Serializable] class PoseClipData
        {
            public string form;
            public string clipName;
            public bool loop;
            public float frameRate;
            public float duration;
            public string sourceVideo;
            public int keyCount;
            public string[] notes;
            public PoseCurve[] curves;
        }

        [Serializable] class PoseCurve
        {
            public string path;
            public string property;
            public string joint;
            public float meanVisibility;
            public PoseKey[] keys;
        }

        [Serializable] class PoseKey
        {
            public float t;
            public float v;
            public float inT;
            public float outT;
        }
    }
}
#endif
