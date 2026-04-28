using UnityEngine;

public class ARPageController : MonoBehaviour
{
    [Tooltip("Object UI Toolkit chinh (MainScreen) de quay lai.")]
    public GameObject nextObject;

    public void SwitchObject()
    {
        if (nextObject == null)
        {
            Debug.LogError("[ARPageController] nextObject is not assigned.");
            return;
        }

        var navigator = nextObject.GetComponent<NavigationManager>();
        if (navigator == null)
        {
            Debug.LogError("[ARPageController] NavigationManager not found on nextObject.");
            return;
        }

        navigator.firstPage = navigator.ConsumeReturnPageFromAR();
        nextObject.SetActive(true);

        if (navigator.ARPageObject != null)
        {
            navigator.ARPageObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[ARPageController] ARPageObject is null on NavigationManager.");
        }
    }
}
