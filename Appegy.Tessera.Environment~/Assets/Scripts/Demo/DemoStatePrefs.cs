using UnityEngine;

namespace Appegy.Tessera.Demo
{
    /// <summary>
    ///     Persists the full playground state in PlayerPrefs so a reload restores the exact setup:
    ///     the selected grid type, every grid's parameter values, and the hover-highlight toggle.
    ///     Theme and text size are persisted separately by PlaygroundUI.
    /// </summary>
    public static class DemoStatePrefs
    {
        private const string GridKey = "tessera_grid";
        private const string HighlightKey = "tessera_highlight";

        public static int LoadGridIndex(int count)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(GridKey, 0), 0, Mathf.Max(0, count - 1));
        }

        public static void SaveGridIndex(int index)
        {
            PlayerPrefs.SetInt(GridKey, index);
            PlayerPrefs.Save();
        }

        public static bool LoadHighlight(bool fallback)
        {
            return PlayerPrefs.GetInt(HighlightKey, fallback ? 1 : 0) == 1;
        }

        public static void SaveHighlight(bool value)
        {
            PlayerPrefs.SetInt(HighlightKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void LoadParams(GridDemo demo)
        {
            foreach (var parameter in demo.Parameters)
            {
                var key = Key(demo, parameter);
                if (!PlayerPrefs.HasKey(key)) continue;
                switch (parameter)
                {
                    case FloatParameter f: f.Value = PlayerPrefs.GetFloat(key, f.Value); break;
                    case IntParameter i: i.Value = PlayerPrefs.GetInt(key, i.Value); break;
                    case SeedParameter s: s.Value = PlayerPrefs.GetInt(key, s.Value); break;
                    case BoolParameter b: b.Value = PlayerPrefs.GetInt(key, b.Value ? 1 : 0) == 1; break;
                    case ChoiceParameter c: c.SelectedIndex = PlayerPrefs.GetInt(key, c.SelectedIndex); break;
                }
            }
        }

        public static void SaveParams(GridDemo demo)
        {
            foreach (var parameter in demo.Parameters)
            {
                var key = Key(demo, parameter);
                switch (parameter)
                {
                    case FloatParameter f: PlayerPrefs.SetFloat(key, f.Value); break;
                    case IntParameter i: PlayerPrefs.SetInt(key, i.Value); break;
                    case SeedParameter s: PlayerPrefs.SetInt(key, s.Value); break;
                    case BoolParameter b: PlayerPrefs.SetInt(key, b.Value ? 1 : 0); break;
                    case ChoiceParameter c: PlayerPrefs.SetInt(key, c.SelectedIndex); break;
                }
            }
            PlayerPrefs.Save();
        }

        private static string Key(GridDemo demo, DemoParameter parameter)
        {
            return "t." + demo.DisplayName + "." + parameter.Id;
        }
    }
}