// ============================================================================
//  CharacterSpriteImporter.cs —— 角色立绘自动导入配置(Editor)
//  美术素材按 2000×2000 的 4 倍超采样绘制;导入时统一:
//    · PPU = 600  →  2008px 立绘 ≈ 3.35 世界单位高,占屏高约 1/3(超采样保精度)
//    · 锚点 = 脚底居中(BottomCenter)→ 与 PlayerController 脚底深度排序 / 地平线对齐一致
//    · 【关键】开 mipmap + 三线性 + 各向异性:2000px 大图缩到屏上 ~270px 是 7.4 倍缩小,
//      这才是"超采样抗锯齿"真正生效处。关 mipmap 会让引擎硬点采样 → 边缘闪线(隐约白线)
//      + 整体发糊。之前关 mipmap 判断反了,现改回开;preserveCoverage 防缩小时 alpha 边缘变细。
//  作用范围:Assets/_Project/Resources/Characters/ 下所有贴图。
//  美术只管把图丢进该目录,PPU/锚点无需手动调。
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public class CharacterSpriteImporter : AssetPostprocessor
    {
        const string TargetDir = "/_Project/Resources/Characters/";
        // 素材按 2000px 的 4 倍超采样绘制;PPU=600 → 2008px 立绘 ≈ 3.35 世界单位高,
        // 相机可见高 10 单位(orthoSize 5)时人物占屏高约 1/3。仍保留超采样精度。
        const float CharacterPPU = 600f;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(TargetDir)) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = CharacterPPU;

            // 锚点 = 脚底居中(导出脚本已把图底裁成脚底)
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect; // 立绘用整矩形,避免透明裁剪影响锚点
            ti.SetTextureSettings(settings);

            // 高清大图缩小显示的正解:开 mipmap + 三线性 + 各向异性过滤。
            // 引擎用预生成的 mip 链做多级采样,把 2000px 无损降到屏上 ~270px = 超采样抗锯齿。
            // 关 mipmap 反而糊+闪(欠采样);之前那版判断反了。
            ti.mipmapEnabled = true;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; // 更锐利的降采样核
            ti.mipMapsPreserveCoverage = true;                        // 缩小时保持 alpha 覆盖,防边缘变细/发虚
            ti.alphaTestReferenceValue = 0.5f;
            ti.filterMode = FilterMode.Trilinear;                     // 三线性:mip 层间平滑过渡,消移动时闪线
            ti.anisoLevel = 8;                                        // 各向异性:斜看/缩小时进一步保清晰
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;

            // 强制所有平台无压缩 + 不缩小(否则编辑器按 Standalone 用 DXT 压缩,alpha 边缘出毛边)
            foreach (var platform in new[] { "Standalone", "Windows", "DefaultTexturePlatform" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 4096;
                ps.format = TextureImporterFormat.RGBA32;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(ps);
            }
        }
    }
}
