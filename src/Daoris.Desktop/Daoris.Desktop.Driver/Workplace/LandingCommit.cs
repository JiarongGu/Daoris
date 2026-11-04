using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>One commit a landing carries, oldest first: its full id and its subject line.</summary>
public sealed record LandingCommit(string Sha, string Subject);
