using System.Net;

namespace Collaby.ServiceDefaults.InternalQueries;

public static class InternalQueryHttpClientExtensions
{
    public static async Task<HttpResponseMessage> SendWithIdempotentQueryResilienceAsync(
        this HttpClient httpClient,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsRetryableIdempotentInternalQuery())
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }

        using var retryRequest = await CloneAsync(request, cancellationToken);

        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!ShouldRetry(response.StatusCode))
            {
                return response;
            }

            response.Dispose();
        }
        catch (Exception ex) when (IsTransient(ex))
        {
        }

        await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        return await httpClient.SendAsync(retryRequest, cancellationToken);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout ||
           statusCode == HttpStatusCode.TooManyRequests ||
           (int)statusCode >= 500;

    private static bool IsTransient(Exception exception)
        => exception is HttpRequestException || exception is TaskCanceledException;

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var bytes = await source.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentClone = new ByteArrayContent(bytes);
            foreach (var header in source.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = contentClone;
        }

        return clone;
    }
}
