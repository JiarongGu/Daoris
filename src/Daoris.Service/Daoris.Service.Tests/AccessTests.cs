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
        Assert.Null(Access.RefuseStartup(ServiceMode.Local, urls, singleKeyConfigured: false));
    }

    [Theory]
    [InlineData("http://0.0.0.0:5177")]
    [InlineData("http://+:80")]
    [InlineData("http://*:80")]
    [InlineData("http://192.168.1.5:5177")]
    [InlineData("http://localhost:5177;http://0.0.0.0:5178")]
    public void Local_mode_refuses_to_bind_beyond_loopback(string urls)
    {
        var refusal = Access.RefuseStartup(ServiceMode.Local, urls, singleKeyConfigured: false);

        Assert.NotNull(refusal);
        Assert.Contains("DAORIS_MODE=shared", refusal);
    }

    [Fact]
    public void Shared_mode_binds_anywhere()
    {
        Assert.Null(Access.RefuseStartup(ServiceMode.Shared, "http://0.0.0.0:5177", singleKeyConfigured: false));
    }

    /// <summary>One credential model per deployment: two would drift, and the weaker one would win.</summary>
    [Fact]
    public void Shared_mode_refuses_the_single_key()
    {
        var refusal = Access.RefuseStartup(ServiceMode.Shared, "http://0.0.0.0:5177", singleKeyConfigured: true);

        Assert.NotNull(refusal);
        Assert.Contains("DAORIS_SERVICE_KEY", refusal);
    }

    /// <summary>The single key stays what it is today — a loopback deployment's write gate (D47 §7).</summary>
    [Fact]
    public void Local_mode_keeps_the_single_key()
    {
        Assert.Null(Access.RefuseStartup(ServiceMode.Local, "http://localhost:5177", singleKeyConfigured: true));
    }
}
