namespace Daoris.Driver;

/// <summary>One server the door's init frame lists, with the status the door gave it (<c>connected</c>, <c>failed</c>, <c>pending</c>…).</summary>
public sealed record WireServer(string Name, string Status);

/// <summary>
/// What a door said it loaded at its start (HELPWIRED1b, the agent-provider design §3.1): the tools, the servers with their status, the
/// skills and the plugins its init frame names, as the door wrote them and never what Daoris handed. A field the door left out is an empty
/// list, so a door that says nothing of its skills reads as none said, never as none loaded.
/// </summary>
public sealed record WireLoaded(
    IReadOnlyList<string> Tools, IReadOnlyList<WireServer> Servers, IReadOnlyList<string> Skills, IReadOnlyList<string> Plugins);
