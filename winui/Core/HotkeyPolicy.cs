namespace CacheTrim;

internal static class HotkeyPolicy
{
    internal static bool Valid(int open, int pet, int anime) =>
        open is >= 0 and <= 6 && pet is >= 0 and <= 6 && anime is >= 0 and <= 6 &&
        (open == 0 || (open != pet && open != anime)) && (pet == 0 || pet != anime);
}
