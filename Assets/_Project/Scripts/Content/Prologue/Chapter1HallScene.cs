// ============================================================================
//  Chapter1HallScene.cs —— 序幕之后【第一章 · 神庙大殿】(占位)
//
//  剧情:老人被剧情杀 → 觉醒 → 落地大殿。这是**主线起点**,但主线内容(第一章)
//  还没开发,当前只作为"序幕落幕、玩家能看到自己在一个新地方"的占位。
//
//  设计:
//    · 背景用 **神庙前厅内部 TempleFoyer**(bg_unlit_full 关灯版,阴森初进氛围)
//      —— 剧本序列 = 黑暗森林 → 神庙入口 TempleGate(门外)→ 推门 → **本场景 内景 TempleFoyer**
//    · 老人(当前 Era)落在场景中央
//    · 首帧强制清一次 FadeOverlayColored(第四幕黑幕单例,防止残留)
//    · 一句独白 "……这里是哪里?"
//    · 提示:P/L 打印/输出状态。之后主线内容开发,这个 Director 就替换掉。
//
//  ⚠ 关键作用:第四幕 GoToScene(Chapter1_Hall) 若找不到该房间,SceneLoader
//    会 LogWarning 什么都不建,加上 FadeOverlayColored 黑幕未清,玩家看到永久黑屏。
//    本文件是"最小可玩落地",保证剧情杀之后还能继续。
// ============================================================================

using System.Collections;
using UnityEngine;

namespace LostGoddess.Content
{
    public static class Chapter1HallScene
    {
        public const float SpawnX = 0f;

        public static void Build()
        {
            // 场景:神庙前厅内部 TempleFoyer(bg_unlit_full 关灯版)
            //   —— 剧本序列 = 黑暗森林 → 神庙入口 TempleGate(外)→ 推门 → **本场景 内景 TempleFoyer**
            //   等策划确认亮灯触发条件后再接开灯版(bg_lit_full / prop_podium_lit)
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.TempleFoyer);
            room.name = "Room_" + Rooms.Chapter1_Hall;
            float groundY = SceneRoomBuilder.TempleFoyer.groundY;

            PlayerBuilder.Build(GameState.CurrentEra, groundY, SpawnX);

            room.AddComponent<Chapter1HallDirector>();
        }
    }

    /// <summary>Chapter1_Hall 首帧 Director:强制清黑幕 + 一句独白。</summary>
    public class Chapter1HallDirector : MonoBehaviour
    {
        void Start()
        {
            // 双重保险:主动清一次 FadeOverlayColored(第四幕死亡黑幕单例遗留)
            //  即使前一场景 Cutscene 没走完 FadeToClear,这里也把它拉回透明
            var overlay = FadeOverlayColored.Get();
            StartCoroutine(overlay.FadeToClear(0.4f));

            // 保证角色可控(以防 Cutscene 遗留了 SetControllable(false))
            var pc = PlayerController.Instance;
            if (pc != null) pc.SetControllable(true);

            // 首帧独白(只播一次)
            const string kFlag = "chapter1_hall_entered";
            if (GameState.GetFlag(kFlag)) return;
            GameState.SetFlag(kFlag, true);

            StartCoroutine(PlayIntro());
        }

        IEnumerator PlayIntro()
        {
            yield return new WaitForSeconds(0.8f);
            bool done = false;
            DialogueSystem.ShowText("……这里是哪里?我不是……死了吗?", () => done = true);
            while (!done) yield return null;

            done = false;
            DialogueSystem.ShowText("(主线第一章占位。序幕已完结,下一步开发主线内容。)", () => done = true);
            while (!done) yield return null;
        }
    }
}
