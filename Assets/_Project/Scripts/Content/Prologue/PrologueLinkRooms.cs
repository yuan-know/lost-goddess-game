// ============================================================================
//  PrologueLinkRooms.cs —— 神庙前厅左右链上 4 个过场房间的落地入口
//
//  策划场景切换图(2026-07-19):
//    Foyer ↔ LadderChamber ↔ Chamber3     (左链)
//    Foyer ↔ GearRoom ↔ StatueRoom        (右链)
//    LadderChamber↑ UpperChamber ↔ UpperHall   (二楼链)
//
//  每个场景真实美术接线:
//    LadderChamber → Scenes/LadderChamber/ (bg_far + prop_ladder)
//    GearRoom      → Scenes/Chamber1/      (齿轮骨骸间 bg_far + bg_near)
//    StatueRoom    → Scenes/Chamber2/      (人形石雕像 + 8 小雕像 + 方格墙)
//    UpperChamber  → Scenes/LadderChamber/ (二楼密室暂复用梯子室,美术后续可另出)
// ============================================================================

namespace LostGoddess.Content
{
    public static class PrologueLadderChamberScene
    {
        public static void Build()
        {
            // 前厅左 2:梯子密室(向左到陶罐间 / 向右返回神庙前厅 / 中央爬梯直达二楼回廊)
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_LadderChamber,
                leftRoom  = Rooms.Prologue_Chamber3,     // 朝左 → 陶罐间
                rightRoom = Rooms.Prologue_Foyer,        // 朝右 → 返回神庙前厅
                upRoom    = Rooms.Prologue_UpperHall,    // 中央木梯 → 二楼回廊(UpperHall)
                title     = "梯子密室",
                scene     = SceneRoomBuilder.LadderChamber,
            });
        }
    }

    public static class PrologueGearRoomScene
    {
        public static void Build()
        {
            // 前厅右 1:齿轮骨骸间(向左返回前厅 / 向右到石雕室)
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_GearRoom,
                leftRoom  = Rooms.Prologue_Foyer,
                rightRoom = Rooms.Prologue_StatueRoom,
                title     = "齿轮骨骸间",
                scene     = SceneRoomBuilder.GearRoom,
            });
        }
    }

    public static class PrologueStatueRoomScene
    {
        public static void Build()
        {
            // 前厅右 2:方格墙石雕室(向左返回齿轮间;右端是尽头)
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_StatueRoom,
                leftRoom  = Rooms.Prologue_GearRoom,
                rightRoom = "",   // 右端尽头
                title     = "石雕室",
                scene     = SceneRoomBuilder.StatueRoom,
            });
        }
    }

    public static class PrologueUpperChamberScene
    {
        public static void Build()
        {
            // 二楼密室:爬梯上来的落地房(向左爬回梯子密室 / 向右到二楼连廊)
            //  暂复用 LadderChamber 美术(策划图第二排"爬楼梯切二楼"格子的构图正是这个),
            //  等美术出"二楼密室专图"后 SceneDef 换一下即可。
            PrologueLinkRoomBuilder.Build(new LinkRoomDef {
                roomName  = Rooms.Prologue_UpperChamber,
                leftRoom  = Rooms.Prologue_LadderChamber,
                rightRoom = Rooms.Prologue_UpperHall,
                title     = "二楼密室",
                scene     = SceneRoomBuilder.LadderChamber,
            });
        }
    }
}
