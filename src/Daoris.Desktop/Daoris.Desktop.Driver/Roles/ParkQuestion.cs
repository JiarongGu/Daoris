using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A session's question as <c>question_ask</c> wrote it: its id, the session, the agent's words, the answers it offers, the one it
/// recommends by number from 1 (null for none, or one that names no answer), by when as it said, when it asked, and why it recommends
/// that answer (null with none recommended), and the agent's mark that the question is the owner's alone
/// (null for none), read as written, a mark this build does not know included: the supervisor answers no marked question. And the
/// site whose sign-in it waits on, a web page's address (null for none): the person's sign-in there, or Daoris's browser seeing it, settles it.
/// </summary>
public sealed record ParkQuestion(
    string Id, string Session, string Question, IReadOnlyList<ParkQuestionChoice> Choices, int? Recommend, string? By, DateTimeOffset At,
    string? Why = null, string? Owner = null, string? Site = null, string? Role = null)
{
    /// <summary>Asked by a conversation, which has no park: its sign-in is said to it and the file goes.</summary>
    public bool Conversation { get; init; }
}

/// <summary>One answer a question offers: the words on its press, and what the agent does on it.</summary>
public sealed record ParkQuestionChoice(string Label, string Does);

/// <summary>
/// The question a file holds, read to the writer's bounds: the rule the supervisor's decisions memory and the question's file share,
/// Roles' since it is the lowest that reads it. <see cref="QuestionFiles"/> keeps the file and names these.
/// </summary>
internal static class ParkQuestions
{
    /// <summary>The writer's bounds (<c>QuestionBox</c>), held on read: the question, its answers, each label and each <c>does</c>, and by when.</summary>
    public const int MaxQuestion = 1000, MaxChoices = 6, MaxLabel = 80, MaxDoes = 300, MaxBy = 200, MaxWhy = 300, MaxSite = 500;

    private static readonly System.Text.RegularExpressions.Regex RoleShape =
        new(@"^(?=.{1,40}\z)[a-z0-9]+(?:-[a-z0-9]+)*\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>A site as a sign-in question names it: an http or https page's address with a host and no credentials, else null.</summary>
    internal static string? SiteOf(string? address) =>
        address is { Length: > 0 and <= MaxSite } && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && uri.Host.Length > 0 && uri.UserInfo.Length == 0
            ? address
            : null;

    /// <summary>The question a file's text holds, held to the writer's bounds; null where it holds none.</summary>
    internal static ParkQuestion? Parsed(string text, string session, Func<DateTimeOffset> written)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "id") is not { Length: > 0 } id
            || Text(root, "question") is not { Length: <= MaxQuestion } question || string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        // An answer with no label is no answer to press; one with no words for what it does says none.
        List<ParkQuestionChoice> choices = root.TryGetProperty("choices", out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray()
                .Select(choice => choice.ValueKind == JsonValueKind.Object && Text(choice, "label") is { } label && !string.IsNullOrWhiteSpace(label)
                    ? new ParkQuestionChoice(label, Text(choice, "does") ?? "")
                    : null)
                .OfType<ParkQuestionChoice>()]
            : [];
        var by = Text(root, "by") is { Length: > 0 } said ? said : null;
        if (choices.Count > MaxChoices || choices.Any(choice => choice.Label.Length > MaxLabel || choice.Does.Length > MaxDoes)
            || choices.Select(choice => choice.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() < choices.Count
            || by is { Length: > MaxBy } || Text(root, "why") is { Length: > MaxWhy })
        {
            return null;
        }

        int? recommend = root.TryGetProperty("recommend", out var number) && number.ValueKind == JsonValueKind.Number
            && number.TryGetInt32(out var chosen) && chosen >= 1 && chosen <= choices.Count ? chosen : null;
        // Its reason explains the recommendation: with none recommended, there is nothing for it to explain.
        var why = recommend is not null && Text(root, "why") is { } reason && !string.IsNullOrWhiteSpace(reason) ? reason : null;
        // The agent's mark that the question is the owner's alone, any words at all: the supervisor answers none of it.
        var owner = Text(root, "owner") is { } mark && !string.IsNullOrWhiteSpace(mark) ? mark : null;
        var role = Text(root, "role") is { } asked && RoleShape.IsMatch(asked) ? asked : null;
        return new ParkQuestion(id, session, question, choices, recommend, by, Moments.Read(root, "at") ?? written(), why, owner, SiteOf(Text(root, "site")), role)
        {
            Conversation = root.TryGetProperty("conversation", out var chat) && chat.ValueKind == JsonValueKind.True,
        };
    }
}
