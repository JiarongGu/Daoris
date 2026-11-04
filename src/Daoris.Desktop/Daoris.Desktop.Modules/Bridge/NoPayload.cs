namespace Daoris.Desktop;

/// <summary>What a route that reads nothing off its request takes: a handler never sees the raw request, only a record, even an empty one.</summary>
public sealed record NoPayload;
