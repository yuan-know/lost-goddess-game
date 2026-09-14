// ============================================================================
//  EyeCorridorLaserDebugBootstrap.cs —— S05 千眼回廊 · 躲光束 独立调试场景 (2026-09-14 重做)
//
//  挂在空场景里唯一的 GameObject 上,Play 后:
//    1) 建 GameManager(若无)+ 相机(正交 orthoSize=6 + 信箱黑边)+ ClickInputManager
//    2) 用 SceneRoomBuilder.EyeCorridor 建出长廊(复用 ChaseCorridor 美术;
//       拱门暗影层 bg_gate_shadow 由 SceneRoomBuilder 的 mid 层加载)
//    3) 建青年形态的提尔(PlayerBuilder),摆在长廊最左端
//    4) 起 EyeCorridorLaserPuzzle:铜眼射出锥形光束左右摆荡,躲着走到最右
//    5) 左上角调试 HUD(**默认隐藏** —— 正式观感不该有调试文字)
//
//  ★ 玩法已整体推翻重做(旧版石像瞬移/3 秒暴露/眼睑动画全部废弃)
//    规则见 EyeCorridorLaserPuzzle.cs 文件头。
//
//  生成场景:Unity 菜单 ▸ 失落的女神 ▸ 生成千眼回廊激光调试场景
//            (Assets/_Project/Scenes/EyeCorridorLaserDebug.unity,纯调试不进正式包)
//
//  操作:
//    鼠标点地面 → 走过去
//    走到右端   → 通关(盲僧赠符文)
//    ← / →      → 光带扫动速度 − / +
//    [ / ]      → 地面危险带半宽 − / +
//    P          → 暂停 / 继续 光带扫动
//    T          → 调试判定区可视化(绿=拱门安全区,红=光带危险区)开关
//    N          → 光区片数 2 ↔ 1(对比"一片 / 两片"的观感与难度)
//    Y / U      → 光锥整体亮度 − / +      (散光质感)
//    , / .      → 暗影调光(角色躲进拱门时的压暗倍率) − / +
//    M          → 死亡判词 开 / 关
//    K          → 强制被抓一次(测白光回退 + 判词)
//    R          → 重置(人回最左端、被抓次数清零、不重播开场)
//    I          → 重播开场(旁白+盲僧+青年那句)
//    1 ~ 8      → 瞬移到第 N 个拱门安全区
//    G / H      → 唤出 / 隐藏 调试 HUD(默认隐藏)
// ============================================================================

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using LostGoddess.Content;

namespace LostGoddess
{
    public class EyeCorridorLaserDebugBootstrap : MonoBehaviour
    {
        Text _hud;
        bool _showHud = false;    // 默认隐藏:游戏画面不再显示调试文字,需要调参时按 G/H 唤出
        EyeCorridorLaserPuzzle _pz;

        void Start()
        {
            EnsureGameManager();
            PlayerBuilder.EnableAutoRebuild();
            AudioManager.Init(this);

            GameState.CurrentRoom = Rooms.Chapter1_EyeCorridor;
            GameState.CurrentEra = Era.Young;

            BuildCamera();
            LetterboxOverlay.Ensure();
            BuildRoomAndPlayer();

            _pz = EyeCorridorLaserPuzzle.Instance;
            if (_pz == null) Debug.LogError("[EyeCorridorDebug] 谜题没起来(EyeCorridorLaserPuzzle.Instance == null)。");

            BuildHud();
            Debug.Log("[EyeCorridorDebug] 躲光束:铜眼射出的锥形光束左右扫,被照到就退回最左端;"
                    + "拱门暗影内是安全点。G/H 唤出调试 HUD(默认隐藏)。");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.H))
            {
                _showHud = !_showHud;
                if (_hud != null) _hud.enabled = _showHud;
            }

            if (_pz == null) _pz = EyeCorridorLaserPuzzle.Instance;
            if (_pz == null) return;

            // ── 手感参数(Shift 按住 = 细调) ──
            float d = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 0.25f : 1f;
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                EyeCorridorLaserPuzzle.BeamGroundSpeed = Mathf.Max(1f, EyeCorridorLaserPuzzle.BeamGroundSpeed - 1f * d);
            if (Input.GetKeyDown(KeyCode.RightArrow))
                EyeCorridorLaserPuzzle.BeamGroundSpeed = Mathf.Min(40f, EyeCorridorLaserPuzzle.BeamGroundSpeed + 1f * d);
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                EyeCorridorLaserPuzzle.BeamGroundHalfWidth = Mathf.Max(0.4f, EyeCorridorLaserPuzzle.BeamGroundHalfWidth - 0.25f * d);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                EyeCorridorLaserPuzzle.BeamGroundHalfWidth = Mathf.Min(10f, EyeCorridorLaserPuzzle.BeamGroundHalfWidth + 0.25f * d);

            // ── 开关 ──
            if (Input.GetKeyDown(KeyCode.P))
                EyeCorridorLaserPuzzle.BeamPaused = !EyeCorridorLaserPuzzle.BeamPaused;
            if (Input.GetKeyDown(KeyCode.T))
            {
                EyeCorridorLaserPuzzle.DebugZones = !EyeCorridorLaserPuzzle.DebugZones;
                _pz.DebugSetZones(EyeCorridorLaserPuzzle.DebugZones);
            }
            if (Input.GetKeyDown(KeyCode.N))
            {
                EyeCorridorLaserPuzzle.BeamCount = EyeCorridorLaserPuzzle.BeamCount == 2 ? 1 : 2;
                Debug.Log("[EyeCorridorDebug] 光区片数 → " + EyeCorridorLaserPuzzle.BeamCount
                        + "(改片数要重进场景才会重建光束)");
            }
            if (Input.GetKeyDown(KeyCode.Y))
                EyeCorridorLaserPuzzle.BeamAlpha = Mathf.Max(0.02f, EyeCorridorLaserPuzzle.BeamAlpha - 0.02f * d);
            if (Input.GetKeyDown(KeyCode.U))
                EyeCorridorLaserPuzzle.BeamAlpha = Mathf.Min(0.60f, EyeCorridorLaserPuzzle.BeamAlpha + 0.02f * d);
            if (Input.GetKeyDown(KeyCode.Comma))
                EyeCorridorLaserPuzzle.ShadowDim = Mathf.Max(0.10f, EyeCorridorLaserPuzzle.ShadowDim - 0.05f * d);
            if (Input.GetKeyDown(KeyCode.Period))
                EyeCorridorLaserPuzzle.ShadowDim = Mathf.Min(1.00f, EyeCorridorLaserPuzzle.ShadowDim + 0.05f * d);
            if (Input.GetKeyDown(KeyCode.M))
                EyeCorridorLaserPuzzle.ShowDeathVerdict = !EyeCorridorLaserPuzzle.ShowDeathVerdict;

            // ── 动作 ──
            if (Input.GetKeyDown(KeyCode.K)) _pz.DebugCatch();
            if (Input.GetKeyDown(KeyCode.R)) _pz.DebugReset();
            if (Input.GetKeyDown(KeyCode.I)) _pz.DebugRestartIntro();
            for (int i = 0; i < EyeCorridorLaserPuzzle.GateMinX.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) _pz.DebugJumpToGate(i);

            if (_hud != null && _showHud) _hud.text = BuildHudText();
        }

        // ── 系统 ──
        void EnsureGameManager()
        {
            if (GameManager.Instance == null)
            {
                var go = new GameObject("GameManager");
                go.AddComponent<GameManager>();
            }
        }

        void BuildCamera()
        {
            if (Camera.main == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                var newCam = go.AddComponent<Camera>();
                newCam.orthographic = true;
                newCam.orthographicSize = 6f;
                newCam.transform.position = new Vector3(0, 0, -10);
            }
            var cam = Camera.main;
            cam.orthographic = true;
            cam.orthographicSize = 6f;          // 12 单位图完整落进中间条带
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            float barPct = LetterboxOverlay.BarHeightPct;
            cam.rect = new Rect(0f, barPct, 1f, 1f - 2f * barPct);
            if (cam.GetComponent<ClickInputManager>() == null)
                cam.gameObject.AddComponent<ClickInputManager>();
        }

        void BuildRoomAndPlayer()
        {
            var room = SceneRoomBuilder.Build(SceneRoomBuilder.EyeCorridor);
            room.name = "Room_" + Rooms.Chapter1_EyeCorridor;
            float groundY = SceneRoomBuilder.EyeCorridor.groundY;

            var player = PlayerBuilder.Build(GameState.CurrentEra, groundY,
                                             EyeCorridorLaserPuzzle.SpawnX,
                                             yOffsetOverride: PlayerBuilder.GetChapter1YOffset(GameState.CurrentEra));
            if (player != null)
            {
                var pc = player.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.spriteFacesRight = false;   // 立绘默认朝左,这里让他朝右(往长廊深处走)
                    var s = player.transform.localScale;
                    s.x = Mathf.Abs(s.x) * -1f;
                    player.transform.localScale = s;
                    pc.SetControllable(true);
                }
            }
            else Debug.LogError("[EyeCorridorDebug] 玩家没建出来(PlayerBuilder.Build 返回 null)。");

            _pz = EyeCorridorLaserPuzzle.Build(room.transform);
        }

        // ── HUD(默认隐藏) ──
        void BuildHud()
        {
            var go = new GameObject("~EyeCorridorDebugHUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;      // 盖在光束(600)/黑边(1000)/对话(1100)之上
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            var tgo = new GameObject("Hint");
            tgo.transform.SetParent(go.transform, false);
            _hud = tgo.AddComponent<Text>();
            _hud.font = GameFonts.Primary;
            _hud.fontSize = 18;
            _hud.color = new Color(1f, 0.92f, 0.7f, 1f);
            _hud.alignment = TextAnchor.UpperLeft;
            _hud.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hud.verticalOverflow = VerticalWrapMode.Overflow;
            _hud.raycastTarget = false;
            var rt = _hud.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -18f);
            rt.sizeDelta = new Vector2(1700f, 460f);

            var shadow = tgo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            _hud.enabled = _showHud;   // 默认不出现在画面上(按 G/H 唤出)
        }

        string BuildHudText()
        {
            var sb = new StringBuilder(900);

            sb.AppendLine("千眼回廊 · 躲光束(青年线 PZ_04) · 调试场景    阶段:" + _pz.Phase);
            var beams = new StringBuilder();
            int bn = Mathf.Clamp(EyeCorridorLaserPuzzle.BeamCount, 1, 4);
            for (int i = 0; i < bn; i++)
                beams.Append($"#{i} {EyeCorridorLaserPuzzle.BeamXAt(i),7:F2}"
                           + (EyeCorridorLaserPuzzle.BeamDirAt(i) > 0 ? "→  " : "←  "));
            sb.AppendLine($"光区 {bn} 片: {beams}");
            sb.AppendLine(
                $"提尔X {_pz.PlayerX,7:F2}   " +
                $"在拱门暗影 {(_pz.PlayerInGate ? "是(安全)" : "否")}   " +
                $"被照到 {(_pz.PlayerLit ? "★是" : "否")}   " +
                $"角色亮度 ×{_pz.ShadowFactor:F2}   累计被抓 {_pz.CaughtCount}");
            sb.AppendLine(
                $"光带速度 {EyeCorridorLaserPuzzle.BeamGroundSpeed:F1} 单位/秒 [ ← → ]   " +
                $"危险带半宽 {EyeCorridorLaserPuzzle.BeamGroundHalfWidth:F2} " +
                $"(总宽 {EyeCorridorLaserPuzzle.BeamGroundHalfWidth * 2f:F2} ≈ 三个拱门) [ [ ] ]   " +
                $"暂停 {(EyeCorridorLaserPuzzle.BeamPaused ? "开" : "关")} [P]");
            sb.AppendLine(
                $"光锥亮度 {EyeCorridorLaserPuzzle.BeamAlpha:F2} [Y/U]   " +
                $"暗影调光 ×{EyeCorridorLaserPuzzle.ShadowDim:F2} [ , . ]   " +
                $"片数 {bn} [N]   判词 {(EyeCorridorLaserPuzzle.ShowDeathVerdict ? "开" : "关")} [M]");
            float prog = (_pz.PlayerX - EyeCorridorLaserPuzzle.SpawnX) /
                         (EyeCorridorLaserPuzzle.ExitX - EyeCorridorLaserPuzzle.SpawnX) * 100f;
            sb.AppendLine(
                $"出生 {EyeCorridorLaserPuzzle.SpawnX:F2}   出口 {EyeCorridorLaserPuzzle.ExitX:F2}   " +
                $"复位光带 ±{EyeCorridorLaserPuzzle.BeamResetX:F1}   进度 {prog:F0}%");
            sb.AppendLine("拱门安全区(8 个,实测自 bg_gate_shadow.png):");
            for (int i = 0; i < EyeCorridorLaserPuzzle.GateMinX.Length; i++)
            {
                float c = (EyeCorridorLaserPuzzle.GateMinX[i] + EyeCorridorLaserPuzzle.GateMaxX[i]) * 0.5f;
                sb.Append($"  {i + 1}:{c,7:F2}  ");
                if (i % 4 == 3) sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine(
                "1-8 跳安全区   K 强制被抓   R 重置   I 重播开场   " +
                "T 判定区" + (EyeCorridorLaserPuzzle.DebugZones ? "(开)" : "(关)") + "   G 隐藏HUD");
            return sb.ToString();
        }
    }
}
