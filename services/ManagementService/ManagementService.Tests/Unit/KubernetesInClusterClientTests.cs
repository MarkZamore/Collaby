using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ManagementService.Application.Services;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class KubernetesInClusterClientTests
{
    private static X509Certificate2 CreateCa(RSA key)
    {
        var request = new CertificateRequest("CN=Kubernetes CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public void ProjectedCertificateWithoutPrivateKeyCreatesClientAndRereadsRotatedToken()
    {
        using var key = RSA.Create(2048);
        using var ca = CreateCa(key);
        var directory = Path.Combine(Path.GetTempPath(), "collaby-k8s-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var pem = ca.ExportCertificatePem();
            Assert.DoesNotContain("PRIVATE KEY", pem);
            File.WriteAllText(Path.Combine(directory, "ca.crt"), pem);
            File.WriteAllText(Path.Combine(directory, "token"), "first-token\n");
            using var client = InClusterKubernetesHttpClient.Create(directory);
            Assert.Equal("https://kubernetes.default.svc/", client.BaseAddress!.ToString());
            Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization!.Scheme);
            Assert.Equal("first-token", client.DefaultRequestHeaders.Authorization.Parameter);
            File.WriteAllText(Path.Combine(directory, "token"), "rotated-token\n");
            using var rotated = InClusterKubernetesHttpClient.Create(directory);
            Assert.Equal("rotated-token", rotated.DefaultRequestHeaders.Authorization!.Parameter);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void TlsValidationRequiresKubernetesCaAndHostnameAndServerAuth()
    {
        using var caKey = RSA.Create(2048);
        using var ca = CreateCa(caKey);
        using var leafKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=kubernetes.default.svc", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        using var leaf = request.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(16));
        using var handler = InClusterKubernetesHttpClient.CreateHandler(ca.ExportCertificatePem());
        using var http = new HttpRequestMessage(HttpMethod.Get, "https://kubernetes.default.svc");
        var validate = handler.ServerCertificateCustomValidationCallback!;
        Assert.True(validate(http, leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(validate(http, leaf, null, SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(validate(http, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
        using var unrelatedKey = RSA.Create(2048);
        using var unrelatedCa = CreateCa(unrelatedKey);
        using var unrelatedHandler = InClusterKubernetesHttpClient.CreateHandler(unrelatedCa.ExportCertificatePem());
        Assert.False(unrelatedHandler.ServerCertificateCustomValidationCallback!(http, leaf, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }
}
