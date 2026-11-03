namespace Daoris.Driver;

/// <summary>
/// The two choices the conversion asks of the person (the cleanup design §4.2): what a <c>land</c> saved with <c>accept: automatic</c> becomes, and
/// whether a place whose access setup composed the automatic switch keeps a review. Each is the default that lowers nothing.
/// </summary>
public sealed record ConversionChoices(string Composed = ConversionChoices.AsLanded, string LandAutomatic = ConversionChoices.WithReview)
{
    /// <summary>A place the access setup landed with no press is converted with no review, as it landed.</summary>
    public const string AsLanded = "none";

    /// <summary>…or keeps the person's review.</summary>
    public const string KeepReview = "review";

    /// <summary>A <c>land</c> saved with <c>automatic</c> gets the person's review: it never landed, so the default lowers nothing.</summary>
    public const string WithReview = "review";

    /// <summary>…or has none, as it was saved, which lowers the person's part.</summary>
    public const string AsSaved = "none";

    public bool KeepsReview => Composed == KeepReview;

    public bool ReviewsAutomatic => LandAutomatic != AsSaved;

    /// <summary>What is wrong with the choices, or null.</summary>
    public string? Problem =>
        Composed is not (AsLanded or KeepReview) ? $"`composed` is `{AsLanded}` or `{KeepReview}`."
        : LandAutomatic is not (WithReview or AsSaved) ? $"`landAutomatic` is `{WithReview}` or `{AsSaved}`."
        : null;
}
