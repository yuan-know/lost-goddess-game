// ============================================================================
//  DialogueSystem.cs —— 对白/旁白(契约 §5)  🟢
//  Show(id) 按 id 从对白表播放。验证期用运行时创建的底部文本条 + 内置示例文本;
//  正式期文案表可换成 ScriptableObject / json,接口对 B 不变。
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public static class DialogueSystem
    {
        // 简单内置对白表(验证用)。正式期从 Data/Dialogues 资源加载。
        static readonly Dictionary<string, string> _table = new Dictionary<string, string>
        {
            { Dialogues.demo_intro,  "……又是这样一扇门。我这把老骨头,还能推开它吗?" },
            { Dialogues.demo_locked, "锁着的。得先找到点亮它的办法。" },

            // ── 前言(黑屏白字) ──
            { Dialogues.prologue_0_prelude,
              "很多年后,他依然会回想起初次踏入这座神庙的那个下午。\n" +
              "而当时他太过年轻,只沉浸在找到神庙的兴奋中,并没有注意到其他。\n" +
              "于是他花了几十年去回想那些被忽略的一切。\n" +
              "而此刻,他正走在重返神庙的路上。" },

            // ── 第 0 幕 荒山野道 ──
            { Dialogues.prologue_0_01, "这么多年了,还是回到了这里……" },
            { Dialogues.prologue_0_02, "什么都没变……这里的时间好像静止了……只有我,老得快要死了。" },
            { Dialogues.prologue_0_03, "它也一直在这里……几十年来,它好像一直在看着我……" },

            // ── 第一幕 神庙门厅 ──
            { Dialogues.prologue_1_01,           "为什么什么声音都没有,风声、雷声好像全被隔绝在这座庙之外了……太安静了……" },
            { Dialogues.prologue_1_02,           "门是锁死的,这好像缺了什么东西。这个形状是……" },
            { Dialogues.prologue_1_insight_hint, "(按 Q 使用【岁月洞察】)" },
            { Dialogues.prologue_1_mural,        "……墙上浮现出的图腾,像是从远古传来的低语。" },
            { Dialogues.prologue_1_upper_glow,   "……上面那里,有什么东西在闪着幽光。" },
            { Dialogues.prologue_1_cant_climb,   "这腿爬不上去了……先在周围看看有没有什么能用的吧。" },
            { Dialogues.prologue_1_door_locked,  "门是锁死的,现在推不开。" },
            { Dialogues.prologue_1_need_relic,   "我需要的东西,应该在上面……或者,先在周围看看有没有什么能用的吧。" },

            // ── 第二幕 青年 ──
            { Dialogues.prologue_2_awake,           "刚刚发生了什么?" },
            { Dialogues.prologue_2_resolve,         "算了,不管了。好不容易找到神庙,我得赶快解开门锁,找到神像!" },
            { Dialogues.prologue_2_got_crowbar,     "这玩意儿倒挺顺手的,这里好像没有其他东西能用了。我再去上面看看吧。" },
            { Dialogues.prologue_2_pry_cage,        "咔——铁笼被撬开了。" },
            { Dialogues.prologue_2_got_base,        "这是什么东西。等等,这个底座……好像和展台的形状挺像的。" },
            { Dialogues.prologue_2_assemble_fail,   "你无法完成精密仪器拼接!" },
            { Dialogues.prologue_2_gear_sound,     "这个声音……" },
            { Dialogues.prologue_2_wrong_era_middle,"该死,怎么拼接不起来。算了。再看看有没有其他的东西能用吧。" },

            // ── 第三幕 中年 ──
            { Dialogues.prologue_3_awake,          "奇怪,这是哪?我不是在修表吗。" },
            { Dialogues.prologue_3_combine,        "这两个,组合一下就可以得到……" },
            { Dialogues.prologue_3_projector_use,  "但这个在这里有什么用呢。" },
            { Dialogues.prologue_3_go_downstairs,  "还是先下楼看看吧。" },

            // ── 第四幕 大门开启 & 剧情杀 ──
            { Dialogues.prologue_4_door_open,   "大门缓缓开启,门后通道却并没有变得明亮,通道的尽头,有黑雾正在凝聚成形……" },
            { Dialogues.prologue_4_dark_fog,    "不对……那是什么,那是什么!" },
            { Dialogues.prologue_4_frozen,      "你转身想跑,但两条腿像灌了铅一样无法动弹……" },
            { Dialogues.prologue_4_switch_hint, "(按 1 切换青年)" },
            { Dialogues.prologue_4_died,        "怎么就这么……死了?" },
            { Dialogues.prologue_4_awakening,   "等等,死了还能想这件事吗?" },

            // ── Era 不匹配的通用提示 ──
            { Dialogues.wrong_era_need_young,  "这需要一双灵活的手,或者强壮的臂膀。此刻我做不到。" },
            { Dialogues.wrong_era_need_middle, "你毛躁的双手无法完成精密的拼接。" },
            { Dialogues.wrong_era_need_old,    "你还没有那双能看穿岁月的眼睛。" },
        };

        static DialogueUI _ui;

        static void EnsureUI()
        {
            if (_ui != null) return;
            _ui = DialogueUI.CreateAttached();
        }

        public static void Show(string dialogueId) => Show(dialogueId, null);

        public static void Show(string dialogueId, Action onFinish)
        {
            EnsureUI();
            string text = _table.TryGetValue(dialogueId, out var t) ? t : dialogueId; // 找不到就直接显示 id 文本
            _ui.Play(text, onFinish);
        }

        /// <summary>直接显示一段文本(不走对白表)。</summary>
        public static void ShowText(string text, Action onFinish = null)
        {
            EnsureUI();
            _ui.Play(text, onFinish);
        }
    }

    /// <summary>运行时创建的底部对白条(纯代码,零预制体)。点击/等待后消失。</summary>
    public class DialogueUI : MonoBehaviour
    {
        Text _label;
        CanvasGroup _cg;
        Action _onFinish;
        Coroutine _routine;

        public static DialogueUI CreateAttached()
        {
            var go = new GameObject("~DialogueUI");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // 底部半透明条:高度 160(容 3 行 34px 字)、alpha 0.45(不切画面感)
            // 上边缘用一个 8px 高的更浅条做柔和过渡,避免"硬切"感
            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(go.transform, false);
            var barImg = barGo.AddComponent<Image>();
            barImg.color = new Color(0, 0, 0, 0.45f);
            var brt = barImg.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.sizeDelta = new Vector2(0, 160);
            brt.anchoredPosition = Vector2.zero;

            // 文本
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(barGo.transform, false);
            var label = txtGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.color = new Color(1f, 0.96f, 0.9f);   // 微暖白
            label.alignment = TextAnchor.MiddleCenter;   // 剧本感:居中
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            // 加轻微描边,让字幕在浅色画面上也读得清
            var outline = txtGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(120, 20);
            lrt.offsetMax = new Vector2(-120, -20);

            var ui = go.AddComponent<DialogueUI>();
            ui._label = label;
            ui._cg = go.AddComponent<CanvasGroup>();
            ui._cg.alpha = 0f;
            ui._cg.blocksRaycasts = false;
            return ui;
        }

        public void Play(string text, Action onFinish)
        {
            if (_routine != null) StopCoroutine(_routine);
            _onFinish = onFinish;
            _label.text = text;
            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // 淡入 0.25s
            _cg.blocksRaycasts = true;
            yield return FadeAlpha(0f, 1f, 0.25f);

            // 最短保底显示时长
            float minShow = 0.4f;
            float t = 0f;
            while (t < minShow) { t += Time.deltaTime; yield return null; }

            // 等玩家点击继续
            while (!Input.GetMouseButtonDown(0)) yield return null;

            // 淡出 0.2s
            yield return FadeAlpha(1f, 0f, 0.2f);
            _cg.blocksRaycasts = false;

            var cb = _onFinish;
            _onFinish = null;
            _routine = null;
            cb?.Invoke();
        }

        IEnumerator FadeAlpha(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                _cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            _cg.alpha = to;
        }
    }
}
