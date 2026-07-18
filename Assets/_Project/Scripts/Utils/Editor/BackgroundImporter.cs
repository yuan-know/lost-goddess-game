// ============================================================================
//  BackgroundImporter.cs —— 场景背景图自动导入配置(Editor)
//  路径:Assets/_Project/Resources/Scenes/**/bg_*.png
//  作用:
//    · PPU = 100(与场景世界坐标对齐,4250px 图=42.5 世界单位宽)
//    · 锚点 = Bottom(图底=地平线,方便与老人对齐)
//    · 关 mipmap(背景在屏上是 1:1 或轻度缩放,mipmap 反而糊)
//    · Bilinear + Clamp + 无压缩
//  【只作用于 Resources/Scenes/ 下的 bg_*.png,不影响其他贴图】
// ============================================================================
using UnityEditor;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public class BackgroundImporter : AssetPostprocessor
    {
        const string TargetDir = "/_Project/Resources/Scenes/";
        const float BgPPU = 100f;

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains(TargetDir)) return;
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            if (!file.StartsWith("bg_")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = BgPPU;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter; // 底居中,与地平线对齐
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);

            ti.mipmapEnabled = false;                              // 背景基本 1:1,不用 mipmap
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 8192;                              // 4250px 需要 >4096

            foreach (var plat in new[] { "Standalone", "Windows", "DefaultTexturePlatform" })
            {
                var ps = ti.GetPlatformTextureSettings(plat);
                ps.overridden = true;
                ps.maxTextureSize = 8192;
                ps.format = TextureImporterFormat.RGBA32;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SetPlatformTextureSettings(ps);
            }
        }
    }
}
