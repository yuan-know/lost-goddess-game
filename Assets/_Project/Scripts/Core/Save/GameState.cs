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

        // ── 时代/形态(分章固定)──
        public static Era CurrentEra
        {
            get => _data.currentEra;
            set => _data.currentEra = value;
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
    }
}
