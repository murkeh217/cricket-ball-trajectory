using UnityEngine;

namespace ParodyStudios.Cricket
{
    /// <summary>
    /// Bounce marker — the circular indicator on the pitch that tells the player
    /// where the ball will land. Moves smoothly under WASD with optional clamping
    /// to a rectangular pitch area.
    ///
    /// W → forward     S → backward     A → left     D → right
    /// </summary>
    public class BounceMarker : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Movement speed in units (metres) per second.")]
        [SerializeField] private float moveSpeed = 5f;

        [Tooltip("Smoothing time. 0 = snappy, higher = more easing. Around 0.05 feels responsive yet smooth.")]
        [SerializeField, Min(0f)] private float smoothing = 0.05f;

        [Header("Pitch Bounds (XZ in world space)")]
        [Tooltip("Minimum allowed (X, Z) position on the pitch.")]
        [SerializeField] private Vector2 minBounds = new Vector2(-1.4f, 1f);

        [Tooltip("Maximum allowed (X, Z) position on the pitch.")]
        [SerializeField] private Vector2 maxBounds = new Vector2( 1.4f, 18f);

        [Tooltip("Y position the marker sits at (just above the pitch surface).")]
        [SerializeField] private float fixedY = 0.02f;

        [Header("Input (optional remapping)")]
        [SerializeField] private KeyCode forwardKey  = KeyCode.W;
        [SerializeField] private KeyCode backwardKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey     = KeyCode.A;
        [SerializeField] private KeyCode rightKey    = KeyCode.D;

        [Header("State")]
        [Tooltip("If true, input is read every frame. Disable while the ball is flying if desired.")]
        public bool inputEnabled = true;

        private Vector3 _velocity; // for SmoothDamp

        public Vector3 Position => transform.position;

        private void Update()
        {
            Vector3 input = Vector3.zero;

            if (inputEnabled)
            {
                if (Input.GetKey(forwardKey))  input.z += 1f;
                if (Input.GetKey(backwardKey)) input.z -= 1f;
                if (Input.GetKey(leftKey))     input.x -= 1f;
                if (Input.GetKey(rightKey))    input.x += 1f;
            }

            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 desired = transform.position + input * moveSpeed * Time.deltaTime;
            desired.x = Mathf.Clamp(desired.x, minBounds.x, maxBounds.x);
            desired.z = Mathf.Clamp(desired.z, minBounds.y, maxBounds.y);
            desired.y = fixedY;

            // SmoothDamp gives a soft, responsive feel that matches "smooth and responsive" in the spec.
            transform.position = smoothing > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothing)
                : desired;
        }

        // -----------------------------------------------------------------
        /// <summary>Programmatically set the marker position (e.g., when changing bowling side).</summary>
        public void SetPosition(Vector3 worldPos)
        {
            worldPos.x = Mathf.Clamp(worldPos.x, minBounds.x, maxBounds.x);
            worldPos.z = Mathf.Clamp(worldPos.z, minBounds.y, maxBounds.y);
            worldPos.y = fixedY;
            transform.position = worldPos;
            _velocity = Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the allowed area in the editor.
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, fixedY,
                                         (minBounds.y + maxBounds.y) * 0.5f);
            Vector3 size   = new Vector3(maxBounds.x - minBounds.x, 0.01f,
                                         maxBounds.y - minBounds.y);
            Gizmos.DrawCube(center, size);
        }
    }
}
