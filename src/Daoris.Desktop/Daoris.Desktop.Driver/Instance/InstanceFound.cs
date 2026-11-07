namespace Daoris.Driver;

/// <summary>What a verb finds when it looks for the running Daoris.</summary>
public enum InstanceFound
{
    /// <summary>No instance on this home, or a workspace build, which reads no address file: the verb acts alone.</summary>
    None,

    /// <summary>A headless <c>drive</c> loop holds the home: alone under the lease's rule, until a headless loop serves the pipe.</summary>
    Headless,

    /// <summary>The instance answered its hello on this home: the verb is its client.</summary>
    Answers,

    /// <summary>The instance lives and its pipe did not answer, or it names none: a press is refused, never done alone.</summary>
    Silent,

    /// <summary>
    /// The hello is refused (<c>protocol</c>, <c>home</c>, <c>stopping</c>), or the pipe refused this terminal as another account's
    /// or elevation's: a press is refused, saying which.
    /// </summary>
    Refused,
}

/// <summary>What a verb found, with the sentence that says it and the connection where the instance answered.</summary>
/// <param name="Nonce">The hello's nonce for this connection, which a holder's proof is bound to; null from an older Daoris.</param>
public sealed record InstanceLook(InstanceFound Found, string Why, InstanceClient? Client = null, string? Nonce = null);
