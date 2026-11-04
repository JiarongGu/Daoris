namespace Daoris.Knowledge;

/// <summary>What a publish carries once judged: the links and files a record may name, or why not.</summary>
internal sealed record QuestCarried(
    QuestPublishRefusal? Refusal, string Message,
    IReadOnlyList<string> Links, IReadOnlyList<QuestAttachment> Attachments);
