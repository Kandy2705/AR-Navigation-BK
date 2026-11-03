using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>Spins the loading arc. Uses unscaled time so it keeps turning while the game is paused.</summary>
    public class UiSpinner : MonoBehaviour
    {
        [Tooltip("Negative spins clockwise.")]
        [SerializeField] private float degreesPerSecond = -360f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
