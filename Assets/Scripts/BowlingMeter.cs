using UnityEngine;
using UnityEngine.UI;

namespace ParodyStudios.Cricket
{
    /// <summary>
    /// Vertical bowling meter (Spin / Swing %).
    ///
    /// The indicator (a small horizontal black bar) oscillates up and down along
    /// the colored track. The CENTER of the track is the maximum effect (blue, 100%).
    /// The further the indicator is from center when locked, the weaker the effect:
    ///
    ///     Red    (edges)  -> 0%   — flat delivery, no swing/spin
    ///     Yellow          -> ~40% — slight movement
    ///     Green           -> ~70% — strong, effective movement
    ///     Blue   (center) -> 100% — maximum
    ///
    /// Note: this meter measures swing/spin STRENGTH, not accuracy.
    /// </summary>
    public class BowlingMeter : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The moving indicator bar (RectTransform). Anchor it to the track's vertical center.")]
        [SerializeField] private RectTransform indicator;

        [Tooltip("The colored track background (RectTransform). Used to compute travel range.")]
        [SerializeField] private RectTransform track;

        [Header("Behaviour")]
        [Tooltip("Indicator oscillation speed. 1.0 = one full top→bottom→top trip per ~2 seconds.")]
        [SerializeField, Min(0.1f)] private float oscillateSpeed = 1.5f;

        [Tooltip("Start oscillating automatically on enable.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Optional: text/image showing the locked %. Updated when Lock() is called.")]
        [SerializeField] private Text strengthText;

        // Internal state
        private bool  _running;
        private float _t = -1f; // -1 (bottom of track) .. +1 (top of track); 0 = center (max effect)
        private int   _dir = 1;
        private float _trackHeight;
        private float _lastStrength;

        // -----------------------------------------------------------------
        public float CurrentStrength => 1f - Mathf.Abs(_t);
        public float LastLockedStrength => _lastStrength;
        public bool  IsRunning => _running;

        // -----------------------------------------------------------------
        private void Start()
        {
            if (track != null) _trackHeight = track.rect.height;
            if (autoStart) Restart();
        }

        private void Update()
        {
            if (!_running || indicator == null || track == null) return;

            _t += oscillateSpeed * _dir * Time.deltaTime;

            // Bounce between -1 and +1.
            if (_t >= 1f)  { _t = 1f;  _dir = -1; }
            if (_t <= -1f) { _t = -1f; _dir =  1; }

            // Position indicator along the track. _t = +1 -> top, _t = -1 -> bottom.
            float halfHeight = _trackHeight * 0.5f;
            Vector2 ap = indicator.anchoredPosition;
            ap.y = _t * halfHeight;
            indicator.anchoredPosition = ap;
        }

        // -----------------------------------------------------------------
        /// <summary>
        /// Stop the indicator and return the resulting spin/swing strength in [0..1].
        /// 1 = perfect (center), 0 = edges.
        /// </summary>
        public float Lock()
        {
            _running = false;
            _lastStrength = Mathf.Clamp01(1f - Mathf.Abs(_t));

            if (strengthText != null)
                strengthText.text = $"{Mathf.RoundToInt(_lastStrength * 100f)}%";

            return _lastStrength;
        }

        /// <summary>Reset the meter and start oscillating again from the bottom.</summary>
        public void Restart()
        {
            _t   = -1f;
            _dir =  1;
            _running = true;
            if (strengthText != null) strengthText.text = "";
        }

        /// <summary>Pause/resume the indicator without resetting position.</summary>
        public void SetRunning(bool running) => _running = running;
    }
}
