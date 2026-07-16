// ============================================================================
//  InventorySystem.cs —— 背包(选中→点目标,契约 §9)  🟢
//  物品数据实际存在 GameState.Data.items(随存档),本系统管"选中态"与事件。
//  手感:背包选中一件 → 光标进入持物态 → 点场景目标物,目标物在 OnClick 里判断。
// ============================================================================

using System;

namespace LostGoddess
{
    public static class InventorySystem
    {
        // 物品持有转发给 GameState(存档单一真源)
        public static void Add(string itemId)    => GameState.AddItem(itemId);
        public static void Remove(string itemId)
        {
            GameState.RemoveItem(itemId);
            if (_selected == itemId) ClearSelection();
        }
        public static bool Has(string itemId)     => GameState.HasItem(itemId);

        // ── 选中态 ──
        static string _selected;
        public static string SelectedItem => _selected;

        public static void Select(string itemId)
        {
            if (!Has(itemId)) return;      // 只能选中拥有的
            _selected = itemId;
            OnSelectionChanged?.Invoke(_selected);
        }

        public static void ClearSelection()
        {
            _selected = null;
            OnSelectionChanged?.Invoke(null);
        }

        /// <summary>UI 用:选中变化时更新光标图标(null = 空手)。</summary>
        public static event Action<string> OnSelectionChanged;
    }
}
