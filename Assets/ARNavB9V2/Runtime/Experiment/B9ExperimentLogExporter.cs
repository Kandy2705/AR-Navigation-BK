using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ARNavB9V2.Experiment
{
    /// <summary>
    /// Copies the active three-file research bundle to a dedicated folder under
    /// persistentDataPath. On iOS this Documents folder is exposed through Files
    /// because the project postprocessor enables iTunes file sharing.
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
        }

        private bool Fail(string message)
        {
            LastMessage = message;
            return false;
        }
    }
}
