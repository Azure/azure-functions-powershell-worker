using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public static class FixtureHelpers
    {
        public static Process GetFuncHostProcess(bool enableAuth = false)
        {
            var funcHostProcess = new Process();
            var rootDir = Path.GetFullPath(String.Format(@"..{0}..{0}..{0}..{0}..{0}..{0}..{0}", Path.DirectorySeparatorChar));
            var funcName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "func.exe": "func";

            funcHostProcess.StartInfo.UseShellExecute = false;
            funcHostProcess.StartInfo.RedirectStandardError = true;
            funcHostProcess.StartInfo.RedirectStandardOutput = true;
            funcHostProcess.StartInfo.CreateNoWindow = true;
            funcHostProcess.StartInfo.WorkingDirectory = Path.Combine(rootDir, String.Format(@"test{0}E2E{0}TestFunctionApp", Path.DirectorySeparatorChar));
            funcHostProcess.StartInfo.FileName = Path.Combine(rootDir, "test", "E2E", "Azure.Functions.Cli", funcName);
            funcHostProcess.StartInfo.ArgumentList.Add("start");
            if (enableAuth)
            {
                funcHostProcess.StartInfo.ArgumentList.Add("--enableAuth");
            }

            return funcHostProcess;
        }

        public static void StartProcessWithLogging(Process funcProcess)
        {
            funcProcess.ErrorDataReceived += (sender, e) => Console.WriteLine(e?.Data);
            funcProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e?.Data);

            funcProcess.Start();

            funcProcess.BeginErrorReadLine();
            funcProcess.BeginOutputReadLine();
        }

        public static void KillExistingFuncHosts()
        {
            foreach (var func in Process.GetProcessesByName("func"))
            {
                func.Kill();
            }
        }
    }
}
