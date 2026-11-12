namespace Daoris.Driver;

/// <summary>
/// The one step a set-up quest may carry behind it: a quest to the same repository, asked on behalf of the same asker and published by the
/// service when the set-up closes done, so work for a repository that has no brief waits for the brief. A decline of the set-up ends the chain.
/// </summary>
/// <param name="Title">One line. <c>{parent}</c> becomes the id of the set-up quest.</param>
/// <param name="Body">Why, and what is wanted. <c>{parent}</c> is expanded here too.</param>
public sealed record SetupThen(string Title, string Body);
