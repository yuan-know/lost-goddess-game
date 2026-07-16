// ============================================================================
//  SandboxSceneCreator.cs —— Editor 工具:一键生成验证场景(菜单)  🟢
//  菜单:失落的女神 ▸ 生成验证沙盒场景
//  作用:用 Unity 自身 API 创建一个新场景,放入一个挂 SandboxBootstrap 的空物体,
//        保存为 Assets/_Project/Scenes/Sandbox.unity,并加入 Build Settings。
//  这样 .unity 文件由 Unity 生成(不手写 YAML),打开按 Play 即可跑通全链路。
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

namespace LostGoddess.EditorTools
{
    public static class SandboxSceneCreator
    {
        const string SceneDir  = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/Sandbox.unity";

        [MenuItem("失落的女神/生成验证沙盒场景", priority = 0)]
        public static void CreateSandboxScene()
        {
            if (!Directory.Exists(SceneDir))
                Directory.CreateDirectory(SceneDir);

            // 新建空场景(不含默认相机/光,由 Bootstrap 程序化生成)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject("__Bootstrap");
            boot.AddComponent<SandboxBootstrap>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (ok)
            {
                AddSceneToBuildSettings(ScenePath);
                Debug.Log($"[失落的女神] 验证沙盒场景已生成: {ScenePath}\n" +
                          "打开它并按 Play,即可验证:点空地→老人走 / 点提灯→走近捡起 / 点门→走近开门 / F5存 F9读。");
                EditorUtility.DisplayDialog("失落的女神",
                    "验证沙盒场景已生成!\n\n" + ScenePath +
                    "\n\n已打开该场景,直接按 Play 即可跑通系统层验证。", "好的");
            }
            else
            {
                Debug.LogError("[失落的女神] 场景保存失败。");
            }
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return; // 已存在
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("失落的女神/删除存档(persistentDataPath)", priority = 20)]
        public static void DeleteSave()
        {
            LostGoddess.SaveSystem.DeleteSave();
            Debug.Log("[失落的女神] 存档已删除。");
        }
    }
}
#endif
