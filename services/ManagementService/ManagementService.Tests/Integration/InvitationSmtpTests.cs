using System.Net;
using System.Net.Sockets;
using System.Text;
using ManagementService.Application.Events;
using ManagementService.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ManagementService.Tests.Integration;

public sealed class InvitationSmtpTests
{
    [Fact]
    public async Task Invitation_DeliversThroughAnonymousSmtp_WithConfiguredLocalUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var received = CaptureAsync(listener);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:SmtpHost"] = "127.0.0.1",
            ["Email:SmtpPort"] = ((IPEndPoint)listener.LocalEndpoint).Port.ToString(),
            ["Email:SenderEmail"] = "noreply@collaby.local",
            ["Email:UseTls"] = "false",
            ["Frontend:BaseUrl"] = "http://localhost:8080/"
        }).Build();
        var service = new EmailQueueService(NullLogger<EmailQueueService>.Instance, configuration);
        await service.QueueInvitationEmailAsync(new InvitationEmailEvent
        {
            Email = "tester@collaby.local", WorkspaceName = "Demo", InviterName = "Admin",
            InvitationToken = "demo-token", Role = "Member", ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        var raw = await received.WaitAsync(TimeSpan.FromSeconds(10));
        var body = raw.Split("\r\n\r\n", 2)[1];
        body = raw.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetString(Convert.FromBase64String(body))
            : System.Text.RegularExpressions.Regex.Replace(body.Replace("=\r\n", ""), "=([A-Fa-f0-9]{2})", m => ((char)Convert.ToByte(m.Groups[1].Value, 16)).ToString());
        Assert.Contains("http://localhost:8080/accept-invitation?token=demo-token", body);
        Assert.Contains("tester@collaby.local", raw);
    }

    private static async Task<string> CaptureAsync(TcpListener listener)
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 localhost SMTP");
        var body = new StringBuilder();
        bool data = false;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (data)
            {
                if (line != ".") { body.Append(line).Append("\r\n"); continue; }
                data = false;
            }
            else if (line.StartsWith("DATA")) { data = true; await writer.WriteLineAsync("354 send data"); continue; }
            else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 bye"); break; }
            else if (line.StartsWith("STARTTLS") || line.StartsWith("AUTH")) throw new InvalidOperationException("Локальный SMTP не должен требовать TLS или AUTH");
            await writer.WriteLineAsync("250 OK");
        }
        return body.ToString();
    }
}
