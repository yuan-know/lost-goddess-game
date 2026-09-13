// ============================================================================
//  GearDialDebugBootstrap.cs —— 齿轮旋钮谜题独立调试场景  (2026-09-13)
//
//  挂在空场景里唯一的 GameObject 上，Play 后：
//    1) 建相机（正交，铺满）
//    2) 打开 GearDialCloseup 谜题（不走展台大厅流程，不切场景）
//    3) 左上角调试 HUD：齿轮/旋钮角度、5 个道具的 base/rot/实际角度与误差
//    4) 可开一条 45° 目标线（含 ±8° 容差楔形），用来核对"指针视觉上是否真的指向 45°"
//       —— 也就是校准 kPropBaseAngles 的现场工具
//
//  操作：
//    拖「大齿轮」        → 切换当前指针界面（松手会吸附到档位）
//    拖「小旋钮」        → 旋转当前道具的指针
//    ← / →              → 齿轮 ±72°（走一档）
//    ↑ / ↓              → 当前指针 ±2°（微调）
//    1 ~ 5              → 直接跳到某个槽位
//    S                  → 直接解锁当前槽位（走咬合动画）
//    R                  → 重置谜题（重新随机 5 个道具）
//    T                  → 显示 / 隐藏 45° 目标线
//    G / H              → 显示 / 隐藏 HUD
//    N                  → 重新打开谜题（关掉后用它找回）
//    Esc                → 关闭特写
//    [ ]                → 磁吸强度 − / +          （核心手感旋钮）
//    ; '                → 齿轮跟手平滑 − / +
//    , .                → 松手吸附时长 − / +
//    - =                → 旋钮平滑时间 − / +
//    9 0                → 旋钮圆心死区 − / +（默认 12px，调大就会"转不动"）
//
//  生成场景：Unity 菜单 ▸ 失落的女神 ▸ 生成齿轮旋钮调试场景
// ============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public class GearDialDebugBootstrap : MonoBehaviour
    {
        Camera _cam;
        Text _hud;
        bool _showHud = true;

        GameObject _guideRoot;
        bool _showGuide;

        bool _solvedAll;

        void Start()
        {
            EnsureCamera();

            // 调试场景不播开门过场（否则会把特写关掉、黑屏）
            GearDialCloseup.SuppressCutscene = true;

            // 让音效可用（没有音频文件时 AudioManager 会静默）
            AudioManager.Init(this);

            OpenPuzzle();
            BuildHud();
            BuildGuide();

            Debug.Log("[GearDialDebug] 拖齿轮=切道具，拖旋钮=转指针；R 重置，T 目标线，[ ] 调磁吸。");
        }

        void OpenPuzzle()
        {
            _solvedAll = false;
            GearDialCloseup.Show(() =>
            {
                _solvedAll = true;
                Debug.Log("[GearDialDebug] ★ 5 个道具全部对齐 —— 谜题完成（调试模式不切场景）。按 R 重来。");
            });
            CloseupView.SetCanClose(true);   // 调试场景允许点背景/Esc 关闭
        }

        void EnsureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                _cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            _cam.orthographic = true;
            _cam.orthographicSize = 5.4f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.045f, 0.04f, 1f);
        }

        // ── HUD ────────────────────────────────────────────────────────────
        void BuildHud()
        {
            var go = new GameObject("~GearDialDebugHUD");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;      // 盖在特写层(1050)之上
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();

            var tgo = new GameObject("Hint");
            tgo.transform.SetParent(go.transform, false);
            _hud = tgo.AddComponent<Text>();
            _hud.font = GameFonts.Primary;
            _hud.fontSize = 19;
            _hud.color = new Color(1f, 0.92f, 0.7f, 1f);
            _hud.alignment = TextAnchor.UpperLeft;
            _hud.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hud.verticalOverflow = VerticalWrapMode.Overflow;
            _hud.raycastTarget = false;      // 别挡住拖拽
            var rt = _hud.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -18f);
            rt.sizeDelta = new Vector2(1500f, 320f);

            var shadow = tgo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // ── 45° 目标线 + ±8° 容差楔形（核对指针视觉朝向 / 校准 baseAngle）──
        void BuildGuide()
        {
            _guideRoot = new GameObject("~GearDialGuide");
            var canvas = _guideRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;      // 特写之上、HUD 之下
            var scaler = _guideRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _guideRoot.AddComponent<GraphicRaycaster>();

            // 容差边界线(45° ± 8°)
            MakeRay("EdgeLow", 45f - 8f, new Color(0.5f, 0.9f, 1f, 0.28f), 520f, 2f);
            MakeRay("EdgeHigh", 45f + 8f, new Color(0.5f, 0.9f, 1f, 0.28f), 520f, 2f);
            // 目标线
            MakeRay("Target", 45f, new Color(1f, 0.35f, 0.25f, 0.75f), 560f, 3f);

            // 中心圆点
            var dot = new GameObject("Center");
            dot.transform.SetParent(_guideRoot.transform, false);
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = new Color(1f, 0.4f, 0.3f, 0.9f);
            dotImg.raycastTarget = false;
            var drt = dotImg.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(12f, 12f);

            _guideRoot.SetActive(false);
        }

        /// <summary>在容器中心沿「时钟角」方向画一条射线(0°=12点,顺时针为正)。</summary>
        void MakeRay(string name, float clockDeg, Color color, float length, float thickness)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_guideRoot.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);   // 左端对准容器中心
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(length, thickness);
            // 时钟角 θ(0=上,顺时针) 的方向向量 = (sinθ, cosθ)
            // Unity z 旋转 φ 为正时方向 = (cosφ, sinφ) → φ = 90° - θ
            rt.localRotation = Quaternion.Euler(0f, 0f, 90f - clockDeg);
        }

        // ── 键盘调试 ────────────────────────────────────────────────────────
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.G) || Input.GetKeyDown(KeyCode.H))
            {
                _showHud = !_showHud;
                if (_hud != null) _hud.enabled = _showHud;
            }

            if (Input.GetKeyDown(KeyCode.T) && _guideRoot != null)
            {
                _showGuide = !_showGuide;
                _guideRoot.SetActive(_showGuide);
            }

            if (Input.GetKeyDown(KeyCode.N)) OpenPuzzle();
            if (Input.GetKeyDown(KeyCode.Escape)) CloseupView.Close();

            // 手感参数(核心调参入口)
            if (Input.GetKeyDown(KeyCode.LeftBracket))
                GearDialCloseup.GearMagnetStrength = Mathf.Clamp01(GearDialCloseup.GearMagnetStrength - 0.05f);
            if (Input.GetKeyDown(KeyCode.RightBracket))
                GearDialCloseup.GearMagnetStrength = Mathf.Clamp01(GearDialCloseup.GearMagnetStrength + 0.05f);
            if (Input.GetKeyDown(KeyCode.Semicolon))
                GearDialCloseup.GearFollowSmoothTime = Mathf.Max(0f, GearDialCloseup.GearFollowSmoothTime - 0.01f);
            if (Input.GetKeyDown(KeyCode.Quote))
                GearDialCloseup.GearFollowSmoothTime = Mathf.Min(0.5f, GearDialCloseup.GearFollowSmoothTime + 0.01f);
            if (Input.GetKeyDown(KeyCode.Comma))
                GearDialCloseup.GearSnapTime = Mathf.Max(0.02f, GearDialCloseup.GearSnapTime - 0.02f);
            if (Input.GetKeyDown(KeyCode.Period))
                GearDialCloseup.GearSnapTime = Mathf.Min(0.6f, GearDialCloseup.GearSnapTime + 0.02f);
            if (Input.GetKeyDown(KeyCode.Minus))
                GearDialCloseup.KnobSmoothTime = Mathf.Max(0f, GearDialCloseup.KnobSmoothTime - 0.01f);
            if (Input.GetKeyDown(KeyCode.Equals))
                GearDialCloseup.KnobSmoothTime = Mathf.Min(0.5f, GearDialCloseup.KnobSmoothTime + 0.01f);
            // 旋钮圆心死区(别调大:可见半径只有 ~62px,超过 ~20px 就会"转不动")
            if (Input.GetKeyDown(KeyCode.Alpha9))
                GearDialCloseup.KnobMinRadiusPx = Mathf.Max(0f, GearDialCloseup.KnobMinRadiusPx - 4f);
            if (Input.GetKeyDown(KeyCode.Alpha0))
                GearDialCloseup.KnobMinRadiusPx = Mathf.Min(60f, GearDialCloseup.KnobMinRadiusPx + 4f);

            var bh = FindObjectOfType<GearDialCloseup.GearDialBehaviour>();
            if (bh == null)
            {
                if (_hud != null && _showHud)
                    _hud.text = "齿轮旋钮谜题 · 调试场景\n\n（特写已关闭）按 N 重新打开，Esc 关闭。";
                return;
            }

            // 键盘操作
            if (Input.GetKeyDown(KeyCode.LeftArrow)) bh.DebugNudgeGear(-72f);
            if (Input.GetKeyDown(KeyCode.RightArrow)) bh.DebugNudgeGear(72f);
            if (Input.GetKeyDown(KeyCode.UpArrow)) bh.DebugNudgeCurrent(2f);
            if (Input.GetKeyDown(KeyCode.DownArrow)) bh.DebugNudgeCurrent(-2f);
            if (Input.GetKeyDown(KeyCode.S)) bh.DebugSolveCurrent();
            if (Input.GetKeyDown(KeyCode.R)) { bh.DebugReset(); _solvedAll = false; }
            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) bh.DebugJumpToSlot(i);
            }

            if (_hud != null && _showHud) _hud.text = BuildHudText(bh);
        }

        string BuildHudText(GearDialCloseup.GearDialBehaviour bh)
        {
            var sb = new System.Text.StringBuilder(1024);
            string curName = ((GearDialCloseup.PropSlot)bh.CurrentSlot).ToString();
            sb.AppendLine("齿轮旋钮谜题 · 调试场景" + (_solvedAll ? "    ★ 全部对齐!" : ""));
            sb.AppendLine(
                $"槽位 {bh.CurrentSlot + 1}/5 ({curName})   " +
                $"齿轮 逻辑 {bh.GearAngle,6:F1}°  视觉 {bh.GearVisual,6:F1}°   " +
                $"旋钮 {bh.KnobAngle,7:F1}°");
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("道具            base      rot      实际    误差45°   状态");
            for (int i = 0; i < bh.PropCount; i++)
            {
                string mark = (i == bh.CurrentSlot) ? ">" : " ";
                string state = bh.IsSolved(i) ? "已锁定" : (bh.IsLocking(i) ? "咬合中" : "-");
                string propName = ((GearDialCloseup.PropSlot)i).ToString();
                sb.AppendLine(
                    $"{mark}{i} {propName,-11} " +
                    $"{GearDialCloseup.GetBaseAngle(i),7:F1}  {bh.GetRotation(i),7:F1}  " +
                    $"{bh.GetActualAngle(i),7:F1}  {bh.AngleErrorToWin(i),7:F1}   {state}");
            }
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine(
                $"手感  磁吸 {GearDialCloseup.GearMagnetStrength:F2} [ [ ] ]   " +
                $"跟手 {GearDialCloseup.GearFollowSmoothTime*1000f:F0}ms [ ; ' ]   " +
                $"吸附 {GearDialCloseup.GearSnapTime*1000f:F0}ms [ , . ]   " +
                $"旋钮平滑 {GearDialCloseup.KnobSmoothTime*1000f:F0}ms [ - = ]   " +
                $"旋钮死区 {GearDialCloseup.KnobMinRadiusPx:F0}px [ 9 0 ]");
            sb.AppendLine(
                "拖齿轮=切道具  拖旋钮=转指针  |  ←→ 齿轮±72°  ↑↓ 指针±2°  1-5 跳档  S 解锁  R 重置  " +
                "T 目标线" + (_showGuide ? "(开)" : "(关)") + "  G HUD  N 重开  Esc 关闭");
            return sb.ToString();
        }
    }
}
