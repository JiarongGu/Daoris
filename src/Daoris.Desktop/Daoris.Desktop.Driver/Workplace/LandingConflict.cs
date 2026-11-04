using System.Text;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// A replay onto the line that conflicts (LANDSYNC1, D87's amendment point 5): git's <c>CONFLICT</c> lines, and the words the review
/// offers to say to the session, asking it to replay its own commits onto the line, keeping only what the line lacks.
/// </summary>
public sealed record LandingConflict(string Says, string Words);
