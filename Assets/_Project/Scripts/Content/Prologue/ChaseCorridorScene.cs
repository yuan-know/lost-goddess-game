// ============================================================================
//  ChaseCorridorScene.cs —— 第一章 · 怨灵追逐长廊
//
//  功能:
//    · 长廊背景(3400×1200,睁眼/闭眼两版)
//    · 左边界 ← 武器室
//    · 右边界触发追逐动画 → 视频播放 → 回到大殿
//    · 首帧文案:长廊介绍
// ============================================================================

using System;
using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class ChaseCorridorScene
    {
        const float SpawnX_Left = -14f;
        const float SpawnX_Right = 14f;

        public static void Build()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.ChaseCorridor);
            room.name = "Room_" + Rooms.Chapter1_ChaseCorridor;
            float groundY = SceneRoomBuilder.ChaseCorridor.groundY;

            float spawnX;
            bool faceRight;
            if (SceneLoader.EnterDirection == "right")
            {
                spawnX = SpawnX_Right;
                faceRight = false;
            }
            else
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

            // 左边界传送 → 武器室
            BuildLeftPortal(room.transform, SceneRoomBuilder.ChaseCorridor);
            // 右边界 → 触发追逐动画
            BuildRightChaseTrigger(room.transform, SceneRoomBuilder.ChaseCorridor);

            room.AddComponent<ChaseCorridorDirector>();
        }

        static void BuildLeftPortal(Transform parent, SceneDef scene)
        {
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = -halfW + 0.6f;
            float groundY = scene.groundY;

            const float w = 2.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("Portal_Left_WeaponsRoom");
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
            portal.enterDirection = "right";

            var ip = new GameObject("interactPoint").transform;
            ip.SetParent(go.transform, false);
            ip.position = new Vector3(portalX + 1.5f, groundY, 0f);
            portal.interactPoint = ip;
        }

        static void BuildRightChaseTrigger(Transform parent, SceneDef scene)
        {
            float halfW = scene.bgPixelWidth / scene.bgPPU * 0.5f;
            float portalX = halfW - 1.0f;
            float groundY = scene.groundY;

            const float w = 3.0f, h = 5.5f;
            float centerY = groundY + h * 0.5f;

            var go = new GameObject("ChaseTrigger_Right");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(portalX, centerY, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            go.transform.localScale = new Vector3(w, h, 1f);

            // 用自定义组件触发追逐,不用 ScenePortal
            go.AddComponent<ChaseTrigger>();
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

    /// <summary>长廊右边界触发点:玩家走入后播放追逐视频,结束后回大殿。
    /// 用 Update 自检测模式(与 ScenePortal triggerOnEnter 一致),避免 collider 阻挡点击。</summary>
    public class ChaseTrigger : MonoBehaviour
    {
        bool _triggered;

        void Update()
        {
            if (_triggered) return;
            var pc = PlayerController.Instance;
            if (pc == null) return;

            Vector2 center = transform.position;
            float halfW = Mathf.Abs(transform.lossyScale.x) * 0.5f;
            float px = pc.transform.position.x;

            if (px >= center.x - halfW && px <= center.x + halfW)
            {
                _triggered = true;
                var director = FindObjectOfType<ChaseCorridorDirector>();
                if (director != null) director.PlayChase();
            }
        }
    }

    public class ChaseCorridorDirector : MonoBehaviour
    {
        bool _chasePlaying;

        void Start()
        {
            QuestPromptManager.Hide();

            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 首帧介绍文案(只播一次)
            const string kFlag = "chapter1_corridor_intro_done";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.5f);
            bool done = false;
            DialogueSystem.ShowNarration(Dialogues.ch1_corridor_intro, () => done = true);
            while (!done) yield return null;
        }

        public void PlayChase()
        {
            if (_chasePlaying) return;
            StartCoroutine(ChaseRoutine());
        }

        IEnumerator ChaseRoutine()
        {
            _chasePlaying = true;

            // 锁定玩家
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            // 播放追逐视频
            string videoPath = FindChaseVideoFile();
            if (videoPath != null)
            {
                var vp = VideoPlayerProxy.Create("~ChaseCorridorVideo");
                if (vp != null && vp.Setup(videoPath))
                {
                    vp.Prepare();
                    float timeout = 5f;
                    while (!vp.IsPrepared && !vp.HasError && timeout > 0)
                    {
                        timeout -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (vp.IsPrepared && !vp.HasError)
                    {
                        vp.Play();
                        while (!vp.IsFinished && !vp.HasError)
                            yield return null;
                        vp.Destroy();

                        // 视频播放完毕 → 切回大殿
                        SceneLoader.GoToRoom(Rooms.Chapter1_Hall, 0.8f);
                        yield break;
                    }
                }
                if (vp != null) vp.Destroy();
            }

            // 视频失败的退化路径:闪黑 + 切场景
            Debug.LogWarning("[ChaseCorridor] 追逐视频未找到或播放失败,退化为闪黑跳转");
            var overlay = FadeOverlayColored.Get();
            if (overlay != null)
            {
                yield return overlay.FadeToColor(Color.black, 0.5f);
            }
            SceneLoader.GoToRoom(Rooms.Chapter1_Hall, 0.8f);
        }

        static string FindChaseVideoFile()
        {
            string fileName = "chase_corridor_cutscene.mp4";
            string p1 = System.IO.Path.Combine(Application.streamingAssetsPath, "Video", fileName);
            if (System.IO.File.Exists(p1)) return p1;
            string p2 = System.IO.Path.Combine(Application.dataPath, "_Project", "Resources", "Video", fileName);
            if (System.IO.File.Exists(p2)) return p2;
            return null;
        }
    }
}
