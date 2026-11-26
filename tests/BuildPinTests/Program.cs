using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
if (args.Length != 2) throw new ArgumentException("Assembly path and expected SHA-256 required.");
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var reader = pe.GetMetadataReader();
int matches = 0;
foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
{
    var attr = reader.GetCustomAttribute(handle);
    if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
    var member = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
    if (member.Parent.Kind != HandleKind.TypeReference) continue;
    var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
    if (reader.GetString(type.Namespace) != "System.Reflection" || reader.GetString(type.Name) != "AssemblyMetadataAttribute") continue;
    var blob = reader.GetBlobReader(attr.Value);
    if (blob.ReadUInt16() != 1) continue;
    string key = blob.ReadSerializedString();
    string value = blob.ReadSerializedString();
    if (key != "TrustedDesktopPetSha256") continue;
    if (!string.Equals(value, args[1], StringComparison.OrdinalIgnoreCase)) throw new Exception("Incorrect pet SHA-256 pin.");
    matches++;
}
if (matches != 1) throw new Exception("CacheTrim must have exactly one matching pet pin.");
Console.WriteLine("Built CacheTrim pins the matching Desktop Pet SHA-256.");

// Verify the compiled assembly contains the exact vetted, user-supplied script bytes.
var expectedScripts = new Dictionary<string, string> {
    ["CacheTrim.TweakerScripts.explorer.bat"] = "9C87A647B1AB6EB8C048816C6D1E9B0A4EF293F4F164596E878D4D053471A44D",
    ["CacheTrim.TweakerScripts.win-tweaker.bat"] = "21013C2E014E7558603D0C3E08E9995B4AE34D7C57A8D296EAA3E988F1DACED1"
};
foreach (var handle in reader.ManifestResources) {
    var resource = reader.GetManifestResource(handle);
    string name = reader.GetString(resource.Name);
    if (!expectedScripts.ContainsKey(name)) continue;
    if (!resource.Implementation.IsNil) throw new Exception("Script must be embedded");
    var body = pe.GetSectionData(pe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress + (int)resource.Offset).GetReader();
    int length = body.ReadInt32();
    var bytes = body.ReadBytes(length);
    string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    if (!string.Equals(hash, expectedScripts[name], StringComparison.OrdinalIgnoreCase)) throw new Exception("Incorrect embedded script hash: " + name);
    expectedScripts.Remove(name);
}
if (expectedScripts.Count != 0) throw new Exception("Bundled scripts are missing");
Console.WriteLine("Both embedded third-party scripts match their pinned hashes.");
