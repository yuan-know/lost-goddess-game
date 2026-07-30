// ============================================================================
//  DiningHallScene.cs —— 第一章 · 餐厅
//
//  功能:
//    · 餐厅背景(3400×1200)
//    · 左边界 ← 大殿,右边界 → 武器室
//    · 首帧文案:餐厅介绍
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class DiningHallScene
    {
        const float SpawnX_Left = -14f;
        const float SpawnX_Right = 14f;

        public static void Build()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.DiningHall);
            room.name = "Room_" + Rooms.Chapter1_DiningHall;
            float groundY = SceneRoomBuilder.DiningHall.groundY;

            float spawnX;
            bool faceRight;
            if (SceneLoader.EnterDirection == "right")
            {
                spawnX = SpawnX_Right;
                faceRight = false;
            }
            else // 从左边进来
            {
                spawnX = SpawnX_Left;
                faceRight = true;
            }

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY, spawnX, yOffsetOverride: PlayerBuilder.GetChapter1YOffset(GameState.CurrentEra));
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;  // 立绘默认朝左
                    var s = player.transform.localScale;
                    s.x = Mathf.Abs(s.x) * (faceRight ? -1f : 1f);  // 朝右时scale.x为负
                    player.transform.localScale = s;
                }
            }

            // 左边界传送 → 大殿
            BuildLeftPortal(room.transform, SceneRoomBuilder.DiningHall);
            // 右边界传送 → 武器室
            BuildRightPortal(room.transform, SceneRoomBuilder.DiningHall);

            room.AddComponent<DiningHallDirector>();
        }

        static void BuildLeftPortal(Transform parent, SceneDef scene)
        {
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = -halfW + 0.6f;
            float groundY = scene.groundY;

            const float w = 2.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Left_MainHall");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(portalX, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PortalSolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Chapter1_Hall;
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "right";

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(portalX + 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        static void BuildRightPortal(Transform parent, SceneDef scene)
        {
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;
            float groundY = scene.groundY;

            const float w = 2.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Right_WeaponsRoom");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(portalX, centerY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PortalSolidSprite();
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = 70;
            go.transform.localScale = new Vector3(w, h, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var portal = go.AddComponent<ScenePortal>();
            portal.targetRoom = Rooms.Chapter1_WeaponsRoom;
            portal.highlightTarget = sr;
            portal.fadeTime = 0.6f;
            portal.triggerOnEnter = true;
            portal.enterDirection = "left";

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(portalX - 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        static Sprite _portalSolid;
        static Sprite PortalSolidSprite()
        {
            if (_portalSolid != null) return _portalSolid;
            var tex = new Texture2D(2, 2);
            var px = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(px); tex.Apply();
            _portalSolid = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _portalSolid;
        }
    }

    public class DiningHallDirector : MonoBehaviour
    {
        void Start()
        {
            QuestPromptManager.Hide();

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            const string kFlag = "chapter1_dining_intro_done";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.5f);
            bool done = false;
            DialogueSystem.ShowNarration(Dialogues.ch1_dining_intro, () => done = true);
            while (!done) yield return null;
        }
    }
}
