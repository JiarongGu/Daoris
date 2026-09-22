using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The startup judgement for a deployment (D47 §3): a host asked to bind beyond loopback without
/// shared mode's credential model refuses to START — the sibling's fail-safe inversion, where the
/// insecure configuration is unreachable exactly where it would matter, not merely discouraged.
/// </summary>
public sealed class AccessTests
{
    [Theory]
    [InlineData(null, ServiceMode.Local)]
    [InlineData("", ServiceMode.Local)]
    [InlineData("local", ServiceMode.Local)]
    [InlineData("shared", ServiceMode.Shared)]
    [InlineData("Shared", ServiceMode.Shared)]
    public void Absence_means_local_and_shared_is_opt_in(string? value, ServiceMode expected)
    {
        var (mode, error) = Access.ParseMode(value);

        Assert.Null(error);
        Assert.Equal(expected, mode);
    }

    /// <summary>An unknown mode errors naming what exists — never a silent fallback (D23's rule).</summary>
    [Fact]
    public void An_unknown_mode_is_refused_naming_what_exists()
    {
        var (_, error) = Access.ParseMode("team");

        Assert.NotNull(error);
        Assert.Contains("local", error);
        Assert.Contains("shared", error);
    }

    [Theory]
    [InlineData("http://localhost:5177")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:5177")]
    [InlineData("http://localhost:5177;http://127.0.0.1:9000")]
    public void Local_mode_serves_loopback(string urls)
    {
        Assert.Null(Access.RefuseStartup(ServiceMode.Local, urls));
    }

    [Theory]
    [InlineData("http://0.0.0.0:5177")]
    [InlineData("http://+:80")]
    [InlineData("http://*:80")]
    // TEST-NET-3 (RFC 5737), which exists to be written down: a specific non-loopback interface that
    // is nobody's actual subnet, because the sensitive scan cannot tell a fixture from a machine.
    [InlineData("http://203.0.113.5:5177")]
    [InlineData("http://localhost:5177;http://0.0.0.0:5178")]
    public void Local_mode_refuses_to_bind_beyond_loopback(string urls)
    {
        var refusal = Access.RefuseStartup(ServiceMode.Local, urls);

        Assert.NotNull(refusal);
        Assert.Contains("DAORIS_MODE=shared", refusal);
    }

    [Fact]
    public void Shared_mode_binds_anywhere()
    {
        Assert.Null(Access.RefuseStartup(ServiceMode.Shared, "http://0.0.0.0:5177"));
    }
}

/// <summary>
/// A shared deployment is a WORKSPACE's deployment (D48 §5): it carries one circle's identity and
/// refuses anything that declares another. The alternative — one multi-tenant host holding many
/// workspaces — puts the sharing boundary inside one store and one key space, which is exactly where
/// a scoping bug becomes a disclosure.
/// </summary>
public sealed class HostWorkspaceTests
{
    /// <summary>Silence is `default`, everywhere (D48 §2) — including a host that never named itself.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_shared_host_that_names_no_workspace_serves_the_default_circle(string? value)
    {
        var (workspace, error) = Access.ParseWorkspace(ServiceMode.Shared, value);

        Assert.Null(error);
        Assert.Equal(Workspaces.Default, workspace);
    }

    [Fact]
    public void A_shared_host_carries_the_circle_it_was_given()
    {
        var (workspace, error) = Access.ParseWorkspace(ServiceMode.Shared, " aurora ");

        Assert.Null(error);
        Assert.Equal("aurora", workspace);
    }

    /// <summary>
    /// A LOCAL host holds every circle the person wired, so an identity there would be a claim it
    /// cannot honour — and a parsed-and-unused input is a claim (`claims-need-checks`). It refuses
    /// rather than ignoring, the same fail-safe shape as the loopback rule above.
    /// </summary>
    [Fact]
    public void A_local_host_has_no_workspace_identity_and_refuses_one()
    {
        var (_, error) = Access.ParseWorkspace(ServiceMode.Local, "aurora");

        Assert.NotNull(error);
        Assert.Contains(Access.WorkspaceVariable, error);
        Assert.Contains("DAORIS_MODE=shared", error);
    }

    [Fact]
    public void A_local_host_naming_nothing_has_no_identity_at_all()
    {
        var (workspace, error) = Access.ParseWorkspace(ServiceMode.Local, null);

        Assert.Null(error);
        Assert.Null(workspace);
    }

    /// <summary>Silence takes the host's own circle: the receiving deployment's wiring decides (D48 §2).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aurora")]
    [InlineData("AURORA")]
    public void A_registration_in_the_host_s_own_circle_is_taken(string? stated)
    {
        Assert.Null(Access.RefuseForeignWorkspace("aurora", "atelier", stated));
    }

    /// <summary>The refusal names BOTH sides — a "no" that does not say which side is where is a puzzle.</summary>
    [Fact]
    public void A_registration_declaring_another_circle_is_refused_naming_both()
    {
        var refusal = Access.RefuseForeignWorkspace("aurora", "foundry", "tools");

        Assert.NotNull(refusal);
        Assert.Contains("aurora", refusal);
        Assert.Contains("tools", refusal);
        Assert.Contains("foundry", refusal);
    }

    /// <summary>A local host has no identity to defend, so it takes every circle on the machine.</summary>
    [Fact]
    public void A_local_host_refuses_nothing()
    {
        Assert.Null(Access.RefuseForeignWorkspace(null, "foundry", "tools"));
    }
}
