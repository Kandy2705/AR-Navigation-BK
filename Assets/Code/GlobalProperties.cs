using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalProperties : MonoBehaviour
{
    public static GlobalProperties Instance { get; private set; }

    public bool IsShowNavigation = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
