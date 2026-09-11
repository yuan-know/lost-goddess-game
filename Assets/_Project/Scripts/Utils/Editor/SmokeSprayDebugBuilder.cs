// ============================================================================
//  SmokeSprayDebugBuilder.cs —— 烟雾喷出序列 → AnimationClip + Controller + 调试场景
//  菜单:失落的女神 ▸ AI 动画 ▸ 生成烟雾喷出调试场景
//
//  与 AIAnimationBuilder(角色 AI 动画)分开,因为烟雾是 **VFX 不是角色**:
//    - 角色帧是 260x512、锚点脚底居中、要套 CharacterCutout 治白线;
//      烟雾帧是 480x204、要**软边 alpha 混合**(套 Cutout 的 alpha 裁剪会把
//      羽化边切掉,反而变回硬锯齿),所以用默认 Sprites-Default 材质。
//    - 角色 clip 是循环的;烟雾喷出是**一次性**,播完停末帧,靠
//      SmokeSprayPreviewLoop 间隔重放。
//
//  素材来源:用户给的 `烟雾喷出.zip` 34 帧 → tools/ai_video/despill_smoke.py
//  做绿幕溢出抑制后落到 Art/AIAnimations/SmokeSpray/(详见该脚本注释)。
// ============================================================================

#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LostGoddess.EditorTools
{
    public static class SmokeSprayDebugBuilder
    {
        public const string FrameDir      = "Assets/_Project/Art/AIAnimations/SmokeSpray";
        public const string ControllerDir = "Assets/_Project/Art/AIAnimations/_Controllers";
        public const string ClipName      = "Smoke_Spray";
        public const string StateName     = "Spray";
        public const string ScenePath     = "Assets/_Project/Scenes/SmokeSprayDebug.unity";

        // ------------------------------------------------------------------
        //  摆进真实场景用的参数(用户 2026-09-11 给的齿轮面板特写 + 红标喷口)
        // ------------------------------------------------------------------
        // 场景图 `gear_mechanism_closeup` 1920x677,红标喷口(喷嘴开口面)质心
        // 实测在 (901, 394);喷嘴端面拟合倾斜 5.05°(顶部偏右)=> 喷嘴**指向左上方**,
        // 和烟雾精灵"从右缘向左喷"的朝向一致,所以只要 z 轴 +5°。
        public const string PanelPath  = "Assets/_Project/Resources/Closeups/gear_mechanism_closeup.png";
        public const string InScenePath = "Assets/_Project/Scenes/SmokeSprayInScene.unity";

        public const float PanelW = 1920f, PanelH = 677f;
        // 2026-09-11 定稿:**用户在场景里手动拖出来的值**,直接照抄,别再改。
        public const float EmitPxX = 903f, EmitPxY = 393f;   // 喷口(场景图像素)
        // ⚠️⚠️⚠️ 2026-09-11 三次修正。前面全错在**符号约定**,记牢:
        //
        //   精灵的喷出方向是**局部 -X**(指向 180°)。Unity 的 Z 轴正角是**逆时针**,
        //   把 180° 转到 192° —— 那是**偏下**!所以:
        //       要让烟往**左上**翘  ->  Z 必须是**负角**
        //       正角反而是往左下喷
        //
        //   历史(全因这个符号搞反):
        //     +5°  -> 用户"角度不对,没对着喷口"(其实是略偏下)
        //     +20° -> 用户"更歪了"(越正越往下)
        //     -15° -> 我文字里误标成"左下",实际方向是对的,被自己绕晕
        //     +12° -> 用户"还是偏左下角喷出啊"
        //   最终 **-13°**:烟羽从喷口往左上翘约 13°。
        //
        //   ⚠️ 另外:这张图上的**像素拟合完全不可信**。喷嘴是六角头+卡箍+滚花环的
        //   透视投影,本体只有 ~50x30px 且是 JPEG,"体轮廓斜率"和"开口面法线"
        //   分别量出 +17° 和 +5°,互相矛盾。以后凡是"视觉朝向"这类主观量,
        //   **直接出多角度对照图让用户选**,别反复量测。
        public const float EmitAngleDeg = -8.9f;   // 用户拖出来的值
        // 烟雾精灵 480x204,喷出源在右缘喉部中心(实测 x=479 处 y 71-123 的中心)
        public const float SpriteW = 480f, SpriteH = 204f;
        public const float SpriteEmitPxX = 479f, SpriteEmitPxY = 97f;
        // 缩放 0.45:喉部 53px*0.45 ≈ 24px = 喷嘴开口高,烟一出来就跟喷口一样宽最自然。
        public const float SmokeScale = 0.45f;   // 用户拖出来的值
        // 相机:1 世界单位 = 100px,与项目 CloseupView 的 1920x1080 参考分辨率一致。
        // 取景对准面板(面板在整图里只占中间 519x615,按全图取景会显得很小)。
        public const float CamOrtho = 4.0f;
        public const float PanelCenterPxX = 979f, PanelCenterPxY = 347f;

        /// <summary>面板中心的世界坐标(相机取景对准它)。</summary>
        public static Vector3 PanelCenterWorld()
        {
            return new Vector3((PanelCenterPxX - PanelW * 0.5f) / 100f,
                               (PanelH * 0.5f - PanelCenterPxY) / 100f, 0f);
        }

        /// <summary>喷口在世界坐标里的位置(面板图居中于原点,PPU=100)。</summary>
        public static Vector3 EmitterWorld()
        {
            return new Vector3((EmitPxX - PanelW * 0.5f) / 100f,
                               (PanelH * 0.5f - EmitPxY) / 100f, 0f);
        }

        /// <summary>烟雾精灵相对"喷口轴心"的本地偏移:让精灵里的喷出源正好落在轴心上。</summary>
        public static Vector3 SmokeLocalOffset()
        {
            return new Vector3(-(SpriteEmitPxX - SpriteW * 0.5f) / 100f * SmokeScale,
                               -(SpriteH - SpriteEmitPxY) / 100f * SmokeScale, 0f);
        }

        // 源视频原生速率。34 帧 @24fps = 1.42s,对"喷出一团烟"是合理时长。
        // ⚠️ 想改快慢只改这里,不要改帧数 —— 帧数是素材定的。
        public const float Fps = 24f;

        [MenuItem("失落的女神/AI 动画/生成烟雾喷出调试场景", priority = 80)]
        public static void BuildFromMenu()
        {
            if (!EnsureNotPlaying()) return;
            string msg = Build();
            EditorUtility.DisplayDialog("烟雾喷出调试场景", msg, "好");
        }

        [MenuItem("失落的女神/AI 动画/生成烟雾摆进齿轮面板的场景", priority = 81)]
        public static void BuildInSceneFromMenu()
        {
            if (!EnsureNotPlaying()) return;
            string msg = BuildInScene();
            EditorUtility.DisplayDialog("烟雾摆进场景", msg, "好");
        }

        /// <summary>生成 clip + controller,返回 controller(失败返回 null)。</summary>
        static RuntimeAnimatorController EnsureController(out int frameCount)
        {
            frameCount = 0;
            EnsureDir(ControllerDir);
            AssetDatabase.ImportAsset(FrameDir,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var sprites = LoadSpritesSorted(FrameDir);
            if (sprites.Count == 0)
            {
                Debug.LogError($"[SmokeSprayDebugBuilder] {FrameDir} 里没有 Sprite,先跑 tools/ai_video/despill_smoke.py");
                return null;
            }
            frameCount = sprites.Count;
            Debug.Log($"[SmokeSprayDebugBuilder] 找到 {sprites.Count} 帧");
            string clipPath = ControllerDir + "/" + ClipName + ".anim";
            string ctrlPath = ControllerDir + "/" + ClipName + ".controller";
            // 循环:首帧(未喷,全空)与末帧(消散完,全空)相接 = 无缝循环,
            // 消散段才播得出来。之前 loop:false + PreviewLoop 1.2s 重播,把消散截掉了。
            var clip = BuildFrameClip(sprites, Fps, loop: true, clipPath);
            return BuildSingleStateController(clip, ctrlPath);
        }

        /// <summary>供 AutoRun 无人值守调用。返回给日志/对话框用的一段说明。</summary>
        public static string Build()
        {
            EnsureDir("Assets/_Project/Scenes");
            int n;
            var controller = EnsureController(out n);
            if (controller == null) return "失败:找不到烟雾帧";
            var sprites = LoadSpritesSorted(FrameDir);

            // 编辑模式下默认显示的贴图。第 0 帧是空的(还没喷),照搬会让人
            // 打开场景"什么都看不见",以为生成失败 —— 取后段一帧当预览图。
            var previewSprite = sprites[sprites.Count * 3 / 4];
            BuildScene(controller, previewSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return $"烟雾喷出调试场景已生成:\n{ScenePath}\n\n" +
                   $"共 {n} 帧 @{Fps:F0}fps ({n / Fps:F2}s),单次播放。\n" +
                   "场景里:中间 1.6x 主实例 + 下方 0.8x/0.5x/0.3x 三种游戏内尺寸参考。\n" +
                   "clip 自循环(含末尾消散段),无需重放组件。\n" +
                   "打开场景按 Play 即可查看。";
        }

        // ------------------------------------------------------------------
        //  把烟雾摆进用户给的齿轮面板特写(场景图上红色标出的喷口)
        // ------------------------------------------------------------------
        public static string BuildInScene()
        {
            EnsureDir("Assets/_Project/Scenes");
            ConfigurePanelImporter();

            int n;
            var controller = EnsureController(out n);
            if (controller == null) return "失败:找不到烟雾帧";
            var sprites = LoadSpritesSorted(FrameDir);
            var previewSprite = sprites[sprites.Count * 3 / 4];

            var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            if (panel == null)
            {
                Debug.LogError("[SmokeSprayDebugBuilder] 面板图不是 Sprite:" + PanelPath);
                return "失败:面板图 " + PanelPath + " 未按 Sprite 导入";
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CamOrtho;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = PanelCenterWorld() + new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // 面板:图居中于原点,PPU=100 => 19.2 x 6.77 世界单位
            var panelGo = new GameObject("GearPanel");
            var psr = panelGo.AddComponent<SpriteRenderer>();
            psr.sprite = panel;
            psr.sortingOrder = 0;
            panelGo.transform.position = Vector3.zero;

            // 喷口轴心:绕它旋转,烟雾就会绕着喷口转
            var emitGo = new GameObject("SmokeEmitter");
            emitGo.transform.position = EmitterWorld();
            emitGo.transform.localRotation = Quaternion.Euler(0f, 0f, EmitAngleDeg);
            // 可手动摆放组件:场景里拖手柄 / Inspector 调 scale,实时生效
            var aim = emitGo.AddComponent<LostGoddess.VFX.SmokeSprayEmitterAim>();
            aim.emitLocalOffset = new Vector2(
                (SpriteEmitPxX - SpriteW * 0.5f) / 100f,
                (SpriteH - SpriteEmitPxY) / 100f);
            aim.scale = SmokeScale;
            aim.angleDeg = EmitAngleDeg;
            aim.ApplyChild();
            aim.ApplyAngle();

            // 烟雾精灵:本地偏移让"精灵里的喷出源"正好落在轴心上
            var smokeGo = new GameObject("SmokeSpray");
            smokeGo.transform.SetParent(emitGo.transform, false);
            smokeGo.transform.localPosition = SmokeLocalOffset();
            smokeGo.transform.localScale = new Vector3(SmokeScale, SmokeScale, 1f);
            var ssr = smokeGo.AddComponent<SpriteRenderer>();
            ssr.sprite = previewSprite;
            ssr.sortingOrder = 10;
            var anim = smokeGo.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // 不挂 SmokeSprayPreviewLoop:clip 已自循环(含消散段),1.2s 重放会把消散截掉

            // 运行时触发接口:内容层(谜题/剧情)通过 SmokeSprayTrigger 控制喷烟。
            // 调试场景默认自动循环;正式玩法把 autoPlayOnStart 关掉,调 PlayOnce()/PlayLooped()。
            var trigger = emitGo.AddComponent<LostGoddess.VFX.SmokeSprayTrigger>();
            trigger.smokeAnimator = anim;
            trigger.mode = LostGoddess.VFX.SmokeSprayTrigger.SprayMode.Loop;
            trigger.autoPlayOnStart = true;
            trigger.hideWhenIdle = false;

            bool ok = EditorSceneManager.SaveScene(scene, InScenePath);
            if (ok) AddSceneToBuildSettings(InScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var e = EmitterWorld();
            var l = SmokeLocalOffset();
            float bpx = e.x * 100f + PanelW * 0.5f, bpy = PanelH * 0.5f - e.y * 100f;
            return $"已生成:{InScenePath}\n\n" +
                   $"喷口(场景图像素 {bpx:F0},{bpy:F0})-> 世界 ({e.x:F3}, {e.y:F3})\n" +
                   $"倾角 {EmitAngleDeg:F1}°,缩放 {SmokeScale:F2}\n" +
                   $"烟雾本地偏移 ({l.x:F3}, {l.y:F3}),精灵 480x204@PPU100\n" +
                   $"共 {n} 帧 @{Fps:F0}fps,单次播放 + 1.2s 重放。\n" +
                   "打开场景按 Play 查看。";
        }

        /// <summary>把面板图配成 Sprite(PPU100 / 轴心居中),Closeups 目录里原本是 Default 贴图。</summary>
        static void ConfigurePanelImporter()
        {
            var ti = AssetImporter.GetAtPath(PanelPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[SmokeSprayDebugBuilder] 找不到面板图 importer:" + PanelPath);
                return;
            }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Center;   // 图居中于原点,坐标好算
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
        }

        /// <summary>
        /// 把场景里调好的摆放参数(喷口位置/朝向/缩放)复制到剪贴板,
        /// 形式就是本类的常量写法 —— 粘回这里即可固化,或者发给程序员。
        /// </summary>
        [MenuItem("失落的女神/AI 动画/烟雾:复制当前摆放参数到剪贴板", priority = 82)]
        public static void CopyPlacement()
        {
            var go = GameObject.Find("SmokeEmitter");
            var aim = go ? go.GetComponent<LostGoddess.VFX.SmokeSprayEmitterAim>() : null;
            if (aim == null)
            {
                EditorUtility.DisplayDialog("烟雾摆放参数",
                    "当前场景里找不到 SmokeEmitter。\n请先打开 SmokeSprayInScene.unity。", "好");
                return;
            }
            float px = aim.transform.position.x * 100f + PanelW * 0.5f;
            float py = PanelH * 0.5f - aim.transform.position.y * 100f;
            string code =
                $"public const float EmitPxX = {px:F0}f;\n" +
                $"public const float EmitPxY = {py:F0}f;\n" +
                $"public const float SmokeScale = {aim.scale:F2}f;\n" +
                $"public const float EmitAngleDeg = {aim.angleDeg:F1}f;";
            EditorGUIUtility.systemCopyBuffer = code;
            EditorUtility.DisplayDialog("已复制", code, "好");
        }

        // ------------------------------------------------------------------
        static void BuildScene(RuntimeAnimatorController controller, Sprite firstFrame)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.2f;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 0.6f, -10);
            camGo.AddComponent<AudioListener>();

            // 主实例。帧 480x204 @PPU100 = 4.8x2.04 世界单位,1.6x = 7.68x3.26。
            Spawn(controller, firstFrame, "SmokeSpray", new Vector3(0f, 0f, 0f), 1.6f);

            // 底部一排游戏内尺寸参考
            Spawn(controller, firstFrame, "Smoke_0.8x", new Vector3(-3.0f, -2.9f, 0f), 0.8f);
            Spawn(controller, firstFrame, "Smoke_0.5x", new Vector3(-0.4f, -2.9f, 0f), 0.5f);
            Spawn(controller, firstFrame, "Smoke_0.3x", new Vector3(1.6f, -2.9f, 0f), 0.3f);

            // 地面参考线 —— 没有它看不出烟雾贴在哪一行
            var line = new GameObject("GroundLine");
            var lr = line.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-8f, -2.9f, 1f));
            lr.SetPosition(1, new Vector3(8f, -2.9f, 1f));
            lr.widthMultiplier = 0.03f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.4f, 0.45f, 1f);

            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            if (ok)
            {
                AddSceneToBuildSettings(ScenePath);
                Debug.Log("[SmokeSprayDebugBuilder] 场景已保存:" + ScenePath);
            }
            else
            {
                Debug.LogError("[SmokeSprayDebugBuilder] 场景保存失败:" + ScenePath);
            }
        }

        static GameObject Spawn(RuntimeAnimatorController ctrl, Sprite previewSprite,
                                string goName, Vector3 pos, float scale)
        {
            var go = new GameObject(goName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = previewSprite;
            sr.sortingOrder = 10;
            // 故意**不套** CharacterCutout:烟雾要软边 alpha 混合,
            // Cutout 的 clip(a-cutoff) 会把羽化边切掉变硬锯齿。
            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // clip 已自循环(含消散段),不需要 1.2s 重放组件
            return go;
        }

        static List<Sprite> LoadSpritesSorted(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var list = new List<(string name, Sprite s)>();
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) list.Add((Path.GetFileName(path), sprite));
            }
            list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            var result = new List<Sprite>();
            foreach (var x in list) result.Add(x.s);
            return result;
        }

        static AnimationClip BuildFrameClip(List<Sprite> sprites, float fps, bool loop, string path)
        {
            var clip = new AnimationClip();
            clip.name = Path.GetFileNameWithoutExtension(path);
            clip.frameRate = fps;

            float interval = 1f / fps;
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i * interval,
                    value = sprites[i]
                };
            }

            var binding = new EditorCurveBinding
            {
                path = "",
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorController BuildSingleStateController(AnimationClip clip, string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);

            var state = sm.AddState(StateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            sm.defaultState = state;
            return controller;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parts = dir.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static bool EnsureNotPlaying()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorUtility.DisplayDialog("请先退出播放模式",
                "生成调试场景需要新建场景,不能在 Play 模式下进行。\n\n" +
                "请按 Stop 停止播放,然后重新点这个菜单。", "好");
            return false;
        }
    }
}
#endif
