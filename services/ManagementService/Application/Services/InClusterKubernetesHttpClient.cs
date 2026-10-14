using System.Net.Http.Headers;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

[assembly: InternalsVisibleTo("ManagementService.Tests")]

namespace ManagementService.Application.Services;

internal static class InClusterKubernetesHttpClient
{
    internal static HttpClient Create(string serviceAccountDirectory)
    {
        var handler = CreateHandler(File.ReadAllText(Path.Combine(serviceAccountDirectory, "ca.crt")));
        try
        {
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://kubernetes.default.svc"),
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                File.ReadAllText(Path.Combine(serviceAccountDirectory, "token")).Trim());
            return client;
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    internal static HttpClientHandler CreateHandler(string certificatePem)
    {
        // Projected ca.crt содержит только сертификат CA, без приватного ключа.
        var ca = X509Certificate2.CreateFromPem(certificatePem);
        return new CertificateHandler(ca);
    }

    private sealed class CertificateHandler : HttpClientHandler
    {
        private readonly X509Certificate2 _ca;
        internal CertificateHandler(X509Certificate2 ca)
        {
            _ca = ca;
            ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                    return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(_ca);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1"));
                return chain.Build(certificate);
            };
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _ca.Dispose();
        }
    }
}
