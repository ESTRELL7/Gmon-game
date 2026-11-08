using UnityEngine;
using UnityEngine.Tilemaps;

namespace DeepDwarf
{
    /// <summary>Draws the rows of the endless world that are currently near the camera.</summary>
    public class WorldView : MonoBehaviour
    {
        const int SideColumns = 14;
        static readonly Color CaveTint = new Color(0.32f, 0.3f, 0.34f);
        static readonly Color WallTint = new Color(0.6f, 0.58f, 0.62f);

        World world;
        GameConfig config;
        Tilemap background, ground, ore, ladder;
        Tile[][] groundTiles;
        Tile[] caveTiles, wallTiles, oreTiles;
        Tile grassTile, ladderTile;
        int firstRow, lastRow = -1;
        bool hasRows;

        public void Init(GameConfig config, World world)
        {
            this.config = config;
            this.world = world;
            gameObject.AddComponent<Grid>();
            background = CreateMap("Background", 0);
            ground = CreateMap("Ground", 1);
            ore = CreateMap("Ore", 2);
            ladder = CreateMap("Ladders", 3);

            int layers = config.layers.Length;
            groundTiles = new Tile[layers][];
            caveTiles = new Tile[layers];
            wallTiles = new Tile[layers];
            for (int i = 0; i < layers; i++)
            {
                string id = config.layers[i].id;
                groundTiles[i] = new[] { MakeTile("tile_" + id + "_0", Color.white), MakeTile("tile_" + id + "_1", Color.white) };
                caveTiles[i] = MakeTile("tile_" + id + "_0", CaveTint);
                wallTiles[i] = MakeTile("tile_" + id + "_1", WallTint);
            }
            oreTiles = new Tile[config.ores.Length];
            for (int i = 0; i < oreTiles.Length; i++)
                oreTiles[i] = MakeTile("ore_" + config.ores[i].id, Color.white);
            grassTile = MakeTile("tile_grass", Color.white);
            ladderTile = MakeTile("ladder", Color.white);
        }

        Tilemap CreateMap(string mapName, int order)
        {
            var go = new GameObject(mapName);
            go.transform.SetParent(transform, false);
            var map = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>().sortingOrder = order;
            return map;
        }

        static Tile MakeTile(string spriteName, Color color)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Art.Sprite(spriteName);
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            return tile;
        }

        /// <summary>Keeps rows [first, last] (world y, downwards) drawn and drops everything else.</summary>
        public void ShowRows(int first, int last)
        {
            first = Mathf.Max(first, 0);
            if (hasRows && first == firstRow && last == lastRow) return;
            if (hasRows)
            {
                for (int y = firstRow; y <= lastRow; y++)
                    if (y < first || y > last) ClearRow(y);
            }
            for (int y = first; y <= last; y++)
                if (!hasRows || y < firstRow || y > lastRow) DrawRow(y);
            firstRow = first;
            lastRow = last;
            hasRows = true;
        }

        public void RedrawCell(int x, int y)
        {
            if (y < 0) return;
            DrawCell(x, y);
        }

        void DrawRow(int y)
        {
            for (int x = -SideColumns; x < config.worldWidth + SideColumns; x++)
                DrawCell(x, y);
        }

        void ClearRow(int y)
        {
            for (int x = -SideColumns; x < config.worldWidth + SideColumns; x++)
            {
                var cell = new Vector3Int(x, -y, 0);
                background.SetTile(cell, null);
                ground.SetTile(cell, null);
                ore.SetTile(cell, null);
                ladder.SetTile(cell, null);
            }
        }

        void DrawCell(int x, int y)
        {
            var cell = new Vector3Int(x, -y, 0);
            int layer = world.LayerIndexAt(y);
            bool grass = y == 0;
            if (x < 0 || x >= config.worldWidth)
            {
                ground.SetTile(cell, grass ? grassTile : wallTiles[layer]);
                return;
            }
            if (world.IsSolid(x, y))
            {
                background.SetTile(cell, null);
                ground.SetTile(cell, grass ? grassTile : groundTiles[layer][world.Variant(x, y)]);
                int oreIndex = world.OreAt(x, y);
                ore.SetTile(cell, oreIndex >= 0 ? oreTiles[oreIndex] : null);
                ladder.SetTile(cell, null);
            }
            else
            {
                background.SetTile(cell, caveTiles[layer]);
                ground.SetTile(cell, null);
                ore.SetTile(cell, null);
                ladder.SetTile(cell, world.HasLadder(x, y) ? ladderTile : null);
            }
        }
    }
}
