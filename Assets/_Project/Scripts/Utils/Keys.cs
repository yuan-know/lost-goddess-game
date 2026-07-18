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

        // ── 序幕(2026-07-18 落地) ──
        public const string Prologue_MetTemple       = "prologue_met_temple";        // 第 0 幕走到神庙外
        public const string Prologue_EnteredFoyer    = "prologue_entered_foyer";     // 首次进神庙门厅
        public const string Prologue_InsightUsed     = "prologue_insight_used";      // 使用过一次岁月洞察
        public const string Prologue_MuralRevealed   = "prologue_mural_revealed";    // 壁画图腾已显影
        public const string Prologue_UnlockedYoung   = "prologue_unlocked_young";    // 陶罐钥匙插锁孔,解锁青年
        public const string Prologue_UnlockedMiddle  = "prologue_unlocked_middle";   // 棺材小游戏解出,解锁中年
        public const string Prologue_ProjectorSolved = "prologue_projector_solved";  // 三层圆盘谜题解出
        public const string Prologue_DoorOpen        = "prologue_door_open";         // 大门开启
        public const string Prologue_DeathCutscene   = "prologue_death_cutscene";    // 剧情杀已播
        public const string Achievement_ReturnToPast = "achievement_return_to_past"; // 成就【重返过去】
    }

    /// <summary>物品 id。snake_case。</summary>
    public static class Items
    {
        public const string item_key    = "item_key";    // 示例:钥匙
        public const string item_lamp   = "item_lamp";   // 老年:提灯
        public const string item_gear   = "item_gear";   // 示例:齿轮

        // ── 序幕(2026-07-18 落地) ──
        public const string Lantern       = "lantern";        // 提灯:老年原始道具(举稳手抖)
        public const string Magnifier     = "magnifier";      // 放大镜:老年原始道具(看清墙壁密语)
        public const string LostRelic     = "lost_relic";     // 遗失物:首次形态切换触发物(策划 .docx 简版)
        public const string Crowbar       = "crowbar";        // 铁撬棍:青年撬铁笼、砸石板
        public const string PotteryKey    = "pottery_key";    // 陶罐里的钥匙(策划 7.15 稿)→ 洗礼池锁孔切青年
        public const string FocusLens     = "focus_lens";     // 聚焦透镜:投影仪组件
        public const string BrassBase     = "brass_base";     // 黄铜底座:投影仪组件
        public const string Gear1         = "gear_1";         // 齿轮 1(策划 7.15 稿,常量先埋)
        public const string Gear2         = "gear_2";         // 齿轮 2(策划 7.15 稿,常量先埋)
        public const string LightProjector = "light_projector"; // 光幕投影仪(中年组合出)
        public const string HexWrench     = "hex_wrench";     // 六角扳手:主线第二阶段场景 1 密室(序幕不用)
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

        // ── 序幕 5 幕场景(2026-07-18 落地清单 §0.1) ──
        public const string Prologue_Woods       = "Prologue_Woods";        // 第 0 幕 荒山野道(复用黑暗森林视差)
        public const string Prologue_Foyer       = "Prologue_Foyer";        // 第一幕 神庙门厅(复用神庙入口视差)
        public const string Prologue_Chamber1    = "Prologue_Chamber1";     // 密室 1:铁撬棍拾取
        public const string Prologue_Chamber2    = "Prologue_Chamber2";     // 密室 2:棺材切中年
        public const string Prologue_Chamber3    = "Prologue_Chamber3";     // 密室 3:陶罐钥匙切青年
        public const string Prologue_UpperHall   = "Prologue_UpperHall";    // 第二幕 二楼回廊(铁笼齿轮箱)
        public const string Prologue_Chase       = "Prologue_Chase";        // 第四幕 黑雾追击剧情杀
        public const string Chapter1_Hall        = "Chapter1_Hall";         // 序幕结束落地(主线起点)
    }

    /// <summary>对白/旁白 id。命名:prologue_&lt;幕&gt;_&lt;序&gt;。</summary>
    public static class Dialogues
    {
        public const string demo_intro   = "demo_intro";
        public const string demo_locked  = "demo_locked";

        // ── 前言(黑屏白字) ──
        public const string prologue_0_prelude = "prologue_0_prelude";  // "很多年后..."

        // ── 第 0 幕 荒山野道 ──
        public const string prologue_0_01 = "prologue_0_01"; // 老年:"这么多年了,还是回到了这里……"
        public const string prologue_0_02 = "prologue_0_02"; // 老年:"什么都没变……这里的时间好像静止了……只有我,老得快要死了。"
        public const string prologue_0_03 = "prologue_0_03"; // 老年:"它也一直在这里……几十年来,它好像一直在看着我……"

        // ── 第一幕 神庙门厅 ──
        public const string prologue_1_01 = "prologue_1_01"; // 内心 os:"为什么什么声音都没有……"
        public const string prologue_1_02 = "prologue_1_02"; // 内心 os:"门是锁死的,这好像缺了什么东西……"
        public const string prologue_1_insight_hint = "prologue_1_insight_hint"; // "按 Q 使用【岁月洞察】"
        public const string prologue_1_mural = "prologue_1_mural"; // 洞察时:壁画显影提示
        public const string prologue_1_upper_glow = "prologue_1_upper_glow"; // 洞察时:二楼高亮提示
        public const string prologue_1_cant_climb = "prologue_1_cant_climb"; // "这腿爬不上去了……"
        public const string prologue_1_door_locked = "prologue_1_door_locked"; // "门是锁死的……"
        public const string prologue_1_need_relic = "prologue_1_need_relic"; // "先在周围看看有没有什么能用的吧。"

        // ── 第二幕 青年 ──
        public const string prologue_2_awake = "prologue_2_awake"; // 青年:"刚刚发生了什么?"
        public const string prologue_2_resolve = "prologue_2_resolve"; // 青年:"我得赶快解开门锁,找到神像!"
        public const string prologue_2_got_crowbar = "prologue_2_got_crowbar"; // "这玩意儿倒挺顺手的……"
        public const string prologue_2_pry_cage = "prologue_2_pry_cage"; // 撬开铁笼
        public const string prologue_2_got_base = "prologue_2_got_base"; // 拽底座:"这个底座……好像和展台的形状挺像的。"
        public const string prologue_2_assemble_fail = "prologue_2_assemble_fail"; // "你毛躁的双手无法完成精密的拼接!"
        public const string prologue_2_gear_sound = "prologue_2_gear_sound"; // "这个声音……"(拨齿轮)
        public const string prologue_2_wrong_era_middle = "prologue_2_wrong_era_middle"; // 通用:非中年拼接失败

        // ── 第三幕 中年 ──
        public const string prologue_3_awake = "prologue_3_awake"; // 中年:"奇怪,这是哪?我不是在修表吗。"
        public const string prologue_3_combine = "prologue_3_combine"; // "这两个,组合一下就可以得到……"
        public const string prologue_3_projector_use = "prologue_3_projector_use"; // "但这个在这里有什么用呢。"
        public const string prologue_3_go_downstairs = "prologue_3_go_downstairs"; // "还是先下楼看看吧。"

        // ── 第四幕 大门开启 & 剧情杀 ──
        public const string prologue_4_door_open = "prologue_4_door_open"; // 旁白:"大门缓缓开启……"
        public const string prologue_4_dark_fog = "prologue_4_dark_fog"; // 中年:"那是什么,那是什么!"
        public const string prologue_4_frozen = "prologue_4_frozen"; // 旁白:"两条腿像灌了铅一样"
        public const string prologue_4_switch_hint = "prologue_4_switch_hint"; // 提示:"按键切换青年"
        public const string prologue_4_died = "prologue_4_died"; // 黑屏白字:"怎么就这么……死了?"
        public const string prologue_4_awakening = "prologue_4_awakening"; // "等等,死了还能想这件事吗?"

        // ── Era 不匹配的通用提示 ──
        public const string wrong_era_need_young  = "wrong_era_need_young";   // 需要青年:"这需要一双灵活的手/强壮的臂膀"
        public const string wrong_era_need_middle = "wrong_era_need_middle";  // 需要中年:"你毛躁的双手无法完成精密的拼接"
        public const string wrong_era_need_old    = "wrong_era_need_old";     // 需要老年:"你还没有那双能看穿岁月的眼睛"
    }

    /// <summary>音效/音乐 clip 名(对应 Audio 文件夹资源名)。</summary>
    public static class Sfx
    {
        public const string click     = "sfx_click";
        public const string footstep  = "sfx_footstep";
        public const string door_open = "sfx_door_open";
        public const string pickup    = "sfx_pickup";

        // ── 序幕 ──
        public const string cane_tap        = "sfx_cane_tap";        // 拐杖敲地
        public const string thunder         = "sfx_thunder";         // 远雷
        public const string wind_low        = "sfx_wind_low";        // 低沉风声
        public const string heartbeat       = "sfx_heartbeat";       // 老人心跳
        public const string breath_heavy    = "sfx_breath_heavy";    // 剧烈喘息
        public const string flash_white     = "sfx_flash_white";     // 闪白切换音
        public const string cage_pry        = "sfx_cage_pry";        // 撬铁笼
        public const string gear_click      = "sfx_gear_click";      // 齿轮"咔哒"
        public const string mechanism_rumble = "sfx_mechanism_rumble"; // 机械轰鸣(大门)
        public const string dark_fog_swell   = "sfx_dark_fog_swell";   // 黑雾涌动
    }

    public static class Bgm
    {
        public const string prologue = "bgm_prologue";
        public const string prologue_woods = "bgm_prologue_woods";  // 第 0 幕荒山环境
        public const string prologue_foyer = "bgm_prologue_foyer";  // 神庙门厅诡异静默
        public const string prologue_chase = "bgm_prologue_chase";  // 第四幕黑雾追击
    }
}
