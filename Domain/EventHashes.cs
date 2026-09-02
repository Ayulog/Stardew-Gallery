using System.Security.Cryptography;
using System.Text;

namespace StardewGallery;

internal static class EventHashes
{
    internal static string RootScript(string script) => Sha256(script);

    internal static string RootDefinition(string rawEventKey, string rootScript)
        => Sha256(rawEventKey + '\0' + rootScript);

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
