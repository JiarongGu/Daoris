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
/// <para>Codex is the CLI's alone: the driver declares no <c>codex</c> toolchain (its door is
/// <c>codex-acp</c>, an npm package), so there is no Codex pin for a screen to offer.</para>
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
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
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

        var executable = await ClaudeReleases.InstallAsync(
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

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleases.InstallAsync(
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

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleases.InstallAsync(
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

        var error = await Assert.ThrowsAsync<DriverException>(() => ClaudeReleases.InstallAsync(
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
            var candidate = Path.Combine(directory.FullName, "src", "Daoris.Cli", "test", "fixtures", "vendor");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("the vendor fixtures were not found above the test binaries");
    }

    /// <summary>A transport over a table of URLs, and the URLs it was asked for, in order.</summary>
    private sealed class Served(Dictionary<string, byte[]> files) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var url = request.RequestUri!.ToString();
            Asked.Add(url);
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
