// ============================================================================
//  PsdCharacterImporterSetup.cs —— 分层角色 PSB 的 PSD Importer 配置(Editor)
//
//  用途:把 Assets/_Project/Art/Characters/characters_3forms.psb 配成
//        「Mosaic 分层 + Character 模式」,这样 Unity 会:
//          · 把每个部位图层拆成独立 Sprite(打成一张图集 Mosaic)
//          · 生成一个 Skeleton/PSB 结构,可在 Skinning Editor 里绑骨骼、蒙皮
//          · 保留图层的相对位置(拼成完整人形)
//        PPU/锚点与整张立绘保持一致(PPU=600,脚底居中)。
//
//  【为什么是 .psb 而不是 .psd】
//        Unity 的 2D PSD Importer 默认只自动接管 .psb 文件,普通 .psd 仍走 TextureImporter
//        (当普通贴图,无法分层/绑骨骼)。.psb 与 .psd 内部数据结构一致(PSB=大文件格式),
//        改扩展名即无损切换。故本项目角色分层图统一用 .psb。
//
//  为什么用菜单而非 AssetPostprocessor:
//        PSDImporter 的关键开关(mosaic / character)是 PSDImporter 专属类型,
//        且 CharacterSpriteImporter(TextureImporter 版)不会作用到 PSB。
//        这里用 SerializedObject 直接改导入器序列化属性 —— 不依赖 internal API,
//        跨 2022.3 小版本稳定。改完 Reimport。
//
//  使用:菜单「失落的女神 ▸ 配置角色PSD(Mosaic骨骼导入)」点一下即可。
//        之后在 Project 里展开该 psb 能看到各部位 Sprite;
//        选中 psb → Inspector「Sprite Editor」→ 右上角切到「Skinning Editor」绑骨骼。
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class PsdCharacterImporterSetup
    {
        const string PsdPath = "Assets/_Project/Art/Characters/characters_3forms.psb";
        const float CharacterPPU = 600f; // 与整张立绘一致(见 CharacterSpriteImporter)

        [MenuItem("失落的女神/配置角色PSD(Mosaic骨骼导入)")]
        public static void SetupPsd()
        {
            var importer = AssetImporter.GetAtPath(PsdPath);
            if (importer == null)
            {
                EditorUtility.DisplayDialog("找不到 PSB",
                    "没找到:\n" + PsdPath +
                    "\n\n确认 characters_3forms.psb 已在该目录,且 com.unity.2d.psdimporter 包已安装" +
                    "(Package Manager 里能看到 2D PSD Importer)。", "知道了");
                return;
            }

            // 若包未装 / 文件不是 .psb,导入器会是普通 TextureImporter 而非 PSDImporter
            string typeName = importer.GetType().Name;
            if (typeName != "PSDImporter")
            {
                EditorUtility.DisplayDialog("PSD Importer 未生效",
                    "当前文件的导入器是:" + typeName +
                    "\n\n可能原因:" +
                    "\n① 文件是 .psd 而非 .psb —— Unity 默认只用 PSD Importer 接管 .psb," +
                    "普通 .psd 走 TextureImporter。本项目已把角色图改为 .psb。" +
                    "\n② com.unity.2d.psdimporter 包没装好 —— Package Manager 里确认「2D PSD Importer」已安装;" +
                    "若没有,manifest.json 已加该包,重启 Unity 触发还原。" +
                    "\n\n处理后重新点本菜单。", "知道了");
                return;
            }

            var so = new SerializedObject(importer);

            // ── 关键开关(PSDImporter 的序列化字段名,2022.3 稳定)──
            SetBool(so, "m_MosaicLayers", true);      // Mosaic:各图层拆成独立 Sprite 打图集
            SetBool(so, "m_ImportHidden", true);      // 导入隐藏图层(本 PSD 部位层默认隐藏!必须开)
            SetBool(so, "m_CharacterMode", true);     // Character 模式:生成可绑骨骼的骨架结构
            SetBool(so, "m_ResliceFromLayer", false); // 不从单层重切
            SetBool(so, "m_KeepDuplicateSpriteName", true);
            SetBool(so, "m_GenerateGOHierarchy", true); // 生成 GameObject 层级(拖进场景即完整人形)

            // 锚点:脚底居中(与立绘一致,便于地平线对齐)
            SetInt(so, "m_TextureImporterSettings.m_Alignment", (int)SpriteAlignment.BottomCenter);
            // PPU
            SetFloat(so, "m_TextureImporterSettings.m_SpritePixelsPerUnit", CharacterPPU);
            // 精灵网格用 FullRect,避免透明裁剪影响锚点
            SetInt(so, "m_TextureImporterSettings.m_SpriteMeshType", (int)SpriteMeshType.FullRect);
            // Sprite 类型
            SetInt(so, "m_TextureImporterSettings.m_TextureType", (int)TextureImporterType.Sprite);
            // 清晰度:mipmap + 三线性(与立绘同理)
            SetBool(so, "m_TextureImporterSettings.m_EnableMipMap", true);
            SetInt(so, "m_TextureImporterSettings.m_FilterMode", (int)FilterMode.Trilinear);
            SetInt(so, "m_TextureImporterSettings.m_Aniso", 8);
            SetInt(so, "m_TextureImporterSettings.m_WrapU", (int)TextureWrapMode.Clamp);
            SetInt(so, "m_TextureImporterSettings.m_WrapV", (int)TextureWrapMode.Clamp);
            SetBool(so, "m_TextureImporterSettings.m_AlphaIsTransparency", true);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log("[PsdCharacterImporterSetup] 已配置并重导入:" + PsdPath +
                      "\n展开该 PSD 应能看到各部位 Sprite;选中它 → Sprite Editor → 右上切 Skinning Editor 绑骨骼。");
            EditorUtility.DisplayDialog("完成",
                "角色 PSD 已配成 Mosaic + Character 骨骼导入模式并重导入。\n\n" +
                "下一步:在 Project 窗口选中 characters_3forms.psd →\n" +
                "Inspector 点「Sprite Editor」→ 右上角下拉切到「Skinning Editor」→ 开始绑骨骼。\n\n" +
                "详细手工步骤见 docs/骨骼动画操作指南.md。", "好");
        }

        // ── SerializedObject 安全设值(字段不存在则跳过并警告,不报错)──
        static void SetBool(SerializedObject so, string path, bool v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = v;
            else Debug.LogWarning("[PsdSetup] 找不到属性(可忽略):" + path);
        }
        static void SetInt(SerializedObject so, string path, int v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.intValue = v;
            else Debug.LogWarning("[PsdSetup] 找不到属性(可忽略):" + path);
        }
        static void SetFloat(SerializedObject so, string path, float v)
        {
            var p = so.FindProperty(path);
            if (p != null) p.floatValue = v;
            else Debug.LogWarning("[PsdSetup] 找不到属性(可忽略):" + path);
        }
    }
}
