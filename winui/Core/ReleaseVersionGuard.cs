using System;

namespace CacheTrim
{
    internal static class ReleaseVersionGuard
    {
        internal static bool Matches(string releaseVersion, string binaryVersion)
        {
            Version expected;
            Version actual;
            if (!Version.TryParse(releaseVersion, out expected) || !Version.TryParse(binaryVersion, out actual))
                return false;

            return expected.Major == actual.Major && expected.Minor == actual.Minor &&
                (expected.Build < 0 || expected.Build == actual.Build) &&
                (expected.Revision < 0 || expected.Revision == actual.Revision);
        }
    }
}
