using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeepDwarf
{
    /// <summary>Builds the whole interface in code: HUD, D-pad, shop, messages.</summary>
    public class GameUI : MonoBehaviour
    {
        public const float ReferenceWidth = 540f;
        public const float ReferenceHeight = 960f;
        public const float HudHeight = 112f;
        public const float ControlsHeight = 236f;

        // Image.pixelsPerUnitMultiplier that draws 9-slice borders 3x bigger than their pixels.
        const float FrameScale = 1.39f;
        static readonly Color Cream = new Color32(250, 238, 210, 255);
        static readonly Color Gold = new Color32(255, 214, 90, 255);
        static readonly Color Green = new Color32(140, 224, 110, 255);
        static readonly Color Red = new Color32(255, 110, 90, 255);

        class ShopRow
        {
            public int index;
            public PixelLabel title, effect, price;
            public Button button;
            public Image buttonImage;
        }

        GameSim sim;
        Action<string> playSound;
        Action onMuteToggle;
        RectTransform safeRoot;
        CanvasScaler scaler;
        Rect appliedSafeArea;

        PixelLabel depthLabel, moneyLabel, energyLabel, bagLabel, liftLabel, messageLabel;
        Image energyFill, bagFill, liftImage;
        GameObject shopPanel, messageBack;
        PixelLabel shopInfo;
        readonly List<ShopRow> rows = new List<ShopRow>();
        readonly Queue<PixelLabel> floatPool = new Queue<PixelLabel>();
        readonly List<(PixelLabel label, float left)> floats = new List<(PixelLabel, float)>();
        float messageLeft;
        Image muteIcon;

        public HoldButton Up { get; private set; }
        public HoldButton Down { get; private set; }
        public HoldButton Left { get; private set; }
        public HoldButton Right { get; private set; }
        public bool ShopOpen => shopPanel.activeSelf;
        public RectTransform Root => safeRoot;

        public void Init(GameSim sim, Action<string> playSound, Action onMuteToggle)
        {
            this.sim = sim;
            this.playSound = playSound;
            this.onMuteToggle = onMuteToggle;

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            canvasObject.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            safeRoot = Node("SafeArea", canvasObject.transform);
            Stretch(safeRoot);

            BuildHud();
            BuildControls();
            BuildMessage();
            BuildShop();
            ApplyScreenLayout();
            Refresh();
        }

        // ---------- construction helpers ----------

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Image Frame(string name, Transform parent, string sprite)
        {
            var image = Node(name, parent).gameObject.AddComponent<Image>();
            image.sprite = Art.Sprite(sprite);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FrameScale;
            return image;
        }

        static Image Icon(string sprite, Transform parent, Vector2 position, float size)
        {
            var image = Node(sprite, parent).gameObject.AddComponent<Image>();
            image.sprite = Art.Sprite(sprite);
            image.preserveAspect = true;
            image.raycastTarget = false;
            Place((RectTransform)image.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), position, new Vector2(size, size));
            return image;
        }

        static PixelLabel Label(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var label = Node("Label", parent).gameObject.AddComponent<PixelLabel>();
            label.Setup(size, anchor);
            label.color = color;
            label.Text = text;
            return label;
        }

        // ---------- layout ----------

        void BuildHud()
        {
            var hud = Frame("Hud", safeRoot, "ui_panel");
            var rt = hud.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, HudHeight);

            depthLabel = Label(rt, "", 3, TextAnchor.MiddleLeft, Cream);
            Place(depthLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(300f, 30f));

            moneyLabel = Label(rt, "", 3, TextAnchor.MiddleRight, Gold);
            Place(moneyLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -12f), new Vector2(180f, 30f));
            var coin = Icon("icon_coin", rt, Vector2.zero, 28f);
            Place((RectTransform)coin.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(28f, 28f));

            energyFill = Bar(rt, "icon_bolt", new Vector2(16f, -48f), new Color32(255, 196, 60, 255), out energyLabel);
            bagFill = Bar(rt, "icon_bag", new Vector2(16f, -78f), new Color32(120, 190, 240, 255), out bagLabel);
        }

        Image Bar(RectTransform parent, string icon, Vector2 position, Color color, out PixelLabel label)
        {
            var image = Icon(icon, parent, Vector2.zero, 24f);
            Place((RectTransform)image.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(24f, 24f));

            var back = Frame("BarBack", parent, "ui_bar_back");
            back.raycastTarget = false;
            back.rectTransform.anchorMin = new Vector2(0f, 1f);
            back.rectTransform.anchorMax = new Vector2(1f, 1f);
            back.rectTransform.pivot = new Vector2(0f, 1f);
            back.rectTransform.anchoredPosition = position + new Vector2(32f, 0f);
            back.rectTransform.sizeDelta = new Vector2(-64f, 24f);

            var fill = Node("Fill", back.rectTransform).gameObject.AddComponent<Image>();
            fill.sprite = Art.Sprite("ui_white");
            fill.color = color;
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

            label = Label(back.rectTransform, "", 2, TextAnchor.MiddleCenter, Cream);
            Stretch(label.rectTransform);
            return fill;
        }

        static void SetFill(Image fill, float share)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
        }

        void BuildControls()
        {
            var panel = Frame("Controls", safeRoot, "ui_panel");
            var rt = panel.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, ControlsHeight);

            const float size = 100f, gap = 6f;
            float bottom = 12f, right = -14f;
            Right = Arrow(rt, new Vector2(right, bottom), size, -90f);
            Down = Arrow(rt, new Vector2(right - size - gap, bottom), size, 180f);
            Left = Arrow(rt, new Vector2(right - (size + gap) * 2f, bottom), size, 90f);
            Up = Arrow(rt, new Vector2(right - size - gap, bottom + size + gap), size, 0f);

            liftImage = Frame("Lift", rt, "ui_button_green");
            Place(liftImage.rectTransform, Vector2.zero, Vector2.zero, new Vector2(14f, bottom + size + gap), new Vector2(190f, size));
            var lift = liftImage.gameObject.AddComponent<Button>();
            lift.targetGraphic = liftImage;
            lift.onClick.AddListener(OnLiftPressed);
            liftLabel = Label(liftImage.rectTransform, "", 2, TextAnchor.MiddleCenter, Cream);
            Stretch(liftLabel.rectTransform);

            var mute = Frame("Mute", rt, "ui_button");
            Place(mute.rectTransform, Vector2.zero, Vector2.zero, new Vector2(14f, bottom), new Vector2(72f, 72f));
            var muteButton = mute.gameObject.AddComponent<Button>();
            muteButton.targetGraphic = mute;
            muteButton.onClick.AddListener(() => { onMuteToggle(); RefreshMute(); });
            muteIcon = Icon("icon_sound", mute.rectTransform, new Vector2(16f, 0f), 40f);
            RefreshMute();
        }

        HoldButton Arrow(RectTransform parent, Vector2 position, float size, float angle)
        {
            var image = Frame("Arrow", parent, "ui_button");
            Place(image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), position, new Vector2(size, size));
            var hold = image.gameObject.AddComponent<HoldButton>();
            hold.Init(image);

            var arrow = Node("Icon", image.rectTransform).gameObject.AddComponent<Image>();
            arrow.sprite = Art.Sprite("ui_arrow");
            arrow.raycastTarget = false;
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(48f, 48f);
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return hold;
        }

        void BuildMessage()
        {
            var back = Frame("MessageBack", safeRoot, "ui_panel");
            back.raycastTarget = false;
            back.color = new Color(1f, 1f, 1f, 0.92f);
            Place(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -HudHeight - 12f), new Vector2(500f, 78f));
            messageBack = back.gameObject;
            messageLabel = Label(back.rectTransform, "", 2, TextAnchor.MiddleCenter, Cream);
            messageLabel.rectTransform.anchorMin = Vector2.zero;
            messageLabel.rectTransform.anchorMax = Vector2.one;
            messageLabel.rectTransform.offsetMin = new Vector2(14f, 6f);
            messageLabel.rectTransform.offsetMax = new Vector2(-14f, -6f);
            messageBack.SetActive(false);
        }

        void BuildShop()
        {
            var panel = Frame("Shop", safeRoot, "ui_panel");
            shopPanel = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Mathf.Min(ReferenceWidth - 16f, 524f), 0f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 8f);
            rt.offsetMax = new Vector2(rt.offsetMax.x, -HudHeight - 4f);

            var title = Label(rt, "ЛАВКА", 4, TextAnchor.MiddleCenter, Gold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            title.rectTransform.sizeDelta = new Vector2(-24f, 34f);

            shopInfo = Label(rt, "", 2, TextAnchor.MiddleCenter, Green);
            shopInfo.rectTransform.anchorMin = new Vector2(0f, 1f);
            shopInfo.rectTransform.anchorMax = new Vector2(1f, 1f);
            shopInfo.rectTransform.pivot = new Vector2(0.5f, 1f);
            shopInfo.rectTransform.anchoredPosition = new Vector2(0f, -52f);
            shopInfo.rectTransform.sizeDelta = new Vector2(-24f, 22f);

            const float rowHeight = 84f;
            for (int i = 0; i < sim.Config.upgrades.Length; i++)
                rows.Add(BuildRow(rt, i, -82f - i * rowHeight, rowHeight - 6f));

            var close = Frame("Close", rt, "ui_button_green");
            close.rectTransform.anchorMin = new Vector2(0f, 0f);
            close.rectTransform.anchorMax = new Vector2(1f, 0f);
            close.rectTransform.pivot = new Vector2(0.5f, 0f);
            close.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            close.rectTransform.sizeDelta = new Vector2(-28f, 72f);
            var button = close.gameObject.AddComponent<Button>();
            button.targetGraphic = close;
            button.onClick.AddListener(CloseShop);
            var label = Label(close.rectTransform, "В ШАХТУ!", 3, TextAnchor.MiddleCenter, Cream);
            Stretch(label.rectTransform);

            shopPanel.SetActive(false);
        }

        ShopRow BuildRow(RectTransform parent, int index, float y, float height)
        {
            var def = sim.Config.upgrades[index];
            var row = new ShopRow { index = index };

            var back = Frame("Row", parent, "ui_bar_back");
            back.raycastTarget = false;
            var rt = back.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-28f, height);

            Icon("icon_" + def.icon, rt, new Vector2(10f, 0f), 54f);

            row.title = Label(rt, "", 2, TextAnchor.MiddleLeft, Cream);
            Place(row.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -12f), new Vector2(270f, 20f));
            row.effect = Label(rt, "", 2, TextAnchor.MiddleLeft, Green);
            Place(row.effect.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(74f, 12f), new Vector2(270f, 20f));

            row.buttonImage = Frame("Buy", rt, "ui_button");
            Place(row.buttonImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(136f, height - 12f));
            row.button = row.buttonImage.gameObject.AddComponent<Button>();
            row.button.targetGraphic = row.buttonImage;
            row.button.transition = Selectable.Transition.None;
            row.button.onClick.AddListener(() => OnBuy(row));
            row.price = Label(row.buttonImage.rectTransform, "", 2, TextAnchor.MiddleCenter, Cream);
            Stretch(row.price.rectTransform);
            return row;
        }

        // ---------- behaviour ----------

        void OnLiftPressed()
        {
            if (sim.OnSurface)
            {
                OpenShop("");
                return;
            }
            if (!sim.Lift())
            {
                playSound("denied");
                ShowMessage("НЕ ХВАТАЕТ СИЛ НА ПОДЪЁМ!\nКАРАБКАЙСЯ ИЛИ ДОБЕРИСЬ ВЫШЕ", 2.5f);
            }
        }

        void OnBuy(ShopRow row)
        {
            if (!sim.Buy(row.index))
            {
                playSound("denied");
                return;
            }
            playSound("upgrade");
            Refresh();
        }

        public void OpenShop(string info)
        {
            shopInfo.Text = info;
            shopPanel.SetActive(true);
            messageBack.SetActive(false);
            Refresh();
        }

        void CloseShop()
        {
            shopPanel.SetActive(false);
            Refresh();
        }

        public void ShowMessage(string text, float seconds)
        {
            messageLabel.Text = text;
            messageLeft = seconds;
            messageBack.SetActive(!ShopOpen);
        }

        public void ShowFloat(string text, Vector3 worldPosition, Color color)
        {
            var label = floatPool.Count > 0 ? floatPool.Dequeue() : Label(safeRoot, "", 2, TextAnchor.MiddleCenter, color);
            label.gameObject.SetActive(true);
            label.color = color;
            label.Text = text;
            var rt = label.rectTransform;
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 20f);
            Vector2 screen = Camera.main.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot, screen, null, out Vector2 local);
            rt.anchoredPosition = local;
            floats.Add((label, 0.9f));
        }

        void Update()
        {
            ApplyScreenLayout();
            Refresh();

            if (messageLeft > 0f)
            {
                messageLeft -= Time.deltaTime;
                if (messageLeft <= 0f) messageBack.SetActive(false);
            }

            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var (label, left) = floats[i];
                left -= Time.deltaTime;
                label.rectTransform.anchoredPosition += new Vector2(0f, 60f * Time.deltaTime);
                if (left <= 0f)
                {
                    label.gameObject.SetActive(false);
                    floatPool.Enqueue(label);
                    floats.RemoveAt(i);
                }
                else
                {
                    floats[i] = (label, left);
                }
            }
        }

        void Refresh()
        {
            depthLabel.Text = sim.OnSurface ? "ПОВЕРХНОСТЬ" : "ГЛУБИНА " + sim.Depth;
            moneyLabel.Text = Art.FormatNumber(sim.Data.money);

            SetFill(energyFill, sim.Energy / sim.MaxEnergy);
            energyLabel.Text = Mathf.CeilToInt(sim.Energy) + "/" + sim.MaxEnergy;
            SetFill(bagFill, sim.BagCount / (float)sim.BagCapacity);
            bagLabel.Text = sim.BagCount + "/" + sim.BagCapacity;

            if (sim.OnSurface)
            {
                liftLabel.Text = "ЛАВКА";
                liftImage.sprite = Art.Sprite("ui_button_green");
            }
            else
            {
                liftLabel.Text = "ПОДЪЁМ\n-" + sim.LiftCost;
                liftImage.sprite = Art.Sprite(sim.CanLift ? "ui_button_green" : "ui_button_off");
            }

            if (!ShopOpen) return;
            foreach (var row in rows)
            {
                var def = sim.Config.upgrades[row.index];
                int level = sim.Data.levels[row.index];
                row.title.Text = def.title + " " + level;
                bool maxed = sim.IsMaxed(row.index);
                row.effect.Text = EffectText(def.type, level, maxed);
                row.price.Text = maxed ? "МАКС" : Art.FormatNumber(sim.UpgradeCost(row.index));
                row.price.color = sim.CanBuy(row.index) || maxed ? Cream : Red;
                row.buttonImage.sprite = Art.Sprite(maxed || !sim.CanBuy(row.index) ? "ui_button_off" : "ui_button");
            }
        }

        string EffectText(UpgradeType type, int level, bool maxed)
        {
            string Pair(string now, string next) => maxed ? now : now + " > " + next;
            switch (type)
            {
                case UpgradeType.Pick: return "СИЛА " + Pair(Short(sim.PowerAt(level)), Short(sim.PowerAt(level + 1)));
                case UpgradeType.Swing: return "УДАР/С " + Pair(Short(1f / sim.SwingAt(level)), Short(1f / sim.SwingAt(level + 1)));
                case UpgradeType.Bag: return "МЕСТ " + Pair(sim.BagAt(level).ToString(), sim.BagAt(level + 1).ToString());
                case UpgradeType.Energy: return "СИЛЫ " + Pair(sim.EnergyAt(level).ToString(), sim.EnergyAt(level + 1).ToString());
                case UpgradeType.Lantern: return "СВЕТ " + Pair(Short(sim.LanternAt(level)), Short(sim.LanternAt(level + 1)));
                case UpgradeType.Lift: return "ЦЕНА/М " + Pair(Short(sim.LiftAt(level)), Short(sim.LiftAt(level + 1)));
                default: return "ДВОЙНАЯ РУДА " + Pair(Percent(sim.LuckAt(level)), Percent(sim.LuckAt(level + 1)));
            }
        }

        static string Short(float value) =>
            value.ToString(value < 100f ? "0.##" : "0", System.Globalization.CultureInfo.InvariantCulture);

        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        void RefreshMute()
        {
            muteIcon.color = sim.Data.muted ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        }

        void ApplyScreenLayout()
        {
            // Phones scale by width; wider windows scale by height so nothing falls off screen.
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect < ReferenceWidth / ReferenceHeight ? 0f : 1f;

            Rect safe = Screen.safeArea;
            if (safe == appliedSafeArea) return;
            appliedSafeArea = safe;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }
    }
}
