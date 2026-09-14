// ============================================================================
//  InventoryUI.cs —— 背包UI面板（2026-07-19 建；2026-09-14 换成新版格子美术）
//  功能：
//    · 屏幕右边缘居中显示背包按钮
//    · 点击打开/关闭背包面板
//    · 显示当前拥有的所有道具（带图标）
//    · 点击道具可选中/取消选中；双击弹「是否使用该道具」
//  美术资源：
//    · backpack_icon.png      - 背包按钮图标
//    · bag_panel_v2.png       - 背包面板底图(4x4 格,来自 背包空白格子.psd)
//    · Icons/*.png            - 各道具图标
//
//  ★ 2026-09-14 改版要点：
//    1) 旧版把 slot_empty.png(3400x1200) **拉伸填满**中间条带,槽位用 4 组硬编码像素
//       定位。新底图 1121x908 里背包只占画布一部分,再拉伸会整体变形,
//       所以改成「按高度等比缩放 + 居中」。
//    2) 所有格位/图标尺寸统一由 InventoryPanelLayout 算 —— 与调试场景
//       (Scenes/InventoryDebug.unity + InventoryDebugBootstrap) 走**同一份**代码,
//       调试场景里调的就是游戏里生效的值。
//    3) 条带高度改用 LetterboxOverlay.BarHeightPct(旧版这里硬编码 0.2018,
//       与 LetterboxOverlay 的 0.1863 不一致 → 面板与黑边对不上)。
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
        GameObject _bagUIGo;             // 中间条带里的背包容器(按高度等比缩放)
        Transform _itemsContainer;       // 道具容器
        Image _dimImage;                 // 全屏灰暗遮罩
        List<InventorySlot> _slots = new List<InventorySlot>();

        const int MaxSlots = InventoryPanelLayout.SlotCount;   // 4x4 = 16 格
        bool _isOpen = false;

        public static InventoryUI Instance => _instance;
        public bool IsOpen => _isOpen;

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

            float barPct = LetterboxOverlay.BarHeightPct;

            // ========== 1. 背包按钮(中间场景条带的右上角,letterbox 内) ==========
            _backpackButton = new GameObject("BackpackButton");
            _backpackButton.transform.SetParent(transform, false);

            var btnRt = _backpackButton.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1, 1 - barPct);
            btnRt.anchorMax = new Vector2(1, 1 - barPct);
            btnRt.pivot = new Vector2(1, 1);
            btnRt.anchoredPosition = new Vector2(-30, -30);  // 距场景右上角 30px 内边距
            btnRt.sizeDelta = new Vector2(120, 120);

            // 背包图标
            var btnImg = _backpackButton.AddComponent<Image>();
            var btnSprite = LoadSprite("UI/Inventory/backpack_icon");
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

            // 2026-07-21 序幕设定:第 0 幕的老人还没捡到背包,序幕开场默认隐藏背包 UI。
            //   触发 SetVisible(true) 才出现——一般由拾取背包事件 (PickupBackpack) 调用。
            _backpackButton.SetActive(false);

            // ========== 2. 背包面板（全屏显示，灰暗遮罩 + 中间条带上的背包图） ==========
            _inventoryPanel = new GameObject("InventoryPanel");
            _inventoryPanel.transform.SetParent(transform, false);

            var panelRt = _inventoryPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;  // 全屏
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            // 半透明黑色遮罩背景（美术 PSD 的「图层 1」= 纯黑 153/255 = 0.6）
            var dimBgGo = new GameObject("DimBackground");
            dimBgGo.transform.SetParent(_inventoryPanel.transform, false);
            var dimBgRt = dimBgGo.AddComponent<RectTransform>();
            dimBgRt.anchorMin = Vector2.zero;
            dimBgRt.anchorMax = Vector2.one;
            dimBgRt.offsetMin = Vector2.zero;
            dimBgRt.offsetMax = Vector2.zero;

            _dimImage = dimBgGo.AddComponent<Image>();
            _dimImage.raycastTarget = true;  // 阻止点击穿透

            // 背包底图容器:锚在屏幕正中(条带是上下对称的 ⇒ 条带中心 = 屏幕中心),
            //   pivot=中心,尺寸 = PanelDisplaySize(按高度等比,不再拉伸变形)
            _bagUIGo = new GameObject("BagUI");
            _bagUIGo.transform.SetParent(_inventoryPanel.transform, false);
            var bagUIRt = _bagUIGo.AddComponent<RectTransform>();
            bagUIRt.anchorMin = new Vector2(0.5f, 0.5f);
            bagUIRt.anchorMax = new Vector2(0.5f, 0.5f);
            bagUIRt.pivot = new Vector2(0.5f, 0.5f);
            bagUIRt.anchoredPosition = Vector2.zero;

            var panelBg = _bagUIGo.AddComponent<Image>();
            var bagUISprite = LoadSprite(InventoryPanelLayout.PanelResourcePath);
            if (bagUISprite != null)
            {
                panelBg.sprite = bagUISprite;
                panelBg.type = Image.Type.Simple;
                panelBg.preserveAspect = false;   // 尺寸我们自己算(已保持宽高比),不用它再算一次
                panelBg.raycastTarget = false;
                panelBg.color = Color.white;
                Debug.Log($"[InventoryUI] 背包底图加载成功 {bagUISprite.rect.width}x{bagUISprite.rect.height}");
            }
            else
            {
                panelBg.color = new Color(0.3f, 0.25f, 0.2f, 0.95f);
                Debug.LogWarning($"[InventoryUI] 找不到 Resources/{InventoryPanelLayout.PanelResourcePath},用兜底背景");
            }

            // 道具容器:撑满 BagUI;每个槽位用 InventoryPanelLayout 的逐格中心定位
            var containerGo = new GameObject("ItemsContainer");
            containerGo.transform.SetParent(_bagUIGo.transform, false);
            var containerRt = containerGo.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;   // 撑满 BagUI
            containerRt.anchorMax = Vector2.one;
            containerRt.offsetMin = Vector2.zero;
            containerRt.offsetMax = Vector2.zero;
            _itemsContainer = containerGo.transform;

            // 关闭按钮(锚到中间场景右上角)
            var closeBtnGo = new GameObject("CloseButton");
            closeBtnGo.transform.SetParent(_inventoryPanel.transform, false);
            var closeBtnRt = closeBtnGo.AddComponent<RectTransform>();
            closeBtnRt.anchorMin = new Vector2(1, 1 - barPct);
            closeBtnRt.anchorMax = new Vector2(1, 1 - barPct);
            closeBtnRt.pivot = new Vector2(1, 1);
            closeBtnRt.anchoredPosition = new Vector2(-40, -40);
            closeBtnRt.sizeDelta = new Vector2(140, 140);

            var closeBtnImg = closeBtnGo.AddComponent<Image>();
            closeBtnImg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);

            var closeBtnText = new GameObject("Text");
            closeBtnText.transform.SetParent(closeBtnGo.transform, false);
            var closeTxtRt = closeBtnText.AddComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero;
            closeTxtRt.offsetMax = Vector2.zero;

            var closeTxt = closeBtnText.AddComponent<Text>();
            closeTxt.text = "×";
            closeTxt.font = GameFonts.Primary;
            closeTxt.fontSize = 90;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.fontStyle = FontStyle.Bold;

            var closeBtn = closeBtnGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(() => ToggleInventory());

            // 创建槽位(位置/尺寸在 LayoutAll() 里按当前参数统一算)
            for (int i = 0; i < MaxSlots; i++)
                _slots.Add(CreateSlot(i));

            // 底部常驻背包描述条
            BuildDescriptionBar(_inventoryPanel.transform);

            LayoutAll();

            _inventoryPanel.SetActive(false);
        }

        /// <summary>底部常驻背包描述条(放到下方黑边区,与对话字幕同位置)。
        /// 关键:InventoryUI canvas order=300 会被 Letterbox(1000)盖住,所以给描述条单独挂
        /// 子 Canvas 覆写 order=1150(高于 letterbox 的 1000,低于 dialogue 的 1200)。</summary>
        void BuildDescriptionBar(Transform parent)
        {
            float barPct = LetterboxOverlay.BarHeightPct;

            var descGo = new GameObject("BackpackDescription");
            descGo.transform.SetParent(parent, false);

            var subCanvas = descGo.AddComponent<Canvas>();
            subCanvas.overrideSorting = true;
            subCanvas.sortingOrder = 1150;   // > Letterbox 1000, 与 DialogueSystem 1100 同层
            descGo.AddComponent<GraphicRaycaster>();

            var rt = descGo.GetComponent<RectTransform>();
            if (rt == null) rt = descGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 1080f * barPct);
            rt.anchoredPosition = Vector2.zero;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(descGo.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text = "容量很大的旧皮革背包,带有可照明的油灯。结实耐用,可在此查看你收集的所有的东西。";
            txt.font = GameFonts.Primary;
            txt.fontSize = 30;
            txt.color = new Color(1f, 0.96f, 0.9f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
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

            // 锚到容器左上角,pivot=中心;anchoredPosition/sizeDelta 由 LayoutAll() 统一设置
            var slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0, 1);
            slotRt.anchorMax = new Vector2(0, 1);
            slotRt.pivot = new Vector2(0.5f, 0.5f);

            // 槽位不需要背景（背包底图已经有格子了）,只要一个几乎透明的 Image 接点击
            var slotImg = slotGo.AddComponent<Image>();
            slotImg.color = new Color(0, 0, 0, 0.01f);
            slotImg.raycastTarget = true;

            // 调试用中心点(默认关,SetSlotDebugOutline 打开):看道具是否落在格子正中
            var dotGo = new GameObject("DebugCenter");
            dotGo.transform.SetParent(slotGo.transform, false);
            var dotRt = dotGo.AddComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.5f, 0.5f);
            dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = Vector2.zero;
            dotRt.sizeDelta = new Vector2(7f, 7f);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = new Color(0.15f, 0.85f, 1f, 0.95f);
            dotImg.raycastTarget = false;
            dotGo.SetActive(false);

            // 道具图标:锚到槽位中心,尺寸/偏移由 LayoutAll() 按内容框算
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.preserveAspect = false;   // 尺寸已按内容框算好,不需要它再算
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
            highlightImg.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            highlightImg.raycastTarget = false;
            highlightGo.SetActive(false);

            // 按钮
            var slotBtn = slotGo.AddComponent<Button>();
            slotBtn.targetGraphic = slotImg;
            slotBtn.transition = UnityEngine.UI.Selectable.Transition.None;

            var slot = new InventorySlot
            {
                slotObject = slotGo,
                iconObject = iconGo,
                iconImage = iconImg,
                highlightObject = highlightGo,
                slotImage = slotImg,
                debugCenter = dotGo,
                button = slotBtn,
                itemId = null
            };

            slotBtn.onClick.AddListener(() => OnSlotClicked(slot));

            return slot;
        }

        void ToggleInventory() => SetOpen(!_isOpen);

        /// <summary>外部(pickup 事件 / 调试场景)也可直接开关。</summary>
        public void SetOpen(bool open)
        {
            _isOpen = open;
            _inventoryPanel.SetActive(_isOpen);

            if (_isOpen)
            {
                LayoutAll();
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

        /// <summary>按当前 InventoryPanelLayout 参数重排面板与全部格位。
        /// ★ 改任何排版参数(或调试场景里调参)后调用它,不需要重建 UI。</summary>
        public void LayoutAll()
        {
            if (_bagUIGo == null) return;

            float k = InventoryPanelLayout.ScaleFactor;
            Vector2 panelSize = InventoryPanelLayout.PanelDisplaySize;

            var bagRt = _bagUIGo.GetComponent<RectTransform>();
            if (bagRt != null) bagRt.sizeDelta = panelSize;

            if (_dimImage != null)
                _dimImage.color = new Color(0f, 0f, 0f, InventoryPanelLayout.DimAlpha);

            var tileSize = new Vector2(InventoryPanelLayout.TileW * k, InventoryPanelLayout.TileH * k);

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var rt = slot.slotObject.GetComponent<RectTransform>();
                Vector2 c = InventoryPanelLayout.SlotCenter(i);
                var slotPos = new Vector2(c.x * k, -c.y * k);
                rt.anchoredPosition = slotPos;
                rt.sizeDelta = tileSize;

                // 图标:位置相对**槽位中心**(槽位自身已经落在格子中心了)
                if (slot.itemId != null)
                {
                    ItemIconGeom g;
                    if (InventoryPanelLayout.TryGet(slot.itemId, out g))
                    {
                        Vector2 size, absPos;
                        InventoryPanelLayout.ComputeSlot(i, slot.itemId, g, out size, out absPos);
                        var iconRt = slot.iconObject.GetComponent<RectTransform>();
                        iconRt.sizeDelta = size;
                        iconRt.anchoredPosition = absPos - slotPos;
                    }
                }
            }
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
                slot.iconObject.SetActive(false);
                slot.slotObject.SetActive(false);
            }

            // 填充拥有的道具
            for (int i = 0; i < items.Count && i < MaxSlots; i++)
            {
                var itemId = items[i];
                var slot = _slots[i];

                slot.itemId = itemId;
                slot.slotObject.SetActive(true);
                slot.iconObject.SetActive(true);

                // 加载道具图标
                var iconSprite = LoadItemIcon(itemId);
                if (iconSprite != null)
                {
                    slot.iconImage.sprite = iconSprite;
                    slot.iconImage.enabled = true;
                }
                else
                {
                    Debug.LogWarning($"[InventoryUI] 槽位 {i} 图标加载失败，itemId={itemId}");
                }

                // 更新选中状态
                UpdateSlotHighlight(slot);
            }

            LayoutAll();
        }

        Sprite LoadItemIcon(string itemId)
        {
            ItemIconGeom g;
            if (!InventoryPanelLayout.TryGet(itemId, out g))
            {
                Debug.LogWarning($"[InventoryUI] InventoryPanelLayout 里没有该道具: {itemId}");
                return null;
            }
            var sp = LoadSprite(g.resourcePath);
            if (sp == null)
                Debug.LogWarning($"[InventoryUI] 图标资源加载失败: {g.resourcePath}");
            return sp;
        }

        /// <summary>先按 Sprite 取;拿不到(有些图是 Default 导入)再退回 Texture2D 现做。</summary>
        static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sp = Resources.Load<Sprite>(path);
            if (sp != null) return sp;
            var tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);
            return null;
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
                    if (_isOpen) SetOpen(false);
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

        /// <summary>调试场景用:把格子框与中心点画出来(检查道具是否居中、大小是否合适)。</summary>
        public void SetSlotDebugOutline(bool on)
        {
            foreach (var s in _slots)
            {
                if (s.slotImage != null)
                    s.slotImage.color = on ? new Color(1f, 0.15f, 0.15f, 0.22f)
                                           : new Color(0f, 0f, 0f, 0.01f);
                if (s.debugCenter != null) s.debugCenter.SetActive(on);
            }
        }

        class InventorySlot
        {
            public GameObject slotObject;
            public GameObject iconObject;
            public Image iconImage;
            public GameObject highlightObject;
            public Image slotImage;
            public GameObject debugCenter;
            public Button button;
            public string itemId;
        }
    }
}
