// ============================================================================
//  AudioManager.cs —— 音频(契约 §6)  🟢
//  clip 按名从 Resources/Audio 加载(验证期无音频文件时静默不报错)。
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace LostGoddess
{
    public static class AudioManager
    {
        static AudioSource _sfxSrc;
        static AudioSource _bgmSrc;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static void Init(MonoBehaviour host)
        {
            _sfxSrc = host.gameObject.AddComponent<AudioSource>();
            _sfxSrc.playOnAwake = false;

            _bgmSrc = host.gameObject.AddComponent<AudioSource>();
            _bgmSrc.playOnAwake = false;
            _bgmSrc.loop = true;
        }

        public static void PlaySfx(string clipName)
        {
            var clip = Load(clipName);
            if (clip != null && _sfxSrc != null) _sfxSrc.PlayOneShot(clip);
        }

        public static void PlayBgm(string clipName, bool loop = true)
        {
            var clip = Load(clipName);
            if (clip == null || _bgmSrc == null) return;
            _bgmSrc.clip = clip;
            _bgmSrc.loop = loop;
            _bgmSrc.Play();
        }

        public static void StopBgm()
        {
            if (_bgmSrc != null) _bgmSrc.Stop();
        }

        static AudioClip Load(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            if (_cache.TryGetValue(clipName, out var c)) return c;
            var clip = Resources.Load<AudioClip>("Audio/" + clipName);
            _cache[clipName] = clip; // 缓存 null 也无妨,避免反复 IO
            if (clip == null)
                Debug.Log($"[AudioManager] 未找到音频 '{clipName}'(验证期正常,忽略)。");
            return clip;
        }
    }
}
