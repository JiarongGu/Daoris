namespace Daoris.Knowledge;

/// <summary>A place a part's pack tells its worker to read, with the blob id it was read at, so a later change of the file marks this pointer alone.</summary>
/// <param name="Role">What it is for: <c>area</c>, <c>knowledge</c>, <c>change</c> or <c>mirror</c>.</param>
/// <param name="File">The repository-relative path.</param>
/// <param name="Range">The lines to read, as <c>12-40</c>; blank for the file whole.</param>
/// <param name="Blob">The file's git blob id when the scout read it.</param>
public sealed record PackPointer(string Role, string File, string Range, string Blob);
