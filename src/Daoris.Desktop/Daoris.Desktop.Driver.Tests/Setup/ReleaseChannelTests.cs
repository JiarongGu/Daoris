using System.Net;
using System.Security.Cryptography;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 <b>A managed Claude Code comes from Anthropic's own release bucket, verified</b> (AGT2b) — the
/// desktop's half of <c>daoris agent pin claude-code</c>, and the twin of the CLI's
/// <c>channels.test.ts</c> and <c>openpgp.test.ts</c>. The manifest is verified against its detached
/// OpenPGP signature under the PINNED release key, and only then is its SHA-256 trusted for the
/// binary, fetched directly.
/// </summary>
/// <remarks>
/// <para><b>No test here touches the network.</b> The vendor's own files are read from the CLI's
/// <c>test/fixtures/vendor/</c> — one copy of the evidence for both artefacts, kept byte for byte by
/// <c>.gitattributes</c> — and every request goes to a transport the test holds.</para>
///
/// <para>Codex's channel is the driver's too since PUSH1f2e (<see cref="CodexReleases"/>): its door is <c>codex-acp</c>, an npm
/// package, and its accounts' holder, <c>codex</c>, pins from the release's own package. Each run stages under its own process's
/// name and publishes by one move (<see cref="ReleaseChannels"/>).</para>
/// </remarks>
public sealed class ReleaseChannelTests : IDisposable
{
    private const string Pinned = "31DD DE24 DDFA B679 F42D  7BD2 BAA9 29FF 1A7E CACE";

    private static readonly string Vendor = VendorFixtures();

    /// <summary>The repository root: vendor → fixtures → test → Daoris.Cli → src → root.</summary>
    private static readonly string Root = Path.GetFullPath(Path.Combine(Vendor, "..", "..", "..", "..", ".."));

    // Scratch in the repository's own gitignored `_fixtures/`, never OS temp.
    private readonly string _home = Path.Combine(Root, "_fixtures", "release-channel", Guid.NewGuid().ToString("N")[..8]);

    private static byte[] Manifest => File.ReadAllBytes(Path.Combine(Vendor, "claude-code", "2.1.281", "manifest.json"));

    private static string Signature => File.ReadAllText(Path.Combine(Vendor, "claude-code", "2.1.281", "manifest.json.sig"));

    public void Dispose()
    {
        GitSeed.Remove(_home);
    }

    // ——— The signature.

    [Fact]
    public void The_release_key_carried_is_the_vendor_s_published_key_and_reads_as_the_pin()
    {
        var published = File.ReadAllText(Path.Combine(Vendor, "claude-code", "claude-code.asc"));

        Assert.Equal(
            OpenPgp.Dearmor(published, "PUBLIC KEY BLOCK"),
            OpenPgp.Dearmor(ClaudeReleases.ReleaseKey, "PUBLIC KEY BLOCK"));
        Assert.Equal("31DDDE24DDFAB679F42D7BD2BAA929FF1A7ECACE", ClaudeReleases.Fingerprint);
        Assert.Equal(ClaudeReleases.Fingerprint, OpenPgp.PrimaryFingerprint(ClaudeReleases.ReleaseKey));
    }

    [Fact]
    public void A_real_release_manifest_verifies_against_the_pinned_key()
    {
        var verdict = OpenPgp.VerifyDetached(Manifest, Signature, ClaudeReleases.ReleaseKey, Pinned);

        Assert.True(verdict.Ok, verdict.Reason);
        Assert.Equal(ClaudeReleases.Fingerprint, verdict.Fingerprint);
    }

    [Fact]
    public void One_changed_byte_in_the_manifest_is_refused()
    {
        var tampered = Tamper(Manifest);

        var verdict = OpenPgp.VerifyDetached(tampered, Signature, ClaudeReleases.ReleaseKey, Pinned);

        Assert.False(verdict.Ok);
        Assert.Contains("not what the release key signed", verdict.Reason);
    }

    [Fact]
    public void A_key_that_is_not_the_pinned_one_is_refused_naming_both()
    {
        using var stranger = Pgp.Stranger();

        var verdict = OpenPgp.VerifyDetached(Manifest, Signature, stranger.Armoured, Pinned);

        Assert.False(verdict.Ok);
        Assert.Contains("the key given is", verdict.Reason);
        Assert.Contains("31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE", verdict.Reason);
    }

    /// <summary>
    /// A forger writes every byte of a signature packet, the issuer it names included. The one thing
    /// it cannot produce is RSA under the pinned key — so this fails if verification ever stops at
    /// the parts a forger writes. The second half is its positive control.
    /// </summary>
    [Fact]
    public void A_signature_that_claims_the_release_key_fails_the_RSA_check_itself()
    {
        using var stranger = Pgp.Stranger();
        var forged = Pgp.Sign(Manifest, stranger.Key, ClaudeReleases.Fingerprint);
        var honest = Pgp.Sign(Manifest, stranger.Key, stranger.Fingerprint);

        var refused = OpenPgp.VerifyDetached(Manifest, forged, ClaudeReleases.ReleaseKey, Pinned);
        var own = OpenPgp.VerifyDetached(Manifest, honest, stranger.Armoured, stranger.Fingerprint);

        Assert.False(refused.Ok);
        Assert.Contains("not what the release key signed", refused.Reason);
        Assert.True(own.Ok, own.Reason);
    }

    // ——— The versions and the builds.

    /// <summary>
    /// 🔴 Before 2.1.89 there is no signature to check (2.1.87's <c>.sig</c> answers 404), so an
    /// earlier version is refused by name — never installed unverified, and never handed to npm.
    /// </summary>
    [Fact]
    public void A_version_from_before_signed_manifests_is_refused_naming_the_first_signed_one()
    {
        var error = Assert.Throws<DriverException>(() => ClaudeReleases.RefuseVersion("2.1.87"));

        Assert.Contains("2.1.89", error.Message);
        Assert.Contains("PATH", error.Message);
        ClaudeReleases.RefuseVersion("2.1.89");
        ClaudeReleases.RefuseVersion("2.1.281");
        foreach (var version in new[] { "latest", "stable", "2.1", "v2.1.281", "2.1.281-beta" })
        {
            Assert.Contains("version", Assert.Throws<DriverException>(() => ClaudeReleases.RefuseVersion(version)).Message);
        }
    }

    [Fact]
    public void Each_machine_maps_to_the_build_the_vendor_publishes_for_it()
    {
        Assert.Equal("win32-x64", ClaudeReleases.Platform("win32", "x64", musl: false));
        Assert.Equal("win32-arm64", ClaudeReleases.Platform("win32", "arm64", musl: false));
        Assert.Equal("darwin-arm64", ClaudeReleases.Platform("darwin", "arm64", musl: false));
        Assert.Equal("linux-x64-musl", ClaudeReleases.Platform("linux", "x64", musl: true));
        Assert.Contains("freebsd-x64", Assert.Throws<DriverException>(
            () => ClaudeReleases.Platform("freebsd", "x64", musl: false)).Message);
    }

    // ——— The install.

    [Fact]
    public async Task A_verified_release_lands_as_bin_claude_byte_for_byte_and_its_staging_is_gone()
    {
        using var stranger = Pgp.Stranger();
        var binary = Encoding.UTF8.GetBytes("MZ-not-really-a-binary");
        var transport = new Served(Release(stranger, "2.1.300", "win32-x64", binary));
        var where = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.300");
        var said = new List<string>();

        var executable = await ClaudeReleaseDownloads.InstallAsync(
            where, "2.1.300", said.Add, CancellationToken.None, transport,
            platform: "win32-x64", trust: (stranger.Armoured, stranger.Fingerprint));

        Assert.Equal(Path.Combine(where, "bin", "claude.exe"), executable);
        Assert.Equal(binary, File.ReadAllBytes(executable));
        Assert.False(Directory.Exists(where + ".part"));
        // The manifest and its signature, THEN the binary — never the binary first.
        Assert.Equal(
            [
                $"{ClaudeReleases.Base}/2.1.300/manifest.json",
                $"{ClaudeReleases.Base}/2.1.300/manifest.json.sig",
                $"{ClaudeReleases.Base}/2.1.300/win32-x64/claude.exe",
            ],
            transport.Asked);
        Assert.Contains(said, line => line.Contains("signature"));
        Assert.Contains(said, line => line.Contains("SHA-256"));
    }

    [Fact]
    public async Task The_real_manifest_verifies_and_a_binary_that_is_not_the_one_it_names_is_refused()
    {
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json"] = Manifest,
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json.sig"] = Encoding.UTF8.GetBytes(Signature),
            [$"{ClaudeReleases.Base}/2.1.281/win32-x64/claude.exe"] = Encoding.UTF8.GetBytes("something else entirely"),
        });
        var where = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.281");
        var said = new List<string>();

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleaseDownloads.InstallAsync(
            where, "2.1.281", said.Add, CancellationToken.None, transport, platform: "win32-x64"));

        Assert.Contains("SHA-256", error.Message);
        // It reached the binary, so the real signature verified under the real pinned key.
        Assert.Equal(3, transport.Asked.Count);
        Assert.Contains(said, line => line.Contains("31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE"));
        Assert.False(Directory.Exists(where));
        Assert.False(Directory.Exists(where + ".part"));
    }

    [Fact]
    public async Task A_tampered_manifest_is_refused_before_any_binary_is_fetched()
    {
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json"] = Tamper(Manifest),
            [$"{ClaudeReleases.Base}/2.1.281/manifest.json.sig"] = Encoding.UTF8.GetBytes(Signature),
        });

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleaseDownloads.InstallAsync(
            Path.Combine(_home, "v"), "2.1.281", _ => { }, CancellationToken.None, transport, platform: "win32-x64"));

        Assert.Contains("did not verify", error.Message);
        Assert.Equal(2, transport.Asked.Count);
    }

    /// <summary>
    /// A genuine signed manifest served as a version it is not. The signature is perfect — it is the
    /// vendor's — so only reading the version it SIGNED stops an old release installing as a new one.
    /// </summary>
    [Fact]
    public async Task A_real_signed_manifest_served_as_another_version_is_refused_as_what_it_is()
    {
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [$"{ClaudeReleases.Base}/2.1.290/manifest.json"] = Manifest,
            [$"{ClaudeReleases.Base}/2.1.290/manifest.json.sig"] = Encoding.UTF8.GetBytes(Signature),
        });

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleaseDownloads.InstallAsync(
            Path.Combine(_home, "v"), "2.1.290", _ => { }, CancellationToken.None, transport, platform: "win32-x64"));

        Assert.Contains("2.1.281, not 2.1.290", error.Message);
    }

    // ——— The verb, and the layout it leaves.

    [Fact]
    public void Claude_Code_pins_from_its_maker_s_channel_and_the_doors_from_npm()
    {
        var set = AdapterSet.Built();
        var pipe = set.Resolve("claude-code").Toolchain!;

        Assert.Equal(ClaudeReleases.Channel, pipe.Channel);
        Assert.Null(pipe.Package);
        foreach (var door in new[] { "claude-code-acp", "codex-acp", "dsh" })
        {
            Assert.NotNull(set.Resolve(door).Toolchain!.Package);
            Assert.Null(set.Resolve(door).Toolchain!.Channel);
        }
    }

    [Fact]
    public async Task A_pin_of_an_unsigned_version_is_refused_before_anything_is_fetched()
    {
        var transport = new Served([]);
        var toolchain = AdapterSet.Built().Resolve("claude-code").Toolchain!;

        await Assert.ThrowsAsync<DriverException>(() => HarnessActions.PinAsync(
            toolchain, _home, "claude-code", "2.1.87", _ => { }, CancellationToken.None, transport: transport));

        Assert.Empty(transport.Asked);
    }

    /// <summary>An install already in place is the proof it verified, so re-pinning fetches nothing.</summary>
    [Fact]
    public async Task Re_pinning_a_version_already_installed_downloads_nothing()
    {
        var bin = Path.Combine(HarnessSettings.ManagedHome(_home, "claude-code", "2.1.281"), "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, OperatingSystem.IsWindows() ? "claude.exe" : "claude"), "");
        var transport = new Served([]);
        var said = new List<string>();

        var code = await HarnessActions.PinAsync(
            AdapterSet.Built().Resolve("claude-code").Toolchain!, _home, "claude-code", "2.1.281", said.Add,
            CancellationToken.None, transport: transport);

        Assert.Equal(0, code);
        Assert.Empty(transport.Asked);
        Assert.Contains(said, line => line.Contains("nothing was downloaded"));
    }

    /// <summary>
    /// The vendor's layout is asked first — <c>bin/&lt;binary&gt;</c> — and npm's still resolves,
    /// because a pin npm made before AGT2b is an install somebody made. The CLI's
    /// <c>managedBinary</c> is the twin.
    /// </summary>
    [Fact]
    public void A_managed_install_in_the_vendor_s_layout_resolves_ahead_of_npm_s()
    {
        var managed = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.281");
        var windows = OperatingSystem.IsWindows();
        Directory.CreateDirectory(Path.Combine(managed, "bin"));
        Directory.CreateDirectory(Path.Combine(managed, "node_modules", ".bin"));
        var vendor = Path.Combine(managed, "bin", windows ? "claude.exe" : "claude");
        File.WriteAllText(vendor, "");
        File.WriteAllText(Path.Combine(managed, "node_modules", ".bin", windows ? "claude.cmd" : "claude"), "");

        Assert.Equal(vendor, HarnessSettings.ManagedBinary(_home, "claude-code", "2.1.281", ["claude"]));
    }

    // ——— Codex: the package, by both of its published hashes, unpacked whole (PUSH1f2e, the CLI's `channels.ts` ported).

    private static string Codex(string version, string file) => $"{CodexReleases.Base}/{version}/{file}";

    [Fact]
    public void Each_machine_maps_to_the_package_codex_publishes_for_it_and_a_version_is_no_tag()
    {
        Assert.Equal("x86_64-pc-windows-msvc", CodexReleases.Target("win32", "x64"));
        Assert.Equal("aarch64-pc-windows-msvc", CodexReleases.Target("win32", "arm64"));
        Assert.Equal("x86_64-apple-darwin", CodexReleases.Target("darwin", "x64"));
        Assert.Equal("aarch64-unknown-linux-musl", CodexReleases.Target("linux", "arm64"));
        Assert.Contains("linux-ia32", Assert.Throws<DriverException>(() => CodexReleases.Target("linux", "ia32")).Message);

        CodexReleases.RefuseVersion("0.156.1");
        CodexReleases.RefuseVersion("0.157.0-alpha.3");
        foreach (var version in new[] { "rust-v0.156.1", "latest", "0.156", "0.156.1-rc.1" })
        {
            Assert.Contains("version", Assert.Throws<DriverException>(() => CodexReleases.RefuseVersion(version)).Message);
        }
    }

    [Fact]
    public async Task A_verified_codex_package_is_unpacked_whole_and_bin_codex_is_what_runs()
    {
        var release = CodexRelease("0.156.1", "x86_64-pc-windows-msvc", windows: true);
        var transport = new Served(release.Files);
        var where = HarnessSettings.ManagedHome(_home, "codex", "0.156.1");
        var said = new List<string>();

        var staged = await ReleaseChannels.StageAsync(
            CodexReleases.Channel, where, "0.156.1", said.Add, CancellationToken.None, transport, platform: "x86_64-pc-windows-msvc");
        var (exe, published) = ReleaseChannels.Publish(staged, said.Add);

        Assert.True(published);
        Assert.Equal(Path.Combine(where, "bin", "codex.exe"), exe);
        // Whole: the helpers it finds through its own layout came with it.
        Assert.True(File.Exists(Path.Combine(where, "codex-path", "rg.exe")));
        Assert.True(File.Exists(Path.Combine(where, "codex-package.json")));
        Assert.False(Directory.Exists(staged.Staging));
        // The SUMS file before the package: the package's hash is checked against both.
        Assert.Equal(
            [
                Codex("0.156.1", "release.json"),
                Codex("0.156.1", CodexReleases.Sums),
                Codex("0.156.1", "codex-package-x86_64-pc-windows-msvc.tar.gz"),
            ],
            transport.Asked);
    }

    [Fact]
    public async Task The_vendor_s_real_metadata_and_sums_agree_and_a_package_that_is_neither_s_hash_is_refused()
    {
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [Codex("0.156.1", "release.json")] = File.ReadAllBytes(Path.Combine(Vendor, "codex", "0.156.1", "release.json")),
            [Codex("0.156.1", CodexReleases.Sums)] = File.ReadAllBytes(Path.Combine(Vendor, "codex", "0.156.1", CodexReleases.Sums)),
            [Codex("0.156.1", "codex-package-x86_64-pc-windows-msvc.tar.gz")] = Encoding.UTF8.GetBytes("not the package"),
        });
        var where = HarnessSettings.ManagedHome(_home, "codex", "0.156.1");

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, where, "0.156.1", _ => { }, CancellationToken.None, transport, platform: "x86_64-pc-windows-msvc"));

        Assert.Contains("a2e017db9807e6a2269a26fea0e1d9546469cef4d472a33016bc9f3ad7d3b733", refused.Message);
        // It reached the package, so the real SUMS file matched the real metadata's digest for it.
        Assert.Equal(3, transport.Asked.Count);
        Assert.False(Directory.Exists(where));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(where)!));
    }

    [Fact]
    public async Task A_sums_file_with_one_changed_byte_is_refused_before_the_package_is_fetched()
    {
        var sums = File.ReadAllBytes(Path.Combine(Vendor, "codex", "0.156.1", CodexReleases.Sums));
        sums[0] = sums[0] == (byte)'a' ? (byte)'b' : (byte)'a';
        var transport = new Served(new Dictionary<string, byte[]>
        {
            [Codex("0.156.1", "release.json")] = File.ReadAllBytes(Path.Combine(Vendor, "codex", "0.156.1", "release.json")),
            [Codex("0.156.1", CodexReleases.Sums)] = sums,
        });

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, Path.Combine(_home, "v"), "0.156.1", _ => { }, CancellationToken.None, transport,
            platform: "x86_64-pc-windows-msvc"));

        Assert.Contains("SHA256SUMS", refused.Message);
        Assert.Equal(2, transport.Asked.Count);
    }

    [Fact]
    public async Task Metadata_and_sums_that_disagree_about_the_package_are_refused()
    {
        var release = CodexRelease("0.156.1", "x86_64-unknown-linux-musl", windows: false,
            sums: _ => Encoding.UTF8.GetBytes($"{new string('f', 64)}  codex-package-x86_64-unknown-linux-musl.tar.gz\n"));
        var transport = new Served(release.Files);

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, Path.Combine(_home, "v"), "0.156.1", _ => { }, CancellationToken.None, transport,
            platform: "x86_64-unknown-linux-musl"));

        Assert.Contains("disagree", refused.Message);
        Assert.Equal(2, transport.Asked.Count);
    }

    [Fact]
    public async Task With_no_release_json_the_github_release_is_asked_instead_and_verified_the_same_way()
    {
        var release = CodexRelease("0.156.1", "aarch64-apple-darwin", windows: false);
        var files = new Dictionary<string, byte[]>(release.Files);
        files[$"{CodexReleases.GitHub}/rust-v0.156.1"] = files[Codex("0.156.1", "release.json")];
        files.Remove(Codex("0.156.1", "release.json"));
        var transport = new Served(files);
        var where = HarnessSettings.ManagedHome(_home, "codex", "0.156.1");

        var staged = await ReleaseChannels.StageAsync(
            CodexReleases.Channel, where, "0.156.1", _ => { }, CancellationToken.None, transport, platform: "aarch64-apple-darwin");

        Assert.Equal("bin/codex", staged.Executable);
        Assert.Equal([Codex("0.156.1", "release.json"), $"{CodexReleases.GitHub}/rust-v0.156.1"], transport.Asked.Take(2));
    }

    [Fact]
    public async Task Metadata_for_another_version_is_refused_as_that_version()
    {
        var release = CodexRelease("0.156.1", "x86_64-pc-windows-msvc", windows: true);
        var transport = new Served(new Dictionary<string, byte[]> { [Codex("0.157.0", "release.json")] = release.Files[Codex("0.156.1", "release.json")] });

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, Path.Combine(_home, "v"), "0.157.0", _ => { }, CancellationToken.None, transport,
            platform: "x86_64-pc-windows-msvc"));

        Assert.Contains("rust-v0.156.1", refused.Message);
    }

    [Fact]
    public async Task A_download_address_that_is_not_https_is_refused_before_anything_is_fetched_from_it()
    {
        var release = CodexRelease("0.156.1", "x86_64-pc-windows-msvc", windows: true, address: "http://releases.example.invalid/pkg.tar.gz");
        var transport = new Served(release.Files);

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, Path.Combine(_home, "v"), "0.156.1", _ => { }, CancellationToken.None, transport,
            platform: "x86_64-pc-windows-msvc"));

        Assert.Contains("HTTPS", refused.Message);
        Assert.DoesNotContain("http://releases.example.invalid/pkg.tar.gz", transport.Asked);
    }

    [Fact]
    public async Task A_package_with_no_bin_codex_is_refused_and_nothing_is_left_where_the_pin_points()
    {
        var release = CodexRelease("0.156.1", "x86_64-pc-windows-msvc", windows: true, entries: "codex-package.json={}");
        var where = HarnessSettings.ManagedHome(_home, "codex", "0.156.1");

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.StageAsync(
            CodexReleases.Channel, where, "0.156.1", _ => { }, CancellationToken.None, new Served(release.Files),
            platform: "x86_64-pc-windows-msvc"));

        Assert.Contains("bin/codex.exe", refused.Message);
        Assert.False(Directory.Exists(where));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(where)!));
    }

    [Fact]
    public async Task The_newest_codex_release_is_read_from_its_metadata_s_tag_and_a_pointer_that_is_no_version_is_refused()
    {
        var newest = await ReleaseChannels.LatestAsync(CodexReleases.Channel, CancellationToken.None, new Served(new Dictionary<string, byte[]>
        {
            [CodexReleases.Latest] = Encoding.UTF8.GetBytes("""{"tag_name":"rust-v0.160.0"}"""),
        }));
        Assert.Equal("0.160.0", newest);

        var refused = await Assert.ThrowsAsync<DriverException>(() => ReleaseChannels.LatestAsync(
            CodexReleases.Channel, CancellationToken.None, new Served(new Dictionary<string, byte[]>
            {
                [CodexReleases.Latest] = Encoding.UTF8.GetBytes("""{"tag_name":"nightly"}"""),
            })));
        Assert.Contains("not a version", refused.Message);
    }

    [Fact]
    public async Task The_window_pins_codex_from_its_maker_s_channel()
    {
        var toolchain = CodexAccounts.Toolchain;
        Assert.Equal(CodexReleases.Channel, toolchain.Channel);
        var release = CodexRelease("0.156.1", CodexReleaseDownloads.Current(), windows: OperatingSystem.IsWindows());
        var transport = new Served(release.Files);

        var code = await HarnessActions.PinAsync(toolchain, _home, "codex", "0.156.1", _ => { }, CancellationToken.None, transport: transport);

        Assert.Equal(0, code);
        Assert.NotNull(HarnessSettings.ManagedBinary(_home, "codex", "0.156.1", toolchain.Binary));
    }

    // ——— Each run stages under its own name (the alone-path design §1.4).

    [Fact]
    public async Task Two_runs_of_one_version_each_stage_their_own_and_the_second_keeps_the_first_s()
    {
        using var stranger = Pgp.Stranger();
        var binary = Encoding.UTF8.GetBytes("MZ-one");
        var where = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.300");
        var trust = (stranger.Armoured, stranger.Fingerprint);

        var first = await ReleaseChannels.StageAsync(ClaudeReleases.Channel, where, "2.1.300", _ => { }, CancellationToken.None,
            new Served(Release(stranger, "2.1.300", "win32-x64", binary)), "win32-x64", trust, process: 101, running: _ => true);
        var second = await ReleaseChannels.StageAsync(ClaudeReleases.Channel, where, "2.1.300", _ => { }, CancellationToken.None,
            new Served(Release(stranger, "2.1.300", "win32-x64", binary)), "win32-x64", trust, process: 202, running: _ => true);

        Assert.NotEqual(first.Staging, second.Staging);
        Assert.True(ReleaseChannels.Publish(first, _ => { }).Published);
        var said = new List<string>();
        Assert.False(ReleaseChannels.Publish(second, said.Add).Published);
        Assert.Contains(said, line => line.Contains("meanwhile"));
        Assert.Equal([where], Directory.EnumerateDirectories(Path.GetDirectoryName(where)!));
    }

    [Fact]
    public async Task A_dead_run_s_staging_is_swept_and_a_living_one_s_kept()
    {
        using var stranger = Pgp.Stranger();
        var where = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.300");
        var dead = ReleaseChannels.StagingOf(where, 7);
        var living = ReleaseChannels.StagingOf(HarnessSettings.ManagedHome(_home, "claude-code", "2.1.301"), 8);
        Directory.CreateDirectory(dead);
        Directory.CreateDirectory(living);

        await ReleaseChannels.StageAsync(ClaudeReleases.Channel, where, "2.1.300", _ => { }, CancellationToken.None,
            new Served(Release(stranger, "2.1.300", "win32-x64", [1])), "win32-x64", (stranger.Armoured, stranger.Fingerprint),
            process: 9, running: process => process == 8);

        Assert.False(Directory.Exists(dead));
        Assert.True(Directory.Exists(living));
    }

    [Fact]
    public async Task A_stop_midway_leaves_no_staging()
    {
        using var stranger = Pgp.Stranger();
        var where = HarnessSettings.ManagedHome(_home, "claude-code", "2.1.300");
        using var stop = new CancellationTokenSource();
        var transport = new Served(Release(stranger, "2.1.300", "win32-x64", [1])) { Asking = stop.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReleaseChannels.StageAsync(
            ClaudeReleases.Channel, where, "2.1.300", _ => { }, stop.Token, transport, "win32-x64",
            (stranger.Armoured, stranger.Fingerprint)));

        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(where)!));
    }

    /// <summary>A Codex release in the vendor's metadata shape, over a package built here.</summary>
    private static (Dictionary<string, byte[]> Files, byte[] Package) CodexRelease(
        string version, string target, bool windows, Func<byte[], byte[]>? sums = null, string? address = null, string? entries = null)
    {
        var exe = windows ? ".exe" : "";
        var package = ToolInstallTests.Archive.Build(
            "tar.gz", entries ?? $"codex-package.json={{\"layout\":1}}; bin/codex{exe}=codex; codex-path/rg{exe}=rg", "");
        var name = CodexReleases.Package(target);
        var listed = sums?.Invoke(package)
            ?? Encoding.UTF8.GetBytes($"{new string('0', 64)}  codex-app-server-package-{target}.tar.gz\n{Sha(package)}  {name}\n");
        var metadata = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            tag_name = $"rust-v{version}",
            assets = new object[]
            {
                new { name, digest = $"sha256:{Sha(package)}", browser_download_url = address ?? Codex(version, name) },
                new { name = CodexReleases.Sums, digest = $"sha256:{Sha(listed)}", browser_download_url = Codex(version, CodexReleases.Sums) },
            },
        });
        return (new Dictionary<string, byte[]>
        {
            [Codex(version, "release.json")] = metadata,
            [Codex(version, name)] = package,
            [Codex(version, CodexReleases.Sums)] = listed,
        }, package);
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // ——— Helpers.

    private static byte[] Tamper(byte[] manifest)
    {
        var tampered = (byte[])manifest.Clone();
        var text = Encoding.UTF8.GetString(tampered);
        var at = text.IndexOf("\"checksum\"", StringComparison.Ordinal) + "\"checksum\": \"".Length;
        tampered[at] = tampered[at] == (byte)'a' ? (byte)'b' : (byte)'a';
        return tampered;
    }

    private static Dictionary<string, byte[]> Release(Pgp.Held stranger, string version, string platform, byte[] binary)
    {
        var name = platform.StartsWith("win32", StringComparison.Ordinal) ? "claude.exe" : "claude";
        var manifest = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            version,
            platforms = new Dictionary<string, object>
            {
                [platform] = new { binary = name, checksum = Convert.ToHexStringLower(SHA256.HashData(binary)), size = binary.Length },
            },
        });
        return new Dictionary<string, byte[]>
        {
            [$"{ClaudeReleases.Base}/{version}/manifest.json"] = manifest,
            [$"{ClaudeReleases.Base}/{version}/manifest.json.sig"] = Encoding.UTF8.GetBytes(Pgp.Sign(manifest, stranger.Key, stranger.Fingerprint)),
            [$"{ClaudeReleases.Base}/{version}/{platform}/{name}"] = binary,
        };
    }

    /// <summary>The vendor's files, found by walking up to the CLI's fixtures — one copy for both artefacts.</summary>
    private static string VendorFixtures()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "vendor");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("the vendor fixtures were not found above the test binaries");
    }

    /// <summary>A transport over a table of URLs, and the URLs it was asked for, in order.</summary>
    private sealed class Served(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        /// <summary>Called as each address is asked: a test's stop midway.</summary>
        public Action? Asking { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Asked.Add(url);
            Asking?.Invoke();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(files.TryGetValue(url, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

/// <summary>
/// A key and a signature made HERE, in the vendor's format — written from the RFC rather than from
/// the verifier, so the two can disagree. The CLI's <c>test/_openpgp.ts</c> is the twin.
/// </summary>
internal static class Pgp
{
    public sealed class Held(RSA key, string armoured, string fingerprint) : IDisposable
    {
        public RSA Key { get; } = key;
        public string Armoured { get; } = armoured;
        public string Fingerprint { get; } = fingerprint;
        public void Dispose() => Key.Dispose();
    }

    public static Held Stranger()
    {
        var key = RSA.Create(2048);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var body = Concat([4, 0x60, 0, 0, 0, 1], Mpi(parameters.Modulus!), Mpi(parameters.Exponent!));
        var fingerprint = Convert.ToHexString(SHA1.HashData(Concat([0x99, (byte)(body.Length >> 8), (byte)body.Length], body)));
        return new Held(key, Armour("PUBLIC KEY BLOCK", Packet(6, body)), fingerprint);
    }

    public static string Sign(byte[] data, RSA key, string issuer)
    {
        var hashed = Concat([5, 2, 0x68, 0, 0, 0], [22, 33, 4], Convert.FromHexString(issuer));
        var hashedPart = Concat([4, 0x00, 1, 10, (byte)(hashed.Length >> 8), (byte)hashed.Length], hashed);
        var trailer = new byte[] { 4, 0xff, 0, 0, 0, (byte)hashedPart.Length };
        var signed = Concat(data, hashedPart, trailer);

        var left16 = SHA512.HashData(signed)[..2];
        var value = key.SignData(signed, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
        return Armour("SIGNATURE", Packet(2, Concat(hashedPart, [0, 0], left16, Mpi(value))));
    }

    private static byte[] Mpi(byte[] value)
    {
        var start = 0;
        while (start < value.Length - 1 && value[start] == 0) start++;
        var trimmed = value[start..];
        var bits = (trimmed.Length - 1) * 8 + (32 - int.LeadingZeroCount(trimmed[0]));
        return Concat([(byte)(bits >> 8), (byte)bits], trimmed);
    }

    private static byte[] Packet(int tag, byte[] body) =>
        Concat([(byte)(0xc0 | tag), 0xff, (byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length], body);

    private static string Armour(string kind, byte[] bytes) =>
        $"-----BEGIN PGP {kind}-----\n\n{string.Join('\n', Convert.ToBase64String(bytes).Chunk(64).Select(chunk => new string(chunk)))}\n-----END PGP {kind}-----\n";

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();
}
