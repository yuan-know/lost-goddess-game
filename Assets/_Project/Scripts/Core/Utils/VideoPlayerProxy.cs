// ============================================================================
//  VideoPlayerProxy.cs —— 反射版 VideoPlayer 封装(2026-07-27)
//   目的:避免编译时强引用 UnityEngine.VideoModule 程序集,
//        解决 CS1069 "type forwarded to UnityEngine.VideoModule" 错误。
//   通过 Assembly 查找 + Expression.Lambda 动态生成事件处理器,
//   完全不用 using UnityEngine.Video。
//
//   用法:
//     var vp = VideoPlayerProxy.Create();
//     if (vp != null) {
//         vp.Setup(fullPath);
//         vp.Prepare();
//         // 等 IsPrepared 后 vp.Play()
//         // 等 IsFinished 后 vp.Destroy()
//     }
//
//   原位于 CoffinDialCloseup.cs 内部类,现提取为独立工具类供多处复用。
// ============================================================================

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public class VideoPlayerProxy
    {
        GameObject _go;
        Component _vp;  // VideoPlayer 组件
        Type _vpType;
        RenderTexture _rt;
        bool _prepared;
        bool _finished;
        bool _error;

        public bool IsPrepared => _prepared;
        public bool IsFinished => _finished;
        public bool HasError => _error;

        /// <summary>创建一个全屏视频播放器。失败返回 null。</summary>
        public static VideoPlayerProxy Create()
        {
            return Create("~VideoPlayerProxy");
        }

        /// <summary>创建一个指定名字的全屏视频播放器。</summary>
        public static VideoPlayerProxy Create(string goName)
        {
            // 反射加载 VideoPlayer 类型
            Assembly videoAsm = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "UnityEngine.VideoModule")
                {
                    videoAsm = asm;
                    break;
                }
            }
            if (videoAsm == null)
            {
                // 再试试 UnityEngine.dll 里有没有(老版本)
                try
                {
                    var t = typeof(Component).Assembly.GetType("UnityEngine.Video.VideoPlayer");
                    if (t != null) videoAsm = t.Assembly;
                }
                catch { }
            }
            if (videoAsm == null) return null;

            var vpType = videoAsm.GetType("UnityEngine.Video.VideoPlayer");
            if (vpType == null) return null;

            var proxy = new VideoPlayerProxy();
            proxy._vpType = vpType;

            // 创建 GameObject 和 UI 画布
            proxy._go = new GameObject(goName);
            UnityEngine.Object.DontDestroyOnLoad(proxy._go);

            var canvasGo = new GameObject("VideoCanvas");
            canvasGo.transform.SetParent(proxy._go.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var rawGo = new GameObject("RawImage");
            rawGo.transform.SetParent(canvasGo.transform, false);
            var rawImg = rawGo.AddComponent<RawImage>();
            var rt = rawImg.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            proxy._rt = new RenderTexture(1920, 1080, 24);
            proxy._rt.Create();
            rawImg.texture = proxy._rt;

            // 添加 VideoPlayer 组件
            proxy._vp = (Component)proxy._go.AddComponent(vpType);

            return proxy;
        }

        public bool Setup(string videoUrl)
        {
            if (_vp == null) return false;
            try
            {
                // playOnAwake = false
                _vpType.GetProperty("playOnAwake").SetValue(_vp, false);
                // waitForFirstFrame = true
                _vpType.GetProperty("waitForFirstFrame").SetValue(_vp, true);
                // source = Url (VideoSource.Url = 1)
                _vpType.GetProperty("source").SetValue(_vp, 1);
                // url
                string url = "file:///" + videoUrl.Replace('\\', '/');
                _vpType.GetProperty("url").SetValue(_vp, url);
                // renderMode = RenderTexture (VideoRenderMode.RenderTexture = 2)
                _vpType.GetProperty("renderMode").SetValue(_vp, 2);
                // targetTexture
                _vpType.GetProperty("targetTexture").SetValue(_vp, _rt);
                // isLooping = false
                _vpType.GetProperty("isLooping").SetValue(_vp, false);
                // audioOutputMode = Direct (VideoAudioOutputMode.Direct = 2)
                _vpType.GetProperty("audioOutputMode").SetValue(_vp, 2);

                // 注册 prepareCompleted 事件
                var prepareEvt = _vpType.GetEvent("prepareCompleted");
                if (prepareEvt != null)
                {
                    var handler = CreateVoidHandler(prepareEvt.EventHandlerType, () => _prepared = true);
                    prepareEvt.AddEventHandler(_vp, handler);
                }

                // 注册 loopPointReached 事件
                var loopEvt = _vpType.GetEvent("loopPointReached");
                if (loopEvt != null)
                {
                    var handler = CreateVoidHandler(loopEvt.EventHandlerType, () => _finished = true);
                    loopEvt.AddEventHandler(_vp, handler);
                }

                // 注册 errorReceived 事件
                var errorEvt = _vpType.GetEvent("errorReceived");
                if (errorEvt != null)
                {
                    var handler = CreateStringHandler(errorEvt.EventHandlerType, msg =>
                    {
                        _error = true;
                        Debug.LogError("[VideoPlayerProxy] 错误: " + msg);
                    });
                    errorEvt.AddEventHandler(_vp, handler);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[VideoPlayerProxy] Setup 失败: " + e.Message);
                return false;
            }
        }

        /// <summary>创建一个 void 委托(用于 VideoPlayer 事件),忽略参数,调用 action。</summary>
        static Delegate CreateVoidHandler(Type delegateType, Action action)
        {
            var paramExprs = GetDelegateParameterTypes(delegateType)
                .Select((t, i) => Expression.Parameter(t, "p" + i)).ToArray();
            var callExpr = Expression.Call(
                Expression.Constant(action), typeof(Action).GetMethod("Invoke"));
            var lambda = Expression.Lambda(delegateType, callExpr, paramExprs);
            return lambda.Compile();
        }

        /// <summary>创建带 string 参数的委托处理器(用于 errorReceived 事件)。</summary>
        static Delegate CreateStringHandler(Type delegateType, Action<string> action)
        {
            var paramTypes = GetDelegateParameterTypes(delegateType);
            var paramExprs = paramTypes.Select((t, i) => Expression.Parameter(t, "p" + i)).ToArray();
            Expression stringArg = null;
            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (paramTypes[i] == typeof(string))
                {
                    stringArg = paramExprs[i];
                    break;
                }
            }
            if (stringArg == null) stringArg = Expression.Constant("unknown");
            var callExpr = Expression.Call(
                Expression.Constant(action), typeof(Action<string>).GetMethod("Invoke"),
                stringArg);
            var lambda = Expression.Lambda(delegateType, callExpr, paramExprs);
            return lambda.Compile();
        }

        static Type[] GetDelegateParameterTypes(Type delegateType)
        {
            var invoke = delegateType.GetMethod("Invoke");
            return invoke.GetParameters().Select(p => p.ParameterType).ToArray();
        }

        public void Play()
        {
            _vpType.GetMethod("Play").Invoke(_vp, null);
        }

        public void Prepare()
        {
            _vpType.GetMethod("Prepare").Invoke(_vp, null);
        }

        public void Destroy()
        {
            if (_go != null)
            {
                UnityEngine.Object.Destroy(_go);
                _go = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                UnityEngine.Object.Destroy(_rt);
                _rt = null;
            }
        }
    }
}
