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
Check(PetPolicy.NearestSize(0.5) == 0.5, "50 percent size");
Check(PetPolicy.NearestSize(0.6) == 0.5, "old saved size rounds to nearest option");
Check(PetPolicy.NearestSize(0.75) == 1.0, "size midpoint rounds up");
Check(PetPolicy.NearestSize(1.2) == 1.0, "100 percent size");
Check(PetPolicy.NearestSize(1.5) == 1.5, "150 percent size");
Check(PetPolicy.NearestSize(2.0) == 1.5, "old 200 percent size capped at 150");
Check(PetPolicy.FeetBelowWorkArea(800, 80, 840), "feet below a higher monitor taskbar must be recovered");
Check(!PetPolicy.FeetBelowWorkArea(760, 80, 840), "feet on work area edge are not below taskbar");
