using DesktopPet;
static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("OK: " + name); }
var now = DateTime.UtcNow;
Check(!PetPolicy.ReminderDue(0, now.AddMinutes(-1), now, false, false), "disabled reminders stay off");
Check(!PetPolicy.ReminderDue(30, now.AddMinutes(1), now, false, false), "reminder waits until due");
Check(PetPolicy.ReminderDue(30, now, now, false, false), "due reminder appears");
Check(!PetPolicy.ReminderDue(30, now.AddMinutes(-1), now, true, false), "quiet mode postpones reminder");
Check(!PetPolicy.ReminderDue(30, now.AddMinutes(-1), now, false, true), "open menu postpones reminder");
Check(PetPolicy.CanChangeAutostart(null, "own"), "missing startup entry can be created");
Check(PetPolicy.CanChangeAutostart("OWN", "own"), "own startup entry can be removed");
Check(!PetPolicy.CanChangeAutostart("other", "own"), "modified startup entry is not touched");
