using System.Collections;
using System.Collections.Generic;
using UnityEngine.UIElements; //them cai nay vao de su dung UIElements
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIOnboarding : MonoBehaviour
{
    private VisualElement homeElement; //khai bao bie de doc duoc tat ca UI trog file UXML

    // Start is called before the first frame update
    void Start()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement; //lay root element tu UIDocument

        homeElement = root.Q<VisualElement>("homePlay"); //tim element co ten la "home" trong UXML

        homeElement.style.display = DisplayStyle.Flex; //hien thi element "homePlay" ra

        Button btnStart = homeElement.Q<Button>("NextOnboarding"); //tim button co ten la "btnStart" trong UXML

        btnStart.RegisterCallback<ClickEvent>(playgame); //dang ky su kien click cho button

    }


    private void playgame(ClickEvent evt)
    {
        SceneManager.LoadScene("ManScene"); //chuyen sang scene "ManScene" khi button duoc click
    }

    // Update is called once per frame
    void Update()
    {

    }
}
