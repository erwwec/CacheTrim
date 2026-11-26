using CacheTrim;
string dir = Path.Combine(Path.GetTempPath(), "ct-games-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
try {
 string path = Path.Combine(dir, "games.json"), exe = Path.Combine(dir, "game.exe");
 var g = new GameLibrary.Game("Example", exe); GameLibrary.Save(path, new[] { g });
 if (GameLibrary.Load(path).Single() != g) throw new Exception("roundtrip failed");
 try { GameLibrary.Save(path, new[] { g, g }); throw new Exception("duplicate accepted"); } catch (InvalidDataException) { }
 try { GameLibrary.Save(path, new[] { new GameLibrary.Game("bad", "relative.exe") }); throw new Exception("relative path accepted"); } catch (InvalidDataException) { }
 File.WriteAllText(path, "broken");
 try { GameLibrary.Save(path, new[] { g }); throw new Exception("corrupt file overwritten"); } catch (InvalidDataException) { }
 if (File.ReadAllText(path) != "broken") throw new Exception("corrupt data lost");
 Console.WriteLine("OK: game list, validation, and fail-closed persistence");
} finally { Directory.Delete(dir, true); }
