// ============================================================================
//  Keys.cs —— 全局字符串常量集中定义(系统层 A 维护)
//  规范:禁止在别处散落魔法字符串。flag / item / room / dialogue / sfx / bgm
//        的 key 一律引用这里的常量。B 新增内容时也在此登记。
// ============================================================================

namespace LostGoddess
{
    /// <summary>Flag 开关键(门是否开、谜题是否解、剧情是否触发)。snake_case。</summary>
    public static class Flags
    {
        // 验证/示例用
        public const string demo_lamp_lit      = "demo_lamp_lit";      // 示例:提灯已点亮
        public const string demo_door_unlocked = "demo_door_unlocked"; // 示例:门已解锁
        public const string prologue_started   = "prologue_started";   // 序章已开始
    }

    /// <summary>物品 id。snake_case。</summary>
    public static class Items
    {
        public const string item_key    = "item_key";    // 示例:钥匙
        public const string item_lamp   = "item_lamp";   // 老年:提灯
        public const string item_gear   = "item_gear";   // 示例:齿轮
    }

    /// <summary>房间/场景名。命名:章前缀_序号_英文名(P=Prologue,O=Old,M=Middle,Y=Young)。</summary>
    public static class Rooms
    {
        public const string Boot        = "Boot";
        public const string MainMenu    = "MainMenu";
        public const string P_00_Entrance = "P_00_Entrance";
        public const string P_01_Hall     = "P_01_Hall";

        // 验证用:程序化生成的占位房间(无 .unity 文件,靠 Bootstrap 建内容)
        public const string Sandbox    = "Sandbox";
    }

    /// <summary>对白/旁白 id。</summary>
    public static class Dialogues
    {
        public const string demo_intro   = "demo_intro";
        public const string demo_locked  = "demo_locked";
    }

    /// <summary>音效/音乐 clip 名(对应 Audio 文件夹资源名)。</summary>
    public static class Sfx
    {
        public const string click     = "sfx_click";
        public const string footstep  = "sfx_footstep";
        public const string door_open = "sfx_door_open";
        public const string pickup    = "sfx_pickup";
    }

    public static class Bgm
    {
        public const string prologue = "bgm_prologue";
    }
}
