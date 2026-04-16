using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DescriptionCollider : MonoBehaviour
{
    [SerializeField] private GameObject description;
    [SerializeField] private GameObject sign;

    void Awake()
    {
        var c = GetComponent<Collider>();
        Debug.Log($"DescriptionCollider Awake: name='{gameObject.name}', active={gameObject.activeInHierarchy}, hasCollider={(c != null)}, isTrigger={(c != null ? c.isTrigger.ToString() : "N/A")}, layer={LayerMask.LayerToName(gameObject.layer)}");

        // Quick runtime check for object with UserTrigger tag
        var user = GameObject.FindWithTag("UserTrigger");
        if (user == null)
        {
            Debug.LogWarning("No GameObject with tag 'UserTrigger' found in scene at Awake.");
        }
        else
        {
            var uc = user.GetComponent<Collider>();
            var rb = user.GetComponent<Rigidbody>();
            Debug.Log($"Found UserTrigger: name='{user.name}', hasCollider={(uc != null)}, hasRigidbody={(rb != null)}, layer={LayerMask.LayerToName(user.layer)}");
        }
    }

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
