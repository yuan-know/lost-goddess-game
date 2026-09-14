// ============================================================================
//  InventoryPanelLayout.cs —— 背包面板「版式单一真源」(2026-09-14)
//
//  底图:Resources/UI/Inventory/bag_panel_v2.png —— 由 背包空白格子.psd 生成
//        (只取「背包空白格子」图层;PSD 里的「图层 1」= 整幅黑 alpha153 是
//         全屏灰暗遮罩,另在 UI 里还原,见 DimAlpha,不烘进底图)
//        1121 x 908,不含任何黑色蒙版。
//
//  几何全部由 tools/inventory/build_bag_asset.py 实测得出(见 panel_grid.json):
//    · 16 格 = 4 列 x 4 行,中心表 SlotCx[]/SlotCy[](行优先,相对底图左上角 px)
//    · 单格可见羊皮纸内框 = TileW x TileH px(16 格均值,用来定道具大小)
//    ★ 底图是手绘的,格子并不等距:列节距 129.4/122.4/128.3,行节距 147/153.5/143.9,
//      逐格中心与"均值行列"最多差 8.4px(屏幕上约 5px)。所以**逐格实测中心**才对得上
//      每一格;用等距公式或行列均值会让个别格明显偏。
//
//  改素材几何后必须重跑 build_bag_asset.py,并同步本文件。
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace LostGoddess
{
    /// <summary>一件道具图标:源贴图尺寸 + alpha 内容外框(Unity 贴图坐标,左下原点)。</summary>
    public struct ItemIconGeom
    {
        public string itemId;
        public string resourcePath;
        public string displayName;
        public float srcW, srcH;            // 源贴图尺寸
        public float x0, y0, x1, y1;        // 内容外框(左下原点, y 向上)

        public ItemIconGeom(string itemId, string path, string name,
                            float sw, float sh, float bx0, float by0, float bx1, float by1)
        {
            this.itemId = itemId; resourcePath = path; displayName = name;
            srcW = sw; srcH = sh;
            x0 = bx0; y0 = by0; x1 = bx1; y1 = by1;
        }

        public float ContentW => x1 - x0;
        public float ContentH => y1 - y0;
        /// <summary>内容框中心相对贴图中心的偏移(UI px, y 向上)。</summary>
        public Vector2 ContentCenterOffset => new Vector2(
            (x0 + x1) * 0.5f - srcW * 0.5f,
            (y0 + y1) * 0.5f - srcH * 0.5f);
    }

    public static class InventoryPanelLayout
    {
        // ── 底图 ──
        public const string PanelResourcePath = "UI/Inventory/bag_panel_v2";
        public const float PanelW = 1121f;      // bag_panel_v2.png 像素尺寸
        public const float PanelH = 908f;

        // ── 16 格中心(相对底图左上角,px;行优先 r0c0..r3c3)──
        //   ★ 这是排版的唯一真源:手绘格子并不严格等距,逐格实测值才能让道具落在每格正中
        //     (逐格与"均值行列"最多差 8.4px,折到屏幕上约 5px,看得见)
        public static readonly float[] SlotCx =
        {
            415.5f, 547.5f, 667.0f, 800.5f,
            413.5f, 538.0f, 668.0f, 788.0f,
            410.5f, 541.5f, 661.0f, 790.0f,
            408.5f, 538.5f, 659.0f, 790.0f,
        };
        public static readonly float[] SlotCy =
        {
            204.5f, 212.5f, 210.0f, 211.0f,
            354.0f, 360.0f, 356.5f, 355.5f,
            509.0f, 512.0f, 509.0f, 510.0f,
            647.0f, 656.0f, 654.0f, 658.5f,
        };

        // 4 列 / 4 行的均值(仅参考、以及取"统一内框尺寸"用)
        public static readonly float[] ColX = { 412.0f, 541.4f, 663.8f, 792.1f };
        public static readonly float[] RowY = { 209.5f, 356.5f, 510.0f, 653.9f };
        public const float TileW = 101.5f;      // 单格可见羊皮纸内框(16 格均值)
        public const float TileH = 124.6f;

        public const int Columns = 4;
        public const int Rows = 4;
        public const int SlotCount = Columns * Rows;   // 16

        // ── 遮罩 ──
        /// <summary>全屏灰暗遮罩 alpha。PSD「图层 1」= 纯黑 153/255 = 0.6。</summary>
        public const float DimAlphaFromArt = 153f / 255f;

        // ====================================================================
        //  可调排版参数 —— ★ 游戏内与调试场景用的是**同一份**静态值,
        //  调试场景里按键改的就是这里,所以「调试所见 = 游戏所见」。
        // ====================================================================

        /// <summary>背包面板显示高度 / 中间条带高度。</summary>
        public static float PanelHeightRatio = 0.94f;

        /// <summary>道具内容框占格子内框的比例(统一大小)。</summary>
        // 2026-09-14 用户定稿:整体缩小 1/5(0.88 -> 0.70)
        public static float ItemFill = 0.70f;

        /// <summary>全屏灰暗遮罩 alpha。</summary>
        public static float DimAlpha = DimAlphaFromArt;

        static readonly Dictionary<string, float> _sizeMul = new Dictionary<string, float>();
        static readonly Dictionary<string, Vector2> _nudge = new Dictionary<string, Vector2>();

        /// <summary>单件道具的额外大小倍率(默认 1)。</summary>
        public static float SizeMul(string itemId)
            => itemId != null && _sizeMul.TryGetValue(itemId, out var v) ? v : 1f;

        public static void SetSizeMul(string itemId, float v)
        {
            if (itemId != null) _sizeMul[itemId] = v;
        }

        /// <summary>单件道具的手工微调(UI px, y 向上,默认 0)。</summary>
        public static Vector2 Nudge(string itemId)
            => itemId != null && _nudge.TryGetValue(itemId, out var v) ? v : Vector2.zero;

        public static void SetNudge(string itemId, Vector2 v)
        {
            if (itemId != null) _nudge[itemId] = v;
        }

        public static void ResetTuning()
        {
            PanelHeightRatio = 0.94f;
            ItemFill = 0.70f;
            DimAlpha = DimAlphaFromArt;
            _sizeMul.Clear();
            _nudge.Clear();
        }

        /// <summary>中间条带高度(CanvasScaler 参考分辨率 1920x1080 下的 px)。</summary>
        public static float BandHeight
            => 1080f * (1f - 2f * LetterboxOverlay.BarHeightPct);

        /// <summary>底图显示缩放 = 面板显示高 / PanelH。</summary>
        public static float ScaleFactor
            => BandHeight * PanelHeightRatio / PanelH;

        /// <summary>底图在屏幕上的显示尺寸。</summary>
        public static Vector2 PanelDisplaySize
            => new Vector2(PanelW * ScaleFactor, BandHeight * PanelHeightRatio);

        // ── 道具图标几何(由 tools/inventory/dump_icon_geom.py 生成,勿手改)──
        static readonly ItemIconGeom[] kItems =
        {
            new ItemIconGeom("crowbar",         "UI/Icons/crowbar_v2",      "铁撬棍",   500, 500,  67,  52, 431, 459),
            new ItemIconGeom("wrench",          "UI/Icons/wrench",          "铸铁扳手", 500, 500,  42,  42, 459, 457),
            new ItemIconGeom("hex_wrench",      "UI/Icons/hex_wrench",      "六角扳手", 500, 500,  42,  42, 459, 457),
            new ItemIconGeom("pottery_key",     "UI/Icons/pottery_key",     "陶罐钥匙", 500, 500,  92,  42, 417, 449),
            new ItemIconGeom("focus_lens",      "UI/Icons/focus_lens_v2",   "聚焦透镜", 500, 500,  49, 157, 457, 379),
            new ItemIconGeom("brass_base",      "UI/Icons/brass_base_v2",   "黄铜底座", 500, 500,  58, 124, 438, 362),
            new ItemIconGeom("gear_1",          "UI/Icons/gear_small_v2",   "小齿轮",   500, 500,  68,  69, 432, 431),
            new ItemIconGeom("gear_2",          "UI/Icons/gear_big_v2",     "大齿轮",   500, 500, 113, 114, 387, 387),
            // 用 Props 里那张(已经是 Sprite 导入);Closeups/light_projector 是同一张图但按 Default 导入
            new ItemIconGeom("light_projector", "Props/Prologue/prop_projector", "光幕投影仪", 500, 500,  71,  53, 421, 449),
            // ↓ 未实装(代码里没有 Add 过),调试场景按 A 键追加,只为看观感
            new ItemIconGeom("lantern",         "UI/Icons/hand_lamp",       "提灯※未实装", 500, 500, 154,  27, 346, 473),
            new ItemIconGeom("magnifier",       "UI/Icons/magnifier",       "放大镜※未实装", 300, 300,  87,  45, 274, 263),
        };

        /// <summary>现阶段游戏里真正能拿到的道具(按格位顺序)。</summary>
        public static readonly string[] ObtainableItems =
        {
            "crowbar", "wrench", "hex_wrench", "pottery_key",
            "focus_lens", "brass_base", "gear_1", "gear_2", "light_projector",
        };

        /// <summary>未实装、仅供观感对比的道具(调试场景 A 键追加)。</summary>
        public static readonly string[] ExtraItems = { "lantern", "magnifier" };

        public static bool TryGet(string itemId, out ItemIconGeom geom)
        {
            for (int i = 0; i < kItems.Length; i++)
                if (kItems[i].itemId == itemId) { geom = kItems[i]; return true; }
            geom = default(ItemIconGeom);
            return false;
        }

        /// <summary>格子中心(相对底图左上角,px)。</summary>
        public static Vector2 SlotCenter(int index)
        {
            int i = Mathf.Clamp(index, 0, SlotCount - 1);
            return new Vector2(SlotCx[i], SlotCy[i]);
        }

        /// <summary>把第 index 件道具摆进第 index 格:算出图标 RectTransform 的尺寸与 anchoredPosition。
        /// 容器锚点 (0,1)、pivot (0.5,0.5),所以 (SlotCx*k, -SlotCy*k) 就是格子中心。</summary>
        /// <param name="index">格位序号(0..15,行优先)</param>
        /// <param name="k">显示缩放 = 面板显示高 / PanelH</param>
        /// <param name="fill">道具内容框占格子内框的比例</param>
        /// <param name="nudge">手工微调(UI px, y 向上)</param>
        public static void ComputeSlot(int index, float k, float fill, Vector2 nudge,
                                       ItemIconGeom g, out Vector2 size, out Vector2 anchoredPos)
        {
            Vector2 center = SlotCenter(index);

            float tileW = TileW * k, tileH = TileH * k;
            float cw = g.ContentW, ch = g.ContentH;
            // 内容框等比 contain 进 (格子内框 * fill)
            float s = Mathf.Min(tileW * fill / Mathf.Max(cw, 1e-4f),
                                tileH * fill / Mathf.Max(ch, 1e-4f));

            size = new Vector2(g.srcW * s, g.srcH * s);
            Vector2 off = g.ContentCenterOffset * s;     // 内容中心偏离贴图中心多少
            anchoredPos = new Vector2(center.x * k, -center.y * k) - off + nudge;
        }

        /// <summary>按当前全局参数(ScaleFactor / ItemFill / 单件倍率与微调)算格位。</summary>
        public static void ComputeSlot(int index, string itemId, ItemIconGeom g,
                                       out Vector2 size, out Vector2 anchoredPos)
        {
            ComputeSlot(index, ScaleFactor, ItemFill * SizeMul(itemId), Nudge(itemId), g,
                        out size, out anchoredPos);
        }
    }
}
