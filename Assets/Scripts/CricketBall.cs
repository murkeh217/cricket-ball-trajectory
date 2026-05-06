using UnityEngine;

namespace ParodyStudios.Cricket
{
    /// <summary>Type of delivery the bowler is releasing.</summary>
    public enum DeliveryType { Swing, Spin }

    /// <summary>Lateral direction relative to the bowler's forward axis.</summary>
    public enum LateralDirection { Left = -1, Right = 1 }

    /// <summary>
    /// Cricket ball trajectory controller.
    ///
    /// Phase I (Swing):
    ///   - In air: ball curves smoothly sideways while travelling forward.
    ///   - At bounce: lateral force stops INSTANTLY.
    ///   - After bounce: ball continues along the tangent direction it had at impact.
    ///
    /// Phase II (Spin):
    ///   - In air: ball travels in a straight line (no lateral deviation).
    ///   - At bounce: a single, instant sideways kick is applied (the "kink").
    ///   - After bounce: ball continues straight along the new direction.
    ///
    /// The whole flight is computed kinematically (transform-driven) so the path is
    /// deterministic, jitter-free, and matches the spec's "no glitches" requirement.
    /// </summary>
    [DisallowMultipleComponent]
    public class CricketBall : MonoBehaviour
    {
        // -----------------------------------------------------------------
        // Trajectory configuration
        // -----------------------------------------------------------------
        [Header("Trajectory")]
        [Tooltip("Forward speed of the ball through the air, m/s. ~28 m/s ≈ 100 km/h.")]
        [SerializeField] private float forwardSpeed = 28f;

        [Tooltip("Peak height of the parabolic arc above the line from release to bounce, in metres.")]
        [SerializeField] private float arcHeight = 2.2f;

        // -----------------------------------------------------------------
        // Phase I — Swing
        // -----------------------------------------------------------------
        [Header("Phase I — Swing (only used when DeliveryType = Swing)")]
        [Tooltip("Direction the ball curves toward while in the air.")]
        public LateralDirection swingDirection = LateralDirection.Right;

        [Tooltip("Strength of swing in [0..1]. Driven by the bowling meter at runtime.")]
        [Range(0f, 1f)] public float swingStrength = 0.7f;

        [Tooltip("Maximum total lateral deviation from the straight line, in metres, when strength = 1.")]
        [SerializeField] private float maxSwingOffset = 1.6f;

        [Tooltip("Shape of the swing offset over flight progress (0 = release, 1 = bounce). " +
                 "Should start gentle and accelerate so the curve feels natural.")]
        [SerializeField] private AnimationCurve swingProgressCurve =
            new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));

        // -----------------------------------------------------------------
        // Phase II — Spin
        // -----------------------------------------------------------------
        [Header("Phase II — Spin (only used when DeliveryType = Spin)")]
        [Tooltip("Off-spin = one side, Leg-spin = the opposite side.")]
        public LateralDirection spinDirection = LateralDirection.Right;

        [Tooltip("Strength of spin in [0..1]. Driven by the bowling meter at runtime.")]
        [Range(0f, 1f)] public float spinStrength = 0.7f;

        [Tooltip("Maximum lateral velocity (m/s) added to the ball at the bounce when strength = 1.")]
        [SerializeField] private float maxSpinDeflectionSpeed = 9f;

        // -----------------------------------------------------------------
        // Post-bounce
        // -----------------------------------------------------------------
        [Header("Post-Bounce")]
        [Tooltip("Velocity damping per second applied after bounce (simulates ground drag).")]
        [SerializeField] private float postBounceDamping = 0.4f;

        [Tooltip("Time (seconds) the ball travels after bouncing before being considered done.")]
        [SerializeField] private float postBounceLifetime = 4f;

        [Tooltip("Optional trail renderer to visualize the path. Cleared on each new delivery.")]
        [SerializeField] private TrailRenderer trail;

        // -----------------------------------------------------------------
        // Runtime state
        // -----------------------------------------------------------------
        private enum Phase { Idle, Air, Ground, Done }
        private Phase _phase = Phase.Idle;

        private DeliveryType _delivery;
        private Vector3 _startPos;
        private Vector3 _bouncePos;
        private Vector3 _flightForward;     // unit XZ forward (from release to bounce)
        private Vector3 _flightRight;       // unit XZ right (used for lateral offsets)
        private float _flightDuration;
        private float _flightTimer;

        private Vector3 _prevPos;            // for tangent calculation at bounce
        private Vector3 _postBounceVelocity;
        private float _postBounceTimer;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>True if the ball can be bowled again (idle or finished previous delivery).</summary>
        public bool IsIdle => _phase == Phase.Idle || _phase == Phase.Done;

        /// <summary>Current phase of the ball, useful for UI state.</summary>
        public bool IsInAir   => _phase == Phase.Air;
        public bool HasBounced => _phase == Phase.Ground || _phase == Phase.Done;

        /// <summary>
        /// Begin a delivery from <paramref name="releasePos"/> to <paramref name="bouncePos"/>.
        /// </summary>
        public void Launch(Vector3 releasePos, Vector3 bouncePos, DeliveryType delivery)
        {
            _delivery   = delivery;
            _startPos   = releasePos;
            _bouncePos  = bouncePos;
            transform.position = releasePos;

            // Compute flight basis on the XZ plane.
            Vector3 fwd = bouncePos - releasePos;
            fwd.y = 0f;
            float dist = fwd.magnitude;
            _flightForward = dist > 0.0001f ? fwd / dist : Vector3.forward;
            _flightRight   = Vector3.Cross(Vector3.up, _flightForward);

            _flightDuration = Mathf.Max(0.05f, dist / Mathf.Max(0.1f, forwardSpeed));
            _flightTimer    = 0f;
            _prevPos        = releasePos;

            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }

            _phase = Phase.Air;
        }

        /// <summary>Reset the ball back to a neutral state (e.g., bowler hand) ready for next delivery.</summary>
        public void ResetBall(Vector3 position)
        {
            transform.position    = position;
            _phase                = Phase.Idle;
            _postBounceVelocity   = Vector3.zero;
            _postBounceTimer      = 0f;

            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }

        // -----------------------------------------------------------------
        // Per-frame update
        // -----------------------------------------------------------------
        private void Update()
        {
            switch (_phase)
            {
                case Phase.Air:    UpdateAirPhase();    break;
                case Phase.Ground: UpdateGroundPhase(); break;
            }
        }

        /// <summary>Air phase: forward + (optional) gradual lateral curve + parabolic arc.</summary>
        private void UpdateAirPhase()
        {
            _flightTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_flightTimer / _flightDuration);

            // 1. Linear interpolation in XZ from release to bounce target.
            Vector3 ground = Vector3.Lerp(
                new Vector3(_startPos.x,  0f, _startPos.z),
                new Vector3(_bouncePos.x, 0f, _bouncePos.z),
                t);

            // 2. Parabolic arc on Y. 4*t*(1-t) peaks at 1.0 at t=0.5.
            float baseY = Mathf.Lerp(_startPos.y, _bouncePos.y, t);
            float y = baseY + arcHeight * 4f * t * (1f - t);

            // 3. Lateral offset — only for swing deliveries.
            //    Spin deliveries travel straight in the air (per spec).
            float lateral = 0f;
            if (_delivery == DeliveryType.Swing)
            {
                float curveT = swingProgressCurve.Evaluate(t);
                lateral = curveT * swingStrength * maxSwingOffset * (int)swingDirection;
            }

            Vector3 next = new Vector3(ground.x, y, ground.z) + _flightRight * lateral;

            _prevPos = transform.position;
            transform.position = next;

            if (t >= 1f) HandleBounce();
        }

        /// <summary>Bounce moment: lock direction, optionally apply spin kick, switch to ground phase.</summary>
        private void HandleBounce()
        {
            // Tangent velocity at the moment of impact.
            // For swing: this naturally already contains the sideways drift built up during flight.
            // For spin:  this is purely along _flightForward.
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 tangentVel = (transform.position - _prevPos) / dt;

            // Snap to the bounce surface height.
            Vector3 pos = transform.position;
            pos.y = _bouncePos.y;
            transform.position = pos;

            // Discard vertical component — post-bounce travel is along the pitch.
            tangentVel.y = 0f;

            // PHASE I — Swing: NOTHING is applied here. The lateral force stops instantly.
            //                  The ball simply continues along its current tangent (a straight
            //                  line that extends naturally from the curve).
            //
            // PHASE II — Spin: a single, instant lateral kick is applied. This is the "kink"
            //                  that separates the straight-before from straight-after segments.
            if (_delivery == DeliveryType.Spin)
            {
                Vector3 kick = _flightRight * (spinStrength * maxSpinDeflectionSpeed * (int)spinDirection);
                tangentVel += kick;
            }

            _postBounceVelocity = tangentVel;
            _postBounceTimer    = 0f;
            _phase              = Phase.Ground;
        }

        /// <summary>Ground phase: pure straight-line motion with mild damping.</summary>
        private void UpdateGroundPhase()
        {
            _postBounceTimer += Time.deltaTime;

            // Exponential damping so the ball gradually slows. No lateral forces are applied,
            // so the direction never changes after the bounce — exactly as the spec requires.
            float damp = Mathf.Exp(-postBounceDamping * Time.deltaTime);
            _postBounceVelocity *= damp;

            transform.position += _postBounceVelocity * Time.deltaTime;

            if (_postBounceTimer >= postBounceLifetime)
            {
                if (trail != null) trail.emitting = false;
                _phase = Phase.Done;
            }
        }

        // -----------------------------------------------------------------
        // Editor helpers
        // -----------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            // Visualize the planned trajectory in the editor when the ball is selected.
            if (!Application.isPlaying) return;
            if (_phase != Phase.Air) return;

            Gizmos.color = Color.yellow;
            Vector3 prev = _startPos;
            const int steps = 32;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 ground = Vector3.Lerp(
                    new Vector3(_startPos.x, 0f, _startPos.z),
                    new Vector3(_bouncePos.x, 0f, _bouncePos.z), t);
                float y = Mathf.Lerp(_startPos.y, _bouncePos.y, t) + arcHeight * 4f * t * (1f - t);
                float lateral = 0f;
                if (_delivery == DeliveryType.Swing)
                {
                    float c = swingProgressCurve.Evaluate(t);
                    lateral = c * swingStrength * maxSwingOffset * (int)swingDirection;
                }
                Vector3 p = new Vector3(ground.x, y, ground.z) + _flightRight * lateral;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
