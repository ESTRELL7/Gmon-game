using System.Collections.Generic;
using UnityEngine;

namespace DeepDwarf
{
    /// <summary>
    /// Endless grid. y = -1 is the surface row the dwarf walks on, y >= 0 is underground.
    /// Terrain is computed from the seed, only what the player changed is stored.
    /// </summary>
    public class World
    {
        public const int SurfaceY = -1;
        const float VeinSpread = 0.45f;

        public readonly int Width;
        readonly GameConfig config;
        readonly int seed;
        readonly HashSet<int> dug = new HashSet<int>();
        readonly HashSet<int> ladders = new HashSet<int>();

        static readonly int[] NeighbourX = { -1, 1, 0, 0 };
        static readonly int[] NeighbourY = { 0, 0, -1, 1 };

        public World(GameConfig config, int seed)
        {
            this.config = config;
            this.seed = seed;
            Width = config.worldWidth;
        }

        int Key(int x, int y) => y * Width + x;

        public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= SurfaceY;
        public bool IsSolid(int x, int y) => y >= 0 && (x < 0 || x >= Width || !dug.Contains(Key(x, y)));
        public bool HasLadder(int x, int y) => y >= 0 && ladders.Contains(Key(x, y));
        public void Dig(int x, int y) => dug.Add(Key(x, y));
        public void AddLadder(int x, int y) { if (y >= 0) ladders.Add(Key(x, y)); }

        public int LayerIndexAt(int y)
        {
            int index = 0;
            for (int i = 0; i < config.layers.Length; i++)
                if (y >= config.layers[i].startDepth) index = i;
            return index;
        }

        /// <summary>Ore index at the cell or -1. Veins grow from rare centers into their neighbours.</summary>
        public int OreAt(int x, int y)
        {
            if (y < 1 || x < 0 || x >= Width) return -1;
            int ore = VeinCenterOre(x, y);
            if (ore >= 0) return ore;
            for (int i = 0; i < 4; i++)
            {
                int centerOre = VeinCenterOre(x + NeighbourX[i], y + NeighbourY[i]);
                if (centerOre >= 0 && Rand(x, y, 100 + i) < VeinSpread) return centerOre;
            }
            return -1;
        }

        int VeinCenterOre(int x, int y)
        {
            if (y < 1 || x < 0 || x >= Width) return -1;
            float roll = Rand(x, y, 1);
            // Rarest first: a cell in range of several ores prefers the valuable one.
            for (int i = config.ores.Length - 1; i >= 0; i--)
            {
                float chance = OreChance(config.ores[i], y);
                if (roll < chance) return i;
                roll -= chance;
            }
            return -1;
        }

        /// <summary>Ramps up to the peak depth, then fades down to 30% and stays there.</summary>
        public static float OreChance(OreDef ore, int depth)
        {
            if (depth < ore.minDepth) return 0f;
            if (depth <= ore.peakDepth)
                return ore.chance * Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(ore.minDepth, ore.peakDepth, depth));
            int range = Mathf.Max(1, ore.peakDepth - ore.minDepth);
            return ore.chance * Mathf.Lerp(1f, 0.3f, (depth - ore.peakDepth) / (float)(range * 2));
        }

        public float BlockHp(int x, int y)
        {
            int index = LayerIndexAt(y);
            var layer = config.layers[index];
            float hp = layer.hp;
            if (index == config.layers.Length - 1)
                hp *= Mathf.Pow(config.deepHpGrowth, y - layer.startDepth);
            int ore = OreAt(x, y);
            return ore >= 0 ? hp * config.ores[ore].hpMultiplier : hp;
        }

        public int Variant(int x, int y) => Hash(x, y, 7) % 2 == 0 ? 0 : 1;

        float Rand(int x, int y, int salt) => (Hash(x, y, salt) & 0xFFFFFF) / 16777216f;

        uint Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1274126177 + seed * 16777619);
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                return h ^ (h >> 16);
            }
        }

        public List<int> ExportDug() => new List<int>(dug);
        public List<int> ExportLadders() => new List<int>(ladders);

        public void Import(List<int> dugCells, List<int> ladderCells)
        {
            dug.Clear();
            ladders.Clear();
            if (dugCells != null) dug.UnionWith(dugCells);
            if (ladderCells != null) ladders.UnionWith(ladderCells);
        }
    }
}
