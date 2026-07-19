// ============================================================================
//  PrologueLinkRoomBuilder.cs —— 神庙前厅左右链上"过场房间"通用生成器
//
//  策划场景切换图(2026-07-19):
//    · 神庙前厅为中心
//      朝左:Foyer ↔ LadderChamber(左2,梯子密室) ↔ Chamber3(左3,陶罐间)
//      朝右:Foyer ↔ GearRoom(右1,齿轮骨骸间) ↔ StatueRoom(右2,石雕室)
//    · 从 LadderChamber 的木梯爬上二楼:UpperChamber ↔ UpperHall(二楼连廊)
//
//  每个链式房间都是"两端 Portal + 老人在中间可控行走"的过场结构,美术占位期先用
//  DarkForest 三层视差顶着,等真图交付后 SceneDef 换一下 sprite 名字就行。
//
//  用法:
//    PrologueLinkRoomBuilder.Build(new LinkRoomDef {
//        roomName = Rooms.Prologue_LadderChamber,
//        leftRoom = Rooms.Prologue_Chamber3,   // 朝左返回/往下走的目标
//        rightRoom = Rooms.Prologue_Foyer,     // 朝右前进/返回门厅
//        title = "梯子密室",
//    });
// ============================================================================

using UnityEngine;

namespace LostGoddess.Content
{
    public struct LinkRoomDef
    {
        public string roomName;    // 本房间常量(Rooms.xxx)
        public string leftRoom;    // 朝左 Portal 目标(空=不建左出口)
        public string rightRoom;   // 朝右 Portal 目标(空=不建右出口)
        public string title;       // 中文名(HUD/首帧独白显示)
        public string upRoom;      // 朝上(爬梯)Portal 目标(空=不建;仅 LadderChamber 用)
        public SceneDef scene;     // 用哪份美术 SceneDef(空=退回 DarkForest 占位)
    }

    public static class PrologueLinkRoomBuilder
    {
        public static void Build(LinkRoomDef def)
        {
            // 背景:优先用 def.scene(真图),缺则退回 DarkForest 占位视差
            var sceneDef = def.scene ?? SceneRoomBuilder.DarkForest;
            var root = SceneRoomBuilder.Build(sceneDef);
            root.name = "Room_" + def.roomName;
            float groundY = sceneDef.groundY;

            // 老人落在场景中央,让玩家自己走向两侧(SceneLoader 没带来源方向信息)
            PlayerBuilder.Build(GameState.CurrentEra, groundY, 0f);

            // 场景左端 Portal:朝左走 = 返回上一房间
            if (!string.IsNullOrEmpty(def.leftRoom))
            {
                BuildEdgePortal(root.transform, def.leftRoom, "Portal_Left_" + def.leftRoom,
                    new Vector2(-15f, groundY + 1.5f), groundY, isLeft: true);
            }

            // 场景右端 Portal:朝右走 = 前进到下一房间
            if (!string.IsNullOrEmpty(def.rightRoom))
            {
                BuildEdgePortal(root.transform, def.rightRoom, "Portal_Right_" + def.rightRoom,
                    new Vector2(15f, groundY + 1.5f), groundY, isLeft: false);
            }

            // 场景中央 Portal:朝上(爬梯)= 上二楼(仅 LadderChamber)
            if (!string.IsNullOrEmpty(def.upRoom))
            {
                BuildLadderPortal(root.transform, def.upRoom, new Vector2(0f, groundY + 2.5f), groundY);
            }

            // Director:首帧独白 + 强制可控
            var dir = root.AddComponent<PrologueLinkRoomDirector>();
            dir.roomTitle = def.title;
        }

        // 场景左右边缘的"看不见的 3×5 Portal 判定块",走近点击即切场景
        static void BuildEdgePortal(Transform parent, string targetRoom, string name,
                                    Vector2 pos, float groundY, bool isLeft)
        {
            const float w = 3.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            // 淡色 debug tint(左=蓝、右=红),验证期看得见判定块;正式期 α=0
            sr.color = isLeft ? new Color(0.3f, 0.6f, 1f, 0.10f)
                              : new Color(1f, 0.5f, 0.4f, 0.10f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;

            // interactPoint:老人走到 Portal 内侧地平线上再触发(左边 portal 站右侧、右边 portal 站左侧)
            float ipx = isLeft ? pos.x + 1.5f : pos.x - 1.5f;
            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(ipx, groundY, 0f);
            portal.interactPoint = ip;

            Debug.Log($"[LinkRoom] {name} @ x={pos.x} → {targetRoom}");
        }

        // 中央爬梯 Portal(纵向长条判定块,更接近木梯的形状)
        static void BuildLadderPortal(Transform parent, string targetRoom, Vector2 pos, float groundY)
        {
            const float w = 1.5f, h = 5.0f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Up_" + targetRoom);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.4f, 1f, 0.5f, 0.12f);  // 淡绿 = 上楼
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = targetRoom;
            portal.successDialogueId = "";
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(pos.x, groundY, 0f);
            portal.interactPoint = ip;

            Debug.Log($"[LinkRoom] Portal_Up @ x={pos.x} → {targetRoom}");
        }

        static Sprite _solid;
        static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _solid;
        }
    }

    /// <summary>链式过场房间的通用 Director:首帧一句"当前所在"独白 + 强制可控。</summary>
    public class PrologueLinkRoomDirector : MonoBehaviour
    {
        public string roomTitle = "";

        void Start()
        {
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 一次性提示"你在哪儿,朝左朝右能去哪儿"(占位期方便测试;正式期删掉)
            string kFlag = "linkroom_entered_" + name;
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        System.Collections.IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.4f);
            bool done = false;
            string msg = string.IsNullOrEmpty(roomTitle)
                ? "(占位房间。走到左/右边缘点击可切场景。)"
                : $"({roomTitle}。走到左/右边缘点击可切场景。)";
            DialogueSystem.ShowText(msg, () => done = true);
            while (!done) yield return null;
        }
    }
}
