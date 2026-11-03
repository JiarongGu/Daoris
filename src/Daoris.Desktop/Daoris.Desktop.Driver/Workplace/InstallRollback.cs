using System.Reflection;

namespace Daoris.Driver;

/// <summary>
/// The update's door back to an older build (LANDNODE1c2; the land node design §4, <i>Older builds</i>): a staged build whose version is below
/// this one's is a going back, refused while any delivery is unresolved (<see cref="DeliveryStore.Rollback"/>), so an older door never lands, or
/// calls a plugin without the key, behind a newer delivery. The launcher's own roll-back before a start confirms meets no delivery, since none
/// starts in that window.
/// </summary>
public static class InstallRollback
{
    /// <summary>The version this build carries, as stamped; null where none is.</summary>
    public static string? This => typeof(InstallRollback).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>
    /// Why installing <paramref name="staged"/> over <paramref name="running"/> is refused, or null: a version that does not read, or one not
    /// below this one's, is no going back.
    /// </summary>
    public static string? Refusal(string home, string? staged, string? running) => Refused(home, staged, running)?.Sentence;

    /// <summary>The code of the refusal a stale delivery makes.</summary>
    public const string Deliveries = "deliveries";

    /// <summary><see cref="Refusal"/> with its code: <see cref="Deliveries"/> while a delivery is unresolved; null otherwise.</summary>
    public static (string Code, string Sentence)? Refused(string home, string? staged, string? running)
    {
        if (!(Numbered(staged) is { } older && Numbered(running) is { } newer && older < newer)) return null;
        var rollback = new DeliveryStore(home).Rollback();
        return rollback.Allowed ? null : (Deliveries, rollback.Refused!);
    }

    /// <summary>A version's numbered core, a prerelease and a build's metadata left off; null where it does not read.</summary>
    public static Version? Numbered(string? version) => HomeLastBuild.Numbered(version);
}
