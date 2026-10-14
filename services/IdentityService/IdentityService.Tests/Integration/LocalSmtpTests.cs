using System.Net;
using System.Net.Sockets;
using System.Text;
using IdentityService.Application.Services;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class LocalSmtpTests
{
    [Theory]
    [InlineData(false, "/verify-email")]
    [InlineData(true, "/reset-password")]
    public async Task AnonymousPlainSmtp_SendsLocalLink(bool reset, string route)
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
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new EmailVerificationService(db, configuration, NullLogger<EmailVerificationService>.Instance);
        var success = reset
            ? await service.SendPasswordResetEmailAsync("tester@collaby.local", "token+safe")
            : await service.SendVerificationEmailAsync("tester@collaby.local", "user", "token+safe");
        Assert.True(success);
        var raw = await received.WaitAsync(TimeSpan.FromSeconds(10));
        var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(raw)));
        Assert.Contains($"http://localhost:8080{route}?token=token%2Bsafe", message.HtmlBody);
        Assert.Equal("tester@collaby.local", message.To.Mailboxes.Single().Address);
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
                if (line != ".") { body.AppendLine(line); continue; }
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
