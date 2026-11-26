using CacheTrim;
string dir=Path.Combine(Path.GetTempPath(),"ct-capture-"+Guid.NewGuid());Directory.CreateDirectory(dir);
try {
 string file=Path.Combine(dir,"test.csv");
 File.WriteAllText(file,"SwapChainAddress,MsBetweenPresents,Application\na,10,game\na,20,game\na,20,game\nb,1,other\na,NA,game\n");
 var text=GameCapture.Summarize(file); if (!text.Contains("60.0 кадров/с") || !text.Contains("20.0 мс") || !text.Contains("Измерений: 3")) throw new Exception(text);
 if (GameCapture.Csv("a,\"b,c\",\"d\"\"e\"")[1] != "b,c") throw new Exception("CSV quoted field");
 var captureArgs=GameCapture.Arguments(100,30,5,file,Guid.NewGuid().ToString("N")); if (!captureArgs.Contains("--terminate_after_timed")) throw new Exception("missing timed exit");
 try { GameCapture.Arguments(100,9999,5,file,Guid.NewGuid().ToString("N")); throw new Exception("duration not validated"); } catch (ArgumentException) { }
 Console.WriteLine("OK: FPS calculations, CSV fields and capture argument bounds");
} finally { Directory.Delete(dir,true); }
