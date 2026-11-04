#if UNITY_EDITOR
using System;
using System.Reflection;
using Game.Features.Auth;
using Game.Foundation.UI;
using Game.Foundation.UI.EditorTools;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TestAR.Tests.Editor.EditMode
{
    /// <summary>
    /// A Canvas screen prefab instanced in a throw-away preview scene, so tests can drive it (type, click, read)
    /// without touching the scene the developer has open. Edit mode does not run Awake, so <see cref="Open{T}"/> does.
    /// </summary>
    internal sealed class PageHarness : IDisposable
    {
        private readonly Scene scene;

        /// <param name="screenName">Prefab name under Assets/Game/Bundles/UI.</param>
        /// <param name="credentialsKeyPrefix">Prefix for remembered-login keys, so tests never touch a real saved login.</param>
        public PageHarness(string screenName, string credentialsKeyPrefix)
        {
            scene = EditorSceneManager.NewPreviewScene();
            var canvas = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvas, scene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screenName));
            Assert.NotNull(prefab, screenName + ".prefab — run Tools/UI Canvas/Run Phase 3 Setup");
            Root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Saved = new SavedCredentials(credentialsKeyPrefix);
        }

        public GameObject Root { get; }

        public SavedCredentials Saved { get; }

        public UiStatusOverlay Overlay => Root.GetComponentInChildren<UiStatusOverlay>(true);

        public UiPickerSheet Sheet => Root.GetComponentInChildren<UiPickerSheet>(true);

        public string ErrorText
        {
            get
            {
                Transform label = Root.transform.Find("Content/ErrorLabel");
                return label != null && label.gameObject.activeSelf ? label.GetComponent<TMP_Text>().text : string.Empty;
            }
        }

        /// <summary>Wires the page's scripts and shows it (so OnShow runs).</summary>
        public T Open<T>(ILoginApi api = null, Action<LoginRes, string> session = null) where T : UiPage
        {
            foreach (MonoBehaviour behaviour in Root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Namespace != null && behaviour.GetType().Namespace.StartsWith("Game."))
                {
                    MethodInfo awake = behaviour.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    awake?.Invoke(behaviour, null);
                }
            }

            var page = Root.GetComponent<T>();
            if (page is LoginUI login)
            {
                login.Configure(api, session ?? ((res, password) => { }), Saved);
            }

            page.Show();
            return page;
        }

        public T Find<T>(string path) where T : Component
        {
            Transform child = Root.transform.Find(path);
            Assert.NotNull(child, path);
            T component = child.GetComponent<T>();
            Assert.NotNull(component, path + " has no " + typeof(T).Name);
            return component;
        }

        public void Click(string path)
        {
            Find<Button>(path).onClick.Invoke();
        }

        public void Type(string path, string text)
        {
            Find<UiTextField>(path).Text = text;
        }

        public void SetDigits(string digits)
        {
            TMP_InputField[] boxes = Find<UiOtpInput>("Content/OtpInput").GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i].SetTextWithoutNotify(i < digits.Length ? digits[i].ToString() : string.Empty);
            }
        }

        public void Dispose()
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
