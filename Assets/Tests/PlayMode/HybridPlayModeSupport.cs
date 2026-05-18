using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TestAR.Tests.PlayMode
{
    /// <summary>
    /// Helper dùng chung cho Play-Mode tests trên scene Hybrid Navigation.
    /// </summary>
    public static class HybridPlayModeSupport
    {
        public const string SceneName = "Hybrid Navigation";

        // ── Scene loading ──────────────────────────────────────────────────────

        /// <summary>
        /// Tải scene "Hybrid Navigation" và chờ đủ thời gian để tất cả MonoBehaviour
        /// (NavigationManager, AR Foundation, GPSMarker…) hoàn tất Awake/OnEnable/Start.
        /// </summary>
        public static IEnumerator LoadHybridScene()
        {
            // yield return AsyncOperation tự động chờ đến khi isDone == true
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            // Cho 3 frame để Awake/OnEnable/Start hoàn tất trên tất cả MonoBehaviour
            yield return null;
            yield return null;
            yield return null;
            // Thêm 0.5 giây thực để NavigationManager kịp điều hướng đến firstPage
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ── Scene helpers ───────────────────────────────────────────────────────

        public static string ActiveSceneName =>
            SceneManager.GetActiveScene().name;

        // ── UIDocument helpers (không cần reflection) ───────────────────────────

        /// <summary>
        /// Tìm UIDocument đầu tiên trong scene – KHÔNG cần reflection.
        /// </summary>
        public static UIDocument FindMainUiDocument()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            return docs.Length > 0 ? docs[0] : null;
        }

        // ── Component search (không cần biết kiểu cụ thể) ─────────────────────

        /// <summary>
        /// Tìm tất cả MonoBehaviour có tên kiểu khớp typeName.
        /// Tránh hoàn toàn việc tra cứu assembly.
        /// </summary>
        public static MonoBehaviour[] FindByTypeName(string typeName)
        {
            return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(mb => mb.GetType().Name == typeName)
                .ToArray();
        }

        // ── Gameplay Type Resolution (multi-strategy, dùng cho Navigate/GetArPage) ──

        private static Assembly _gameplayAsm;

        /// <summary>
        /// Tìm Assembly-CSharp bằng 3 chiến lược lần lượt:
        /// 1. Tên chính xác "Assembly-CSharp"
        /// 2. Anchor class GPSMarker (class không bị trùng với SDK nào)
        /// 3. Quét tất cả assembly, bỏ qua MultiSet/Editor/Unity framework
        /// </summary>
        private static Assembly GameplayAssembly
        {
            get
            {
                if (_gameplayAsm != null) return _gameplayAsm;

                // Chiến lược 1: tên chính xác
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name == "Assembly-CSharp")
                    {
                        _gameplayAsm = a; return _gameplayAsm;
                    }
                }

                // Chiến lược 2: anchor GPSMarker
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = a.GetType("GPSMarker");
                        if (t != null && t.IsPublic)
                        {
                            _gameplayAsm = a; return _gameplayAsm;
                        }
                    }
                    catch { }
                }

                // Chiến lược 3: quét rộng, bỏ MultiSet/Editor/Unity
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var n = a.GetName().Name;
                    if (n.IndexOf("MultiSet",  StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (n.IndexOf("Editor",    StringComparison.Ordinal) >= 0) continue;
                    if (n.StartsWith("Unity.") || n.StartsWith("UnityEngine")
                        || n.StartsWith("System") || n.StartsWith("mscorlib")) continue;
                    try
                    {
                        var t = a.GetType("GPSMarker");
                        if (t != null) { _gameplayAsm = a; return _gameplayAsm; }
                    }
                    catch { }
                }
                return null;
            }
        }

        /// <summary>Tra cứu type từ gameplay assembly, bỏ qua MultiSet SDK.</summary>
        public static Type ResolveGameplayType(string typeName)
        {
            var asm = GameplayAssembly;
            if (asm == null) return null;

            var direct = asm.GetType(typeName);
            if (direct != null) return direct;
            try
            {
                return asm.GetExportedTypes().FirstOrDefault(t => t.Name == typeName);
            }
            catch { return null; }
        }

        /// <summary>Tên assembly gameplay – dùng để debug trong test messages.</summary>
        public static string GameplayAssemblyName =>
            GameplayAssembly?.GetName().Name ?? "(không tìm thấy)";

        // ── NavigationManager helpers ──────────────────────────────────────────

        /// <summary>
        /// Gọi NavigationManager.Navigate(pageId) thông qua reflection trên component.
        /// Không cần biết trước type của NavigationManager.
        /// </summary>
        public static bool Navigate(string pageIdName)
        {
            var nmType = ResolveGameplayType("NavigationManager");
            if (nmType == null) return false;

            var nms = UnityEngine.Object.FindObjectsByType(
                nmType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (nms == null || nms.Length == 0) return false;

            var nm = nms[0];
            var pageIdType = ResolveGameplayType("PageID");
            if (pageIdType == null) return false;

            object pageIdVal;
            try { pageIdVal = Enum.Parse(pageIdType, pageIdName); }
            catch { return false; }

            var method = nmType.GetMethod("Navigate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { pageIdType, typeof(bool) }, null);

            method?.Invoke(nm, new object[] { pageIdVal, false });
            return method != null;
        }

        /// <summary>
        /// Đọc field ARPageObject từ NavigationManager qua string-based GetComponent.
        /// </summary>
        public static GameObject GetArPageObject()
        {
            var nmType = ResolveGameplayType("NavigationManager");
            if (nmType == null) return null;

            var nms = UnityEngine.Object.FindObjectsByType(
                nmType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (nms == null || nms.Length == 0) return null;

            var field = nmType.GetField("ARPageObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(nms[0]) as GameObject;
        }
    }
}
