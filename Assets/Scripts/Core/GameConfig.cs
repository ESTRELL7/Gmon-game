using System;
using UnityEngine;

namespace DeepDwarf
{
    public enum UpgradeType { Pick, Swing, Bag, Energy, Lantern, Lift, Luck }

    [Serializable]
    public class LayerDef
    {
        public string id;
        public string title;
        public int startDepth;
        public float hp;
        public Color32 chipColor;
    }

    [Serializable]
    public class OreDef
    {
        public string id;
        public string title;
        public int minDepth;
        public int peakDepth;
        [Range(0f, 0.2f)] public float chance;
        public long value;
        public float hpMultiplier;
        public Color32 color;
    }

    [Serializable]
    public class UpgradeDef
    {
        public UpgradeType type;
        public string title;
        public string icon;
        public long baseCost;
        public float costGrowth;
        public int maxLevel;
    }

    /// <summary>All balance numbers. Edit Assets/Resources/GameConfig.asset in the Inspector.</summary>
    [CreateAssetMenu(menuName = "Deep Dwarf/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public int worldWidth = 11;

        [Header("Upgrade effects")]
        public float powerBase = 1f;
        public float powerGrowth = 1.6f;
        public float swingBase = 0.4f;
        public float swingFactor = 0.9f;
        public int bagBase = 10;
        public int bagPerLevel = 8;
        public int energyBase = 60;
        public int energyPerLevel = 25;
        public float lanternBase = 3.5f;
        public float lanternPerLevel = 0.8f;
        public float liftBase = 0.5f;
        public float liftFactor = 0.85f;
        public float luckPerLevel = 0.05f;

        [Header("Movement, tiles per second")]
        public float walkSpeed = 6f;
        public float ladderSpeed = 10f;
        public float fallSpeed = 14f;

        [Header("Other")]
        [Tooltip("Blocks below the last layer get this much harder per tile of depth.")]
        public float deepHpGrowth = 1.02f;
        [Tooltip("Share of the bag lost when the dwarf runs out of energy.")]
        public float faintBagLoss = 0.5f;

        public LayerDef[] layers;
        public OreDef[] ores;
        public UpgradeDef[] upgrades;

        public static GameConfig Load()
        {
            var config = Resources.Load<GameConfig>("GameConfig");
            return config != null ? config : CreateDefault();
        }

        public static GameConfig CreateDefault()
        {
            var c = CreateInstance<GameConfig>();
            c.layers = new[]
            {
                Layer("soil", "ПОЧВА", 0, 2, 122, 78, 44),
                Layer("clay", "ГЛИНА", 12, 4, 160, 94, 58),
                Layer("stone", "КАМЕНЬ", 30, 9, 112, 112, 118),
                Layer("granite", "ГРАНИТ", 55, 20, 136, 112, 112),
                Layer("slate", "СЛАНЕЦ", 90, 45, 70, 82, 104),
                Layer("basalt", "БАЗАЛЬТ", 135, 100, 56, 54, 66),
                Layer("magma", "МАГМОВАЯ ПОРОДА", 190, 220, 232, 96, 36),
                Layer("crystal", "КРИСТАЛЬНАЯ ПОРОДА", 260, 480, 150, 120, 220),
                Layer("abyss", "БЕЗДНА", 350, 1000, 96, 40, 140),
            };
            c.ores = new[]
            {
                Ore("coal", "УГОЛЬ", 1, 8, 0.07f, 2, 1f, 84, 84, 96),
                Ore("copper", "МЕДЬ", 5, 18, 0.05f, 5, 1.2f, 206, 120, 60),
                Ore("iron", "ЖЕЛЕЗО", 18, 35, 0.045f, 12, 1.3f, 182, 158, 146),
                Ore("silver", "СЕРЕБРО", 35, 60, 0.04f, 30, 1.4f, 190, 206, 220),
                Ore("gold", "ЗОЛОТО", 60, 90, 0.035f, 70, 1.5f, 244, 190, 40),
                Ore("ruby", "РУБИН", 90, 125, 0.03f, 160, 1.6f, 222, 38, 72),
                Ore("sapphire", "САПФИР", 125, 165, 0.028f, 350, 1.7f, 44, 104, 232),
                Ore("emerald", "ИЗУМРУД", 165, 215, 0.026f, 800, 1.8f, 34, 190, 104),
                Ore("diamond", "АЛМАЗ", 215, 280, 0.024f, 1800, 2f, 150, 226, 244),
                Ore("mithril", "МИФРИЛ", 280, 360, 0.022f, 4000, 2.2f, 80, 226, 200),
                Ore("adamantite", "АДАМАНТИТ", 360, 460, 0.02f, 9000, 2.5f, 200, 50, 220),
            };
            c.upgrades = new[]
            {
                Upgrade(UpgradeType.Pick, "КИРКА", "pick", 30, 2f, 20),
                Upgrade(UpgradeType.Swing, "ЗАМАХ", "swing", 40, 1.9f, 10),
                Upgrade(UpgradeType.Bag, "РЮКЗАК", "bag", 25, 1.8f, 20),
                Upgrade(UpgradeType.Energy, "ВЫНОСЛИВОСТЬ", "bolt", 35, 1.8f, 20),
                Upgrade(UpgradeType.Lantern, "ФОНАРЬ", "lantern", 45, 2f, 8),
                Upgrade(UpgradeType.Lift, "ПОДЪЁМНИК", "lift", 60, 1.9f, 12),
                Upgrade(UpgradeType.Luck, "УДАЧА", "clover", 80, 2.1f, 10),
            };
            return c;
        }

        static LayerDef Layer(string id, string title, int start, float hp, byte r, byte g, byte b) =>
            new LayerDef { id = id, title = title, startDepth = start, hp = hp, chipColor = new Color32(r, g, b, 255) };

        static OreDef Ore(string id, string title, int min, int peak, float chance, long value, float hp, byte r, byte g, byte b) =>
            new OreDef { id = id, title = title, minDepth = min, peakDepth = peak, chance = chance, value = value, hpMultiplier = hp, color = new Color32(r, g, b, 255) };

        static UpgradeDef Upgrade(UpgradeType type, string title, string icon, long cost, float growth, int max) =>
            new UpgradeDef { type = type, title = title, icon = icon, baseCost = cost, costGrowth = growth, maxLevel = max };
    }
}
