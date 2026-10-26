using System.Diagnostics.CodeAnalysis;

namespace Daoris.Driver;

/// <summary>The submitted state carried by a service quest read, independent of the consumer's policy.</summary>
public static class QuestSubmissions
{
    public static bool Current([NotNullWhen(true)] QuestView? quest) => quest is { Status: "Taken", Closure: not null, Submission: not null };
}
