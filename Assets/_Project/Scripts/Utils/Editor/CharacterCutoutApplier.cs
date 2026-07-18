// ============================================================================
//  CharacterCutoutApplier.cs —— 一键把角色骨骼版全身 SpriteRenderer 换成
//  LostGoddess/CharacterCutout 材质,治边缘白线。
//
//  流程:
//    菜单「失落的女神 ▸ 给骨骼角色套 Cutout 材质(治白线)」
//    → 找 Resources/Characters_Rigged/*.prefab
//    → 遍历所有 SpriteRenderer.sharedMaterial ← CharacterCutout.mat
//    → 场景里当前的 Player 若已实例化,也批量套上
//
//  【为什么不用 AssetPostprocessor】psb 每次 Reimport 都会重生成子 GameObject,
//  静态挂 Material 需要在 prefab 层持久化。本菜单幂等,psb 变动后再点一次即可。
//
//  【调阈值】选中生成的 CharacterCutout.mat → Inspector 拖 Alpha Cutoff。
//   0.5 手感软(残留少许白)/ 0.75 默认 / 0.85 更狠(可能咬进人物)。
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class CharacterCutoutApplier
    {
        const string MatPath = "Assets/_Project/Art/Materials/CharacterCutout.mat";
        const string ShaderName = "LostGoddess/CharacterCutout";
        const string RiggedDir = "Assets/_Project/Resources/Characters_Rigged";

        [MenuItem("失落的女神/给骨骼角色套 Cutout 材质(治白线)")]
        public static void Apply()
        {
            // 1. 确保 Material 存在
            var mat = EnsureMaterial();
            if (mat == null) return;

            // 2. 找所有骨骼角色 prefab
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { RiggedDir });
            int prefabCount = 0, rendererCount = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                var srs = root.GetComponentsInChildren<SpriteRenderer>(true);
                int changed = 0;
                foreach (var sr in srs)
                {
                    if (sr.sharedMaterial == mat) continue;
                    sr.sharedMaterial = mat;
                    changed++;
                }
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CutoutApplier] {path}: 换材质 {changed}/{srs.Length}");
                }
                PrefabUtility.UnloadPrefabContents(root);
                prefabCount++;
                rendererCount += changed;
            }

            // 3. 场景里当前活着的 Player 也顺手套上(方便运行时看效果不用重启)
            var player = GameObject.Find("Player");
            int liveCount = 0;
            if (player != null)
            {
                foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (sr.sharedMaterial != mat)
                    {
                        sr.sharedMaterial = mat;
                        liveCount++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                $"扫描 {prefabCount} 个骨骼 prefab,共替换 {rendererCount} 个 SpriteRenderer 材质。\n" +
                (liveCount > 0 ? $"场景里活着的 Player 又替换 {liveCount} 个。\n\n" : "\n") +
                "白线仍在?选中 Materials/CharacterCutout.mat → 把 Alpha Cutoff 从 0.75 调到 0.85 再试。",
                "好");
            Selection.activeObject = mat;
        }

        static Material EnsureMaterial()
        {
            var sh = Shader.Find(ShaderName);
            if (sh == null)
            {
                EditorUtility.DisplayDialog("找不到 Shader",
                    "找不到 shader " + ShaderName + "\n" +
                    "确认 Assets/_Project/Art/Shaders/CharacterCutout.shader 存在且已编译。",
                    "好");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MatPath));
                mat = new Material(sh);
                mat.SetFloat("_Cutoff", 0.75f);
                AssetDatabase.CreateAsset(mat, MatPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CutoutApplier] 新建材质:" + MatPath);
            }
            else if (mat.shader != sh)
            {
                mat.shader = sh;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
