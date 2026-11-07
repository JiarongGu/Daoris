using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Daoris.Driver.AccountFileNodes;

namespace Daoris.Driver;

/// <summary>One month's file of a log as it stands: where <see cref="UsageLedger"/> reads on from.</summary>
internal sealed record HistoryMonth(string Name, DateTimeOffset Month, long Length);
