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
 // Historical JSON without new fields must retain the original confirmation policy.
 var oldPath=Path.Combine(dir,"old.json");
 File.WriteAllText(oldPath,"[{\"Name\":\"Старая\",\"Phrases\":[\"старая команда\"],\"Enabled\":true,\"Actions\":[{\"Type\":\"url\",\"Target\":\"https://example.org/\"}]}]");
 var legacy=CustomCommands.Load(oldPath)[0]; Check(legacy.Confirm && !legacy.ScheduleEnabled && legacy.OptionalPhrases.Count==0 && Guid.TryParseExact(legacy.Id,"N",out _),"legacy data defaults to safe settings");
 CustomCommands.Save(oldPath,new[]{legacy}); Check(CustomCommands.Load(oldPath)[0].Id==legacy.Id,"legacy command id persists after save");
 var advanced=CustomCommands.Validate(new[]{command with {OptionalPhrases=new(){"пожалуйста"}, ScheduleEnabled=true, ScheduleDays="weekdays", ScheduleTime="09:30", Actions=new(){u,new("delay","5"),new("search","новости"),new("cmd","echo test"),new("powershell","Write-Output test")}}})[0];
 Check(CustomCommands.Find(new[]{advanced},"пожалуйста игра есть вопросы")!=null && CustomCommands.Find(new[]{advanced},"игра есть вопросы пожалуйста")!=null,"optional phrase before or after exact activation");
 Check(CustomCommands.Find(new[]{advanced},"пожалуйста игра есть вопросы другое")==null,"no unconfigured extras");
 Reject(()=>CustomCommands.Validate(new[]{advanced, command with {Name="Другая", Phrases=new(){"пожалуйста игра есть вопросы"}}}),"optional variant collision");
 var openItems=CustomCommands.Validate(new[]{command with {Actions=new(){new("file",Path.Combine(dir,"document.txt")),new("folder",dir),new("sound",Path.Combine(dir,"tone.wav"))}}})[0];
 Check(openItems.Actions.Select(x=>x.Type).SequenceEqual(new[]{"file","folder","sound"}),"local file, folder, and sound actions validated");
 Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("sound",Path.Combine(dir,"bad.exe"))}}}),"unsupported sound rejected");
 Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("delay","0")}}}),"invalid pause rejected");
 Reject(()=>CustomCommands.Validate(new[]{command with {Actions=new(){new("cmd","echo ok\necho other")}}}),"multiline command rejected");
 Reject(()=>CustomCommands.Validate(new[]{command with {ScheduleTime="25:00"}}),"invalid schedule time");
 Reject(()=>CustomCommands.Validate(new[]{command with {ScheduleDays="never"}}),"invalid schedule days");
 var monday=new DateTime(2026,11,23,9,30,10); var sunday=new DateTime(2026,11,22,9,30,10);
 Check(CustomCommands.IsDue(advanced,monday,null) && !CustomCommands.IsDue(advanced,sunday,null),"weekday schedule");
 Check(!CustomCommands.IsDue(advanced,monday.AddMinutes(1),null) && !CustomCommands.IsDue(advanced,monday,"2026-11-23"),"exact minute and once per day");
 var missingIdPath=Path.Combine(dir,"missing-id.json");
 File.WriteAllText(missingIdPath,"[{\"Name\":\"Scheduled\",\"Phrases\":[\"unique phrase\"],\"Enabled\":true,\"ScheduleEnabled\":true,\"Actions\":[{\"Type\":\"url\",\"Target\":\"https://example.org/\"}]}]");
 Reject(()=>CustomCommands.Load(missingIdPath),"scheduled command without stable id is not loaded");
 var schedulePath=Path.Combine(dir,"schedule.json"); var tracker=new CustomScheduleTracker(schedulePath); Check(tracker.Due(advanced,monday),"schedule initially due"); tracker.Mark(advanced,monday);
 Check(!new CustomScheduleTracker(schedulePath).Due(advanced,monday),"schedule deduplicates after restart");
 File.WriteAllText(schedulePath,"{broken"); Reject(()=>new CustomScheduleTracker(schedulePath),"corrupt schedule is not overwritten"); Check(File.ReadAllText(schedulePath)=="{broken","corrupt schedule preserved");
 var sequence=new List<string>(); await CustomCommandExecutor.RunAsync(new[]{u,new CustomAction("delay","5")},(step,ct)=>{sequence.Add(step.Type);return Task.CompletedTask;},CancellationToken.None); Check(sequence.SequenceEqual(new[]{"url","delay"}),"actions run in order");
 sequence.Clear(); try { await CustomCommandExecutor.RunAsync(new[]{u,new CustomAction("delay","5")},(step,ct)=>{sequence.Add(step.Type);throw new InvalidOperationException();},CancellationToken.None); } catch (InvalidOperationException) {} Check(sequence.SequenceEqual(new[]{"url"}),"failure stops later actions");
 using(var cts=new CancellationTokenSource()) { cts.Cancel(); try { await CustomCommandExecutor.RunAsync(new[]{u},(step,ct)=>throw new Exception("unexpected"),cts.Token); throw new Exception("cancel failed"); } catch (OperationCanceledException) { Console.WriteLine("OK: cancellation stops chain"); } }
 File.WriteAllText(path,"{broken");Reject(()=>CustomCommands.Save(path,new[]{command}),"corrupt file not overwritten");Check(File.ReadAllText(path)=="{broken","preserve corrupt data");
} finally { Directory.Delete(dir,true); }
