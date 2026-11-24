using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
namespace CacheTrim;
internal static class NetworkDiagnostics
{
    internal static string ValidateTarget(string input)
    {
        string target = (input ?? "").Trim();
        if (target.Length == 0 || target.Length > 253 || Uri.CheckHostName(target) == UriHostNameType.Unknown || target.Any(char.IsWhiteSpace))
            throw new ArgumentException("Укажи имя хоста или IP-адрес без http://, порта и пути.");
        return target;
    }
    internal static async Task<string> RunAsync(string input, CancellationToken token)
    {
        string target = ValidateTarget(input);
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(target).WaitAsync(TimeSpan.FromSeconds(8), token);
        if (addresses.Length == 0) return "DNS не вернул адресов.";
        IPAddress chosen = addresses[0]; var lines = new System.Collections.Generic.List<string> { "Адрес для проверки: " + chosen };
        int success = 0; long sum = 0;
        for (int i = 0; i < 4; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var ping = new Ping(); var reply = await ping.SendPingAsync(chosen, 1500).WaitAsync(token);
                if (reply.Status == IPStatus.Success) { success++; sum += reply.RoundtripTime; lines.Add((i + 1) + ": " + reply.RoundtripTime + " мс"); }
                else lines.Add((i + 1) + ": " + reply.Status);
            }
            catch (PingException ex) { lines.Add((i + 1) + ": ошибка — " + ex.Message); }
        }
        lines.Add("Ответов: " + success + " из 4; потеря в этой короткой проверке: " + ((4 - success) * 25) + "%.");
        if (success > 0) lines.Add("Средняя задержка по ответам: " + (sum / (double)success).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " мс.");
        lines.Add("Отсутствие ответа не обязательно означает проблему: узел или сеть могут блокировать ICMP. Настройки сети не изменялись.");
        return string.Join(Environment.NewLine, lines);
    }
}
