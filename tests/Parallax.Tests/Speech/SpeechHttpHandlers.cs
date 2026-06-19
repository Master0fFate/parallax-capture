using System.Net;
using System.Net.Http;

namespace Parallax.Tests.Speech;

internal sealed class CapturingHandler : HttpMessageHandler
{
    public string RequestUri { get; private set; } = string.Empty;

    public string AuthorizationScheme { get; private set; } = string.Empty;

    public string AuthorizationParameter { get; private set; } = string.Empty;

    public string MultipartBody { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri?.ToString() ?? string.Empty;
        AuthorizationScheme = request.Headers.Authorization?.Scheme ?? string.Empty;
        AuthorizationParameter = request.Headers.Authorization?.Parameter ?? string.Empty;
        MultipartBody = request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"text\":\"hello parallax\"}")
        };
    }
}

internal sealed class StaticHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _body;

    public StaticHandler(HttpStatusCode statusCode, string body)
    {
        _statusCode = statusCode;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_body)
        });
    }
}

internal sealed class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("network unavailable");
    }
}
