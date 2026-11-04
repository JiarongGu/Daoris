namespace Daoris.Knowledge.Http.Routes;

/// <summary>What a person reads off a quest that is not a record of the ledger: its handback and its files. A local host's alone.</summary>
public sealed class QuestFileRoutes(HostWiring host) : IRouteModule
{
    private ComposedService Service => host.Service;

    public void Map(RouteMap routes)
    {
        if (host.Mode != ServiceMode.Local) return;

        // Any id is answered, one naming nothing as not found, so a client tells this door from an older host's 404. Every workspace, or the one named.
        routes.Map<ReadHandback, HandbackResponse>(async (request, _, ct) =>
            Of(await new HandbackReader(Service.Quests, Service.Ledger).ReadAsync(request.Id, request.Workspace, ct)));
        routes.MapFile<ReadQuestAttachment>(AttachmentAsync);
    }

    // The bytes never left this machine (D65 §2), and a shared deployment has no door here because it has none of them. Found by hash,
    // never by a path a caller names, so nothing outside the quest's own directory is reachable through it.
    private async Task<IResult> AttachmentAsync(ReadQuestAttachment request, HttpContext http, CancellationToken ct)
    {
        var id = request.Id.TrimStart('#');
        var quest = host.MachineLocal(http) ? await Service.Quests.FindAsync(id, ct) : null;
        var attachment = quest?.Attachments.FirstOrDefault(a => string.Equals(a.Sha256, request.Sha256, StringComparison.Ordinal));
        if (quest is null || attachment is null || Service.Files is null || !Service.Files.Has(quest.Id, attachment))
        {
            return Refusal.NotFound(
                quest is not null && attachment is not null
                    ? $"`{attachment.Name}` is named on quest `#{quest.Id}` and not kept on this machine — its bytes "
                      + "are on the machine that published it."
                    : $"quest `#{id}` carries no file with that hash").Result();
        }

        // 🔴 An attached page must not run as the platform. The file is served from the platform's own origin, so an HTML or SVG a session
        // attached would otherwise be a script with every route this host answers: `sandbox` makes it an opaque origin with no script,
        // `nosniff` stops a browser promoting a text file into a page, and anything that is not plainly an image, a PDF or text is a
        // download rather than a document.
        http.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        var type = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider()
            .TryGetContentType(attachment.Name, out var known) ? known : "application/octet-stream";
        var inline = type is "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "application/pdf"
            || type.StartsWith("text/plain", StringComparison.Ordinal);

        return Results.File(Service.Files.PathOf(quest.Id, attachment), type, fileDownloadName: inline ? null : attachment.Name);
    }

    private static HandbackResponse Of(HandbackReading read) => new(
        read.Id, read.Found, read.Session?.Id,
        read.Quest is { } quest
            ? new HandbackQuestResponse(quest.Id, quest.Title, quest.Status.ToString(), quest.From, quest.To, quest.Workspace, quest.Note)
            : null,
        read.Handback is { } said
            ? new HandbackRecordResponse(
                said.Spelled, said.Note,
                said.Answers.Count == 0 ? null : [.. said.Answers.Select(answer => new QuestAnswerWire(answer.Requirement, answer.Met, answer.Departed, answer.Quote))],
                said.At, said.Machine, said.Session)
            : null,
        read.Said);
}
