using UnityEngine;

public class DescriptionCollider : MonoBehaviour
{
    [SerializeField] private GameObject description;
    [SerializeField] private GameObject sign;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("UserTrigger")) return;

        sign.SetActive(false);
        description.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("UserTrigger")) return;

        description.SetActive(false);
        sign.SetActive(true);
    }
}