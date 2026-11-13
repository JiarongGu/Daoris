namespace Daoris.Driver;

/// <summary>What a library call reads, the class its admission is judged for (<see cref="GitLibraryAdmission"/>).</summary>
internal enum GitLibraryQuestion
{
    /// <summary>The refs' one listing: names, stored ids, upstreams and derived fields (<see cref="GitLibraryRefs"/>).</summary>
    Refs,

    /// <summary>The commit graph: merge-bases, ancestry and counts (<see cref="GitLibraryWalks"/>).</summary>
    Walks,

    /// <summary>Two commits' changed names (<see cref="GitLibraryNames"/>).</summary>
    Names,

    Revisions,

    Layout,
}
