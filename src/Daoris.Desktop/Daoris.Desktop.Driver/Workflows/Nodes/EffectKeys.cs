using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// An act's key (EFFECTKEY1, D309): what the act is for, never who asked or how often. A digest of its run (the record's unit), its node,
/// its slot (the act within its step: the step a performed node is) and its target subject (the unit it acts on at its revision), with no
/// holder, attempt or time in it, so a retry and a second holder on the same target name the same act, and a new revision a new one.
/// </summary>
public static class EffectKeys
{
    /// <summary>Every effect key begins so, so no reader takes one for an attempt's own <c>k-</c> key.</summary>
    public const string Prefix = "e-";

    /// <summary>The key of the act <paramref name="slot"/> of <paramref name="node"/> in <paramref name="run"/> makes on <paramref name="target"/>.</summary>
    public static string Of(string run, string node, string slot, string target)
    {
        // A separator no part may hold: a run, a node, a step and a unit at a revision are single-line names.
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', run, node, slot, target)));
        return Prefix + Convert.ToHexStringLower(digest.AsSpan(0, 16));
    }

    /// <summary>The target subject of an act on <paramref name="unit"/> at <paramref name="revision"/>: the unit alone where it has none.</summary>
    public static string Target(string unit, string? revision) => string.IsNullOrEmpty(revision) ? unit : $"{unit}@{revision}";
}
