using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatInputLegacy : MonoBehaviour
{
    public InputField inputField;
    public Image talkImage;
    public TMP_Text talkText;
    private string[] keyWords = new[] { "B10", "B9", "B8", "A4", "B4" };

    void Start()
    {
        StartCoroutine(ShowTalking("Hãy hỏi tui"));
        inputField.onSubmit.AddListener(HandleSubmit);
    }

    IEnumerator ShowTalking(string text)
    {
        Debug.Log(text);
        talkImage.gameObject.SetActive(true);
        talkText.text = text;
        yield return new WaitForSecondsRealtime(2f);
        talkImage.gameObject.SetActive(false);
    }

    void HandleSubmit(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Debug.Log("Send: " + text);
            foreach (string word in keyWords)
            {
                if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    StartCoroutine(ShowTalking(word));
                    GlobalProperties.Instance.IsShowNavigation = true;
                }
            }

            inputField.text = "";
            inputField.ActivateInputField();
        }
    }
}
