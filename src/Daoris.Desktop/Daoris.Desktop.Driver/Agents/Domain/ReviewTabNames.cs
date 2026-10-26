namespace Daoris.Driver;

/// <summary>The pure tab naming convention shared by a session's instruction and the higher review operation.</summary>
public static class ReviewTabNames
{
    /// <summary>A set-up step's tab title before its session navigates it (the review environment design §2.2).</summary>
    public static string Title(string quest) => $"Review #{quest.TrimStart('#')}";
}
