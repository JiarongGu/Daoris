namespace Daoris.Driver;

/// <summary>What one chat message's files may come to, which the instance's door judges by size and <see cref="ChatFiles"/> holds the files to.</summary>
internal static class ChatAttachmentLimit
{
    /// <summary>How many bytes one message's files come to together — a quest's limit, 20 MB.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;
}
