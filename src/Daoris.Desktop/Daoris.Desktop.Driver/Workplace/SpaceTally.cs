using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>What a run's kind removed and kept, as it goes.</summary>
internal sealed class SpaceTally
{
    public int Removed { get; private set; }

    public long Bytes { get; private set; }

    public int EarlyRemoved { get; private set; }

    public long EarlyBytes { get; private set; }

    public Dictionary<string, int> Kept { get; } = new(StringComparer.Ordinal);

    public void Remove(long bytes, bool early)
    {
        Removed++;
        Bytes += bytes;
        if (!early) return;
        EarlyRemoved++;
        EarlyBytes += bytes;
    }

    public void Keep(string why, bool early) => Kept[why] = Kept.GetValueOrDefault(why) + 1;
}
