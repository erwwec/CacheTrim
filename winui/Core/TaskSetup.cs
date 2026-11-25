using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using System.Globalization;

namespace CacheTrim
{
    /// <summary>
    /// Registers/unregisters the logon task through schtasks + an XML definition.
    /// HighestAvailable means the task runs elevated without a UAC prompt.
    /// </summary>
    internal static class TaskSetup
    {
        public const string TaskName = "CacheTrim Autostart";
        public const string IdleTaskName = "CacheTrim Idle Cleanup";

        public static bool IsInstalled()
        {
            return Run(string.Format("/Query /TN \"{0}\"", TaskName)) == 0;
        }

        public static string Install(string exePath, int delaySeconds)
        {
            string xmlPath = null;
            try
            {
                xmlPath = Path.Combine(Path.GetTempPath(), "CacheTrimTask-" + Guid.NewGuid().ToString("N") + ".xml");
                File.WriteAllText(xmlPath, BuildXml(exePath, delaySeconds), Encoding.Unicode);

                int code = Run("/Create /XML \"" + xmlPath + "\" /TN \"" + TaskName + "\" /F");

                if (code != 0)
                    return "schtasks вернул код " + code;

                return IsInstalled() ? null : "задача создана, но не найдена при проверке";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                if (xmlPath != null) TryDelete(xmlPath);
            }
        }

        public static string Uninstall()
        {
            try
            {
                int code = Run("/Delete /TN \"" + TaskName + "\" /F");
                if (code == 0)
                    return null;

                return IsInstalled() ? "schtasks вернул код " + code : null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string InstallIdle(string exePath, TimeSpan time)
        {
            string xmlPath = null;
            try
            {
                xmlPath = Path.Combine(Path.GetTempPath(), "CacheTrimIdle-" + Guid.NewGuid().ToString("N") + ".xml");
                string user = WindowsIdentity.GetCurrent().Name;
                File.WriteAllText(xmlPath, BuildIdleXml(exePath, time, user, DateTime.Now.Date), Encoding.Unicode);
                int code = Run("/Create /XML \"" + xmlPath + "\" /TN \"" + IdleTaskName + "\" /F");
                if (code != 0) return "schtasks вернул код " + code;
                return IsIdleInstalled() ? null : "задача создана, но не найдена при проверке";
            }
            catch (Exception ex) { return ex.Message; }
            finally { if (xmlPath != null) TryDelete(xmlPath); }
        }

        public static bool IsIdleInstalled() => Run("/Query /TN \"" + IdleTaskName + "\"") == 0;
        public static string UninstallIdle()
        {
            try { int code = Run("/Delete /TN \"" + IdleTaskName + "\" /F"); return code == 0 || !IsIdleInstalled() ? null : "schtasks вернул код " + code; }
            catch (Exception ex) { return ex.Message; }
        }

        internal static string BuildIdleXml(string exePath, TimeSpan time, string user, DateTime day)
        {
            if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(time));
            XDocument doc = XDocument.Parse(BuildXmlForUser(exePath, 0, user));
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            var root = doc.Root;
            root.Element(ns + "RegistrationInfo").Element(ns + "Description").Value = "CacheTrim: ежедневная очистка при простое и питании от сети";
            root.Element(ns + "RegistrationInfo").Element(ns + "URI").Value = "\\" + IdleTaskName;
            root.Element(ns + "Triggers").ReplaceNodes(new XElement(ns + "CalendarTrigger",
                new XElement(ns + "StartBoundary", day.Date.Add(time).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
                new XElement(ns + "Enabled", "true"), new XElement(ns + "ScheduleByDay", new XElement(ns + "DaysInterval", "1"))));
            var settings = root.Element(ns + "Settings");
            settings.Element(ns + "RunOnlyIfIdle").Value = "true";
            settings.Element(ns + "DisallowStartIfOnBatteries").Value = "true";
            // Never terminate a cleaner mid-operation (e.g. while a Windows service is stopped).
            settings.Element(ns + "ExecutionTimeLimit").Value = "PT0S";
            settings.Element(ns + "IdleSettings").Element(ns + "StopOnIdleEnd").Value = "false";
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            doc.Save(writer); return writer.ToString();
        }

        private static string BuildXml(string exePath, int delaySeconds) => BuildXmlForUser(exePath, delaySeconds, WindowsIdentity.GetCurrent().Name);
        internal static string BuildXmlForUser(string exePath, int delaySeconds, string user)
        {
            string delay = "PT" + Math.Max(0, delaySeconds) + "S";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-16\"?>");
            sb.AppendLine("<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">");
            sb.AppendLine("  <RegistrationInfo>");
            sb.AppendLine("    <Description>CacheTrim: чистка кэша и standby-списка при входе в систему</Description>");
            sb.AppendLine("    <URI>\\" + TaskName + "</URI>");
            sb.AppendLine("  </RegistrationInfo>");
            sb.AppendLine("  <Triggers>");
            sb.AppendLine("    <LogonTrigger>");
            sb.AppendLine("      <Enabled>true</Enabled>");
            sb.AppendLine("      <UserId>" + Escape(user) + "</UserId>");
            sb.AppendLine("      <Delay>" + delay + "</Delay>");
            sb.AppendLine("    </LogonTrigger>");
            sb.AppendLine("  </Triggers>");
            sb.AppendLine("  <Principals>");
            sb.AppendLine("    <Principal id=\"Author\">");
            sb.AppendLine("      <UserId>" + Escape(user) + "</UserId>");
            sb.AppendLine("      <LogonType>InteractiveToken</LogonType>");
            sb.AppendLine("      <RunLevel>HighestAvailable</RunLevel>");
            sb.AppendLine("    </Principal>");
            sb.AppendLine("  </Principals>");
            sb.AppendLine("  <Settings>");
            sb.AppendLine("    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>");
            sb.AppendLine("    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>");
            sb.AppendLine("    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>");
            sb.AppendLine("    <AllowHardTerminate>true</AllowHardTerminate>");
            sb.AppendLine("    <StartWhenAvailable>true</StartWhenAvailable>");
            sb.AppendLine("    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>");
            sb.AppendLine("    <IdleSettings>");
            sb.AppendLine("      <StopOnIdleEnd>false</StopOnIdleEnd>");
            sb.AppendLine("      <RestartOnIdle>false</RestartOnIdle>");
            sb.AppendLine("    </IdleSettings>");
            sb.AppendLine("    <AllowStartOnDemand>true</AllowStartOnDemand>");
            sb.AppendLine("    <Enabled>true</Enabled>");
            sb.AppendLine("    <Hidden>true</Hidden>");
            sb.AppendLine("    <RunOnlyIfIdle>false</RunOnlyIfIdle>");
            sb.AppendLine("    <WakeToRun>false</WakeToRun>");
            // Do not terminate cleanup while Windows Update services are stopped.
            sb.AppendLine("    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>");
            sb.AppendLine("    <Priority>7</Priority>");
            sb.AppendLine("  </Settings>");
            sb.AppendLine("  <Actions Context=\"Author\">");
            sb.AppendLine("    <Exec>");
                sb.AppendLine("      <Command>" + Escape(exePath) + "</Command>");
            sb.AppendLine("      <Arguments>--silent</Arguments>");
            sb.AppendLine("    </Exec>");
            sb.AppendLine("  </Actions>");
            sb.AppendLine("</Task>");
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static int Run(string arguments)
        {
            string schtasks = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
            return ProcessRunner.Run(schtasks, arguments, 20000).ExitCode;
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception) { }
        }
    }
}
