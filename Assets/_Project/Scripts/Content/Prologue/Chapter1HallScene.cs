// ============================================================================
//  Chapter1HallScene.cs —— 第一章 · 神庙大殿
//
//  剧情:第四幕剧情杀后觉醒 → 落地大殿。这是主线第一章的起点。
//  结构:大殿是中心枢纽,左接餐厅/武器/长廊,目前只有向右一条线。
//
//  功能:
//    · 大殿背景(4500×1200)
//    · 角色左侧出生朝右(从追踪长廊回来时从右侧出生朝左)
//    · 右边界传送 → 餐厅
//    · 首帧文案:大殿介绍
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class Chapter1HallScene
    {
        // 左右出生点(距中心)
        const float SpawnX_Left = -18f;
        const float SpawnX_Right = 18f;

        public static void Build()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.MainHall);
            room.name = "Room_" + Rooms.Chapter1_Hall;
            float groundY = SceneRoomBuilder.MainHall.groundY;

            // 根据进入方向决定出生点
            float spawnX;
            bool faceRight;
            if (SceneLoader.EnterDirection == "right")
            {
                spawnX = SpawnX_Right;
                faceRight = false;
            }
            else // 默认从左侧进入
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

            // 右边界传送 → 餐厅
            BuildRightPortal(room.transform, SceneRoomBuilder.MainHall);

            room.AddComponent<Chapter1HallDirector>();
        }

        static void BuildRightPortal(Transform parent, SceneDef scene)
        {
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 0.6f;
            float groundY = scene.groundY;

            const float w = 2.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Right_DiningHall");
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
            portal.targetRoom = Rooms.Chapter1_DiningHall;
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

    /// <summary>大殿首帧 Director:播介绍文案,隐藏 QuestPrompt。</summary>
    public class Chapter1HallDirector : MonoBehaviour
    {
        void Start()
        {
            // 进入主线后隐藏"回到神庙前厅"的指引
            QuestPromptManager.Hide();

            // 保证角色可控
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 首帧介绍文案(只播一次)
            const string kFlag = "chapter1_hall_intro_done";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.5f);
            bool done = false;
            DialogueSystem.ShowNarration(Dialogues.ch1_hall_intro, () => done = true);
            while (!done) yield return null;
        }
    }
}
