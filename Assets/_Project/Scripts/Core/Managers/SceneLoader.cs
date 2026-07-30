// ============================================================================
//  SceneLoader.cs —— 场景/房间切换(含协程淡入淡出)  🟢  契约 §4
//  B 只调用 GoToRoom(name)。系统内部处理淡入淡出与加载。
//
//  加载策略(两条路,自动选择):
//   1) 若该房间名在 Build Settings 里有对应 .unity 场景 → SceneManager.LoadScene。
//   2) 否则若注册了"程序化房间构建器"(验证阶段无美术、无 .unity 时用)→ 调用它重建内容。
//  美术/正式场景就绪后走 (1),验证期走 (2),接口对 B 一致。
// ============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostGoddess
{
    public static class SceneLoader
    {
        static MonoBehaviour _host;          // 协程宿主(GameManager)
        static FadeOverlay _fade;            // 运行时创建的淡入淡出遮罩
        static bool _busy;

        /// <summary>程序化房间构建器:房间名 → 在当前场景重建该房间内容。
        /// 验证期由 Bootstrap 注册。为 null 时走标准 LoadScene。</summary>
        public static Func<string, IEnumerator> ProceduralRoomBuilder;

        /// <summary>切换完成后触发(新房间已就位,淡入完毕前)。参数=新房间名。</summary>
        public static event Action<string> OnAfterLoad;

        /// <summary>暴露全屏淡入淡出遮罩,供 Cutscene 步骤等复用(白闪 / 屏幕抖等)。</summary>
        public static FadeOverlay Fade => _fade;

        /// <summary>记录进入当前场景的方向,供场景构建器决定角色出生位置和朝向。</summary>
        public static string EnterDirection { get; set; } = "right";  // 默认朝右(游戏开始时)

        /// <summary>
        /// 2026-07-24 外部(如 ScenePortal)显式指定 EnterDirection 后设为 true,
        /// Transition() 就跳过 InferEnterDirection 覆盖,尊重调用方意图。
        /// 每次 Transition 结束后自动复位为 false。
        /// 用法:SceneLoader.EnterDirection = "left"; SceneLoader.EnterDirectionOverridden = true;
        /// </summary>
        public static bool EnterDirectionOverridden { get; set; } = false;

        /// <summary>记录上一个场景名,用于判断来源方向。</summary>
        public static string PreviousRoom { get; private set; } = "";

        public const float DefaultFade = 0.4f;

        public static void Init(MonoBehaviour host)
        {
            _host = host;
            _fade = FadeOverlay.CreateAttached();
        }

        public static void GoToRoom(string sceneName) => GoToRoom(sceneName, DefaultFade);

        public static void GoToRoom(string sceneName, float fadeTime)
        {
            if (_host == null)
            {
                Debug.LogError("[SceneLoader] 未初始化(GameManager 未运行?)");
                return;
            }
            if (_busy) { Debug.LogWarning("[SceneLoader] 正在切换,忽略重复请求。"); return; }
            _host.StartCoroutine(Transition(sceneName, fadeTime));
        }

        static IEnumerator Transition(string sceneName, float fadeTime)
        {
            _busy = true;

            // 记录来源方向:通过比较场景名推断(简化版:后续可用更精确的Portal标记)
            //   2026-07-24:若 ScenePortal 已显式设置 EnterDirectionOverridden,则尊重外部意图,
            //   不做推断覆盖(避免新加房间必须回来改 InferEnterDirection 才能出生在正确位置)。
            PreviousRoom = GameState.CurrentRoom;
            if (!EnterDirectionOverridden)
                EnterDirection = InferEnterDirection(PreviousRoom, sceneName);
            EnterDirectionOverridden = false;   // 消耗一次,下次要覆盖需再显式设置

            GameState.CurrentRoom = sceneName;

            yield return _fade.FadeOut(fadeTime);

            bool sceneExistsInBuild = CanLoadScene(sceneName);
            if (sceneExistsInBuild)
            {
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (op != null && !op.isDone) yield return null;
            }
            else if (ProceduralRoomBuilder != null)
            {
                yield return ProceduralRoomBuilder(sceneName);
            }
            else
            {
                Debug.LogWarning($"[SceneLoader] 房间 '{sceneName}' 既不在 Build Settings,也无程序化构建器。");
            }

            yield return _fade.FadeIn(fadeTime);
            _busy = false;
            OnAfterLoad?.Invoke(sceneName);
        }

        /// <summary>根据来源和目标场景推断进入方向。</summary>
        static string InferEnterDirection(string from, string to)
        {
            if (string.IsNullOrEmpty(from)) return "right";  // 游戏开始,默认朝右

            // 精确匹配场景连接拓扑(2026-07-19策划切换图)
            // 左链: Foyer ↔ LadderChamber ↔ Chamber3
            // 右链: Foyer ↔ GearRoom ↔ StatueRoom
            // 二楼: LadderChamber ↑ UpperChamber ↔ UpperHall

            // === Foyer（前厅）相关 ===
            if (to == "Prologue_Foyer")
            {
                if (from == "Prologue_Gate") return "gate";           // 从石拱门进入（特殊）
                if (from == "Prologue_LadderChamber") return "left";  // 从左链回来
                if (from == "Prologue_GearRoom") return "right";      // 从右链回来
                return "right";
            }

            if (from == "Prologue_Foyer")
            {
                if (to == "Prologue_LadderChamber") return "right";   // 去左链
                if (to == "Prologue_GearRoom") return "left";         // 去右链
                if (to == Rooms.Chapter1_Hall) return "left";         // 齿轮解谜后去大殿:从左侧出生朝右
                return "right";
            }

            // === 左链：Chamber3 ↔ LadderChamber ===
            if (to == "Prologue_Chamber3" && from == "Prologue_LadderChamber")
                return "right";  // 从梯子室去陶罐间，从右边进入
            if (to == "Prologue_LadderChamber" && from == "Prologue_Chamber3")
                return "left";   // 从陶罐间回梯子室，从左边进入

            // === 右链：Foyer ↔ GearRoom ↔ StatueRoom ===
            if (to == "Prologue_GearRoom")
            {
                if (from == "Prologue_Foyer") return "right";         // 从前厅去
                if (from == "Prologue_StatueRoom") return "right";    // 从石雕室回来
                return "right";
            }

            if (to == "Prologue_StatueRoom" && from == "Prologue_GearRoom")
                return "left";   // 从齿轮间去石雕室

            if (from == "Prologue_StatueRoom" && to == "Prologue_GearRoom")
                return "right";  // 从石雕室回齿轮间

            // === 二楼：LadderChamber ↑ 二楼密室(Prologue_UpperChamber) ↔ 二楼回廊(Prologue_UpperHall) ===
            // 2026-07-23 命名修正:二楼密室用真实美术(原 UpperHall),二楼回廊暂用占位(原 UpperChamber)
            if (to == "Prologue_UpperChamber" && from == "Prologue_LadderChamber")
                return "up";     // 爬楼梯上二楼密室

            if (to == "Prologue_UpperChamber" && from == "Prologue_UpperHall")
                return "right";  // 从二楼回廊到二楼密室,从右侧进入

            if (to == "Prologue_UpperHall" && from == "Prologue_UpperChamber")
                return "left";   // 从二楼密室到二楼回廊,从左侧进入

            if (to == "Prologue_UpperHall" && from == "Prologue_LadderChamber")
                return "up";     // 爬楼梯上二楼回廊(理论上不会发生,梯子目标为二楼密室)

            if (to == "Prologue_LadderChamber" && from == "Prologue_UpperHall")
                return "down";   // 从二楼回廊回到一楼

            // === 第一章：大殿 ↔ 餐厅 ↔ 武器室 ↔ 追踪长廊 ===
            // 主线横向链: MainHall ↔ DiningHall ↔ WeaponsRoom ↔ ChaseCorridor
            if (to == Rooms.Chapter1_Hall)
            {
                // 从餐厅回来 → 从右侧进入朝左
                if (from == Rooms.Chapter1_DiningHall) return "right";
                // 从追踪长廊复活回来 → 从左侧进入朝右
                if (from == Rooms.Chapter1_ChaseCorridor) return "left";
                return "left";  // 默认从左边来(剧情杀复活后)
            }

            if (to == Rooms.Chapter1_DiningHall)
            {
                if (from == Rooms.Chapter1_Hall) return "left";
                if (from == Rooms.Chapter1_WeaponsRoom) return "right";
                return "left";
            }

            if (to == Rooms.Chapter1_WeaponsRoom)
            {
                if (from == Rooms.Chapter1_DiningHall) return "left";
                if (from == Rooms.Chapter1_ChaseCorridor) return "right";
                return "left";
            }

            if (to == Rooms.Chapter1_ChaseCorridor)
            {
                if (from == Rooms.Chapter1_WeaponsRoom) return "left";
                return "left";
            }

            // 默认
            return "right";
        }

        static bool CanLoadScene(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == sceneName) return true;
            }
            return false;
        }
    }

    /// <summary>运行时创建的全屏淡入淡出遮罩(纯代码,零预制体依赖)。</summary>
    public class FadeOverlay : MonoBehaviour
    {
        CanvasGroup _cg;

        public static FadeOverlay CreateAttached()
        {
            var go = new GameObject("~FadeOverlay");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // 盖住一切
            go.AddComponent<UnityEngine.UI.CanvasScaler>();

            var imgGo = new GameObject("Black");
            imgGo.transform.SetParent(go.transform, false);
            var img = imgGo.AddComponent<UnityEngine.UI.Image>();
            img.color = Color.black;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var fade = go.AddComponent<FadeOverlay>();
            fade._cg = go.AddComponent<CanvasGroup>();
            fade._cg.alpha = 0f;
            fade._cg.blocksRaycasts = false;
            return fade;
        }

        public IEnumerator FadeOut(float t) => FadeTo(1f, t);
        public IEnumerator FadeIn(float t)  => FadeTo(0f, t);

        IEnumerator FadeTo(float target, float dur)
        {
            _cg.blocksRaycasts = true;
            float start = _cg.alpha, e = 0f;
            if (dur <= 0f) { _cg.alpha = target; }
            else
            {
                while (e < dur)
                {
                    e += Time.unscaledDeltaTime;
                    _cg.alpha = Mathf.Lerp(start, target, e / dur);
                    yield return null;
                }
                _cg.alpha = target;
            }
            _cg.blocksRaycasts = target > 0.5f;
        }
    }
}
