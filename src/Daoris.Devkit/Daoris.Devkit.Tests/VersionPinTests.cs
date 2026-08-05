using Daoris.Devkit;

namespace Daoris.Devkit.Tests;

public sealed class VersionPinTests
{
    /// <summary>
    /// A pin that nothing checks is a comment. This one was parsed and never read for a while, under a
    /// doc comment claiming a launcher enforced it — a launcher that did not exist.
    /// </summary>
    [Fact]
    public void A_mismatched_pin_stops_the_run_and_names_both_versions()
    {
        var error = Assert.Throws<DevkitException>(() => VersionPin.Require("0.2.0", "0.0.1"));

        Assert.Contains("0.2.0", error.Message);
        Assert.Contains("0.0.1", error.Message);
    }

    [Fact]
    public void A_matching_pin_passes()
    {
        VersionPin.Require("0.0.1", "0.0.1");
    }

    /// <summary>Pinning is a choice made when a repository starts caring, not a demand on its first run.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unpinned_declaration_is_allowed_and_silent(string? declared)
    {
        VersionPin.Require(declared, "0.0.1");
    }

    [Fact]
    public void Surrounding_whitespace_in_a_hand_edited_field_is_not_a_mismatch()
    {
        VersionPin.Require("  0.0.1  ", "0.0.1");
    }
}
