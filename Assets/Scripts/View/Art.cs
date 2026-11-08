using System.Collections.Generic;
using UnityEngine;

namespace DeepDwarf
{
    /// <summary>Loads and caches sprites and sounds from Resources.</summary>
    public static class Art
    {
        public const int TilePixels = 24;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public static Sprite Sprite(string name)
        {
            if (!sprites.TryGetValue(name, out var sprite))
            {
                sprite = Resources.Load<Sprite>("Art/" + name);
                if (sprite == null) Debug.LogError("Missing sprite: " + name);
                sprites[name] = sprite;
            }
            return sprite;
        }

        public static AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null) Debug.LogError("Missing sound: " + name);
                clips[name] = clip;
            }
            return clip;
        }

        public static string FormatNumber(long value)
        {
            if (value < 10000) return value.ToString();
            if (value < 1000000) return (value / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "К";
            if (value < 1000000000) return (value / 1000000f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "М";
            return (value / 1000000000f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "Б";
        }
    }
}
