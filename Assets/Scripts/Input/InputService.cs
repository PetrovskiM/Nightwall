using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nightwall
{
    /// <summary>
    /// The single input abstraction for Nightwall. It reads raw mouse/keyboard (Editor + desktop)
    /// and touch (phones/tablets) once per frame and exposes <b>high-level, device-agnostic
    /// intents</b> — tap, long-press, a pan/pinch/twist camera gesture — so gameplay and camera code
    /// never touch <see cref="Mouse"/> or <see cref="Touchscreen"/> directly.
    ///
    /// Mobile mapping:
    ///   • Single tap        → <see cref="Tap"/>            (select / build)
    ///   • Tap-and-hold      → <see cref="Hold"/>           (contextual interaction, e.g. remove)
    ///   • Two-finger drag   → <see cref="PanDelta"/>       (pan the camera)
    ///   • Pinch             → <see cref="PinchDelta"/>     (zoom)
    ///   • Two-finger twist  → <see cref="TwistDelta"/>     (optional rotate)
    /// Editor/desktop mapping (kept for development):
    ///   • Left click        → <see cref="Tap"/> / <see cref="PrimaryHeld"/> (paint-build)
    ///   • Right click / held left → <see cref="Hold"/>     (contextual interaction)
    ///   • Scroll wheel      → <see cref="PinchDelta"/>
    ///
    /// A tap is only reported when the press stayed put, was brief, started off UI, and never became
    /// a multi-finger gesture — this is what keeps building from firing while the player pans.
    /// Runs early (<see cref="DefaultExecutionOrder"/>) so every consumer reads a settled snapshot.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }

        [Header("Tap / hold tuning")]
        [Tooltip("Max screen-pixel travel for a press to still count as a tap (not a drag).")]
        [SerializeField] float tapMoveTolerance = 24f;
        [Tooltip("Seconds a stationary press must be held before a long-press fires.")]
        [SerializeField] float holdDuration = 0.45f;

        [Header("Camera gesture tuning")]
        [Tooltip("Mouse-scroll notches are scaled to pinch units so one abstraction drives zoom.")]
        [SerializeField] float scrollToPinch = 12f;
        [Tooltip("Ignore two-finger twist below this many degrees/frame to avoid jitter while panning.")]
        [SerializeField] float twistDeadzone = 0.4f;

        // ── Pointer ───────────────────────────────────────────────────────────
        /// <summary>True when a usable pointer (mouse or primary touch) exists this frame.</summary>
        public bool HasPointer { get; private set; }
        /// <summary>Screen position of the primary pointer (mouse cursor or first touch).</summary>
        public Vector2 PointerPosition { get; private set; }

        // ── Discrete intents (true for a single frame) ──────────────────────────
        /// <summary>A completed tap: brief, stationary, off-UI, single-finger.</summary>
        public bool Tap { get; private set; }
        public Vector2 TapPosition { get; private set; }
        /// <summary>A long-press fired this frame — use for contextual actions.</summary>
        public bool Hold { get; private set; }
        public Vector2 HoldPosition { get; private set; }

        // ── Continuous state ────────────────────────────────────────────────────
        /// <summary>Primary pointer held down off-UI — drives desktop paint-building.</summary>
        public bool PrimaryHeld { get; private set; }
        /// <summary>A multi-finger pan/pinch/twist is in progress; suppress placement.</summary>
        public bool IsGesturing { get; private set; }

        // ── Camera gestures (per frame) ─────────────────────────────────────────
        /// <summary>Screen-pixel pan requested this frame (two-finger drag).</summary>
        public Vector2 PanDelta { get; private set; }
        /// <summary>Positive = zoom in, negative = zoom out (pinch or scroll), in abstract units.</summary>
        public float PinchDelta { get; private set; }
        /// <summary>Clockwise twist in degrees this frame (two-finger rotate); 0 on desktop.</summary>
        public float TwistDelta { get; private set; }

        // Persistent UI-blocker rects in screen space (y-up). A press that begins inside one of
        // these is never turned into a tap/hold, so on-screen buttons don't also build the world.
        readonly Dictionary<object, Rect> _uiBlockers = new();

        // Single-press tracking (shared by mouse and single-touch).
        bool _pressActive;
        bool _pressOverUI;
        bool _multiDuringPress;
        bool _holdFired;
        Vector2 _pressDownPos;
        float _pressDownTime;
        float _pressMaxMove;
        Vector2 _lastPointerPos;

        // Two-finger gesture tracking.
        bool _twoActive;
        float _prevPinchDist;
        Vector2 _prevCentroid;
        float _prevTwistAngle;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Registers (or updates) a screen-space rectangle, in GUI coordinates (y-down, as IMGUI
        /// reports), that should swallow taps/holds so UI chrome never builds through. Call each frame
        /// while the UI is visible; call <see cref="ClearUiBlocker"/> when it hides.
        /// </summary>
        public void SetUiBlockerGui(object owner, Rect guiRect)
        {
            // Convert IMGUI (top-left origin) to input screen space (bottom-left origin).
            _uiBlockers[owner] = new Rect(guiRect.x, Screen.height - guiRect.yMax, guiRect.width, guiRect.height);
        }

        /// <summary>Removes a previously registered UI blocker.</summary>
        public void ClearUiBlocker(object owner) => _uiBlockers.Remove(owner);

        bool OverUi(Vector2 screenPos)
        {
            foreach (var kv in _uiBlockers)
                if (kv.Value.Contains(screenPos)) return true;
            return false;
        }

        void Update()
        {
            // Reset per-frame outputs; continuous ones are recomputed below.
            Tap = false;
            Hold = false;
            PanDelta = Vector2.zero;
            PinchDelta = 0f;
            TwistDelta = 0f;
            IsGesturing = false;

            var ts = Touchscreen.current;
            if (ts != null && ts.touches.Count > 0 && AnyTouchActive(ts))
                UpdateTouch(ts);
            else
                UpdateMouse();
        }

        static bool AnyTouchActive(Touchscreen ts)
        {
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].isInProgress) return true;
            return false;
        }

        // ── Touch ───────────────────────────────────────────────────────────────

        void UpdateTouch(Touchscreen ts)
        {
            // Collect active touch positions (no allocation: fixed scan).
            Vector2 p0 = default, p1 = default;
            int active = 0;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (!touches[i].isInProgress) continue;
                if (active == 0) p0 = touches[i].position.ReadValue();
                else if (active == 1) p1 = touches[i].position.ReadValue();
                active++;
            }

            if (active >= 2)
            {
                HasPointer = true;
                PointerPosition = (p0 + p1) * 0.5f;
                _multiDuringPress = true;     // any in-flight single press is no longer a tap
                _pressActive = false;
                PrimaryHeld = false;
                UpdateTwoFingerGesture(p0, p1);
                return;
            }

            _twoActive = false;

            // Single finger → behaves like a pointer press.
            HasPointer = true;
            PointerPosition = p0;
            // Touch builds on tap-release only; held-paint is a desktop convenience (a held touch is
            // reserved for the long-press contextual action), so held-paint is disabled here.
            UpdateSinglePress(p0, pressed: true, began: JustBeganSingle(ts), ended: JustEndedSingle(ts),
                enableHeldPaint: false);
        }

        void UpdateTwoFingerGesture(Vector2 p0, Vector2 p1)
        {
            IsGesturing = true;
            float dist = Vector2.Distance(p0, p1);
            Vector2 centroid = (p0 + p1) * 0.5f;
            float angle = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;

            if (!_twoActive)
            {
                _twoActive = true;
                _prevPinchDist = dist;
                _prevCentroid = centroid;
                _prevTwistAngle = angle;
                return;
            }

            PanDelta = centroid - _prevCentroid;
            PinchDelta = dist - _prevPinchDist;   // fingers apart → positive → zoom in

            float twist = Mathf.DeltaAngle(_prevTwistAngle, angle);
            TwistDelta = Mathf.Abs(twist) >= twistDeadzone ? twist : 0f;

            _prevPinchDist = dist;
            _prevCentroid = centroid;
            _prevTwistAngle = angle;
        }

        bool JustBeganSingle(Touchscreen ts)
        {
            var t = ts.touches[0];
            return t.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
        }

        bool JustEndedSingle(Touchscreen ts)
        {
            var t = ts.touches[0];
            var ph = t.phase.ReadValue();
            return ph == UnityEngine.InputSystem.TouchPhase.Ended ||
                   ph == UnityEngine.InputSystem.TouchPhase.Canceled;
        }

        // ── Mouse / keyboard (Editor + desktop) ──────────────────────────────────

        void UpdateMouse()
        {
            _twoActive = false;
            var mouse = Mouse.current;
            if (mouse == null) { HasPointer = false; PrimaryHeld = false; _pressActive = false; return; }

            HasPointer = true;
            PointerPosition = mouse.position.ReadValue();

            bool began = mouse.leftButton.wasPressedThisFrame;
            bool ended = mouse.leftButton.wasReleasedThisFrame;
            bool pressed = mouse.leftButton.isPressed;
            UpdateSinglePress(PointerPosition, pressed, began, ended, enableHeldPaint: true);

            // Right click is a direct contextual (long-press equivalent) on desktop.
            if (mouse.rightButton.wasPressedThisFrame && !OverUi(PointerPosition))
            {
                Hold = true;
                HoldPosition = PointerPosition;
            }

            // Scroll wheel maps onto the same pinch channel the camera consumes.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                PinchDelta = Mathf.Sign(scroll) * scrollToPinch;
        }

        // ── Shared single-press state machine ────────────────────────────────────

        void UpdateSinglePress(Vector2 pos, bool pressed, bool began, bool ended, bool enableHeldPaint)
        {
            if (began)
            {
                _pressActive = true;
                _pressOverUI = OverUi(pos);
                _multiDuringPress = false;
                _holdFired = false;
                _pressDownPos = pos;
                _lastPointerPos = pos;
                _pressDownTime = Time.unscaledTime;
                _pressMaxMove = 0f;
            }

            if (_pressActive && pressed)
            {
                _pressMaxMove = Mathf.Max(_pressMaxMove, Vector2.Distance(pos, _pressDownPos));
                _lastPointerPos = pos;

                // Fire a long-press once, while the finger is still down and stationary. Only when
                // held-paint is OFF (touch): on mouse the left button is the paint button, so a
                // stationary hold must keep painting, not turn into a contextual long-press/remove.
                if (!enableHeldPaint && !_holdFired && !_pressOverUI && !_multiDuringPress &&
                    _pressMaxMove <= tapMoveTolerance &&
                    Time.unscaledTime - _pressDownTime >= holdDuration)
                {
                    _holdFired = true;
                    Hold = true;
                    HoldPosition = pos;
                }
            }

            // PrimaryHeld drives desktop paint-building: down, off-UI, not a multi-finger gesture.
            // It stays true for the whole press (even stationary) so painting never drops out.
            PrimaryHeld = enableHeldPaint && _pressActive && pressed &&
                          !_pressOverUI && !_multiDuringPress && !_holdFired;

            if (ended && _pressActive)
            {
                bool wasTap = !_pressOverUI && !_multiDuringPress && !_holdFired &&
                              _pressMaxMove <= tapMoveTolerance &&
                              Time.unscaledTime - _pressDownTime < holdDuration;
                if (wasTap)
                {
                    Tap = true;
                    TapPosition = pos;
                }
                _pressActive = false;
                PrimaryHeld = false;
            }
        }
    }
}
