// ============================================================================
//  InventoryUI.cs —— 背包UI面板（2026-07-19）
//  功能：
//    · 屏幕右边缘居中显示背包按钮
//    · 点击打开/关闭背包面板
//    · 显示当前拥有的所有道具（带图标）
//    · 点击道具可选中/取消选中
//  美术资源：
//    · backpack_icon.png - 背包按钮图标
//    · slot_empty.png - 空槽位背景
//    · backpack_grid.png - 背包网格背景（可选）
//    · Icons/*.png - 各道具图标
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostGoddess
{
    public class InventoryUI : MonoBehaviour
    {
        static InventoryUI _instance;

        // UI组件
        GameObject _backpackButton;      // 右侧背包按钮
        GameObject _inventoryPanel;      // 背包面板
        Transform _itemsContainer;       // 道具容器
        List<InventorySlot> _slots = new List<InventorySlot>();

        const int MaxSlots = 16;         // 2026-07-24 参考图为 4列×4行 = 16 格
        bool _isOpen = false;

        // 2026-07-24 背包槽位按参考图(背包空白格子.png)红色格子像素精准定位。
        //   slot_empty.png 3400×1200 拉伸填满 BagUI(1920×644.11):
        //   scaleX=1920/3400=0.56471, scaleY=644.11/1200=0.53676。
        //   下列值 = 红色格子中心像素 × scale,单位为 BagUI 参考分辨率下的 px。
        static readonly float[] kSlotColX = { 779.3f, 884.0f, 994.2f, 1101.5f }; // 距 BagUI 左边缘
        static readonly float[] kSlotRowY = { 179.0f, 277.2f, 379.5f, 477.7f };  // 距 BagUI 顶部
        const float kSlotW = 86f;   // 格子宽(≈154img*0.56471)
        const float kSlotH = 82f;   // 格子高(≈154img*0.53676)

        public static InventoryUI Instance => _instance;

        public static InventoryUI CreateAttached()
        {
            var go = new GameObject("~InventoryUI");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;  // 在游戏UI之上，在对话/特写之下
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            var ui = go.AddComponent<InventoryUI>();
            ui.BuildUI();
            return ui;
        }

        void Awake()
        {
            _instance = this;
            // 监听道具变化事件，自动刷新背包
            GameState.OnItemsChanged += OnItemsChanged;
        }

        void OnDestroy()
        {
            // 取消事件监听
            GameState.OnItemsChanged -= OnItemsChanged;
        }

        void OnItemsChanged()
        {
            // 如果背包打开着，立即刷新
            if (_isOpen)
            {
                RefreshInventory();
            }
        }

        void BuildUI()
        {
            // 确保场景中有EventSystem,否则UI点击事件不会触发
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                DontDestroyOnLoad(esGo);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("[InventoryUI] 创建了EventSystem");
            }

            // ========== 1. 背包按钮(中间场景条带的右上角,letterbox 内) ==========
            //   2026-07-20 letterbox 后,把背包按钮放到中间场景右上角,
            //   避免按钮被上方黑边压住或与画面无关地漂在屏幕边缘。
            //   中间条带垂直范围:y ∈ [barPct, 1-barPct] = [0.2018, 0.7982],
            //   右上锚点 = (1, 1 - barPct) = (1, 0.7982),pivot=(1,1) 让按钮悬挂在场景顶部之下。
            _backpackButton = new GameObject("BackpackButton");
            _backpackButton.transform.SetParent(transform, false);

            const float BarPct = 0.2018f;   // 与 LetterboxOverlay.BarHeightPct 一致
            var btnRt = _backpackButton.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 1 - BarPct);
            btnRt.anchorMax = new Vector2(1, 1 - BarPct);
            btnRt.pivot = new Vector2(1, 1);
            btnRt.anchoredPosition = new Vector2(-30, -30);  // 距场景右上角 30px 内边距
            btnRt.sizeDelta = new Vector2(120, 120);

            // 背包图标
            var btnImg = _backpackButton.AddComponent<Image>();
            var btnSprite = Resources.Load<Sprite>("UI/Inventory/backpack_icon");
            if (btnSprite == null)
            {
                // 如果是Texture类型，转换为Sprite
                var tex = Resources.Load<Texture2D>("UI/Inventory/backpack_icon");
                if (tex != null)
                    btnSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            if (btnSprite != null)
            {
                btnImg.sprite = btnSprite;
                btnImg.preserveAspect = true;
                Debug.Log("[InventoryUI] 背包图标加载成功");
            }
            else
            {
                // 兜底：使用纯色背景
                btnImg.color = new Color(0.6f, 0.4f, 0.2f);
                Debug.LogWarning("[InventoryUI] 背包图标加载失败，使用兜底颜色");
            }

            btnImg.raycastTarget = true;  // 确保可以接收点击

            // 按钮组件
            var btn = _backpackButton.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            btn.onClick.AddListener(() => {
                Debug.Log("[InventoryUI] 背包按钮被点击");
                ToggleInventory();
            });

            Debug.Log("[InventoryUI] 背包按钮已创建");

            // 2026-07-21 序幕设定:第 0 幕的老人还没捡到背包,序幕开场默认隐藏背包 UI(按钮 + 面板)。
            //   触发 InventoryUI.SetVisible(true) 才出现——一般由拾取背包事件 (PickupBackpack) 调用。
            //   已经存在物品(如从 Load 进入或跳过序幕)时会跳过默认隐藏,见 SetVisible 语义。
            _backpackButton.SetActive(false);

            // ========== 2. 背包面板（全屏显示，半透明背景） ==========
            _inventoryPanel = new GameObject("InventoryPanel");
            _inventoryPanel.transform.SetParent(transform, false);

            var panelRt = _inventoryPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;  // 全屏
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // 半透明黑色遮罩背景（让游戏画面变暗但仍可见）
            var dimBgGo = new GameObject("DimBackground");
            dimBgGo.transform.SetParent(_inventoryPanel.transform, false);
            var dimBgRt = dimBgGo.AddComponent<RectTransform>();
            dimBgRt.anchorMin = Vector2.zero;
            dimBgRt.anchorMax = Vector2.one;
            dimBgRt.offsetMin = Vector2.zero;
            dimBgRt.offsetMax = Vector2.zero;

            var dimBg = dimBgGo.AddComponent<Image>();
            dimBg.color = new Color(0, 0, 0, 0.75f);  // 半透明黑色，游戏画面可透出
            dimBg.raycastTarget = true;  // 阻止点击穿透

            // 背包UI容器(2026-07-21 修:只覆盖中间场景条带,不再撑满全屏)
            //   slot_empty.png 是 3400×1200(≈2.83:1),中间条带 1920×644(≈2.98:1),
            //   宽高比几乎一致,直接拉伸填满中间条带即可,视觉不再压扁。
            //   letterbox 是独立 canvas(order 1000)遮在最上,不会被这块 UI 挤掉。
            var bagUIGo = new GameObject("BagUI");
            bagUIGo.transform.SetParent(_inventoryPanel.transform, false);
            var bagUIRt = bagUIGo.AddComponent<RectTransform>();
            bagUIRt.anchorMin = new Vector2(0, BarPct);
            bagUIRt.anchorMax = new Vector2(1, 1 - BarPct);
            bagUIRt.offsetMin = Vector2.zero;
            bagUIRt.offsetMax = Vector2.zero;

            // 使用slot_empty作为背包UI（拉伸模式）
            var panelBg = bagUIGo.AddComponent<Image>();
            var bagUISprite = Resources.Load<Sprite>("UI/Inventory/slot_empty");
            if (bagUISprite == null)
            {
                var tex = Resources.Load<Texture2D>("UI/Inventory/slot_empty");
                if (tex != null)
                {
                    bagUISprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    Debug.Log("[InventoryUI] 使用slot_empty纹理创建背包UI: " + tex.width + "x" + tex.height);
                }
            }

            if (bagUISprite != null)
            {
                panelBg.sprite = bagUISprite;
                panelBg.type = Image.Type.Simple;  // 简单拉伸模式，填充整个屏幕
                panelBg.preserveAspect = false;  // 不保持宽高比，拉伸填充
                panelBg.raycastTarget = false;
                panelBg.color = new Color(1f, 1f, 1f, 1f);  // 完全不透明显示背包图
                Debug.Log("[InventoryUI] 背包UI(slot_empty)加载成功");
            }
            else
            {
                // 兜底：棕色背景
                panelBg.color = new Color(0.3f, 0.25f, 0.2f, 0.95f);
                Debug.LogWarning("[InventoryUI] slot_empty加载失败，使用兜底背景");
            }

            // 标题文本（移除，因为背包UI已经是完整设计）
            // 道具容器不需要单独的标题

            // 道具容器(2026-07-24 改为绝对像素定位,填满整个 BagUI,
            //   每个槽位用 kSlotColX/kSlotRowY 在其内部精准落位,不再用 GridLayoutGroup)
            var containerGo = new GameObject("ItemsContainer");
            containerGo.transform.SetParent(bagUIGo.transform, false);
            var containerRt = containerGo.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;   // 撑满 BagUI
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;
            _itemsContainer = containerGo.transform;

            Debug.Log("[InventoryUI] 道具容器已创建(绝对像素定位 4x4)");

            // 关闭按钮(2026-07-21 修:锚到中间场景右上角,不再是屏幕右上;尺寸加大)
            //   anchor = (1, 1-BarPct) → 中间条带的右上角,pivot=(1,1) 让按钮悬挂在场景顶部之下
            var closeBtnGo = new GameObject("CloseButton");
            closeBtnGo.transform.SetParent(_inventoryPanel.transform, false);
            var closeBtnRt = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRt.anchorMin = new Vector2(1, 1 - BarPct);
            closeBtnRt.anchorMax = new Vector2(1, 1 - BarPct);
            closeBtnRt.pivot = new Vector2(1, 1);
            closeBtnRt.anchoredPosition = new Vector2(-40, -40);  // 距场景右上角 40px 内边距
            closeBtnRt.sizeDelta = new Vector2(140, 140);  // 加大到 140×140

            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);  // 红色背景

            var closeBtnText = new GameObject("Text");
            closeBtnText.transform.SetParent(closeBtnGo.transform, false);
            var closeTxtRt = closeBtnText.AddComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero;
            closeTxtRt.offsetMax = Vector2.zero;

            var closeTxt = closeBtnText.AddComponent<Text>();
            closeTxt.text = "×";  // 使用×符号
            closeTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeTxt.fontSize = 90;  // 2026-07-21 加大字号,配合更大的按钮
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.fontStyle = FontStyle.Bold;

            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(() => ToggleInventory());

            // 创建槽位
            for (int i = 0; i < MaxSlots; i++)
            {
                var slot = CreateSlot(i);
                _slots.Add(slot);
            }

            // 2026-07-21 底部常驻背包描述条(与"字幕"同高度,黑底白字)。
            //   放在 _inventoryPanel 下、bagUI 之外,盖在中间条带底部;不影响操作,
            //   面板关闭时随 _inventoryPanel 一起 SetActive(false) 隐藏。
            BuildDescriptionBar(_inventoryPanel.transform);

            _inventoryPanel.SetActive(false);
        }

        /// <summary>底部常驻背包描述条(2026-07-21)。放到下方黑边区,与对话字幕同位置。
        /// 关键:InventoryUI canvas order=300 会被 Letterbox(1000)盖住,所以给描述条单独挂
        /// 子 Canvas 覆写 order=1150(高于 letterbox 的 1000,低于 dialogue 的 1200 特写)。</summary>
        void BuildDescriptionBar(Transform parent)
        {
            var descGo = new GameObject("BackpackDescription");
            descGo.transform.SetParent(parent, false);

            // 独立子 Canvas 覆写 sortingOrder,越过 letterbox 黑边
            var subCanvas = descGo.AddComponent<Canvas>();
            subCanvas.overrideSorting = true;
            subCanvas.sortingOrder = 1150;   // > Letterbox 1000, 与 DialogueSystem 1100 同层
            descGo.AddComponent<GraphicRaycaster>();

            // 位置:锚在屏幕底部,高度 = 下方黑边高度(与字幕条完全一致)
            var rt = descGo.GetComponent<RectTransform>();
            if (rt == null) rt = descGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 1080f * 0.2018f);   // 与 LetterboxOverlay.BarHeightPct 同
            rt.anchoredPosition = Vector2.zero;

            // 不加底色 —— 黑边本身就是底
            // 文本
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(descGo.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text = "容量很大的旧皮革背包,带有可照明的油灯。结实耐用,可在此查看你收集的所有的东西。";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 30;                    // 与 DialogueSystem 字幕 fontSize=30 一致
            txt.color = new Color(1f, 0.96f, 0.9f);  // 与字幕相同的暖白
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            // 描边(和字幕保持一致的可读性)
            var outline = txtGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var lrt = txt.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(120, 20);
            lrt.offsetMax = new Vector2(-120, -20);
        }

        InventorySlot CreateSlot(int index)
        {
            var slotGo = new GameObject($"Slot_{index}");
            slotGo.transform.SetParent(_itemsContainer, false);

            // 2026-07-24 绝对像素定位:锚到容器左上角,pivot=中心,
            //   anchoredPosition = (列中心X, -行中心Y),尺寸 = 单格大小。
            int col = index % 4;
            int row = index / 4;
            var slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0, 1);
            slotRt.anchorMax = new Vector2(0, 1);
            slotRt.pivot = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = new Vector2(kSlotColX[col], -kSlotRowY[row]);
            slotRt.sizeDelta = new Vector2(kSlotW, kSlotH);

            // 槽位不需要背景（背包UI已经有网格了）
            // 只需要一个透明的Image接收点击
            var slotImg = slotGo.AddComponent<Image>();
            slotImg.color = new Color(0, 0, 0, 0.01f);  // 几乎透明，但能接收点击

            // 道具图标(略小于格子,四周留 6px 内边距,视觉上不顶格)
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(6, 6);
            iconRt.offsetMax = new Vector2(-6, -6);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = true;  // 保持宽高比
            iconImg.raycastTarget = false;

            // 选中高亮边框
            var highlightGo = new GameObject("Highlight");
            highlightGo.transform.SetParent(slotGo.transform, false);
            var highlightRt = highlightGo.AddComponent<RectTransform>();
            highlightRt.anchorMin = Vector2.zero;
            highlightRt.anchorMax = Vector2.one;
            highlightRt.offsetMin = Vector2.zero;
            highlightRt.offsetMax = Vector2.zero;

            var highlightImg = highlightGo.AddComponent<Image>();
            highlightImg.color = new Color(1f, 0.8f, 0.2f, 0.5f);  // 金色高亮
            highlightImg.raycastTarget = false;
            highlightGo.SetActive(false);

            // 按钮
            var slotBtn = slotGo.AddComponent<Button>();
            slotBtn.targetGraphic = slotImg;
            slotBtn.transition = UnityEngine.UI.Selectable.Transition.None;

            var slot = new InventorySlot
            {
                slotObject = slotGo,
                iconImage = iconImg,
                highlightObject = highlightGo,
                button = slotBtn,
                itemId = null
            };

            slotBtn.onClick.AddListener(() => OnSlotClicked(slot));

            return slot;
        }

        void ToggleInventory()
        {
            _isOpen = !_isOpen;
            _inventoryPanel.SetActive(_isOpen);

            if (_isOpen)
            {
                RefreshInventory();
            }

            // 打开背包时锁定玩家
            if (PlayerController.Instance != null)
                PlayerController.Instance.SetControllable(!_isOpen);
        }

        /// <summary>2026-07-21 序幕拾取背包前隐藏 UI,拾取后调 SetVisible(true) 让按钮出现。
        /// 面板打开状态不受影响(如果玩家在拾取前不该能打开面板,这里也会顺便关掉)。</summary>
        public void SetVisible(bool on)
        {
            if (_backpackButton != null) _backpackButton.SetActive(on);
            if (!on && _isOpen) { _isOpen = false; _inventoryPanel.SetActive(false); }
        }

        void RefreshInventory()
        {
            var items = GameState.GetAllItems();
            Debug.Log($"[InventoryUI] 刷新背包，当前道具数量: {items.Count}");

            // 清空所有槽位
            foreach (var slot in _slots)
            {
                slot.itemId = null;
                slot.iconImage.sprite = null;
                slot.iconImage.enabled = false;
                slot.slotObject.SetActive(false);
            }

            // 填充拥有的道具
            for (int i = 0; i < items.Count && i < MaxSlots; i++)
            {
                var itemId = items[i];
                var slot = _slots[i];

                slot.itemId = itemId;
                slot.slotObject.SetActive(true);

                Debug.Log($"[InventoryUI] 槽位 {i}: itemId={itemId}");

                // 加载道具图标
                var iconSprite = LoadItemIcon(itemId);
                if (iconSprite != null)
                {
                    slot.iconImage.sprite = iconSprite;
                    slot.iconImage.enabled = true;
                    Debug.Log($"[InventoryUI] 槽位 {i} 图标加载成功");
                }
                else
                {
                    Debug.LogWarning($"[InventoryUI] 槽位 {i} 图标加载失败，itemId={itemId}");
                }

                // 更新选中状态
                UpdateSlotHighlight(slot);
            }

            Debug.Log($"[InventoryUI] 背包刷新完成，显示了 {items.Count} 个道具");
        }

        Sprite LoadItemIcon(string itemId)
        {
            // 道具ID到图标路径的映射
            string iconPath = GetIconPath(itemId);
            if (string.IsNullOrEmpty(iconPath))
            {
                Debug.LogWarning($"[InventoryUI] 未找到道具图标映射: itemId={itemId}");
                return null;
            }

            Debug.Log($"[InventoryUI] 加载图标: itemId={itemId}, path={iconPath}");

            var sprite = Resources.Load<Sprite>(iconPath);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(iconPath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    Debug.Log($"[InventoryUI] 从Texture2D创建Sprite: {tex.width}x{tex.height}");
                }
                else
                {
                    Debug.LogWarning($"[InventoryUI] 图标资源加载失败: path={iconPath}");
                }
            }
            return sprite;
        }

        string GetIconPath(string itemId)
        {
            // 道具ID到图标路径的映射
            switch (itemId)
            {
                case "crowbar": return "UI/Icons/crowbar_v2";
                case "focus_lens": return "UI/Icons/focus_lens_v2";
                case "brass_base": return "UI/Icons/brass_base_v2";
                case "gear_1": return "UI/Icons/gear_small_v2";
                case "gear_2": return "UI/Icons/gear_big_v2";
                case "pottery_key": return "UI/Icons/pottery_key";  // 500×500 铜钥匙图标,与其他道具尺寸一致
                case "lantern": return "UI/Icons/hand_lamp";
                case "magnifier": return "UI/Icons/magnifier";
                case "hex_wrench": return "UI/Icons/hex_wrench";
                case "wrench": return "UI/Icons/wrench";  // 铸铁扳手(残骸间拾取)
                // 注意:背包本身不 Add 到 InventorySystem(见 Interact_PickupBackpack),这里不设映射
                default:
                    Debug.LogWarning($"[InventoryUI] 未配置道具图标映射: {itemId}");
                    return null;
            }
        }

        // 2026-07-25 双击检测:双击道具 → 弹"是否使用该道具"确认 → 是 → 特写+文案介绍
        float _lastClickTime = -1f;
        string _lastClickItemId = null;
        const float kDoubleClickInterval = 0.35f;

        void OnSlotClicked(InventorySlot slot)
        {
            if (string.IsNullOrEmpty(slot.itemId)) return;

            // ── 双击判定 ──
            float now = Time.unscaledTime;
            bool isDoubleClick = (slot.itemId == _lastClickItemId) &&
                                 (now - _lastClickTime <= kDoubleClickInterval);
            if (isDoubleClick)
            {
                _lastClickTime = -1f;      // 消费掉,避免三击连触
                _lastClickItemId = null;
                PromptUseItem(slot.itemId);
                return;
            }
            _lastClickTime = now;
            _lastClickItemId = slot.itemId;

            // ── 单击:切换选中状态(保留原有手感) ──
            if (InventorySystem.SelectedItem == slot.itemId)
            {
                InventorySystem.ClearSelection();
            }
            else
            {
                InventorySystem.Select(slot.itemId);
            }

            RefreshInventory();
        }

        /// <summary>2026-07-25 双击道具:弹确认框,玩家选"是"→ 展示道具特写图 + 文案介绍。
        /// 特殊:brass_base + 三件套齐 + 非中年 → 走"组装失败"特殊剧情。</summary>
        void PromptUseItem(string itemId)
        {
            ConfirmDialog.Show("是否使用该道具？",
                onYes: () =>
                {
                    // 关闭背包面板,避免背包底部描述条(order 1150)盖住道具文案(order 1100)。
                    if (_isOpen)
                    {
                        _isOpen = false;
                        _inventoryPanel.SetActive(false);
                    }
                    // 记录该道具"已被使用"
                    GameState.SetFlag($"item_used_{itemId}", true);
                    InventorySystem.ClearSelection();

                    // 2026-07-25 黄铜底座特殊剧情:集齐三件套 + 非中年 → 组装失败
                    if (itemId == Items.BrassBase &&
                        InventorySystem.Has(Items.Gear1) &&
                        InventorySystem.Has(Items.Gear2) &&
                        InventorySystem.Has(Items.BrassBase) &&
                        GameState.CurrentEra != Era.Middle)
                    {
                        PlayBrassBaseAssemblyFailure();
                        return;
                    }

                    ItemUseCloseup.Show(itemId);
                },
                onNo: null);
        }

        /// <summary>组装失败:播失败文案 + 头顶感叹号 + 4 句独白 + 更新任务指引。</summary>
        void PlayBrassBaseAssemblyFailure()
        {
            string failText = GameState.CurrentEra == Era.Young
                ? "你毛躁的双手无法完成精密的拼接！"
                : "你颤抖的双手无法完成精密的拼接！";
            DialogueSystem.ShowText(failText);

            var pc = PlayerController.Instance;
            var player = pc != null ? pc.gameObject : null;
            if (player == null) return;

            var talk = PlayerTalkPrompt.Attach(player);
            talk.SetDialogue(
                new PlayerTalkPrompt.Line("无法完成精密的拼接？",          "frown"),
                new PlayerTalkPrompt.Line("这是怎么回事？",                "think"),
                new PlayerTalkPrompt.Line("你觉得这些部件会拼成什么东西？",   "think"),
                new PlayerTalkPrompt.Line("(无奈)先回到前厅里吧",          "shy")
            );
            talk.OnAllSpoken += () =>
            {
                QuestPromptManager.UpdateText("回到神庙前厅");
                if (pc != null) pc.SetControllable(true);
            };
            talk.Show();
        }

        void UpdateSlotHighlight(InventorySlot slot)
        {
            bool isSelected = !string.IsNullOrEmpty(slot.itemId) &&
                             InventorySystem.SelectedItem == slot.itemId;
            slot.highlightObject.SetActive(isSelected);
        }

        class InventorySlot
        {
            public GameObject slotObject;
            public Image iconImage;
            public GameObject highlightObject;
            public Button button;
            public string itemId;
        }
    }
}
