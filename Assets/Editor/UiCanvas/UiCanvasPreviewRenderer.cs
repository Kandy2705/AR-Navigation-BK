#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Features.Auth;
using Game.Features.Chat;
using Game.Features.Home;
using Game.Features.Settings;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Renders a screen prefab to a PNG at 2x of the 375x812 Figma frame, so a layout can be compared with
    /// the design without entering Play mode, at several screen heights to check the layout stretches.
    /// Output: Logs/UiPreview/&lt;Name&gt;_&lt;height&gt;.png. Edit mode only: building a preview scene is not allowed in Play mode.
    /// </summary>
    public static class UiCanvasPreviewRenderer
    {
        private const int Scale = 2;

        [MenuItem("Tools/UI Canvas/Render Page Previews")]
        public static void RenderAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before rendering previews.");
                return;
            }

            Directory.CreateDirectory(UiCanvasPaths.PreviewFolder);
            IChatApi realChat = ChatServices.Api;
            string realProfile = PlayerPrefs.GetString(AppConst.KEY_CACHE, string.Empty);
            bool hadProfile = PlayerPrefs.HasKey(AppConst.KEY_CACHE);
            ChatServices.Api = new SampleChatApi();
            PlayerPrefs.SetString(AppConst.KEY_CACHE, "{\"email\":\"kandy@example.com\",\"name\":\"Kandy\",\"phone\":\"04384983483\",\"gender\":\"Nữ\",\"birthday\":\"2004-05-27T00:00:00.000Z\"}");
            try
            {
                RenderEverything();
            }
            finally
            {
                // Previews must not leave the sample data behind: the profile cache is the signed-in user's.
                ChatServices.Api = realChat;
                ChatState.Clear();
                if (hadProfile)
                {
                    PlayerPrefs.SetString(AppConst.KEY_CACHE, realProfile);
                }
                else
                {
                    PlayerPrefs.DeleteKey(AppConst.KEY_CACHE);
                }
            }
        }

        private static void RenderEverything()
        {
            foreach (UiCanvasShellRoutes.Route route in UiCanvasShellRoutes.All)
            {
                // 812 is the Figma frame; 667 is an iPhone SE / 16:9 screen (width stays 375); 900 is a tall phone.
                foreach (float height in new[] { 667f, 812f, 900f })
                {
                    Render(route.Screen, height, IsLive(route.Screen) ? Live() : null);
                }
            }

            // States that start hidden in the prefab (success card, picker sheet) are shown explicitly.
            foreach (float height in new[] { 667f, 812f })
            {
                Render(UiCanvasAuthBuilder.LoginName, height, ShowOverlay(LoginUI.SuccessTitle, LoginUI.SuccessMessage), "success");
                Render(UiCanvasAuthBuilder.NewPasswordName, height, ShowOverlay(NewPasswordUI.SuccessTitle, NewPasswordUI.SuccessMessage), "success");
                Render(UiCanvasRegisterBuilder.CreatePasswordName, height, ShowOverlay(CreatePasswordUI.SuccessTitle, CreatePasswordUI.SuccessMessage), "success");
                Render(UiCanvasRegisterBuilder.RegisterName, height, ShowGenderSheet, "gender");
                Render(UiCanvasRegisterBuilder.RegisterName, height, ShowBirthdaySheet, "birthday");

                Render(UiCanvasHomeBuilder.ArHudName, height, Live(page => page.GetComponent<ArHudUI>().SetDestination("Tòa A4")), "route");
                Render(UiCanvasHomeBuilder.HistoryName, height, Live(page =>
                    page.GetComponentInChildren<UiModal>(true).Show(HistoryUI.DeleteAllTitle, HistoryUI.DeleteAllMessage, "Xóa", "Hủy")), "delete-all");
                Render(UiCanvasHomeBuilder.HistoryName, height, Live(page =>
                    page.GetComponentsInChildren<UiSwipeRow>()[4].Open()), "swiped");
                Render(UiCanvasHomeBuilder.SearchName, height, Live(page => TypeQuery(page, "toa")), "results");
                Render(UiCanvasHomeBuilder.SearchName, height, Live(page => TypeQuery(page, "burger")), "empty");
                Render(UiCanvasSettingsBuilder.SettingsName, height, Live(page =>
                {
                    var modal = page.GetComponentInChildren<UiModal>(true);
                    modal.SetTitleColor(UiTheme.DangerText);
                    modal.Show(SettingsUI.LogoutTitle, SettingsUI.LogoutMessage, SettingsUI.LogoutTitle, "Hủy");
                }), "logout");
                Render(UiCanvasSettingsBuilder.SupportCenterName, height, Live(page =>
                    page.GetComponentsInChildren<UiAccordionItem>()[1].SetOpen(true)), "open");
                Render(UiCanvasSettingsBuilder.ChangePasswordName, height, ShowOverlay(ChangePasswordUI.SuccessTitle, ChangePasswordUI.SuccessMessage), "success");
            }
        }

        private static bool IsLive(string screen)
        {
            return screen == UiCanvasHomeBuilder.ArHudName || screen == UiCanvasHomeBuilder.HistoryName
                || screen == UiCanvasHomeBuilder.ChatName || screen == UiCanvasHomeBuilder.SearchName
                || screen == UiCanvasSettingsBuilder.SettingsName || screen == UiCanvasSettingsBuilder.ProfileName
                || screen == UiCanvasSettingsBuilder.SupportCenterName || screen == UiCanvasSettingsBuilder.ContactName;
        }

        /// <summary>
        /// Runs Awake (edit mode does not) and shows the page, so lists and fields are filled from the sample data;
        /// then applies <paramref name="extra"/>.
        /// </summary>
        private static Action<GameObject> Live(Action<GameObject> extra = null)
        {
            return root =>
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Namespace != null && behaviour.GetType().Namespace.StartsWith("Game."))
                    {
                        MethodInfo awake = behaviour.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        awake?.Invoke(behaviour, null);
                    }
                }

                var page = root.GetComponent<UiPage>();
                if (root.GetComponent<SearchUI>() != null)
                {
                    new SampleChatApi().GetHistories(items => ChatState.Histories = items, null);
                }

                if (root.GetComponent<ChatUI>() != null)
                {
                    ChatState.Current = SampleChatApi.Conversation();
                }

                page.Show();
                extra?.Invoke(root);
            };
        }

        private static void TypeQuery(GameObject page, string query)
        {
            page.GetComponentInChildren<TMP_InputField>(true).text = query;
        }

        private sealed class SampleChatApi : IChatApi
        {
            private static readonly string[] Titles =
            {
                "Tòa A4", "Thông tin về tòa OISP", "Tòa A4", "Tòa A4",
                "Thông tin chi tiết về tòa OISP, chỗ hỏi, chỗ hỏi, chỗ hỏi, chỗ hỏi, chỗ hỏi",
            };

            public static ChatHistoryItem Conversation()
            {
                const string answer = "Đây là dữ liệu mà tôi biết và sau đây là câu trả lời";
                return new ChatHistoryItem
                {
                    id = "sample",
                    header = "Tòa A$",
                    messages = new List<ChatMessage>
                    {
                        ChatMessages.FromUser("Lịch sử hình thành của tòa B4 đi."),
                        new ChatMessage { contact_person = "assistant", content = answer },
                        new ChatMessage { contact_person = "assistant", content = answer },
                        ChatMessages.FromUser(answer),
                        new ChatMessage { contact_person = "assistant", content = answer },
                    },
                };
            }

            public void GetHistories(Action<List<ChatHistoryItem>> onSuccess, Action<long, string> onError)
            {
                var items = new List<ChatHistoryItem>();
                for (int i = 0; i < Titles.Length; i++)
                {
                    items.Add(new ChatHistoryItem { id = "s" + i, header = Titles[i], create_date = "2024-03-08T03:23:00" });
                }

                onSuccess(items);
            }

            public void CreateHistory(string header, Action<ChatHistoryItem> onSuccess, Action<long, string> onError) { }

            public void DeleteHistory(string historyId, Action onSuccess, Action<long, string> onError) { }

            public void GetMessages(string historyId, Action<List<ChatMessage>> onSuccess, Action<long, string> onError) { }

            public void Send(string content, string historyId, Action<ChatMessage> onSuccess, Action<long, string> onError) { }
        }

        private static Action<GameObject> ShowOverlay(string title, string message)
        {
            return page => page.GetComponentInChildren<UiStatusOverlay>(true).Show(title, message);
        }

        private static void ShowGenderSheet(GameObject page)
        {
            var layout = new List<UiPickerSheet.Column>
            {
                new UiPickerSheet.Column { Options = RegisterUI.Genders, Selected = 1 },
            };
            page.GetComponentInChildren<UiPickerSheet>(true).Show("Chọn giới tính", layout, null);
        }

        private static void ShowBirthdaySheet(GameObject page)
        {
            List<int> years = BirthdayFormat.Years(DateTime.Today);
            var layout = new List<UiPickerSheet.Column>
            {
                new UiPickerSheet.Column { Options = BirthdayFormat.Days(), Selected = 26 },
                new UiPickerSheet.Column { Options = BirthdayFormat.Months(), Selected = 4 },
                new UiPickerSheet.Column { Options = years.ConvertAll(year => year.ToString()), Selected = years.IndexOf(2004) },
            };
            page.GetComponentInChildren<UiPickerSheet>(true).Show("Chọn ngày sinh", layout, null);
        }

        public static void Render(string screenName, float frameHeight, Action<GameObject> prepare = null, string state = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(screenName));
            if (prefab == null)
            {
                Debug.LogError("[UiCanvas] Missing prefab for " + screenName);
                return;
            }

            Vector2 frame = new Vector2(UiTheme.ReferenceResolution.x, frameHeight);
            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary((int)frame.x * Scale, (int)frame.y * Scale, 24);
            try
            {
                // World-space canvas sized exactly like the Figma frame; an orthographic camera frames it.
                var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, preview);
                var camera = cameraGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = frame.y * 0.5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.scene = preview;

                var canvasGo = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasGo, preview);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = frame;
                canvasRect.position = Vector3.zero;

                var page = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
                prepare?.Invoke(page);

                foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                }

                // Dynamic font atlases fill on first use, so render twice.
                for (int pass = 0; pass < 2; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                }

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string suffix = state != null ? "_" + state : string.Empty;
                string file = Path.Combine(UiCanvasPaths.PreviewFolder, screenName + "_" + (int)frameHeight + suffix + ".png");
                File.WriteAllBytes(file, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                Debug.Log("[UiCanvas] Preview written: " + file);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
#endif
