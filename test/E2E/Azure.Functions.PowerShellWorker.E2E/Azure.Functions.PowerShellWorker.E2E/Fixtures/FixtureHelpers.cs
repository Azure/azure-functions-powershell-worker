using System;
using System.Diagnostics;
using System.IO;

namespace Azure.Functions.PowerShell.Tests.E2E
{
    public static class FixtureHelpers
    {
        public static Process GetFuncHostProcess(bool enableAuth = false)
        {
            var funcHostProcess = new Process();
            var rootDir = Path.GetFullPath(@"..\..\..\..\..\..\..\");

            funcHostProcess.StartInfo.UseShellExecute = false;
            funcHostProcess.StartInfo.RedirectStandardError = true;
            funcHostProcess.StartInfo.RedirectStandardOutput = true;
            funcHostProcess.StartInfo.CreateNoWindow = true;
            funcHostProcess.StartInfo.WorkingDirectory = Path.Combine(rootDir, @"test\E2E\TestFunctionApp");
            funcHostProcess.StartInfo.FileName = Path.Combine(rootDir, @"test\E2E\Azure.Functions.Cli\func.exe");
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
