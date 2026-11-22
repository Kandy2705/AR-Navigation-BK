using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// uGUI needs exactly one EventSystem. Hybrid scenes may already have one (e.g. from the Multiset
    /// samples), so only create one when none exists.
    /// </summary>
    public class UiEventSystemEnsurer : MonoBehaviour
    {
        private void Awake()
        {
            // Edit-mode previews and tests wake prefabs by hand; they must not add an EventSystem to the open scene.
            if (!Application.isPlaying)
            {
                return;
            }

            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
