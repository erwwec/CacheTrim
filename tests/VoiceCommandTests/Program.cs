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
