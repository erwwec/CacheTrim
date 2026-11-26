using CacheTrim;
ProcessAffinity.ValidateMask(3,15);
foreach (var m in new ulong[] {0,16,32}) { try { ProcessAffinity.ValidateMask(m,15); throw new Exception("bad mask accepted"); } catch(ArgumentException) {} }
string dir=Path.Combine(Path.GetTempPath(),"ct-affinity-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
try { string path=Path.Combine(dir,"backups.json"); var e=new ProcessManager.Entry(123,"game",Path.Combine(dir,"game.exe"),1234,1,0,"Normal");
 ProcessAffinity.SaveOriginal(path,e,15); ProcessAffinity.SaveOriginal(path,e,3);
 if(ProcessAffinity.Load(path).Single().OriginalMask!=15) throw new Exception("original overwritten");
 if(ProcessAffinity.Matches(ProcessAffinity.Load(path).Single(),e with { StartUtcTicks=1235 })) throw new Exception("PID reuse accepted");
 File.WriteAllText(path,"broken"); try { ProcessAffinity.SaveOriginal(path,e,3); throw new Exception("corrupt data overwritten"); } catch(System.Text.Json.JsonException) { }
 if(File.ReadAllText(path)!="broken") throw new Exception("corrupt data lost");
 Console.WriteLine("OK: mask validation and process-specific original backup");
} finally { Directory.Delete(dir,true); }
