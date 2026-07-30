// ============================================================================
//  GameState.cs —— 全局状态(存档核心,契约 §1)  🟢
//  对外静态 API,内部持有 SaveData。B 只通过这些方法读写进度,不碰内部字段。
// ============================================================================

using System;

namespace LostGoddess
{
    public static class GameState
    {
        // 当前存档数据(由 SaveSystem 在读档/新游戏时替换)
        static SaveData _data = new SaveData();

        /// <summary>SaveSystem 专用:替换当前存档数据体。</summary>
        public static SaveData Data => _data;
        public static void _SetData(SaveData data)
        {
            _data = data ?? new SaveData();
            _data.UnpackAfterLoad();
        }

        // ── Flag 开关 ──
        public static bool GetFlag(string key)
        {
            return _data.flags.TryGetValue(key, out bool v) && v;
        }

        public static void SetFlag(string key, bool value)
        {
            _data.flags[key] = value;
            OnFlagChanged?.Invoke(key, value);
        }

        // ── 物品 ──
        public static void AddItem(string itemId)
        {
            if (!_data.items.Contains(itemId))
            {
                _data.items.Add(itemId);
                OnItemsChanged?.Invoke();
            }
        }

        public static bool HasItem(string itemId) => _data.items.Contains(itemId);

        public static void RemoveItem(string itemId)
        {
            if (_data.items.Remove(itemId))
                OnItemsChanged?.Invoke();
        }

        /// <summary>获取所有拥有的道具列表（用于背包UI显示）。</summary>
        public static System.Collections.Generic.List<string> GetAllItems()
        {
            return new System.Collections.Generic.List<string>(_data.items);
        }

        // ── 时代/形态(序幕内可剧情驱动切多次)──
        public static Era CurrentEra
        {
            get => _data.currentEra;
            set
            {
                var prev = _data.currentEra;
                if (prev == value) return;
                _data.currentEra = value;
                OnEraChanged?.Invoke(prev, value);
            }
        }

        /// <summary>剧情脚本调用:切换 Era + 触发 OnEraChanged。等价于 CurrentEra = newEra。</summary>
        public static void SetEra(Era newEra) => CurrentEra = newEra;

        /// <summary>某 Era 是否已被剧情解锁(玩家自由切换按键才能选它)。老年从起始就解锁。</summary>
        public static bool IsEraUnlocked(Era era) => _data.unlockedEras != null && _data.unlockedEras.Contains(era);

        /// <summary>剧情推进时解锁一个 Era(如陶罐钥匙→解锁 Young、棺材小游戏→解锁 Middle)。幂等。</summary>
        public static void UnlockEra(Era era)
        {
            if (_data.unlockedEras == null) _data.unlockedEras = new System.Collections.Generic.List<Era>();
            if (_data.unlockedEras.Contains(era)) return;
            _data.unlockedEras.Add(era);
            OnEraUnlocked?.Invoke(era);
        }

        // ── 当前房间 ──
        public static string CurrentRoom
        {
            get => _data.currentRoom;
            set => _data.currentRoom = value;
        }

        // ── 事件(UI / 其它系统订阅)──
        public static event Action<string, bool> OnFlagChanged;  // (key, value)
        public static event Action OnItemsChanged;
        public static event Action<Era, Era> OnEraChanged;  // (from, to)  剧情切换/闪白/换立绘
        public static event Action<Era> OnEraUnlocked;      // Era 首次解锁(可用于成就 UI 弹窗)
    }
}
