using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace DeepDwarf.Tests
{
    public class GameSimTests
    {
        const float Dt = 1f / 30f;

        static GameSim NewSim(int seed = 12345) =>
            new GameSim(GameConfig.CreateDefault(), new SaveData { seed = seed });

        static void Run(GameSim sim, float seconds, int dx, int dy)
        {
            for (float t = 0f; t < seconds; t += Dt) sim.Tick(Dt, dx, dy);
        }

        static void RunUntil(GameSim sim, int dx, int dy, System.Func<bool> done, float limit = 30f)
        {
            for (float t = 0f; t < limit && !done(); t += Dt) sim.Tick(Dt, dx, dy);
            Assert.IsTrue(done(), "Condition not reached in " + limit + " s");
        }

        [Test]
        public void WorldIsDeterministicForASeed()
        {
            var config = GameConfig.CreateDefault();
            var a = new World(config, 42);
            var b = new World(config, 42);
            var c = new World(config, 43);
            int same = 0, total = 0;
            for (int y = 0; y < 300; y++)
            {
                for (int x = 0; x < config.worldWidth; x++)
                {
                    Assert.AreEqual(a.OreAt(x, y), b.OreAt(x, y));
                    if (a.OreAt(x, y) == c.OreAt(x, y)) same++;
                    total++;
                }
            }
            Assert.Less(same, total, "Different seeds must give different worlds");
        }

        [Test]
        public void DeeperRowsHoldMoreValuableOre()
        {
            var config = GameConfig.CreateDefault();
            var world = new World(config, 7);
            Assert.Greater(AverageOreValue(world, config, 100, 160), AverageOreValue(world, config, 1, 30) * 5f);
            Assert.Greater(AverageOreValue(world, config, 300, 360), AverageOreValue(world, config, 100, 160) * 5f);
        }

        static float AverageOreValue(World world, GameConfig config, int from, int to)
        {
            long sum = 0;
            int count = 0;
            for (int y = from; y < to; y++)
            {
                for (int x = 0; x < config.worldWidth; x++)
                {
                    int ore = world.OreAt(x, y);
                    if (ore < 0) continue;
                    sum += config.ores[ore].value;
                    count++;
                }
            }
            Assert.Greater(count, 0, $"No ore between {from} and {to}");
            return sum / (float)count;
        }

        [Test]
        public void OreDensityStaysReasonable()
        {
            var config = GameConfig.CreateDefault();
            var world = new World(config, 99);
            int ore = 0, cells = 0;
            for (int y = 1; y < 500; y++)
            {
                for (int x = 0; x < config.worldWidth; x++)
                {
                    if (world.OreAt(x, y) >= 0) ore++;
                    cells++;
                }
            }
            float share = ore / (float)cells;
            Assert.That(share, Is.InRange(0.08f, 0.5f), "Ore share " + share);
        }

        [Test]
        public void DiggingDownDescendsAndLeavesALadder()
        {
            var sim = NewSim();
            int x = sim.X;
            Assert.IsTrue(sim.OnSurface);
            Run(sim, 3f, 0, 1);
            Assert.Greater(sim.Depth, 0);
            Assert.AreEqual(x, sim.X);
            Assert.Less(sim.Energy, sim.MaxEnergy);
            Assert.IsTrue(sim.World.HasLadder(x, 0));
        }

        [Test]
        public void DwarfCannotLeaveTheWorld()
        {
            var sim = NewSim();
            Run(sim, 5f, -1, 0);
            Assert.AreEqual(0, sim.X);
            Run(sim, 5f, 1, 0);
            Assert.AreEqual(sim.Config.worldWidth - 1, sim.X);
            Run(sim, 1f, 0, -1);
            Assert.AreEqual(World.SurfaceY, sim.Y);
        }

        [Test]
        public void DwarfFallsIntoAHoleAfterDiggingSideways()
        {
            var sim = NewSim();
            Run(sim, 4f, 0, 1);
            Assert.Greater(sim.Y, 1);
            int shaftX = sim.X, floorY = sim.Y;
            RunUntil(sim, 1, 0, () => sim.X == shaftX + 1 && !sim.IsMoving);
            Run(sim, 0.5f, 0, 0);
            Assert.AreEqual(floorY, sim.Y);
            // Walking back onto the ladder must not drop the dwarf any further.
            RunUntil(sim, -1, 0, () => sim.X == shaftX && !sim.IsMoving);
            Run(sim, 0.5f, 0, 0);
            Assert.AreEqual(floorY, sim.Y);
        }

        [Test]
        public void ClimbingBackSellsTheBagAndRefillsEnergy()
        {
            var sim = NewSim();
            Run(sim, 4f, 0, 1);
            int depth = sim.Depth;
            Assert.Greater(depth, 0);
            sim.Data.bag[0] = 3;
            long surfaced = -1;
            sim.Surfaced += (earned, sold) => surfaced = earned;
            Run(sim, depth * 0.3f, 0, -1);
            Assert.IsTrue(sim.OnSurface);
            Assert.AreEqual(3 * sim.Config.ores[0].value, surfaced);
            Assert.AreEqual(3 * sim.Config.ores[0].value, sim.Data.money);
            Assert.AreEqual(0, sim.BagCount);
            Assert.AreEqual(sim.MaxEnergy, sim.Energy);
        }

        [Test]
        public void WalkingOnTheSurfaceDoesNotSellAgain()
        {
            var sim = NewSim();
            int surfaced = 0;
            sim.Surfaced += (earned, sold) => surfaced++;
            Run(sim, 2f, 1, 0);
            Assert.AreEqual(0, surfaced);
        }

        [Test]
        public void LiftCostsEnergyAndNeedsEnough()
        {
            var sim = NewSim();
            Run(sim, 8f, 0, 1);
            int cost = sim.LiftCost;
            Assert.Greater(cost, 0);
            Assert.IsTrue(sim.CanLift);
            sim.Data.bag[0] = 1;
            Assert.IsTrue(sim.Lift());
            Assert.IsTrue(sim.OnSurface);
            Assert.AreEqual(sim.Config.ores[0].value, sim.Data.money);
            Assert.IsFalse(sim.Lift(), "No lift from the surface");
        }

        [Test]
        public void RunningOutOfEnergyLosesHalfTheBag()
        {
            var config = GameConfig.CreateDefault();
            foreach (var ore in config.ores) ore.chance = 0f;
            var sim = new GameSim(config, new SaveData { seed = 5 });
            sim.Data.bag[0] = 4;
            sim.Data.bag[1] = 1;
            int lost = -1;
            sim.Fainted += amount => { if (lost < 0) lost = amount; };
            Run(sim, 120f, 0, 1);
            Assert.AreEqual(3, lost, "Half of 4 plus the rounded-up half of 1");
            Assert.IsTrue(sim.OnSurface || sim.Energy > 0f);
        }

        [Test]
        public void BagNeverOverflows()
        {
            var sim = NewSim();
            sim.Data.levels[(int)UpgradeType.Energy] = 20;
            int oreX = -1, oreY = -1;
            for (int y = 1; y < 20 && oreX < 0; y++)
                for (int x = 0; x < sim.Config.worldWidth && oreX < 0; x++)
                    if (sim.World.OreAt(x, y) >= 0) { oreX = x; oreY = y; }
            Assert.GreaterOrEqual(oreX, 0, "No ore in the first 20 rows");

            if (oreX != sim.X) RunUntil(sim, oreX > sim.X ? 1 : -1, 0, () => sim.X == oreX && !sim.IsMoving);
            sim.Data.bag[0] = sim.BagCapacity;
            bool full = false;
            sim.BagFull += () => full = true;
            RunUntil(sim, 0, 1, () => !sim.World.IsSolid(oreX, oreY), 120f);
            Assert.IsTrue(full, "Breaking ore with a full bag must report it");
            Assert.AreEqual(sim.BagCapacity, sim.BagCount);
        }

        [Test]
        public void UpgradesCostMoneyAndImproveStats()
        {
            var sim = NewSim();
            int pick = (int)UpgradeType.Pick;
            float power = sim.Power;
            long cost = sim.UpgradeCost(pick);
            Assert.IsFalse(sim.Buy(pick), "No money yet");
            sim.Data.money = cost + 1;
            Assert.IsTrue(sim.Buy(pick));
            Assert.AreEqual(1, sim.Data.money);
            Assert.Greater(sim.Power, power);
            Assert.Greater(sim.UpgradeCost(pick), cost);
        }

        [Test]
        public void SaveRoundTripRestoresTheWorld()
        {
            var sim = NewSim();
            Run(sim, 4f, 0, 1);
            sim.CaptureState();
            string json = JsonUtility.ToJson(sim.Data);
            var restored = new GameSim(sim.Config, JsonUtility.FromJson<SaveData>(json));
            Assert.AreEqual(sim.X, restored.X);
            Assert.AreEqual(sim.Y, restored.Y);
            Assert.AreEqual(sim.Energy, restored.Energy);
            Assert.IsFalse(restored.World.IsSolid(sim.X, 0));
            Assert.IsTrue(restored.World.HasLadder(sim.X, 0));
        }

        /// <summary>A simple bot plays many dives: the game must keep progressing, and the log shows the pacing.</summary>
        [Test]
        public void BotProgressesThroughTheLayers()
        {
            Random.InitState(1);
            var sim = NewSim(2024);
            var log = new StringBuilder("dive | best depth | money | levels | seconds played\n");
            float played = 0f;
            int column = sim.X;

            for (int dive = 1; dive <= 150; dive++)
            {
                BuyEverythingAffordable(sim);
                column = (column + 3) % sim.Config.worldWidth;
                while (sim.X != column) { sim.Tick(Dt, column > sim.X ? 1 : -1, 0); played += Dt; }

                for (int guard = 0; guard < 200000; guard++)
                {
                    sim.Tick(Dt, 0, 1);
                    played += Dt;
                    bool bagFull = sim.BagCount >= sim.BagCapacity;
                    bool tired = sim.Energy <= sim.LiftCost + 3;
                    if (!sim.IsMoving && !sim.OnSurface && (bagFull || tired))
                    {
                        if (!sim.Lift()) Run(sim, 60f, 0, 1);
                        break;
                    }
                }
                if (dive % 15 == 0 || dive == 1)
                    log.AppendLine($"{dive} | {sim.Data.bestDepth} | {sim.Data.money} | {string.Join(",", sim.Data.levels)} | {played:0}");
            }
            Debug.Log(log.ToString());
            Assert.Greater(sim.Data.bestDepth, 60, log.ToString());
        }

        static void BuyEverythingAffordable(GameSim sim)
        {
            bool bought = true;
            while (bought)
            {
                bought = false;
                int best = -1;
                for (int i = 0; i < sim.Config.upgrades.Length; i++)
                {
                    if (sim.CanBuy(i) && (best < 0 || sim.UpgradeCost(i) < sim.UpgradeCost(best))) best = i;
                }
                if (best >= 0) bought = sim.Buy(best);
            }
        }
    }
}
