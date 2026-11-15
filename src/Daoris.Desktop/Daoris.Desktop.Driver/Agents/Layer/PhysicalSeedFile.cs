namespace Daoris.Driver;

/// <summary>A seed's file as the agent keeps it on disk, which is not always the text its writer returns.</summary>
/// <param name="Name">The name relative to the account's home.</param>
/// <param name="Bytes">The file's bytes.</param>
internal readonly record struct PhysicalSeedFile(string Name, byte[] Bytes);
