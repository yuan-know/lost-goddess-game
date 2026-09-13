// ============================================================================
//  GearDialDebugSceneCreator.cs —— Editor 工具:一键生成齿轮旋钮谜题的调试场景
//  菜单:失落的女神 ▸ 生成齿轮旋钮调试场景
//
//  与 EyeConsoleDebugSceneCreator 同一套路:.unity 文件由 Unity 自身 API 生成
//  (不手写 YAML),打开按 Play 即可调试手感。
//  场景是纯调试用途,不进正式包;手感定稿后可连本场景一起删掉。
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class GearDialDebugSceneCreator
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/GearDialDebug.unity";
        const string GoName = "__GearDialDebug";

        [MenuItem("失落的女神/生成齿轮旋钮调试场景", priority = 4)]
        public static void CreateScene()
        {
            if (!Directory.Exists(SceneDir))
                Directory.CreateDirectory(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject(GoName);
            boot.AddComponent<GearDialDebugBootstrap>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (ok)
            {
                AddSceneToBuildSettings(ScenePath);
                Debug.Log($"[失落的女神] 齿轮旋钮调试场景已生成: {ScenePath}\n" +
                          "按 Play 后: 拖大齿轮=切指针界面，拖小旋钮=转指针；\n" +
                          "←→ 齿轮一档，↑↓ 指针微调，1~5 跳档，S 解锁当前，R 重置，T 45°目标线，[ ] 调磁吸手感。");
                EditorUtility.DisplayDialog("失落的女神",
                    $"齿轮旋钮调试场景已生成!\n\n{ScenePath}\n\n" +
                    "直接按 Play 即可试。手感参数可在左上角 HUD 里实时调。", "好的");
            }
            else
            {
                Debug.LogError("[失落的女神] 场景保存失败: " + ScenePath);
            }
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
