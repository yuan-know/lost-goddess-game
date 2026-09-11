using UnityEngine;

namespace LostGoddess
{
    /// <summary>
    /// 全局字体入口。默认字体：霞鹜文楷 LXGW WenKai（2026-09-11 选型，编号5）。
    /// 字体文件位于 Assets/_Project/Resources/Fonts/，通过 Resources 加载；
    /// 加载失败时回退 Unity 内置字体，保证 UI 不会因缺字体而丢文本。
    /// </summary>
    public static class GameFonts
    {
        private const string PrimaryResourcePath = "Fonts/LXGWWenKai-Regular";
        private static Font _primary;

        public static Font Primary
        {
            get
            {
                if (_primary == null)
                {
                    _primary = Resources.Load<Font>(PrimaryResourcePath);
                    if (_primary == null)
                    {
                        Debug.LogWarning($"[GameFonts] 主字体加载失败：Resources/{PrimaryResourcePath}，回退内置字体");
                        _primary = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                                   ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                }
                return _primary;
            }
        }
    }
}
