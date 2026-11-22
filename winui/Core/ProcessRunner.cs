using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CacheTrim
{
    internal sealed class ProcessResult
    {
        public int ExitCode { get; private set; }
        public string Output { get; private set; }
        public bool TimedOut { get; private set; }

        public ProcessResult(int exitCode, string output, bool timedOut)
        {
            ExitCode = exitCode;
            Output = output;
            TimedOut = timedOut;
        }
    }

    /// <summary>Runs a system command without blocking on one redirected output stream.</summary>
    internal static class ProcessRunner
    {
        public static ProcessResult Run(string executable, string arguments, int timeoutMilliseconds)
        {
            ProcessStartInfo info = new ProcessStartInfo(executable, arguments);
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            using (Process process = Process.Start(info))
            {
                if (process == null)
                    throw new InvalidOperationException("Не удалось запустить " + executable);

                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                bool timedOut = !process.WaitForExit(timeoutMilliseconds);
                if (timedOut)
                {
                    try { process.Kill(true); }
                    catch (Exception) { }
                    process.WaitForExit();
                }

                try { Task.WaitAll(stdout, stderr); }
                catch (AggregateException) { }

                string output = (stdout.IsCompletedSuccessfully ? stdout.Result : string.Empty) +
                    Environment.NewLine + (stderr.IsCompletedSuccessfully ? stderr.Result : string.Empty);
                if (timedOut)
                    output = (output + Environment.NewLine + "Превышено время ожидания команды.").Trim();
                else
                    output = output.Trim();

                return new ProcessResult(timedOut ? -1 : process.ExitCode, output, timedOut);
            }
        }
    }
}
