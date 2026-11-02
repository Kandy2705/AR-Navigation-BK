using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ARNavB9V2.Experiment
{
    /// <summary>
    /// Copies the active three-file research bundle to a dedicated folder.
    /// On Android, the export is written through MediaStore to the public
    /// Download folder so Samsung My Files can access it without Android/data.
    /// On iOS, persistentDataPath remains exposed through Files.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class B9ExperimentLogExporter : MonoBehaviour
    {
        [SerializeField] private B9ExperimentLogger logger;

        public string LastExportDirectory { get; private set; } = string.Empty;
        public string LastMessage { get; private set; } = string.Empty;

        public void Configure(B9ExperimentLogger experimentLogger)
        {
            logger = experimentLogger;
        }

        public bool ExportLatestBundle()
        {
            if (logger == null)
                return Fail("Chưa kết nối bộ ghi log.");

            logger.FlushNow();
            string[] sourcePaths =
            {
                logger.EventsFilePath,
                logger.SamplesFilePath,
                logger.SummaryFilePath,
            };
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sourcePaths[i]) || !File.Exists(sourcePaths[i]))
                    return Fail("Chưa đủ 3 file log để xuất.");
            }

            string session = string.IsNullOrWhiteSpace(logger.SessionId)
                ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                : logger.SessionId;
            string directory = Path.Combine(
                Application.persistentDataPath,
                "SharedLogs",
                session);
            Directory.CreateDirectory(directory);

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!ExportToPublicAndroidDownloads(sourcePaths, session, out string androidExportError))
                return Fail("Android export lỗi: " + androidExportError + ". Bản gốc vẫn ở Android/data.");

            LastExportDirectory = "Download/ARNavigation/HarmonyLogs/" + session;
            LastMessage = "Đã xuất đủ 3 CSV · Download/ARNavigation/HarmonyLogs/" + session;
            return true;
#else
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                string destination = Path.Combine(directory, Path.GetFileName(sourcePaths[i]));
                File.Copy(sourcePaths[i], destination, true);
            }

            File.WriteAllText(
                Path.Combine(directory, "README.txt"),
                "B9 Navigation research log bundle\n"
                + "Session: " + session + "\n"
                + "Files: events, samples, summary\n"
                + "Exported UTC: " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n",
                new UTF8Encoding(true));
            LastExportDirectory = directory;
            LastMessage = "Đã xuất đủ 3 CSV · SharedLogs/" + session;
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool ExportToPublicAndroidDownloads(
            string[] sourcePaths,
            string session,
            out string error)
        {
            error = string.Empty;
            AndroidJavaObject resolver = null;
            AndroidJavaObject collection = null;
            try
            {
                AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION");
                int sdkInt = version.GetStatic<int>("SDK_INT");

                // Android 10+ supports scoped-storage-safe public Downloads writes.
                if (sdkInt < 29)
                {
                    bool legacySuccess = ExportToLegacyAndroidDownloads(sourcePaths, session, out error);
                    return legacySuccess;
                }

                AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
                resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                collection = new AndroidJavaClass("android.provider.MediaStore$Downloads")
                    .GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

                string relativePath = "Download/ARNavigation/HarmonyLogs/" + session + "/";
                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    if (!WriteToMediaStore(resolver, collection, sourcePaths[i], relativePath, out error))
                        return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[HarmonyLogs] Android public export failed: " + exception);
                error = exception.GetBaseException().Message;
                return false;
            }
            finally
            {
                collection?.Dispose();
                resolver?.Dispose();
            }
        }

        private static bool WriteToMediaStore(
            AndroidJavaObject resolver,
            AndroidJavaObject collection,
            string sourcePath,
            string relativePath,
            out string error)
        {
            error = string.Empty;
            AndroidJavaObject values = null;
            AndroidJavaObject uri = null;
            AndroidJavaObject output = null;
            AndroidJavaObject pendingValue = null;
            try
            {
                string fileName = Path.GetFileName(sourcePath);
                values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "text/csv");
                values.Call("put", "relative_path", relativePath);
                pendingValue = new AndroidJavaObject("java.lang.Integer", 1);
                values.Call("put", "is_pending", pendingValue);

                uri = resolver.Call<AndroidJavaObject>("insert", collection, values);
                if (uri == null)
                {
                    error = "MediaStore.insert trả về rỗng cho " + fileName;
                    return false;
                }

                output = resolver.Call<AndroidJavaObject>("openOutputStream", uri);
                if (output == null)
                {
                    error = "không mở được output stream cho " + fileName;
                    return false;
                }

                output.Call("write", File.ReadAllBytes(sourcePath));
                output.Call("flush");
                output.Call("close");
                output.Dispose();
                output = null;

                values.Call("clear");
                pendingValue.Dispose();
                pendingValue = new AndroidJavaObject("java.lang.Integer", 0);
                values.Call("put", "is_pending", pendingValue);
                resolver.Call<int>("update", uri, values, null, null);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[HarmonyLogs] Cannot export " + sourcePath + ": " + exception);
                error = exception.GetBaseException().Message;
                if (uri != null)
                    resolver.Call<int>("delete", uri, null, null);
                return false;
            }
            finally
            {
                output?.Dispose();
                pendingValue?.Dispose();
                values?.Dispose();
                uri?.Dispose();
            }
        }

        private static bool ExportToLegacyAndroidDownloads(
            string[] sourcePaths,
            string session,
            out string error)
        {
            error = string.Empty;
            string directory = Path.Combine(
                "/storage/emulated/0/Download/ARNavigation/HarmonyLogs",
                session);
            try
            {
                Directory.CreateDirectory(directory);
                for (int i = 0; i < sourcePaths.Length; i++)
                {
                    string destination = Path.Combine(directory, Path.GetFileName(sourcePaths[i]));
                    File.Copy(sourcePaths[i], destination, true);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[HarmonyLogs] Legacy Android public export failed: " + exception);
                error = exception.GetBaseException().Message;
                return false;
            }
        }
#endif

        private bool Fail(string message)
        {
            LastMessage = message;
            return false;
        }
    }
}
