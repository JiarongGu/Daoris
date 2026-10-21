using System.Text.Json;
using Shenora.Core.Ipc;
using static Daoris.Desktop.DriverArguments;

namespace Daoris.Desktop;

/// <summary>
/// <i>Done and failed</i>'s doors on the page, `bridge/notices.ts` (NOTIFY2f, D196 point 6; the notification design §2.3, §2.5): the
/// person's read and dismissal of the notices the record keeps, each written under the record's keep lock by this process and pushed
/// by it. The notices themselves are read from the <c>notices</c> slice, never answered here.
/// </summary>
/// <remarks>
/// <para><b>The person's own</b>: the pipe's census classes both <c>Person</c>, since a terminal that marked the person's news read
/// would hide it from them; Ask Daoris's coverage holds them exempt, the person's own reading.</para>
///
/// <para><b>A write refused is an answer</b>, <c>{ written: false }</c>: the record's lock held past its wait, or a record this build
/// will not write over. The machine log says why; the notice stays as it was, and the press may be made again.</para>
/// </remarks>
public sealed class NoticesRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    /// <summary>The notices of <c>ids</c> read, as the view shows them or its <i>Mark all read</i> names them.</summary>
    [DriverRoute("NOTICES_READ")]
    [Publishes("NOTICES_READ", StateScopes.Notices)]
    private Task<object?> NoticesReadAsync(IpcRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<object?>(new NoticesWritten(_loop.Notices.MarkRead(Ids(request)) is null));

    /// <summary>
    /// The notice <c>id</c> dismissed; or with <c>all</c>, every notice of the window's <c>workspace</c> and every machine-wide one,
    /// every notice where it names none.
    /// </summary>
    [DriverRoute("NOTICE_DISMISS")]
    [Publishes("NOTICE_DISMISS", StateScopes.Notices)]
    private Task<object?> NoticeDismissAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ids = Flag(request, "all")
            ? _loop.Notices.Scoped(Optional(request, "workspace"))
            : [Optional(request, "id") ?? throw new ArgumentException("NOTICE_DISMISS names the notice it dismisses (id), or all.")];
        return Task.FromResult<object?>(new NoticesWritten(_loop.Notices.Dismiss(ids) is null));
    }

    private static List<string> Ids(IpcRequest request) =>
        request.Payload is { } payload && payload.TryGetProperty("ids", out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)]
            : [];
}

/// <summary>A notice press's answer: whether its write went in; one refused is said in the machine log.</summary>
public sealed record NoticesWritten(bool Written);
