using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// The main gameplay HUD. Presents the player's run state for the current phase and exposes the
    /// two phase-level actions — start the night early during the day, and pause during the night.
    /// It is a presentation/input surface: it reads <see cref="GameManager"/>, <see cref="MaterialBank"/>,
    /// <see cref="WaveSpawner"/> and the HQ <see cref="Health"/>, and drives gameplay only through
    /// their public entry points; it never mutates their state directly (one-directional flow).
    ///
    /// Structure selection lives in the sibling <see cref="BuildBar"/>; this HUD owns the surrounding
    /// chrome so the two don't overlap (build strip bottom-left, day actions bottom-right for
    /// one-handed reach). IMGUI for the prototype so it needs no canvas/prefab wiring, with chunky
    /// touch targets laid out inside the device safe area and placeholder text icons. To be replaced
    /// by real mobile UI later.
    /// </summary>
    public class GameplayHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] BuildingPlacer placer;
        [SerializeField] WaveSpawner waveSpawner;
        [SerializeField] Health hqHealth;

        [Header("Layout")]
        [SerializeField] Vector2 margin = new Vector2(28f, 28f);
        [SerializeField] float buttonHeight = 64f;

        // Distinct owner keys so each interactive region blocks world taps independently.
        readonly object _buildKey = new object();
        readonly object _startNightKey = new object();
        readonly object _pauseKey = new object();
        readonly object _pausePanelKey = new object();

        bool _paused;

        GUIStyle _title;   // large phase heading
        GUIStyle _info;    // status readouts
        GUIStyle _button;  // chunky touch buttons

        static readonly Color PanelBg   = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color DayTint    = new Color(1f, 0.95f, 0.75f);
        static readonly Color NightTint  = new Color(0.70f, 0.80f, 1f);
        static readonly Color BarBack    = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        static readonly Color BarFill    = new Color(0.85f, 0.35f, 0.35f, 0.95f);
        static readonly Color ActionTint = new Color(0.45f, 0.9f, 0.55f);

        void OnDisable()
        {
            // Never leave the game frozen or world taps blocked if the HUD is torn down.
            if (_paused) { Time.timeScale = 1f; _paused = false; }
            ClearBlockers();
        }

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            EnsureStyles();
            ClearBlockers();   // re-registered below only for the regions actually shown this phase

            switch (gm.State)
            {
                case GameState.Day:   DrawDay(gm);   break;
                case GameState.Night: DrawNight(gm); break;
                case GameState.Dawn:  DrawBanner($"DAWN — WAVE {gm.Day} SURVIVED", DayTint); break;
                case GameState.GameOver: DrawBanner("GAME OVER", new Color(1f, 0.4f, 0.4f)); break;
            }

            if (_paused) DrawPauseOverlay();
        }

        // ── Day ───────────────────────────────────────────────────────────────

        void DrawDay(GameManager gm)
        {
            Edges(out float left, out float right, out float top, out float bottom);
            int seconds = Mathf.CeilToInt(gm.PhaseTimeRemaining);

            // Top-left: phase + countdown.
            StatusPanel(left, top, DayTint, $"DAY {gm.Day}", $"{seconds}s to nightfall");

            // Top-right: materials.
            int mats = MaterialBank.Instance != null ? MaterialBank.Instance.Balance : 0;
            Chip(right, top, DayTint, $"[MAT] {mats}");

            // Bottom-right action cluster: BUILD toggle + START NIGHT.
            float gap = 12f, startW = 230f, buildW = 170f, by = bottom - buttonHeight;

            var startRect = new Rect(right - startW, by, startW, buttonHeight);
            if (Button(startRect, _startNightKey, "START NIGHT  >>", ActionTint, true))
                gm.StartNightEarly();

            if (placer != null)
            {
                var buildRect = new Rect(startRect.x - gap - buildW, by, buildW, buttonHeight);
                bool active = placer.IsActive;
                if (Button(buildRect, _buildKey, active ? "EXIT BUILD" : "BUILD", default, active))
                    placer.Toggle();
            }
        }

        // ── Night ─────────────────────────────────────────────────────────────

        void DrawNight(GameManager gm)
        {
            Edges(out float left, out float right, out float top, out float bottom);
            int seconds = Mathf.CeilToInt(gm.PhaseTimeRemaining);
            int alive = waveSpawner != null ? waveSpawner.AliveCount : 0;

            // Top-left: wave, countdown, enemies remaining.
            StatusPanel(left, top, NightTint,
                $"NIGHT {gm.Day}", $"{seconds}s", $"[FOE] {alive} remaining");

            // Pause / settings button, top-right corner (hidden while the pause overlay is up).
            var pauseRect = new Rect(right - buttonHeight, top, buttonHeight, buttonHeight);
            if (!_paused && Button(pauseRect, _pauseKey, "||", default, false))
                SetPaused(true);

            // Base health bar, left of the pause button.
            if (hqHealth != null)
            {
                float barW = 260f, gap = 12f;
                var barRect = new Rect(pauseRect.x - gap - barW, top, barW, buttonHeight);
                float pct = Mathf.Clamp01(hqHealth.Normalized);
                Fill(barRect, BarBack);
                Fill(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), BarFill);
                var prev = _info.alignment;
                _info.alignment = TextAnchor.MiddleCenter;
                GUI.Label(barRect, $"[BASE] {Mathf.RoundToInt(pct * 100f)}%", _info);
                _info.alignment = prev;
            }
        }

        // ── Pause overlay ───────────────────────────────────────────────────────

        void DrawPauseOverlay()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));

            var titleRect = new Rect(0f, Screen.height * 0.5f - 90f, Screen.width, 60f);
            var prev = _title.alignment;
            _title.alignment = TextAnchor.MiddleCenter;
            GUI.Label(titleRect, "PAUSED", _title);
            _title.alignment = prev;

            float w = 260f;
            var resumeRect = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f, w, buttonHeight);
            // The dim panel swallows every world tap while paused; the Resume button sits on top.
            if (InputService.Instance != null)
                InputService.Instance.SetUiBlockerGui(_pausePanelKey, new Rect(0f, 0f, Screen.width, Screen.height));
            if (Button(resumeRect, _pauseKey, "RESUME", ActionTint, true))
                SetPaused(false);
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        // ── Drawing helpers ─────────────────────────────────────────────────────

        /// <summary>A labelled corner chip at the top-right (right edge is the anchor).</summary>
        void Chip(float right, float top, Color tint, string text)
        {
            var size = _info.CalcSize(new GUIContent(text));
            float w = size.x + 32f, h = 44f;
            var r = new Rect(right - w, top, w, h);
            Fill(r, PanelBg);
            _info.normal.textColor = tint;
            var prevA = _info.alignment;
            _info.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, text, _info);
            _info.alignment = prevA;
        }

        /// <summary>A top-left stacked status panel: a tinted heading plus detail lines.</summary>
        void StatusPanel(float left, float top, Color tint, string heading, params string[] lines)
        {
            float w = 300f;
            float h = 52f + lines.Length * 30f + 16f;
            var box = new Rect(left, top, w, h);
            Fill(box, PanelBg);

            _title.normal.textColor = tint;
            GUI.Label(new Rect(left + 14f, top + 8f, w - 28f, 46f), heading, _title);

            _info.normal.textColor = Color.white;
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(left + 16f, top + 56f + i * 30f, w - 32f, 28f), lines[i], _info);
        }

        /// <summary>A centred full-width banner for the transient phases (Dawn, Game Over).</summary>
        void DrawBanner(string text, Color tint)
        {
            var r = new Rect(0f, Screen.height * 0.45f, Screen.width, 60f);
            _title.normal.textColor = tint;
            var prev = _title.alignment;
            _title.alignment = TextAnchor.MiddleCenter;
            GUI.Label(r, text, _title);
            _title.alignment = prev;
        }

        /// <summary>
        /// A chunky touch button that also registers its footprint with <see cref="InputService"/>
        /// so a tap on it never falls through to world building. Returns true when clicked.
        /// </summary>
        bool Button(Rect rect, object blockerKey, string label, Color tint, bool emphasised)
        {
            if (InputService.Instance != null)
                InputService.Instance.SetUiBlockerGui(blockerKey, rect);

            var prevBg = GUI.backgroundColor;
            if (emphasised) GUI.backgroundColor = tint == default ? ActionTint : tint;
            else if (tint != default) GUI.backgroundColor = tint;
            bool clicked = GUI.Button(rect, label, _button);
            GUI.backgroundColor = prevBg;
            return clicked;
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        // ── Geometry / styles ───────────────────────────────────────────────────

        /// <summary>Safe-area edges in GUI (top-left origin) coordinates, inset by the margin.</summary>
        void Edges(out float left, out float right, out float top, out float bottom)
        {
            Rect safe = Screen.safeArea;   // bottom-left origin
            left   = safe.x + margin.x;
            right  = safe.xMax - margin.x;
            top    = (Screen.height - safe.yMax) + margin.y;
            bottom = (Screen.height - safe.y) - margin.y;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title  = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _info   = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        }

        void ClearBlockers()
        {
            if (InputService.Instance == null) return;
            InputService.Instance.ClearUiBlocker(_buildKey);
            InputService.Instance.ClearUiBlocker(_startNightKey);
            InputService.Instance.ClearUiBlocker(_pauseKey);
            InputService.Instance.ClearUiBlocker(_pausePanelKey);
        }
    }
}
