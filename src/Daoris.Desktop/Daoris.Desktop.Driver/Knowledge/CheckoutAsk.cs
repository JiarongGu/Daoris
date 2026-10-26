namespace Daoris.Driver;

/// <summary>
/// What <see cref="HelpCheckouts"/> reads of a request for a checkout (HELPMODE1c4; FOUNDATION1 mod-viii, the design §14.3): the
/// session that asked, the read it asks and the checkout it names. The course's <see cref="SessionRequest"/> is it by being it, so the
/// loop that takes the request hands it as it is and the room's reading names no type of the course.
/// </summary>
public interface ICheckoutAsk
{
    /// <summary>The conversation that asked, by its record's id.</summary>
    string Session { get; }

    /// <summary>The read asked, one of <see cref="HelpCheckouts.Operations"/>; null where none was named.</summary>
    string? Operation { get; }

    /// <summary>The checkout's repository, by its registered name.</summary>
    string? Repository { get; }

    /// <summary>A file's or folder's path relative to the checkout, as the agent wrote it; null for its top.</summary>
    string? Path { get; }
}
