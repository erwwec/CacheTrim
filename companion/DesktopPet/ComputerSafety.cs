using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopPet;

// A narrow heuristic is a second layer, not a security boundary or a guarantee.
// A click may have side effects even when its description looks harmless.
internal static class ComputerSafety
{
    private static readonly string[] Sensitive = {
        "delete", "remove file", "erase", "uninstall", "format disk", "overwrite", "publish", "post message", "send message", "send email", "send mail", "share", "payment", "purchase", "transfer money", "password", "verification code", "recovery code", "powershell", "cmd.exe", "terminal", "shell command",
        "удал", "деинсталл", "форматирован", "перезапис", "отправ", "оплат", "платеж", "платёж", "покуп", "перевод средств", "поделить", "публикац", "опубликов", "парол", "код подтверж", "код восстанов", "терминал", "командн" };
    internal static bool NeedsReview(ComputerStep step)
    {
        if (step.Action == "key" && (step.Key == "DELETE" || step.Key == "CTRL+V")) return true;
        string text = step.Reason + " " + step.Text;
        foreach (string token in Sensitive)
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    internal static Task WaitBeforeAutomaticAction(CancellationToken ct) => Task.Delay(TimeSpan.FromSeconds(3), ct);
}
