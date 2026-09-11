// ============================================================================
//  SmokeSprayAutoRun.cs —— 无焦点驱动烟雾调试场景(临时工具,做完可删)
//
//  为什么要这个文件:本机工程**一直开着 Unity 编辑器**,再起一个 -batchmode 实例
//  会因 HandleProjectAlreadyOpenInAnotherInstance 直接崩(见 build_check.log)。
//  所以改为让**已经在跑的那个编辑器**自己干活:
//    [InitializeOnLoad] + EditorApplication.update 持续轮询 Temp/ 下的 flag,
//    编辑器即使失去焦点也照常轮询。flag 连点两次是没用的,见下。
//
//  三个 flag:
//    Temp/smoke_autobuild.flag -> 先退播放,再调 SmokeSprayDebugBuilder.Build()
//                                 结果写 Temp/smoke_autobuild.done
//    Temp/smoke_play.flag      -> 打开 SmokeSprayDebug.unity 并进入播放(给用户看)
//    Temp/smoke_capture.flag   -> **逐帧渲染**成 PNG 到 Temp/smoke_preview/
//                                 结果写 Temp/smoke_capture.done
//
//  实拍为什么不进播放模式(踩过的坑):
//    最早写成"OpenScene + EnterPlaymode + 采样渲染",但在**失去焦点**的编辑器里
//    进播放模式并不可靠(实测 flag 被消费了却一直没进 Play,日志里也没有
//    "开始实拍")。改成**完全在编辑模式**跑:新建一个 EditorSceneManager
//    preview scene 放临时相机 + SpriteRenderer,逐帧换 Sprite 后 Camera.Render()
//    到 RenderTexture 再 ReadPixels/EncodeToPNG。
//    好处:不依赖播放模式、不碰用户当前打开的场景(对象都建在 preview scene 里,
//    并挪到 90000 远的坐标避免串到用户的场景物体)、而且能一次出**全部 34 帧**。
//
//  ⚠️ 本文件是新脚本,第一次必须先让编辑器编译它。若编辑器没自动刷新,
//     可以先把编译好即可用的 FontDebugAutoRun 的 flag 顶一下(它内部会
//     AssetDatabase.Refresh)——但注意 FontDebugSceneBuilder 会 NewScene,
//     有风险,能用自动刷新就别用。
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    [InitializeOnLoad]
    public static class SmokeSprayAutoRun
    {
        const string BuildFlag   = "Temp/smoke_autobuild.flag";
        const string BuildDone   = "Temp/smoke_autobuild.done";
        const string PlayFlag    = "Temp/smoke_play.flag";
        const string CaptureFlag = "Temp/smoke_capture.flag";
        const string CaptureDone = "Temp/smoke_capture.done";
        const string CaptureDir  = "Temp/smoke_preview";
        // 摆进齿轮面板那版:构建 + 实拍
        const string InSceneFlag = "Temp/smoke_inscene.flag";
        const string InSceneDone = "Temp/smoke_inscene.done";
        const string SceneCapFlag = "Temp/smoke_scene_capture.flag";
        const string SceneCapDone = "Temp/smoke_scene_capture.done";
        const string SceneCapDir  = "Temp/smoke_scene_preview";

        const int W = 1440, H = 816;   // 2x:贴近用户 Game 视图分辨率,检查高倍率下才可见的纹理瑕疵
        const int PerTick = 4;              // 每个轮询 tick 渲染几帧
        const float OFF = 90000f;           // 远离用户场景的坐标

        static int s_Frame;
        static bool s_Capturing;
        static int s_CapIdx;
        static List<Sprite> s_Sprites;
        static Camera s_Cam;
        static SpriteRenderer s_Sr;
        static SpriteRenderer s_PanelSr;
        static GameObject s_EmitGo;
        static bool s_InScene;
        static string s_CapDir = "";
        static string s_CapDone = "";
        static string s_Err = "";

        static SmokeSprayAutoRun()
        {
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (++s_Frame % 15 != 0) return;            // 约每 1/4 秒查一次
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            // ---------- 1) 构建场景 ----------
            if (File.Exists(BuildFlag))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();   // 等下一帧完全退出再建
                    return;
                }
                File.Delete(BuildFlag);
                try
                {
                    string msg = SmokeSprayDebugBuilder.Build();
                    File.WriteAllText(BuildDone, "OK\n" + msg);
                    Debug.Log("[SmokeSprayAutoRun] 构建完成\n" + msg);
                }
                catch (System.Exception e)
                {
                    File.WriteAllText(BuildDone, "FAIL\n" + e);
                    Debug.LogException(e);
                }
                return;
            }

            // ---------- 2) 试玩 ----------
            if (File.Exists(PlayFlag))
            {
                if (EditorApplication.isPlaying) return;
                File.Delete(PlayFlag);
                if (File.Exists(SmokeSprayDebugBuilder.ScenePath))
                {
                    EditorSceneManager.OpenScene(SmokeSprayDebugBuilder.ScenePath);
                    EditorApplication.EnterPlaymode();
                }
                else
                {
                    Debug.LogWarning("[SmokeSprayAutoRun] 场景不存在,请先构建:" +
                                     SmokeSprayDebugBuilder.ScenePath);
                }
                return;
            }

            // ---------- 2b) 构建"摆进齿轮面板"的场景 ----------
            if (File.Exists(InSceneFlag))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();
                    return;
                }
                File.Delete(InSceneFlag);
                try
                {
                    string msg = SmokeSprayDebugBuilder.BuildInScene();
                    File.WriteAllText(InSceneDone, "OK\n" + msg);
                    Debug.Log("[SmokeSprayAutoRun] 场景内构建完成\n" + msg);
                }
                catch (System.Exception e)
                {
                    File.WriteAllText(InSceneDone, "FAIL\n" + e);
                    Debug.LogException(e);
                }
                return;
            }

            // ---------- 3) 实拍(纯动画 / 摆进场景 两版) ----------
            if (File.Exists(CaptureFlag) && !s_Capturing)
            {
                File.Delete(CaptureFlag);
                try { BeginCapture(false); }
                catch (System.Exception e) { s_Err = e.ToString(); FinishCapture("FAIL\n" + e); }
                return;
            }
            if (File.Exists(SceneCapFlag) && !s_Capturing)
            {
                File.Delete(SceneCapFlag);
                try { BeginCapture(true); }
                catch (System.Exception e) { s_Err = e.ToString(); FinishCapture("FAIL\n" + e); }
                return;
            }

            if (s_Capturing)
            {
                int done = 0;
                while (s_CapIdx < s_Sprites.Count && done < PerTick)
                {
                    RenderOne(s_CapIdx);
                    s_CapIdx++;
                    done++;
                }
                if (s_CapIdx >= s_Sprites.Count)
                    FinishCapture("OK " + s_Sprites.Count + " -> " + s_CapDir);
            }
        }

        static void BeginCapture(bool inScene)
        {
            s_InScene = inScene;
            s_CapDir  = inScene ? SceneCapDir : CaptureDir;
            s_CapDone = inScene ? SceneCapDone : CaptureDone;
            s_Sprites = LoadSpritesSorted(SmokeSprayDebugBuilder.FrameDir);
            if (s_Sprites.Count == 0)
            {
                FinishCapture("FAIL: 帧目录为空 " + SmokeSprayDebugBuilder.FrameDir);
                return;
            }
            Directory.CreateDirectory(s_CapDir);

            // ⚠️ 不要在 EditorSceneManager.NewPreviewScene() 里渲 —— 实测那套相机
            // 渲染出来只有背景色(34 张全是 0.077,烟雾一张没画上)。
            // 改为在当前场景里放临时对象,但打 HideFlags.HideAndDontSave:
            // 既不保存进场景、也不把场景弄脏,用完 DestroyImmediate 即可。
            // 坐标挪到 90000 远,避免和用户场景里的东西同框。
            var camGo = new GameObject("~capcam");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            s_Cam = camGo.AddComponent<Camera>();
            s_Cam.orthographic = true;
            s_Cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f, 1f);
            s_Cam.clearFlags = CameraClearFlags.SolidColor;
            s_Cam.cullingMask = ~0;
            s_Cam.enabled = true;               // 有 targetTexture 时只渲到 RT,不动 Game 视图

            if (inScene)
            {
                // 复刻 SmokeSprayDebugBuilder.BuildInScene 的摆位常量
                s_Cam.orthographicSize = SmokeSprayDebugBuilder.CamOrtho;
                var cw = SmokeSprayDebugBuilder.PanelCenterWorld();
                camGo.transform.position = new Vector3(OFF + cw.x, OFF + cw.y, -10f);

                var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SmokeSprayDebugBuilder.PanelPath);
                if (panelSprite == null)
                    { FinishCapture("FAIL: 面板图不是 Sprite"); return; }
                var panelGo = new GameObject("~panel");
                panelGo.hideFlags = HideFlags.HideAndDontSave;
                var psr = panelGo.AddComponent<SpriteRenderer>();
                psr.sprite = panelSprite;
                psr.sortingOrder = 0;
                panelGo.transform.position = new Vector3(OFF, OFF, 0f);
                s_PanelSr = psr;

                var emitGo = new GameObject("~emitter");
                emitGo.hideFlags = HideFlags.HideAndDontSave;
                var ew = SmokeSprayDebugBuilder.EmitterWorld();
                emitGo.transform.SetParent(null);
                emitGo.transform.position = new Vector3(OFF + ew.x, OFF + ew.y, 0f);
                emitGo.transform.localRotation = Quaternion.Euler(0f, 0f, SmokeSprayDebugBuilder.EmitAngleDeg);

                var smokeGo = new GameObject("~smoke");
                smokeGo.hideFlags = HideFlags.HideAndDontSave;
                smokeGo.transform.SetParent(emitGo.transform, false);
                smokeGo.transform.localPosition = SmokeSprayDebugBuilder.SmokeLocalOffset();
                smokeGo.transform.localScale = Vector3.one * SmokeSprayDebugBuilder.SmokeScale;
                s_Sr = smokeGo.AddComponent<SpriteRenderer>();
                s_Sr.sortingOrder = 10;
                s_Sr.sprite = s_Sprites[0];
                s_EmitGo = emitGo;
            }
            else
            {
                s_Cam.orthographicSize = 4.2f;
                camGo.transform.position = new Vector3(OFF, OFF + 0.6f, -10f);
                var go = new GameObject("~smoke");
                go.hideFlags = HideFlags.HideAndDontSave;
                s_Sr = go.AddComponent<SpriteRenderer>();
                s_Sr.sortingOrder = 10;
                s_Sr.sprite = s_Sprites[0];
                go.transform.position = new Vector3(OFF, OFF, 0f);
                go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            }

            s_CapIdx = 0;
            s_Capturing = true;
            s_Err = "";
            Debug.Log($"[SmokeSprayAutoRun] 开始逐帧渲染({(inScene ? "摆进场景" : "纯动画")}) " +
                      $"{s_Sprites.Count} 张 -> {s_CapDir}");
        }

        static void RenderOne(int idx)
        {
            s_Sr.sprite = s_Sprites[idx];
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            s_Cam.targetTexture = rt;
            s_Cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            s_Cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(s_CapDir);
            File.WriteAllBytes($"{s_CapDir}/cap_{idx:00}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void FinishCapture(string msg)
        {
            s_Capturing = false;
            // 取**中间**那张判断是不是渲染成黑的了(第 0 帧本来就是空帧,拿它当探针会误报)
            string probe = $"{s_CapDir}/cap_{Mathf.Min(s_CapIdx + 15, 33):00}.png";
            if (File.Exists(probe))
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(probe));
                var px = t.GetPixels();
                float sum = 0f;
                foreach (var c in px) sum += c.r + c.g + c.b;
                float avg = sum / (px.Length * 3f);
                Object.DestroyImmediate(t);
                msg += $"\n探针 {System.IO.Path.GetFileName(probe)} 平均亮度 {avg:F3}";
            }
            if (!string.IsNullOrEmpty(s_Err)) msg += "\n" + s_Err;

            if (s_Sr != null) Object.DestroyImmediate(s_Sr.gameObject);   // 先子后父
            if (s_EmitGo != null) Object.DestroyImmediate(s_EmitGo);
            if (s_PanelSr != null) Object.DestroyImmediate(s_PanelSr.gameObject);
            if (s_Cam != null) Object.DestroyImmediate(s_Cam.gameObject);
            s_EmitGo = null; s_PanelSr = null; s_Cam = null; s_Sr = null; s_Sprites = null;

            File.WriteAllText(s_CapDone, msg);
            Debug.Log("[SmokeSprayAutoRun] 实拍结束:" + msg);
        }

        static List<Sprite> LoadSpritesSorted(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var list = new List<(string name, Sprite s)>();
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) list.Add((Path.GetFileName(path), sp));
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var res = new List<Sprite>();
            foreach (var x in list) res.Add(x.s);
            return res;
        }
    }
}
#endif
