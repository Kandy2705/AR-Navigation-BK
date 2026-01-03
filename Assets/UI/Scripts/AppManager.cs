// using UnityEngine;
// using UnityEngine.UIElements;

// public class AppManager : MonoBehaviour
// {

//     private UIDocument _uiDoc;

//     public VisualTreeAsset historyUXML;
//     public VisualTreeAsset chatUXML;
//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
//         _uiDoc = GetComponent<UIDocument>();
//         GoToHistory();
//     }

//     public void GoToHistory()
//     {
//         _uiDoc.visualTreeAsset = historyUXML;

//         var root = _uiDoc.rootVisualElement;
//         //new HistoryLogic(root, this);
//     }

//     public void GoToChat(string chatName)
//     {
//         _uiDoc.visualTreeAsset = chatUXML;

//         var root = _uiDoc.rootVisualElement;
//         var chatLogic = new ChatDetailController(root);
//         chatLogic.SetupData(chatName);

//         chatLogic.OnCloseRequest = () => {
//             GoToHistory();
//         };
//     }

//     // Update is called once per frame
//     void Update()
//     {
        
//     }
// }
