using UnityEngine;

public class ARPageController : MonoBehaviour
{
    [Tooltip("Kéo object màn hình mới vào đây")]
    public GameObject nextObject;
    private NavigationManager navigator;

    // private void Awake()
    // {
    //     if(nextObject != null)
    //     {
    //         navigator = nextObject.GetComponent<NavigationManager>();
    //         navigator.firstPage = NavigationManager.pageHistory.Peek();
    //         Debug.Log($"Trang trước đó trước khi vào trang AR là {navigator.firstPage}");
    //     }
    // }
    public void SwitchObject()
    {
         if(nextObject != null)
        {
            navigator = nextObject.GetComponent<NavigationManager>();
            navigator.firstPage = navigator.PreviousPage();
            Debug.Log($"Trang trước đó trước khi vào trang AR là {navigator.firstPage}");
        }
        if (nextObject != null)
        {
            nextObject.SetActive(true); 
        }
        
        gameObject.SetActive(false);
    }
}