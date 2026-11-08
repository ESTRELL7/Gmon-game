using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepDwarf
{
    public enum DwarfAction { Idle, Walk, Climb, Fall, Dig }

    [Serializable]
    public class SaveData
    {
        public int seed;
        public long money;
        public int bestDepth;
        public int x;
        public int y = World.SurfaceY;
        public float energy = -1f;
        public bool muted;
        public int[] levels;
        public int[] bag;
        public List<int> dug;
        public List<int> ladders;
    }

    /// <summary>The whole game rule set, free of rendering so it can run in tests.</summary>
    public class GameSim
    {
        public event Action<int, int, float> BlockHit;      // x, y, remaining hp share
        public event Action<int, int, int> BlockBroken;     // x, y, ore index or -1
        public event Action<int, int> OreCollected;         // ore index, amount
        public event Action BagFull;
        public event Action<int> LayerReached;
        public event Action<long, int> Surfaced;            // money earned, items sold
        public event Action<int> Fainted;                   // items lost
        public event Action Lifted;
        public event Action DwarfMoved;

        public readonly GameConfig Config;
        public readonly World World;
        public readonly SaveData Data;

        public int X { get; private set; }
        public int Y { get; private set; }
        public int FromX { get; private set; }
        public int FromY { get; private set; }
        public float MoveT { get; private set; }
        public bool IsMoving { get; private set; }
        public int FacingX { get; private set; } = -1;
        public int DigDx { get; private set; }
        public int DigDy { get; private set; }
        public DwarfAction Action { get; private set; }
        public float Energy { get; private set; }
        public float SwingCooldown { get; private set; }

        public int Depth => Y < 0 ? 0 : Y + 1;
        public bool OnSurface => Y < 0;

        float moveSpeed;
        int targetX, targetY = int.MinValue;
        float targetHp;
        int deepestLayer;

        public GameSim(GameConfig config, SaveData data)
        {
            Config = config;
            Data = data;
            if (data.seed == 0) data.seed = Environment.TickCount | 1;
            Array.Resize(ref data.levels, config.upgrades.Length);
            Array.Resize(ref data.bag, config.ores.Length);
            World = new World(config, data.seed);
            World.Import(data.dug, data.ladders);
            X = data.dug == null ? config.worldWidth / 2 : Mathf.Clamp(data.x, 0, config.worldWidth - 1);
            Y = Mathf.Max(data.y, World.SurfaceY);
            FromX = X;
            FromY = Y;
            Energy = data.energy < 0f ? MaxEnergy : Mathf.Min(data.energy, MaxEnergy);
            deepestLayer = World.LayerIndexAt(Mathf.Max(0, data.bestDepth - 1));
        }

        public int Level(UpgradeType type) => Data.levels[(int)type];
        public float Power => PowerAt(Level(UpgradeType.Pick));
        public float SwingInterval => SwingAt(Level(UpgradeType.Swing));
        public int BagCapacity => BagAt(Level(UpgradeType.Bag));
        public int MaxEnergy => EnergyAt(Level(UpgradeType.Energy));
        public float LanternRadius => LanternAt(Level(UpgradeType.Lantern));
        public float LiftFactor => LiftAt(Level(UpgradeType.Lift));
        public float Luck => LuckAt(Level(UpgradeType.Luck));

        public float PowerAt(int level) => Config.powerBase * Mathf.Pow(Config.powerGrowth, level);
        public float SwingAt(int level) => Config.swingBase * Mathf.Pow(Config.swingFactor, level);
        public int BagAt(int level) => Config.bagBase + Config.bagPerLevel * level;
        public int EnergyAt(int level) => Config.energyBase + Config.energyPerLevel * level;
        public float LanternAt(int level) => Config.lanternBase + Config.lanternPerLevel * level;
        public float LiftAt(int level) => Config.liftBase * Mathf.Pow(Config.liftFactor, level);
        public float LuckAt(int level) => Config.luckPerLevel * level;

        public int BagCount
        {
            get
            {
                int total = 0;
                foreach (int count in Data.bag) total += count;
                return total;
            }
        }

        public int LiftCost => OnSurface ? 0 : Mathf.Max(1, Mathf.CeilToInt(Depth * LiftFactor));
        public bool CanLift => !OnSurface && Energy >= LiftCost;

        /// <summary>Advances the game. (dx, dy) is the held direction, y grows downwards.</summary>
        public void Tick(float dt, int dx, int dy)
        {
            SwingCooldown = Mathf.Max(0f, SwingCooldown - dt);

            if (IsMoving)
            {
                MoveT += dt * moveSpeed;
                if (MoveT >= 1f) FinishMove();
                return;
            }

            if (ShouldFall())
            {
                StartMove(X, Y + 1, Config.fallSpeed, DwarfAction.Fall);
                return;
            }

            if (dx == 0 && dy == 0)
            {
                Action = DwarfAction.Idle;
                return;
            }

            if (dx != 0) FacingX = dx;
            int nx = X + dx, ny = Y + dy;
            if (!World.IsInside(nx, ny))
            {
                Action = DwarfAction.Idle;
                return;
            }

            if (World.IsSolid(nx, ny)) Swing(nx, ny, dx, dy);
            else WalkTo(nx, ny, dy);
        }

        bool ShouldFall() => !OnSurface && !World.HasLadder(X, Y) && !World.IsSolid(X, Y + 1);

        void WalkTo(int nx, int ny, int dy)
        {
            if (dy < 0)
            {
                Energy -= 1f;
                if (CheckFaint()) return;
                World.AddLadder(X, Y);
                World.AddLadder(nx, ny);
                StartMove(nx, ny, Config.ladderSpeed, DwarfAction.Climb);
            }
            else if (dy > 0)
            {
                StartMove(nx, ny, Config.ladderSpeed, DwarfAction.Climb);
            }
            else
            {
                StartMove(nx, ny, Config.walkSpeed, DwarfAction.Walk);
            }
        }

        void Swing(int nx, int ny, int dx, int dy)
        {
            Action = DwarfAction.Dig;
            DigDx = dx;
            DigDy = dy;
            if (nx != targetX || ny != targetY)
            {
                targetX = nx;
                targetY = ny;
                targetHp = World.BlockHp(nx, ny);
            }
            if (SwingCooldown > 0f) return;

            SwingCooldown = SwingInterval;
            Energy -= 1f;
            targetHp -= Power;
            if (targetHp > 0f)
            {
                BlockHit?.Invoke(nx, ny, targetHp / World.BlockHp(nx, ny));
                CheckFaint();
                return;
            }

            int ore = World.OreAt(nx, ny);
            World.Dig(nx, ny);
            if (dy != 0)
            {
                World.AddLadder(X, Y);
                World.AddLadder(nx, ny);
            }
            targetY = int.MinValue;
            BlockBroken?.Invoke(nx, ny, ore);
            if (ore >= 0) Collect(ore);
            if (CheckFaint()) return;
            StartMove(nx, ny, dy != 0 ? Config.ladderSpeed : Config.walkSpeed, dy != 0 ? DwarfAction.Climb : DwarfAction.Walk);
        }

        void Collect(int ore)
        {
            int free = BagCapacity - BagCount;
            if (free <= 0)
            {
                BagFull?.Invoke();
                return;
            }
            int amount = Mathf.Min(free, UnityEngine.Random.value < Luck ? 2 : 1);
            Data.bag[ore] += amount;
            OreCollected?.Invoke(ore, amount);
        }

        void StartMove(int nx, int ny, float speed, DwarfAction action)
        {
            FromX = X;
            FromY = Y;
            X = nx;
            Y = ny;
            MoveT = 0f;
            moveSpeed = speed;
            IsMoving = true;
            Action = action;
        }

        void FinishMove()
        {
            bool cameFromUnderground = FromY >= 0;
            IsMoving = false;
            MoveT = 0f;
            FromX = X;
            FromY = Y;
            if (Depth > Data.bestDepth) Data.bestDepth = Depth;
            int layer = World.LayerIndexAt(Mathf.Max(0, Y));
            if (layer > deepestLayer)
            {
                deepestLayer = layer;
                LayerReached?.Invoke(layer);
            }
            DwarfMoved?.Invoke();
            if (OnSurface && cameFromUnderground) ArriveAtSurface();
        }

        bool CheckFaint()
        {
            if (Energy > 0f) return false;
            int lost = 0;
            for (int i = 0; i < Data.bag.Length; i++)
            {
                int drop = Mathf.CeilToInt(Data.bag[i] * Config.faintBagLoss);
                Data.bag[i] -= drop;
                lost += drop;
            }
            Fainted?.Invoke(lost);
            TeleportToSurface();
            return true;
        }

        public bool Lift()
        {
            if (!CanLift || IsMoving) return false;
            Energy -= LiftCost;
            Lifted?.Invoke();
            TeleportToSurface();
            return true;
        }

        void TeleportToSurface()
        {
            IsMoving = false;
            MoveT = 0f;
            Y = World.SurfaceY;
            FromX = X;
            FromY = Y;
            Action = DwarfAction.Idle;
            targetY = int.MinValue;
            DwarfMoved?.Invoke();
            ArriveAtSurface();
        }

        void ArriveAtSurface()
        {
            long earned = 0;
            int sold = 0;
            for (int i = 0; i < Data.bag.Length; i++)
            {
                earned += Data.bag[i] * Config.ores[i].value;
                sold += Data.bag[i];
                Data.bag[i] = 0;
            }
            Data.money += earned;
            Energy = MaxEnergy;
            Surfaced?.Invoke(earned, sold);
        }

        public long UpgradeCost(int index)
        {
            var def = Config.upgrades[index];
            return (long)Math.Round(def.baseCost * Math.Pow(def.costGrowth, Data.levels[index]));
        }

        public bool IsMaxed(int index) => Data.levels[index] >= Config.upgrades[index].maxLevel;
        public bool CanBuy(int index) => !IsMaxed(index) && Data.money >= UpgradeCost(index);

        public bool Buy(int index)
        {
            if (!CanBuy(index)) return false;
            Data.money -= UpgradeCost(index);
            Data.levels[index]++;
            if (Config.upgrades[index].type == UpgradeType.Energy && OnSurface) Energy = MaxEnergy;
            return true;
        }

        /// <summary>Copies the live state into Data right before saving.</summary>
        public void CaptureState()
        {
            Data.x = X;
            Data.y = Y;
            Data.energy = Energy;
            Data.dug = World.ExportDug();
            Data.ladders = World.ExportLadders();
        }
    }
}
