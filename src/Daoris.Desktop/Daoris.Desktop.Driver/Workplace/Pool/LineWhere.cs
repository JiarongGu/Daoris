namespace Daoris.Driver;

/// <summary>What the disk and the pool say of a line's tree.</summary>
public enum LineStand
{
    /// <summary>A folder holds the line's tree: its slot, or the record's own path.</summary>
    Stands,

    /// <summary>No folder holds it, and a slot that let the line go can be switched back to it.</summary>
    CanBeBroughtBack,

    /// <summary>No folder holds it and the pool has no let-go of the line.</summary>
    Gone,
}

/// <summary>
/// Where a record's tree is to be opened as a folder. A record names its line and never a slot, so <paramref name="Folder"/> is the
/// slot's where the line holds one and the record's own path otherwise.
/// </summary>
/// <param name="Folder">The folder to open; the record's own path where the line holds no slot.</param>
/// <param name="Stand">Whether a folder holds it, or one can be had.</param>
/// <param name="Slot">The slot the line holds, or null.</param>
public sealed record LineWhere(string Folder, LineStand Stand, KeptTree? Slot)
{
    /// <summary>Whether a folder holds the tree now.</summary>
    public bool Stands => Stand == LineStand.Stands;

    /// <summary>Whether it stands, or can be had: what a go-on asks before it brings the tree back.</summary>
    public bool StandsOrCanBeHad => Stand != LineStand.Gone;
}
