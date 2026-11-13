namespace Daoris.Driver;

/// <summary>A read the library answered (<see cref="GitLibraryReads"/>), as git would have: its exit code and what it printed, and what it said on the error stream where it fails in words.</summary>
internal sealed record GitLibraryAnswer(int Code, string Stdout, string Stderr = "");
