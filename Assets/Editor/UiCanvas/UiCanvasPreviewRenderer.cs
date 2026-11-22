#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ARNavB9V2.UI;
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

            RenderNavigationHud();
        }

        /// <summary>
        /// The B9 guidance overlay with sample states: on its own (B9 scene alone), under the AR page (app shell),
        /// with the minimap expanded, inside a building, after a failed VPS scan, and with the research panel open.
        /// </summary>
        [MenuItem("Tools/UI Canvas/Render Navigation HUD Previews")]
        public static void RenderNavigationHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UiCanvas] Stop Play mode before rendering previews.");
                return;
            }

            Directory.CreateDirectory(UiCanvasPaths.PreviewFolder);
            string name = UiCanvasNavigationHudBuilder.HudName;
            try
            {
                foreach (float height in new[] { 667f, 812f, 900f })
                {
                    ArNavigationBridge.Reset();
                    Render(name, height, Hud(SampleNavigationModel.Outdoor()), "standalone");
                    Render(name, height, Shell(SampleNavigationModel.Outdoor()), "shell");
                }

                foreach (float height in new[] { 667f, 812f })
                {
                    Render(name, height, Shell(SampleNavigationModel.Expanded()), "expanded");
                    Render(name, height, Shell(SampleNavigationModel.Indoor()), "indoor");
                    Render(name, height, Shell(SampleNavigationModel.VpsFailed()), "vps-failed");
                    Render(name, height, Shell(SampleNavigationModel.Outdoor(), hud => hud.OpenResearch()), "research");
                    Render(name, height, Shell(SampleNavigationModel.Outdoor(), hud => hud.OpenPicker()), "picker");
                }
            }
            finally
            {
                ArNavigationBridge.Reset();
            }
        }

        private static Action<GameObject> Hud(SampleNavigationModel model, Action<NavigationHudUI> extra = null)
        {
            return root =>
            {
                ArNavigationBridge.Reset();
                FitNestedCanvas(root);
                WakeAll(root);
                var hud = root.GetComponent<NavigationHudUI>();
                hud.Bind(model);
                extra?.Invoke(hud);
            };
        }

        /// <summary>The HUD under the AR page, as the app shows it: ArHudUI's buttons on top, the assistant at the bottom.</summary>
        private static Action<GameObject> Shell(SampleNavigationModel model, Action<NavigationHudUI> extra = null)
        {
            return root =>
            {
                ArNavigationBridge.Reset();
                var arPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiCanvasPaths.ScreenPrefab(UiCanvasHomeBuilder.ArHudName));
                var arPage = (GameObject)PrefabUtility.InstantiatePrefab(arPrefab, root.transform.parent);
                WakeAll(arPage);
                arPage.GetComponent<ArHudUI>().Show();
                FitNestedCanvas(root);
                WakeAll(root);
                var hud = root.GetComponent<NavigationHudUI>();
                hud.Bind(model);
                extra?.Invoke(hud);
                // The research panel and picker cover the AR page in the app (the HUD canvas sits under AppCanvas only
                // for touches); draw them over it here too.
                if (extra != null)
                {
                    arPage.transform.SetAsFirstSibling();
                }
            };
        }

        /// <summary>
        /// The HUD prefab's root is a screen-space canvas, whose transform Unity drives (saved with scale 0 in batch mode).
        /// Nested in the preview canvas it has to be stretched over the frame by hand.
        /// </summary>
        public static void FitNestedCanvas(GameObject root)
        {
            var rect = (RectTransform)root.transform;
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localPosition = Vector3.zero;
        }

        private static void WakeAll(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Namespace != null && behaviour.GetType().Namespace.StartsWith("Game."))
                {
                    MethodInfo awake = behaviour.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    awake?.Invoke(behaviour, null);
                }
            }
        }

        /// <summary>Stand-in for the scene's B9NavigationHud: fixed texts, no controllers.</summary>
        private sealed class SampleNavigationModel : IB9NavigationHudModel
        {
            private static readonly string[] Choices =
            {
                "[Trong] B9 · B9-101", "[Trong] B9 · B9-104", "[Trong] B9 · B9-201", "[Trong] B10 · B10-305", "[Ngoài] Thư viện", "[Ngoài] Tòa A4",
            };

            private static readonly string[] Versions = { "V1", "V2", "V3", "V4", "V5", "BQ", "BT", "TF", "V6" };

            public B9NavigationHudState State { get; } = new B9NavigationHudState();
            public IReadOnlyList<string> DestinationChoices => Choices;
            public string SelectedDestination { get; private set; } = Choices[1];
            public IReadOnlyList<string> HarmonyVersionCodes => Versions;
            public Texture MinimapTexture => null;

            public event Action Changed { add { } remove { } }

            public static SampleNavigationModel Outdoor()
            {
                var model = new SampleNavigationModel();
                B9NavigationHudState s = model.State;
                s.Status = "Đi theo mũi tên tới cửa B9";
                s.Detail = "GPS ±4 m · còn 126 m";
                s.Summary = "Đang đi tới cửa B9 trước · đích cuối: B9-104";
                s.HarmonyProfile = "HARMONY V5 · Full HARMONY  Q✓ D✓ M✓ R✓ A–";
                s.ActiveVersionCode = "V5";
                s.Experiment = "LOG · ĐANG GHI 02:14 · 671 mẫu";
                s.ExperimentToggleLabel = B9NavigationHudState.SaveTrialLabel;
                s.StartVisible = true;
                s.CancelVisible = true;
                s.ActiveDestination = "B9-104";
                return model;
            }

            public static SampleNavigationModel Expanded()
            {
                SampleNavigationModel model = Outdoor();
                model.State.MinimapExpanded = true;
                model.State.MinimapHint = B9NavigationHudState.ExpandedMinimapHint;
                return model;
            }

            public static SampleNavigationModel Indoor()
            {
                SampleNavigationModel model = Outdoor();
                B9NavigationHudState s = model.State;
                s.Status = "Đi theo mũi tên tới B9-104";
                s.Detail = "Trong B9 · Tầng 1 · còn 18.4 m · VPS đã căn chỉnh · 212 bước · AR";
                s.Summary = "Đang dẫn đường bên trong tới B9-104";
                s.StartVisible = false;
                s.ExitVisible = true;
                return model;
            }

            public static SampleNavigationModel VpsFailed()
            {
                SampleNavigationModel model = Outdoor();
                B9NavigationHudState s = model.State;
                s.Status = "Quét VPS chưa thành công";
                s.Detail = "Hãy hướng camera quanh sảnh rồi quét lại";
                s.Summary = "Giữ nguyên đích B9-104 · nhấn Quét lại VPS";
                s.StartVisible = false;
                s.RetryVisible = true;
                return model;
            }

            public void SelectDestination(string choice) => SelectedDestination = choice;
            public void StartNavigation() { }
            public void CancelNavigation() { }
            public void ExitBuilding() { }
            public void RetryVps() { }
            public void ToggleMinimap() { }
            public void SelectHarmonyVersion(string versionCode) { }
            public void AddResearchMarker() { }
            public void ToggleTrial() { }
            public void ExportLogs() { }
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
