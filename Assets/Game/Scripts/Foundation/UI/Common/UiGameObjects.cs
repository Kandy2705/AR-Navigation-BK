using UnityEngine;

namespace Game.Foundation.UI
{
    public static class UiGameObjects
    {
        /// <summary>Removes a clone a screen made: Destroy at runtime, DestroyImmediate in edit mode (tests, previews), where Destroy is refused.</summary>
        public static void Discard(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
