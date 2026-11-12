namespace Daoris.Driver;

/// <summary>
/// A run's step the runtime passed on the person's through words (<see cref="ThroughAccepts"/>): the quote it applied and the ask it is
/// kept on, which the run's view says as <i>Accepted on your words</i>.
/// </summary>
/// <param name="Ask">The ask whose through words they are.</param>
/// <param name="Quote">The person's words, verbatim.</param>
public sealed record RunStepThrough(string Ask, string Quote);
