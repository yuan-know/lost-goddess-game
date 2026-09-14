// ============================================================================
//  EyeCorridorLaserDebugSceneCreator.cs —— Editor 工具:一键生成千眼回廊躲避激光调试场景
//  菜单:失落的女神 ▸ 生成千眼回廊激光调试场景
//
//  与 GearDialDebugSceneCreator 同一套路:.unity 由 Unity 自身 API 生成(不手写 YAML),
//  打开按 Play 即可调手感。纯调试用途,不进正式包;手感定稿后可连本场景一起删。
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class EyeCorridorLaserDebugSceneCreator
    {
        const string SceneDir  = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/EyeCorridorLaserDebug.unity";
        const string GoName    = "__EyeCorridorLaserDebug";

        [MenuItem("失落的女神/生成千眼回廊激光调试场景", priority = 5)]
        public static void CreateScene()
        {
            if (!Directory.Exists(SceneDir))
                Directory.CreateDirectory(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject(GoName);
            boot.AddComponent<EyeCorridorLaserDebugBootstrap>();

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            if (ok)
            {
                AddSceneToBuildSettings(ScenePath);
                Debug.Log($"[失落的女神] 千眼回廊激光调试场景已生成: {ScenePath}\n" +
                          "按 Play:点石像=瞬移躲光;被光柱照到 3 秒内必须进阴影,否则灼烧死亡回滚。\n" +
                          "←→ 光柱速度, [ ] 危险区宽度, - = 3 秒窗口, , . 阴影宽, T 看判定区, K 强制死亡, R 重置。");
                EditorUtility.DisplayDialog("失落的女神",
                    $"千眼回廊激光调试场景已生成!\n\n{ScenePath}\n\n" +
                    "直接按 Play 即可试。所有手感参数在左上角 HUD 里有实时按键。", "好的");
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
