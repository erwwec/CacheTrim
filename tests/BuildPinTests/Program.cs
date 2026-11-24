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
