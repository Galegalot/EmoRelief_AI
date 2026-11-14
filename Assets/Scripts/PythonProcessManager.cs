using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EmoRelief
{
    /// <summary>
    /// Module 2: PythonProcessManager
    /// Automatically launches the Python AI backend in the background on game start
    /// and kills it cleanly on game exit.
    /// </summary>
    public sealed class PythonProcessManager : MonoBehaviour
    {
        private const string HealthEndpoint = "http://127.0.0.1:8000/health";

        private static Process serverProcess;
        private static bool isServerReady;

        public static bool IsServerReady => isServerReady;

        private void Start()
        {
            StartCoroutine(EnsureServerRunning());
        }

        private static string FindServerFolder()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            for (var depth = 0; directory != null && depth < 5; depth++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "PathummaLLM");
                if (File.Exists(Path.Combine(candidate, "server.py"))) return candidate;
            }
            return null;
        }

        private static bool PingHealthSync()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(HealthEndpoint);
                req.Method = "GET";
                req.Timeout = 1500;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    return resp.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private IEnumerator EnsureServerRunning()
        {
            // 1. Check if server is already responding
            var alreadyRunning = false;
            var checkThread = new Thread(() =>
            {
                alreadyRunning = PingHealthSync();
            });
            checkThread.IsBackground = true;
            checkThread.Start();

            var checkDeadline = Time.realtimeSinceStartup + 2f;
            while (checkThread.IsAlive && Time.realtimeSinceStartup < checkDeadline)
            {
                yield return null;
            }

            if (alreadyRunning)
            {
                Debug.Log("[PythonProcessManager] Python server is already running and healthy at " + HealthEndpoint);
                isServerReady = true;
                yield break;
            }

            // 2. Find PathummaLLM next to the project, walking up from the Editor's Assets folder
            //    or from a build's EmoRelief_Data folder (Builds/Windows/EmoRelief_Data -> repo root).
            var workingDir = FindServerFolder();
            if (workingDir == null)
            {
                Debug.LogWarning("[PythonProcessManager] PathummaLLM/server.py not found near " + Application.dataPath + ". Offline tips will be used.");
                yield break;
            }

            // 3. Launch background python process ("python" on PATH, else the Windows "py" launcher)
            foreach (var launcher in new[] { "python", "py" })
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = launcher,
                        Arguments = launcher == "py" ? "-3 server.py" : "server.py",
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    serverProcess = Process.Start(startInfo);
                    Debug.Log("[PythonProcessManager] Started background Python server with '" + launcher + "' from: " + workingDir + " (PID: " + serverProcess?.Id + ")");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PythonProcessManager] Could not start '" + launcher + "': " + ex.Message);
                }
            }
            if (serverProcess == null) yield break;

            // 4. Poll health endpoint until ready (up to 10 seconds)
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                yield return new WaitForSeconds(1.0f);

                var ready = false;
                var pollThread = new Thread(() =>
                {
                    ready = PingHealthSync();
                });
                pollThread.IsBackground = true;
                pollThread.Start();

                var pollDeadline = Time.realtimeSinceStartup + 1.2f;
                while (pollThread.IsAlive && Time.realtimeSinceStartup < pollDeadline)
                {
                    yield return null;
                }

                if (ready)
                {
                    Debug.Log("[PythonProcessManager] Background Python server is now ready (attempt " + attempt + ")");
                    isServerReady = true;
                    yield break;
                }
            }

            Debug.LogWarning("[PythonProcessManager] Python server did not respond to health checks within timeout. Fallback EMO tips will be used if needed.");
        }

        private void OnApplicationQuit()
        {
            KillProcess();
        }

        private void OnDestroy()
        {
            // If in Unity Editor and stopping play mode
            if (Application.isEditor)
            {
                KillProcess();
            }
        }

        public static void KillProcess()
        {
            if (serverProcess != null)
            {
                try
                {
                    if (!serverProcess.HasExited)
                    {
                        Debug.Log("[PythonProcessManager] Terminating Python server process (PID: " + serverProcess.Id + ")...");
                        serverProcess.Kill();
                        serverProcess.WaitForExit(1000);
                    }
                    serverProcess.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PythonProcessManager] Error killing process: " + ex.Message);
                }
                finally
                {
                    serverProcess = null;
                    isServerReady = false;
                }
            }
        }
    }
}
