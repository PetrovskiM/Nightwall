using System.Collections.Generic;
using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Day-phase IMGUI overlay that tells the player what enemy types will arrive in the next wave.
    /// Visible only during <see cref="GameState.Day"/>; hidden during Night / Dawn / GameOver.
    ///
    /// Reads the upcoming <see cref="WaveDefinition"/> from <see cref="WaveSpawner"/> without
    /// touching gameplay state (one-directional flow: state → UI). Uses IMGUI so it needs no
    /// canvas or prefab wiring — prototype only; to be replaced by proper mobile UI later.
    /// </summary>
    [RequireComponent(typeof(WaveSpawner))]
    public class WavePreviewHud : MonoBehaviour
    {
        [SerializeField] int fontSize = 18;
        [SerializeField] Vector2 margin = new Vector2(24f, 24f);

        WaveSpawner _spawner;
        GUIStyle _headerStyle;
        GUIStyle _lineStyle;

        // Cached to avoid per-frame allocation.
        readonly Dictionary<string, int> _archetypeCounts = new();

        void Awake() => _spawner = GetComponent<WaveSpawner>();

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Day) return;

            int nextWave = gm.Day;
            WaveDefinition? defNullable = _spawner != null ? _spawner.PeekWaveDefinition(nextWave) : null;

            EnsureStyles();

            Rect safe = Screen.safeArea;
            float x = safe.x + safe.width - margin.x;
            float y = (Screen.height - safe.yMax) + margin.y;
            float lineH = fontSize + 6f;

            // Heading
            string heading = $"NIGHT {nextWave} — INCOMING";
            float headW = _headerStyle.CalcSize(new GUIContent(heading)).x;
            GUI.Label(new Rect(x - headW, y, headW + 4f, lineH + 4f), heading, _headerStyle);
            y += lineH + 6f;

            if (defNullable.HasValue)
            {
                WaveDefinition def = defNullable.Value;
                BuildArchetypeSummary(def);

                foreach (var kvp in _archetypeCounts)
                {
                    string line = $"• {kvp.Key} ×{kvp.Value}";
                    float w = _lineStyle.CalcSize(new GUIContent(line)).x;
                    GUI.Label(new Rect(x - w, y, w + 4f, lineH), line, _lineStyle);
                    y += lineH;
                }

                // Show difficulty modifiers when non-identity.
                if (!def.difficultyModifier.IsIdentity)
                {
                    string modLine = BuildModifierLine(def.difficultyModifier);
                    float w = _lineStyle.CalcSize(new GUIContent(modLine)).x;
                    y += 4f;
                    GUI.Label(new Rect(x - w, y, w + 4f, lineH), modLine, _lineStyle);
                }

                // Announce group delays when they're significant.
                if (def.groupDelay > 1f)
                {
                    string delayLine = $"⏸ groups separated by {def.groupDelay:0}s";
                    float w = _lineStyle.CalcSize(new GUIContent(delayLine)).x;
                    y += lineH;
                    GUI.Label(new Rect(x - w, y, w + 4f, lineH), delayLine, _lineStyle);
                }
            }
            else
            {
                // Procedural night — show rough budget.
                string line = _spawner != null
                    ? $"• Mixed horde (procedural)"
                    : "• Unknown";
                float w = _lineStyle.CalcSize(new GUIContent(line)).x;
                GUI.Label(new Rect(x - w, y, w + 4f, lineH), line, _lineStyle);
            }
        }

        void BuildArchetypeSummary(WaveDefinition def)
        {
            _archetypeCounts.Clear();
            if (def.groups == null) return;
            foreach (SpawnGroup g in def.groups)
            {
                if (g.count <= 0) continue;
                string name = g.enemyDefinition != null ? g.enemyDefinition.DisplayName : "Basic";
                if (!_archetypeCounts.TryGetValue(name, out int current))
                    current = 0;
                _archetypeCounts[name] = current + g.count;
            }
        }

        static string BuildModifierLine(DifficultyModifier mod)
        {
            var parts = new System.Text.StringBuilder("⚠");
            float h = DifficultyModifier.Effective(mod.healthMultiplier);
            float s = DifficultyModifier.Effective(mod.speedMultiplier);
            float d = DifficultyModifier.Effective(mod.damageMultiplier);
            if (h != 1f) parts.Append($" HP×{h:0.#}");
            if (s != 1f) parts.Append($" SPD×{s:0.#}");
            if (d != 1f) parts.Append($" DMG×{d:0.#}");
            return parts.ToString();
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
            };
            _headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = fontSize - 2,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperRight,
            };
            _lineStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }
    }
}
