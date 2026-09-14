// ============================================================================
//  InventoryDebugSceneCreator.cs —— Editor 工具:一键生成背包 UI 调试场景
//  菜单:失落的女神 ▸ 生成背包 UI 调试场景
//
//  与 EyeCorridorLaserDebugSceneCreator 同一套路:.unity 由 Unity 自身 API 生成。
//  纯调试用途,不进正式包(定稿后可连场景一起删)。
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class InventoryDebugSceneCreator
    {
        const string SceneDir  = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/InventoryDebug.unity";
        const string GoName    = "__InventoryDebug";

        [MenuItem("失落的女神/生成背包 UI 调试场景", priority = 6)]
        public static void CreateScene()
        {
            if (!Directory.Exists(SceneDir))
                Directory.CreateDirectory(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject(GoName);
            boot.AddComponent<InventoryDebugBootstrap>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (ok)
            {
                AddSceneToBuildSettings(ScenePath);
                Debug.Log($"[失落的女神] 背包 UI 调试场景已生成: {ScenePath}\n" +
                          "按 Play:Tab 选格 / 方向键微调 / [ ] 单件缩放 / - = 统一大小 / , . 面板大小 / " +
                          "; ' 遮罩 / T 格子框 / A 追加未实装道具 / B 遮罩开关 / C 底色 / P 面板开关 / L 打印参数 / R 复位。");
                EditorUtility.DisplayDialog("失落的女神",
                    $"背包 UI 调试场景已生成!\n\n{ScenePath}\n\n" +
                    "直接按 Play 即可试。左上角 HUD 有全部实时按键说明。", "好的");
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
