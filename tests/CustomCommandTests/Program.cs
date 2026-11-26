using DesktopPet;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("OK: " + name); }
void Reject(Action f, string name) { try { f(); throw new Exception("Not rejected: " + name); } catch (InvalidDataException) { Console.WriteLine("OK: " + name); } }
var dir=Path.Combine(Path.GetTempPath(), "ClaudeCustomTest-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
try {
 var path=Path.Combine(dir,"commands.json"); var app=Path.Combine(dir,"game.exe"); var a=new CustomAction("app",app); var u=new CustomAction("url","https://example.org/game");
 var command=new CustomCommand("Игра",new(){"Игра, есть вопросы?"},true,new(){a,u});
 CustomCommands.Save(path,new[]{command});var loaded=CustomCommands.Load(path);Check(loaded.Count==1 && loaded[0].Actions.Count==2,"save and load action chain");
 Check(CustomCommands.Find(loaded,"ИГРА ЕСТЬ ВОПРОСЫ")!=null,"exact normalized matching");Check(CustomCommands.Find(loaded,"игра есть вопросы лишнее")==null,"no fuzzy execution");
 Check(CustomCommands.Find(loaded,"игра есть вопросы",.74f)==null,"reject low voice confidence");
 var supported=CustomCommands.SupportedVoicePhrases(new[]{"открой игру", "открой непонятноеслово"}, w => w is "открой" or "игру");
 Check(supported.SequenceEqual(new[]{"открой игру"}),"skip only unsupported custom phrases for voice");
 Check(CustomCommands.Find(new[]{command with {Enabled=false}},"игра есть вопросы")==null,"disabled commands do not run");
 Reject(()=>CustomCommands.Validate(new[]{command, command with {Name="Другой"}}),"duplicate phrases");
 Reject(()=>CustomCommands.Validate(new[]{command with {Phrases=new(){"прыгни"}}}),"built-in phrase reserved");
 Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("url","javascript:alert(1)")}}}),"reject script links");
 Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("app",app+" --bad")}}}),"reject command arguments");
 if (OperatingSystem.IsWindows()) Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("app","//server/share/game.exe")}}}),"reject UNC paths after normalization");
 File.WriteAllText(path,"{broken");Reject(()=>CustomCommands.Save(path,new[]{command}),"corrupt file not overwritten");Check(File.ReadAllText(path)=="{broken","preserve corrupt data");
} finally { Directory.Delete(dir,true); }
