using System.Collections.Generic;
using UnityEngine;

namespace DeepDwarf
{
    /// <summary>Scene entry point: creates everything and connects the sim to the visuals.</summary>
    public class GameController : MonoBehaviour
    {
        public const string SaveKey = "deepdwarf.save.v1";
        const float AutosaveInterval = 15f;
        const float ChipLife = 0.45f;
        const float FullDarkDepth = 15f;
        static readonly Color SkyColor = new Color32(116, 188, 236, 255);

        class Chip
        {
            public SpriteRenderer renderer;
            public Vector2 velocity;
            public float left;
        }

        GameSim sim;
        GameUI ui;
        WorldView worldView;
        DwarfView dwarf;
        Camera mainCamera;
        AudioSource audioSource;
        SpriteRenderer crack, darkness;
        Sprite[] crackSprites;
        readonly List<Chip> chips = new List<Chip>();
        float autosave, shake;

        void Awake()
        {
            Application.targetFrameRate = 60;
            var config = GameConfig.Load();
            sim = new GameSim(config, LoadSave());

            mainCamera = Camera.main;
            mainCamera.orthographic = true;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            audioSource = mainCamera.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            var worldObject = new GameObject("World");
            worldView = worldObject.AddComponent<WorldView>();
            worldView.Init(config, sim.World);

            var dwarfObject = new GameObject("Dwarf");
            dwarf = dwarfObject.AddComponent<DwarfView>();
            dwarf.Init(sim);

            crackSprites = new[] { Art.Sprite("crack_0"), Art.Sprite("crack_1"), Art.Sprite("crack_2") };
            crack = new GameObject("Crack").AddComponent<SpriteRenderer>();
            crack.sortingOrder = 4;
            crack.enabled = false;

            darkness = new GameObject("Darkness").AddComponent<SpriteRenderer>();
            darkness.sprite = Art.Sprite("dark");
            darkness.sortingOrder = 20;

            BuildSurface(config);

            var uiObject = new GameObject("UI");
            ui = uiObject.AddComponent<GameUI>();
            ui.Init(sim, Play, ToggleMute);

            sim.BlockHit += OnBlockHit;
            sim.BlockBroken += OnBlockBroken;
            sim.OreCollected += OnOreCollected;
            sim.BagFull += () => ui.ShowMessage("РЮКЗАК ПОЛОН! ПОРА НАВЕРХ", 2f);
            sim.LayerReached += index => ui.ShowMessage("НОВЫЙ СЛОЙ: " + config.layers[index].title, 2.5f);
            sim.Surfaced += OnSurfaced;
            sim.Fainted += OnFainted;
            sim.Lifted += () => Play("lift");

            UpdateCamera(true);
            if (sim.Data.bestDepth == 0)
                ui.ShowMessage("КОПАЙ ВНИЗ, СОБИРАЙ РУДУ\nИ ВОЗВРАЩАЙСЯ ДО КОНЦА СИЛ!", 5f);
        }

        void BuildSurface(GameConfig config)
        {
            var shop = new GameObject("Shop").AddComponent<SpriteRenderer>();
            shop.sprite = Art.Sprite("shop");
            shop.transform.position = new Vector3(config.worldWidth - 2.2f, 1f);
            shop.sortingOrder = 1;

            var cloudSprite = Art.Sprite("cloud");
            foreach (var position in new[] { new Vector3(2f, 6f), new Vector3(8f, 8.5f), new Vector3(4f, 11f) })
            {
                var cloud = new GameObject("Cloud").AddComponent<SpriteRenderer>();
                cloud.sprite = cloudSprite;
                cloud.transform.position = position;
            }
        }

        void Update()
        {
            ReadInput(out int dx, out int dy);
            if (ui.ShopOpen) dx = dy = 0;
            sim.Tick(Mathf.Min(Time.deltaTime, 0.1f), dx, dy);

            autosave += Time.deltaTime;
            if (autosave >= AutosaveInterval) Save();
        }

        void LateUpdate()
        {
            UpdateCamera(false);
            UpdateDarkness();
            UpdateChips();
        }

        void ReadInput(out int dx, out int dy)
        {
            dx = dy = 0;
            if (Held(ui.Down) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) dy = 1;
            else if (Held(ui.Left) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) dx = -1;
            else if (Held(ui.Right) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) dx = 1;
            else if (Held(ui.Up) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) dy = -1;
        }

        // On-screen buttons exist only on touch devices.
        static bool Held(HoldButton button) => button != null && button.Held;

        void UpdateCamera(bool snap)
        {
            int width = sim.Config.worldWidth * Art.TilePixels;
            int scale = Mathf.Max(1, Screen.width / width);
            scale = Mathf.Max(1, Mathf.Min(scale, Screen.height / (16 * Art.TilePixels)));
            float size = Screen.height / (2f * Art.TilePixels * scale);
            mainCamera.orthographicSize = size;

            // Keep the dwarf above the middle of the free space between the HUD and the D-pad.
            float canvasHeight = ui.Root.rect.height;
            float free = canvasHeight - GameUI.HudHeight - ui.BottomHeight;
            float focus = (ui.BottomHeight + free * 0.62f) / canvasHeight;
            Vector3 feet = dwarf != null && !snap ? dwarf.CellPosition : (Vector3)DwarfView.CellToWorld(sim.X, sim.Y);
            float targetY = feet.y + (0.5f - focus) * 2f * size;

            float y = snap ? targetY : Mathf.Lerp(mainCamera.transform.position.y, targetY, 1f - Mathf.Exp(-12f * Time.deltaTime));
            shake = Mathf.Max(0f, shake - Time.deltaTime * 0.5f);
            float pixel = 1f / (Art.TilePixels * scale);
            float px = sim.Config.worldWidth / 2f + (shake > 0f ? Random.Range(-shake, shake) : 0f);
            float py = y + (shake > 0f ? Random.Range(-shake, shake) : 0f);
            mainCamera.transform.position = new Vector3(Mathf.Round(px / pixel) * pixel, Mathf.Round(py / pixel) * pixel, -10f);

            int top = Mathf.FloorToInt(-(y + size)) - 2;
            int bottom = Mathf.CeilToInt(-(y - size)) + 2;
            worldView.ShowRows(top, bottom);

            float depthShare = Mathf.Clamp01(-y / 12f);
            mainCamera.backgroundColor = Color.Lerp(SkyColor, Color.black, depthShare);
        }

        void UpdateDarkness()
        {
            Vector3 center = dwarf.CellPosition + new Vector3(0f, 0.5f, 0f);
            darkness.transform.position = center;
            darkness.transform.localScale = Vector3.one * (sim.LanternRadius / 2f);
            float depth = -center.y;
            darkness.color = new Color(1f, 1f, 1f, Mathf.Clamp01(depth / FullDarkDepth) * 0.97f);
        }

        // ---------- events ----------

        void OnBlockHit(int x, int y, float hpShare)
        {
            crack.enabled = true;
            crack.transform.position = new Vector3(x + 0.5f, -y + 0.5f);
            crack.sprite = crackSprites[Mathf.Clamp(Mathf.FloorToInt((1f - hpShare) * 3f), 0, 2)];
            SpawnChips(x, y, sim.Config.layers[sim.World.LayerIndexAt(y)].chipColor, 3);
            Play("hit");
        }

        void OnBlockBroken(int x, int y, int ore)
        {
            crack.enabled = false;
            worldView.RedrawCell(x, y);
            worldView.RedrawCell(sim.X, sim.Y);
            Color color = ore >= 0 ? sim.Config.ores[ore].color : (Color)sim.Config.layers[sim.World.LayerIndexAt(y)].chipColor;
            SpawnChips(x, y, color, 8);
            shake = 0.04f;
            Play("break");
        }

        void OnOreCollected(int ore, int amount)
        {
            var def = sim.Config.ores[ore];
            string text = "+" + amount + " " + def.title;
            ui.ShowFloat(text, dwarf.CellPosition + Vector3.up * 1.2f, def.color);
            Play("ore");
        }

        void OnSurfaced(long earned, int sold)
        {
            RedrawAroundDwarf();
            if (sold > 0) Play("coin");
            ui.OpenShop(sold > 0 ? "ПРОДАНО РУДЫ: " + sold + "  +" + Art.FormatNumber(earned) : "");
            Save();
        }

        void OnFainted(int lost)
        {
            Play("faint");
            ui.ShowMessage("ДВОРФ БЕЗ СИЛ! ЕГО ВЫТАЩИЛИ НАВЕРХ.\nПОТЕРЯНО РУДЫ: " + lost, 4f);
        }

        void RedrawAroundDwarf()
        {
            for (int dy = -1; dy <= 1; dy++)
                worldView.RedrawCell(sim.X, sim.Y + dy);
        }

        void SpawnChips(int x, int y, Color color, int count)
        {
            Vector2 center = new Vector2(x + 0.5f, -y + 0.5f);
            for (int i = 0; i < count; i++)
            {
                Chip chip = chips.Find(c => c.left <= 0f);
                if (chip == null)
                {
                    var renderer = new GameObject("Chip").AddComponent<SpriteRenderer>();
                    renderer.sprite = Art.Sprite("chip");
                    renderer.sortingOrder = 12;
                    chip = new Chip { renderer = renderer };
                    chips.Add(chip);
                }
                chip.renderer.color = color;
                chip.renderer.enabled = true;
                chip.renderer.transform.position = center + Random.insideUnitCircle * 0.35f;
                chip.velocity = Random.insideUnitCircle * 3f + Vector2.up * 2f;
                chip.left = ChipLife;
            }
        }

        void UpdateChips()
        {
            foreach (var chip in chips)
            {
                if (chip.left <= 0f) continue;
                chip.left -= Time.deltaTime;
                chip.velocity += Vector2.down * (14f * Time.deltaTime);
                chip.renderer.transform.position += (Vector3)(chip.velocity * Time.deltaTime);
                if (chip.left <= 0f) chip.renderer.enabled = false;
            }
        }

        void Play(string clip)
        {
            if (!sim.Data.muted) audioSource.PlayOneShot(Art.Clip(clip));
        }

        void ToggleMute()
        {
            sim.Data.muted = !sim.Data.muted;
            Save();
        }

        // ---------- saving ----------

        static SaveData LoadSave()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (string.IsNullOrEmpty(json)) return new SaveData();
            try
            {
                return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning("Save is damaged, starting over: " + e.Message);
                return new SaveData();
            }
        }

        void Save()
        {
            autosave = 0f;
            sim.CaptureState();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(sim.Data));
            PlayerPrefs.Save();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && sim != null) Save();
        }

        void OnApplicationQuit()
        {
            if (sim != null) Save();
        }
    }
}
