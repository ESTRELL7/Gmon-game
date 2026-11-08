using UnityEngine;

namespace DeepDwarf
{
    /// <summary>Animates the dwarf sprite and the pick from the sim state.</summary>
    public class DwarfView : MonoBehaviour
    {
        const float StrikeTime = 0.1f;

        // Hand position inside the sprite, in world units from the feet (sprite faces left).
        static readonly Vector2 Hand = new Vector2(0.2f, 0.42f);
        static readonly Vector2 GripToPickCenter = new Vector2(0f, 0.25f);

        SpriteRenderer body, pick;
        Sprite idle, breath, walk1, walk2;
        GameSim sim;
        float clock, strikeLeft;

        public Vector3 CellPosition { get; private set; }

        public void Init(GameSim sim)
        {
            this.sim = sim;
            idle = Art.Sprite("dwarf_idle");
            breath = Art.Sprite("dwarf_breath");
            walk1 = Art.Sprite("dwarf_walk1");
            walk2 = Art.Sprite("dwarf_walk2");

            body = gameObject.AddComponent<SpriteRenderer>();
            body.sortingOrder = 10;

            var pickObject = new GameObject("Pick");
            pickObject.transform.SetParent(transform, false);
            pick = pickObject.AddComponent<SpriteRenderer>();
            pick.sprite = Art.Sprite("pick");

            sim.BlockHit += (x, y, left) => strikeLeft = StrikeTime;
            sim.BlockBroken += (x, y, ore) => strikeLeft = StrikeTime;
        }

        void Update()
        {
            clock += Time.deltaTime;
            strikeLeft -= Time.deltaTime;

            Vector2 from = CellToWorld(sim.FromX, sim.FromY);
            Vector2 to = CellToWorld(sim.X, sim.Y);
            Vector3 position = Vector2.Lerp(from, to, sim.MoveT);
            CellPosition = position;
            transform.position = position;

            bool walking = sim.Action == DwarfAction.Walk || sim.Action == DwarfAction.Climb;
            if (walking) body.sprite = Mathf.FloorToInt(clock * 8f) % 2 == 0 ? walk1 : walk2;
            else if (sim.Action == DwarfAction.Fall) body.sprite = idle;
            else body.sprite = Mathf.FloorToInt(clock * 1.5f) % 2 == 0 ? idle : breath;

            // The sprite is drawn facing left.
            bool flip = sim.FacingX > 0;
            body.flipX = flip;
            UpdatePick(flip);
        }

        void UpdatePick(bool flip)
        {
            float side = flip ? -1f : 1f;
            Vector2 hand = new Vector2(Hand.x * side, Hand.y);
            Vector2 offset = hand + GripToPickCenter;
            float angle = 0f;
            bool inFront = false;

            if (sim.Action == DwarfAction.Dig && strikeLeft > 0f)
            {
                inFront = true;
                if (sim.DigDy > 0)
                {
                    offset = new Vector2(0.28f * side, 0.12f);
                    angle = 180f;
                }
                else if (sim.DigDy < 0)
                {
                    offset = new Vector2(0.28f * side, 1.05f);
                }
                else
                {
                    offset = new Vector2(sim.DigDx * 0.5f, 0.42f);
                    angle = sim.DigDx > 0 ? -90f : 90f;
                }
            }
            else if (sim.Action == DwarfAction.Dig)
            {
                // Wind-up: lift the pick over the head.
                offset = hand + GripToPickCenter + new Vector2(0f, 0.25f);
            }

            pick.flipX = flip;
            pick.sortingOrder = inFront ? 11 : 9;
            pick.transform.localPosition = offset;
            pick.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>World position of the dwarf's feet standing in the cell.</summary>
        public static Vector2 CellToWorld(int x, int y) => new Vector2(x + 0.5f, -y);
    }
}
