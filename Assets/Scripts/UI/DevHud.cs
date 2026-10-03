using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Minimal on-screen development overlay: current phase, time left in the phase, and the
    /// day/wave number. Reads <see cref="GameManager"/> state only — it never mutates gameplay
    /// (one-directional flow). Intentionally IMGUI so it needs no canvas/prefab wiring; this is a
    /// prototype debug readout, to be replaced by real mobile UI later.
    /// </summary>
    public class DevHud : MonoBehaviour
    {
        [SerializeField] int fontSize = 22;
        [SerializeField] Vector2 margin = new Vector2(24f, 24f);

        GUIStyle _style;

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold };
            _style.normal.textColor = ColorFor(gm.State);

            int seconds = Mathf.CeilToInt(gm.PhaseTimeRemaining);
            string line = gm.State switch
            {
                GameState.Day      => $"DAY {gm.Day}   BUILD   {seconds}s left",
                GameState.Night    => $"NIGHT {gm.Day}   HORDE   {seconds}s",
                GameState.Dawn     => $"DAWN   wave {gm.Day} survived",
                GameState.GameOver => "GAME OVER",
                _                  => gm.State.ToString(),
            };

            // Anchor inside the device safe area (notch / rounded corners). Screen.safeArea uses a
            // bottom-left origin; GUI uses top-left, so flip the top inset.
            Rect safe = Screen.safeArea;
            float x = safe.x + margin.x;
            float y = (Screen.height - safe.yMax) + margin.y;
            GUI.Label(new Rect(x, y, Screen.width, fontSize + 12f), line, _style);
        }

        static Color ColorFor(GameState state) => state switch
        {
            GameState.Day      => new Color(1f, 0.95f, 0.75f),
            GameState.Night    => new Color(0.70f, 0.80f, 1f),
            GameState.Dawn     => new Color(1f, 0.80f, 0.50f),
            GameState.GameOver => new Color(1f, 0.40f, 0.40f),
            _                  => Color.white,
        };
    }
}
