// ============================================================================
//  FixIconImportSettings.cs —— 一键修复 magnifying_glass.png 和 triangle_flag.png 的导入设置
//   运行一次就够了，之后可以删除本文件。
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class FixIconImportSettings
    {
        [MenuItem("Tools/Fix Icon Import Settings")]
        public static void Fix()
        {
            string[] paths = new string[] {
                "Assets/_Project/Resources/UI/Icons/magnifying_glass.png",
                "Assets/_Project/Resources/UI/Icons/triangle_flag.png",
            };

            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"找不到文件: {path}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 100;
                importer.SaveAndReimport();
                Debug.Log($"已修复导入设置: {path}");
            }

            Debug.Log("完成！所有图标现在都是 Sprite 类型，可以正常显示了。");
        }
    }
}
#endif
