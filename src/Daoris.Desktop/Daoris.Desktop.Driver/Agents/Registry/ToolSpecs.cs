namespace Daoris.Driver;

/// <summary>What the old seam's toolchain states of a program, as Install's <see cref="ToolSpec"/>, until row t takes its classes away.</summary>
internal static class ToolSpecs
{
    public static ToolSpec Of(HarnessToolchain toolchain) => new(
        toolchain.Binary, toolchain.VersionArguments, toolchain.Install, toolchain.UpdateArguments, toolchain.Package,
        toolchain.Channel, toolchain.PinnedEnvironment, toolchain.ProbeByPresence);
}
