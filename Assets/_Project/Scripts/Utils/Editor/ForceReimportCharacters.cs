// ============================================================================
//  ForceReimportCharacters.cs —— 强制重导角色立绘(Editor 菜单)
//  右键 Reimport 有时只更新 meta 不重建纹理缓存,导致改了图却看不到变化。
//  本菜单用 ForceUpdate + ForceSynchronousImport 彻底重导 Resources/Characters
//  下所有贴图,确保拿到最新 PNG + 最新导入设置。用后重新 Play。
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class ForceReimportCharacters
    {
        [MenuItem("失落的女神/强制重导角色立绘")]
        public static void Reimport()
        {
            const string dir = "Assets/_Project/Resources/Characters";
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
            int n = 0;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                AssetDatabase.ImportAsset(p,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"[强制重导] {p}");
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[强制重导] 完成,共 {n} 张角色立绘。请重新按 Play 验证。");
        }
    }
}
