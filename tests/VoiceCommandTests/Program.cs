using DesktopPet;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
foreach (var phrase in VoiceCommandCatalog.Phrases) Check(VoiceCommandCatalog.Parse(phrase) != PetVoiceCommand.None, phrase);
Check(VoiceCommandCatalog.Parse("  ПРИВЕТ   КЛОД ") == PetVoiceCommand.Hello, "normalize text");
Check(VoiceCommandCatalog.Parse("выключи пк") == PetVoiceCommand.None, "block power command");
Check(VoiceCommandCatalog.Parse("удали все файлы") == PetVoiceCommand.None, "block deletion");
Check(VoiceCommandCatalog.Parse("открой загрузки && cmd") == PetVoiceCommand.None, "block command injection");
Check(VoiceCommandCatalog.Parse("прыгни", .74f) == PetVoiceCommand.None, "low confidence");
Check(VoiceCommandCatalog.Parse("прыгни", float.NaN) == PetVoiceCommand.None, "invalid confidence");
Check(VoiceCommandCatalog.Parse(null) == PetVoiceCommand.None, "empty input");
Check(EmergencyStopPhrase.Matches("стоп", 0.9f), "stop recognized");
Check(EmergencyStopPhrase.Matches("  ДЖАРВИС   СТОП ", 0.9f), "named stop normalized");
Check(EmergencyStopPhrase.Matches("клод стоп", .35f) && EmergencyStopPhrase.Matches("остановись", .8f), "emergency variants accepted");
Check(!EmergencyStopPhrase.Matches("стоп", .34f), "too low confidence rejected");
Check(!EmergencyStopPhrase.Matches("стоп", float.NaN), "invalid confidence rejected");
Check(!EmergencyStopPhrase.Matches("не стоп", 1f) && !EmergencyStopPhrase.Matches("продолжай", 1f), "unrelated phrases do not trigger");
Check(!EmergencyStopPhrase.Matches(null, 1f), "missing voice does not trigger");

Check(WakeWordPhrase.Matches("джарвис", .9f), "wake word accepted");
Check(WakeWordPhrase.Matches("  ДЖАРВИС  ", .55f), "wake word normalized");
Check(!WakeWordPhrase.Matches("джарвис", .54f), "weak wake recognition rejected");
Check(!WakeWordPhrase.Matches("джарвис", float.NaN), "invalid wake confidence rejected");
Check(!WakeWordPhrase.Matches("не джарвис", 1f) && !WakeWordPhrase.Matches(null, 1f), "unrelated wake phrases rejected");

Check(WakeWordPhrase.MatchesOffline("джарвис") && WakeWordPhrase.MatchesOffline("[unk] джервис"), "offline wake variants recognized");
Check(!WakeWordPhrase.MatchesOffline("[unk] просто голос"), "offline wake unrelated text rejected");
Check(EmergencyStopPhrase.MatchesOffline("[unk] стоп") && EmergencyStopPhrase.MatchesOffline("джарвис остановись"), "offline stop tokens recognized");
Check(!EmergencyStopPhrase.MatchesOffline("неизвестный звук"), "offline stop unrelated text rejected");
