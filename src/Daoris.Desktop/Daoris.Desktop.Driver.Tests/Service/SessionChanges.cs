using System.Net;
using System.Text;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Wraps a stand-in service so it answers the lists' change doors (<c>GET /api/sessions/changes</c>, <c>GET /api/quests/changes</c>) in
/// the contract's shape, from what the stand-in answers the whole list (<c>/api/sessions?includeClosed=true</c>,
/// <c>/api/quests?includeClosed=true</c>): every record changed, in the list's order, as a first read is. Every other request passes
/// through, so no stand-in writes the change's shape itself. A list the stand-in refuses is handed back as refused, which is what the
/// driver reads as a host before the door.
/// </summary>
internal sealed class SessionChanges(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Get || SessionChangeShape.ListDoorOf(request.RequestUri!.AbsolutePath) is not { } door)
        {
            return await base.SendAsync(request, ct).ConfigureAwait(false);
        }

        using var list = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri, door));
        var listed = await base.SendAsync(list, ct).ConfigureAwait(false);
        if (!listed.IsSuccessStatusCode) return listed;
        var text = await listed.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SessionChangeShape.Whole(text), Encoding.UTF8, "application/json") };
    }
}
