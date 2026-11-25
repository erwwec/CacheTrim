using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;

namespace DesktopPet;

// Creates an editable PPTX from a prebuilt two-slide template. Never accepts a path from the model.
internal static class PresentationBuilder
{
    public static bool IsValid(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3 ||
            !Text(root, "title", 50) || !Text(root, "subtitle", 100) ||
            !root.TryGetProperty("slides", out var slides) || slides.ValueKind != JsonValueKind.Array ||
            slides.GetArrayLength() < 1 || slides.GetArrayLength() > 6) return false;
        return slides.EnumerateArray().All(s => s.ValueKind == JsonValueKind.Object && s.EnumerateObject().Count() == 3 &&
            Text(s, "title", 50) && Text(s, "left", 145) && Text(s, "right", 145));
    }
    private static bool Text(JsonElement obj, string name, int max)
    {
        if (!obj.TryGetProperty(name, out var val) || val.ValueKind != JsonValueKind.String) return false;
        string s = val.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(s) || s.Length > max || s.Count(c => c == '\n') > 2 || s.Any(c => char.IsControl(c) && c != '\n')) return false;
        try { System.Xml.XmlConvert.VerifyXmlChars(s); } catch (System.Xml.XmlException) { return false; }
        return s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).All(word => word.Length <= 30);
    }
    public static string Create(JsonElement root, string directory)
    {
        if (!IsValid(root)) throw new InvalidOperationException("Неверная структура презентации.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Клод-презентация-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".pptx");
        bool created = false;
        try
        {
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                created = true;
                using var resource = typeof(PresentationBuilder).Assembly.GetManifestResourceStream("DesktopPet.PresentationTemplate") ?? throw new InvalidOperationException("Шаблон презентации отсутствует.");
                using var reader = new StreamReader(resource, Encoding.UTF8);
                var template = Convert.FromBase64String(reader.ReadToEnd());
                output.Write(template); output.Position = 0;
                using var doc = PresentationDocument.Open(output, true);
                var pres = doc.PresentationPart ?? throw new InvalidOperationException("Шаблон повреждён.");
                var ids = pres.Presentation.SlideIdList ?? throw new InvalidOperationException("Слайды отсутствуют.");
                var idsArray = ids.Elements<SlideId>().ToArray();
                if (idsArray.Length != 2) throw new InvalidOperationException("Неверный шаблон презентации.");
                var cover = (SlidePart)pres.GetPartById(idsArray[0].RelationshipId!);
                var original = (SlidePart)pres.GetPartById(idsArray[1].RelationshipId!);
                var layout = original.SlideLayoutPart ?? throw new InvalidOperationException("Разметка отсутствует.");
                var slides = root.GetProperty("slides").EnumerateArray().ToArray();
                var slideParts = new SlidePart[slides.Length]; slideParts[0] = original;
                uint id = idsArray.Max(x => x.Id!.Value);
                // Copy the untouched source part before substituting the first slide.
                for (int i = 1; i < slides.Length; i++)
                {
                    var added = pres.AddNewPart<SlidePart>();
                    using (var source = original.GetStream(FileMode.Open, FileAccess.Read)) added.FeedData(source);
                    added.AddPart(layout);
                    ids.Append(new SlideId { Id = ++id, RelationshipId = pres.GetIdOfPart(added) });
                    slideParts[i] = added;
                }
                Replace(cover, "COVER_TITLE", root.GetProperty("title").GetString()!);
                Replace(cover, "COVER_SUBTITLE", root.GetProperty("subtitle").GetString()!);
                for (int i = 0; i < slides.Length; i++)
                {
                    Replace(slideParts[i], "SLIDE_TITLE", slides[i].GetProperty("title").GetString()!);
                    Replace(slideParts[i], "LEFT_CONTENT", slides[i].GetProperty("left").GetString()!);
                    Replace(slideParts[i], "RIGHT_CONTENT", slides[i].GetProperty("right").GetString()!);
                    slideParts[i].Slide.Save();
                }
                cover.Slide.Save(); pres.Presentation.Save();
            }
            return path;
        }
        catch { if (created) { try { File.Delete(path); } catch { } } throw; }
    }
    private static void Replace(SlidePart part, string placeholder, string value)
    {
        var matching = part.Slide.Descendants<A.Text>().Where(t => t.Text == placeholder).ToArray();
        if (matching.Length != 1) throw new InvalidOperationException("Не найден элемент шаблона.");
        matching[0].Text = value;
    }
}
