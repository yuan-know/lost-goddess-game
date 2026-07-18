// ============================================================================
//  PrologueChaseScene.cs —— 第四幕【黑雾追击 → 剧情杀】(D7)
//
//  剧本(docs/序幕落地清单.md §2.7 + docs/序幕脚本.md 第四幕):
//    大门开 → 门后黑影凝聚 → 老人两条腿灌铅无法动弹 → 提示按 1 切青年 →
//    青年逃跑向右 → 黑幕 → 白字"怎么就这么死了" → 觉醒 → 去大殿
//
//  D7 落地:
//    · 背景先用 TempleEntry 视差(暗色调,后期换 Chase 专门画布)
//    · 老年在门厅右侧被冻住,黑雾从门口涌入(用一个渐红/渐黑 overlay)
//    · Cutscene 步骤:
//      1) 落地即淡入 + Say(dark_fog)"不对……那是什么"
//      2) Say(frozen)"你转身想跑,但两条腿灌了铅"
//      3) 屏幕抖 + Say(switch_hint) → AwaitKey(Alpha1)
//      4) SwitchEra(Young, flash) → 加速右跑动画(Walk+SetControllable) → Fade(黑, 2s)
//      5) Say(died) 黑屏白字 → SetFlag(DeathCutscene/Achievement)
//      6) Say(awakening)"死了还能想这件事吗?"
//      7) GoToScene(Chapter1_Hall)
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class PrologueChaseScene
    {
        public const float SpawnX = 6f;   // 老人被冻在门前,靠近石门
        public const float RunEndX = 14f; // 青年逃跑目标

        public static void Build()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleEntry);
            room.name = "Room_" + Rooms.Prologue_Chase;
            float groundY = SceneRoomBuilder.TempleEntry.groundY;

            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            // 黑雾 overlay(纯代码占位:一块深紫黑色方块从右侧涌入)
            BuildDarkFog(room.transform, groundY);

            // Cutscene 挂根节点
            room.AddComponent<PrologueChaseDirector>();
        }

        static void BuildDarkFog(Transform parent, float groundY)
        {
            var go = new GameObject("DarkFog");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(11f, groundY + 2.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SolidSprite();
            sr.color = new Color(0.08f, 0.04f, 0.12f, 0f);   // 初始不可见,由 Director 淡入
            sr.sortingOrder = 55;   // 在近景 60 之下、老人之上
            go.transform.localScale = new Vector3(8f, 6f, 1f);
            go.AddComponent<DarkFogPulse>();
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

    /// <summary>黑雾脉动:随时间左右微幅飘,alpha 由 Director 控。</summary>
    public class DarkFogPulse : MonoBehaviour
    {
        SpriteRenderer _sr;
        float _t;
        float _baseX;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _baseX = transform.position.x;
        }

        void Update()
        {
            _t += Time.deltaTime;
            var p = transform.position;
            p.x = _baseX + Mathf.Sin(_t * 1.3f) * 0.3f;
            transform.position = p;
        }

        public void FadeIn(float duration = 1.5f, float targetAlpha = 0.85f)
        {
            StartCoroutine(FadeCoroutine(0f, targetAlpha, duration));
        }

        IEnumerator FadeCoroutine(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                var c = _sr.color;
                c.a = Mathf.Lerp(from, to, k);
                _sr.color = c;
                yield return null;
            }
        }
    }

    /// <summary>第四幕导演:Awake 后即播 Cutscene。</summary>
    public class PrologueChaseDirector : MonoBehaviour
    {
        Cutscene _cs;

        void Start()
        {
            // 每次重进本场景都从头播(不做 flag 短路,剧情杀是唯一叙事段)
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(false);

            _cs = gameObject.AddComponent<Cutscene>();

            _cs.Add(new WaitStep(0.5f))
               .Add(new SayStep(Dialogues.prologue_4_dark_fog))    // "不对……那是什么"
               .Add(new WaitStep(0.3f));

            // 黑雾涌入:自定义 step 走 DarkFog 淡入
            _cs.Add(new FadeInDarkFogStep());

            _cs.Add(new SayStep(Dialogues.prologue_4_frozen))       // "两条腿像灌了铅"
               .Add(new ScreenshakeStep(0.3f, 0.6f))
               .Add(new SayStep(Dialogues.prologue_4_switch_hint))  // "(按 1 切换青年)"
               .Add(new AwaitKeyStep(KeyCode.Alpha1))
               .Add(new SwitchEraStep(Era.Young, flash: true))
               .Add(new WaitStep(0.15f));

            // 青年向右狂奔
            _cs.Add(new WalkPlayerToStep(PrologueChaseScene.RunEndX));

            // 黑幕(2s) + 白字"死了"
            _cs.Add(new FadeStep(Color.black, 1.5f, FadeType.In))
               .Add(new SayStep(Dialogues.prologue_4_died))
               .Add(new SayStep(Dialogues.prologue_4_awakening))
               .Add(new SetFlagStep(Flags.Prologue_DeathCutscene, true))
               .Add(new SetFlagStep(Flags.Achievement_ReturnToPast, true))
               // 序幕结束 → 主线大殿(占位场景,SceneLoader 找不到会警告,可先不建)
               .Add(new GoToSceneStep(Rooms.Chapter1_Hall, 1.0f));

            _cs.Play();
        }
    }

    /// <summary>黑雾淡入 step:找场景里的 DarkFog 组件调 FadeIn。</summary>
    public class FadeInDarkFogStep : CutsceneStep
    {
        public override IEnumerator Execute(Cutscene owner)
        {
            var fog = Object.FindObjectOfType<DarkFogPulse>();
            if (fog == null) yield break;
            fog.FadeIn(1.5f, 0.85f);
            yield return new WaitForSeconds(1.5f);
        }
    }
}
