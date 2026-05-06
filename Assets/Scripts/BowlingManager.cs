using UnityEngine;
using UnityEngine.UI;

namespace ParodyStudios.Cricket
{
    /// <summary>
    /// Orchestrates the bowling flow:
    ///
    ///   1. Player chooses delivery type via Swing / Spin buttons.
    ///   2. Player optionally toggles bowling side via "Bowling Change side".
    ///   3. Player aims with the bounce marker (WASD).
    ///   4. Player presses Bowl.
    ///       - The meter is locked, returning a strength in [0..1].
    ///       - That strength is applied to swing or spin depending on selected delivery.
    ///       - The ball is launched from the active release point toward the marker.
    ///   5. After the delivery completes, the ball resets and the meter restarts
    ///      automatically, ready for the next ball.
    /// </summary>
    public class BowlingManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private CricketBall   ball;
        [SerializeField] private BounceMarker  bounceMarker;
        [SerializeField] private BowlingMeter  meter;

        [Header("Release Points")]
        [Tooltip("Bowler's release point when bowling from the right side of the wicket.")]
        [SerializeField] private Transform releasePointRight;

        [Tooltip("Bowler's release point when bowling from the left side of the wicket. " +
                 "If unset, the right release point is always used.")]
        [SerializeField] private Transform releasePointLeft;

        [Header("UI Buttons")]
        [SerializeField] private Button swingButton;
        [SerializeField] private Button spinButton;
        [SerializeField] private Button bowlButton;
        [SerializeField] private Button changeSideButton;

        [Header("UI Visual Feedback (optional)")]
        [Tooltip("Color applied to the currently selected delivery button.")]
        [SerializeField] private Color selectedTint = new Color(0.6f, 0.85f, 1f, 1f);
        [SerializeField] private Color unselectedTint = Color.white;

        [Header("Timing")]
        [Tooltip("Seconds after launching before the ball is reset and the meter restarts.")]
        [SerializeField] private float resetDelay = 5f;

        // -----------------------------------------------------------------
        private DeliveryType _selectedDelivery = DeliveryType.Swing;
        private bool         _bowlerOnRight    = true;

        // -----------------------------------------------------------------
        private void Awake()
        {
            if (swingButton      != null) swingButton.onClick.AddListener(() => SetDelivery(DeliveryType.Swing));
            if (spinButton       != null) spinButton.onClick.AddListener (() => SetDelivery(DeliveryType.Spin));
            if (bowlButton       != null) bowlButton.onClick.AddListener (Bowl);
            if (changeSideButton != null) changeSideButton.onClick.AddListener(ChangeSide);
        }

        private void Start()
        {
            // Apply initial selection visuals.
            UpdateButtonVisuals();
        }

        // -----------------------------------------------------------------
        // Button handlers
        // -----------------------------------------------------------------
        public void SetDelivery(DeliveryType type)
        {
            _selectedDelivery = type;
            UpdateButtonVisuals();
        }

        public void ChangeSide()
        {
            if (releasePointLeft == null) return; // No alternate side configured.
            _bowlerOnRight = !_bowlerOnRight;
        }

        public void Bowl()
        {
            if (ball == null || bounceMarker == null || meter == null) return;
            if (!ball.IsIdle) return;

            // 1. Lock the meter to read strength.
            float strength = meter.Lock();

            // 2. Apply strength to the appropriate parameter on the ball.
            if (_selectedDelivery == DeliveryType.Swing) ball.swingStrength = strength;
            else                                          ball.spinStrength  = strength;

            // 3. Pick the active release point.
            Transform release = (_bowlerOnRight || releasePointLeft == null)
                ? releasePointRight
                : releasePointLeft;
            if (release == null) return;

            // 4. Launch the ball.
            ball.Launch(release.position, bounceMarker.Position, _selectedDelivery);

            // 5. Schedule reset for the next delivery.
            CancelInvoke(nameof(ResetForNextBall));
            Invoke(nameof(ResetForNextBall), resetDelay);
        }

        // -----------------------------------------------------------------
        private void ResetForNextBall()
        {
            Transform release = (_bowlerOnRight || releasePointLeft == null)
                ? releasePointRight
                : releasePointLeft;
            if (ball != null && release != null) ball.ResetBall(release.position);
            if (meter != null) meter.Restart();
        }

        private void UpdateButtonVisuals()
        {
            if (swingButton != null)
            {
                var img = swingButton.GetComponent<Image>();
                if (img != null) img.color = (_selectedDelivery == DeliveryType.Swing) ? selectedTint : unselectedTint;
            }
            if (spinButton != null)
            {
                var img = spinButton.GetComponent<Image>();
                if (img != null) img.color = (_selectedDelivery == DeliveryType.Spin)  ? selectedTint : unselectedTint;
            }
        }
    }
}
