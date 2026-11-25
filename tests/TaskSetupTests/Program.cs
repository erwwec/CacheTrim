using CacheTrim;
using System.Xml.Linq;
static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
string executable = @"C:\Some & Folder\CacheTrim.exe", user = @"PC\Name & Other";
var doc = XDocument.Parse(TaskSetup.BuildIdleXml(executable, new TimeSpan(13, 45, 0), user, new DateTime(2026, 11, 24)));
var root = doc.Root!; var settings = root.Element(ns + "Settings")!;
Check(root.Descendants(ns + "CalendarTrigger").Single().Element(ns + "StartBoundary")!.Value == "2026-11-24T13:45:00", "daily start time");
Check(root.Descendants(ns + "DaysInterval").Single().Value == "1", "daily trigger");
Check(root.Descendants(ns + "LogonTrigger").Count() == 0, "no unintended logon trigger");
Check(settings.Element(ns + "RunOnlyIfIdle")!.Value == "true" && settings.Element(ns + "DisallowStartIfOnBatteries")!.Value == "true", "idle and AC required");
Check(settings.Element(ns + "IdleSettings")!.Element(ns + "StopOnIdleEnd")!.Value == "false" && settings.Element(ns + "ExecutionTimeLimit")!.Value == "PT0S", "no forced termination on activity or timeout");
var logon = XDocument.Parse(TaskSetup.BuildXmlForUser(executable, 20, user));
Check(logon.Root!.Element(ns + "Settings")!.Element(ns + "ExecutionTimeLimit")!.Value == "PT0S", "logon cleanup has no forced execution timeout");
Check(root.Descendants(ns + "Command").Single().Value == executable && root.Descendants(ns + "UserId").Single().Value == user, "XML escapes user and executable");
Check(root.Descendants(ns + "Arguments").Single().Value == "--silent", "silent command");
bool invalid = false; try { TaskSetup.BuildIdleXml(executable, TimeSpan.FromDays(1), user, DateTime.Today); } catch (ArgumentOutOfRangeException) { invalid = true; }
Check(invalid, "invalid times rejected");
