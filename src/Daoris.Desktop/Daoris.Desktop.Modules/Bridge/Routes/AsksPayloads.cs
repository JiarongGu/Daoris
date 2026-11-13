namespace Daoris.Desktop;

/// <summary>An ask's channel as the person reads it; <c>Entry</c> reads as that entry would, <c>Since</c> only the items after a moment.</summary>
public sealed record ChannelReadRequest(string? Ask, string? Entry, string? Since);

/// <summary>An ask's plan as the page reads it: its parts, each with the quest it became.</summary>
public sealed record AskPlanRead(string? Ask);

/// <summary>A post to an ask's channel; <c>Said</c> asks for the verb's words, <c>Ordinary</c> its level.</summary>
public sealed record ChannelPostPress(string? Ask, string? Text, string? To, string? Re, bool Said = false, bool Ordinary = false);

/// <summary>The channel of the line <c>Session</c> is a part of, as the person reads it.</summary>
public sealed record LineChannelRead(string? Session);

/// <summary>The person's post on the own channel of the line <c>Session</c> is a part of, to everyone on it or to one repository.</summary>
public sealed record LineChannelPostPress(string? Session, string? Text, string? To);

/// <summary>The channel's read mark moved through a moment.</summary>
public sealed record ChannelMarkPress(string? Ask, string? Through);

/// <summary>A message of the ask's channel sent on to the newest part of a line of work in a repository.</summary>
public sealed record ChannelForwardPress(string? Ask, string? Message, string? Repository);
