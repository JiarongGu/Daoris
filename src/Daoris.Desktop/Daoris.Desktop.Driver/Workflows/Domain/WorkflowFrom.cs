using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// Where a version came from: the default Daoris ships it was copied from, and that default's digest then. Kept in the version
/// as <c>from</c>, which a build before it reads past, as it reads no key but its own.
/// </summary>
public sealed record WorkflowFrom(string Default, string Digest);
