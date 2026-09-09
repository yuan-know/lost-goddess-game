// ============================================================================
//  AISpriteImporter.cs —— AI 视频 PNG 序列帧自动导入配置(Editor)
//  作用范围:Assets/_Project/Art/AIAnimations/ 下的所有 PNG。
//  自动配成 Sprite(单帧) + 脚底居中锚点 + 无压缩 mipmap,
//  这样 AI 动画生成器脚本能直接用这些 Sprite 拼成 AnimationClip。
// ============================================================================

using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public class AISpriteImporter : AssetPostprocessor
    {
        const string TargetDir = "/_Project/Art/AIAnimations/";
        // 游戏里角色高 5 世界单位,AI 帧高 512px → PPU = 102.4
        // 但故意设成 100 (整数好算),scale 微调即可。
        const float PPU = 100f;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(TargetDir)) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;

            // 锚点 = 脚底居中(后处理脚本已把脚底钉到图底)
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);

            ti.mipmapEnabled = true;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;

            foreach (var platform in new[] { "Standalone", "Windows", "DefaultTexturePlatform" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.maxTextureSize = 2048;
                ps.format = TextureImporterFormat.RGBA32;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(ps);
            }
        }
    }
}
