using System.Net;
using System.Net.Http.Headers;

namespace Daoris.Driver;

/// <summary>
/// DeepSeek's balance read as the check of a key: <c>GET /user/balance</c> with the key as a bearer token, which spends nothing.
/// 200 is a key accepted, 401 and 403 are the key refused (dsh's own transport reads those two as its <c>AUTH</c>), and anything else,
/// or no answer, is not a reading.
/// </summary>
/// <remarks>The base is the one dsh's DeepSeek transport defaults to (<c>@deepseek-ai/dsh-llm-deepseek</c>).</remarks>
internal sealed class DeepSeekBalance(HttpMessageHandler? handler = null) : IKeyCheck
{
    private static readonly Uri Balance = new("https://api.deepseek.com/user/balance");

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public async Task<LoginState> CheckAsync(string key, CancellationToken ct)
    {
        using var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = Patience;
        using var request = new HttpRequestMessage(HttpMethod.Get, Balance);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => LoginState.In,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LoginState.Out,
                _ => LoginState.Unknown,
            };
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return LoginState.Unknown;
        }
    }
}
