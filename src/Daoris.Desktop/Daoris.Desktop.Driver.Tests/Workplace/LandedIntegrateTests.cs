using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// INTEGRATE1e part e1 (D215's note; the integration design §17.4, F40 and C30): the landing record keeps its acceptance's provenance,
/// that <c>integrate</c> stood, its <c>request</c> there and whether it was the rule's or the bound version's step, on the landing and on
/// each advance beside its own <c>acceptedBy</c>. A record written without it, by an older build or before the field, says neither, and a
/// shape the reader does not know is read as neither too, so a rewrite can only under-admit. Files only; the landing's own write over real
/// git is <c>IntegrationRequestsGitTests</c>'.
/// </summary>
public sealed class LandedIntegrateTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly string _scratch = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "landed-integrate", Guid.NewGuid().ToString("N")[..8]);

    private string Home => Path.Combine(_scratch, "daoris-home");

    public LandedIntegrateTests() => Directory.CreateDirectory(Home);

    public void Dispose() => GitSeed.Discard(_scratch);

    private string FileText() => File.ReadAllText(DaorisHome.Entry(Home, HomeEntry.Landings));

    private static LandedBranch Entry(string branch, string session) =>
        new("engine", "aurora", branch, "main", "tip00001", session, "q1", "Fix the gap", At) { AcceptedBy = AcceptedBy.Person };

    /// <summary>The landing's and the advance's own provenance round trip, and a later write of the file keeps both.</summary>
    [Fact]
    public void A_landing_and_an_advance_keep_their_own_provenance_through_every_write()
    {
        var landings = new LandedBranches(Home);
        landings.Record(Entry("feature/q1-fix", "s1") with { Integrate = new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule) });
        landings.Advanced("engine", "feature/q1-fix", new LandedAdvance("tip00001", "tip00002", At, "s2")
        {
            AcceptedBy = AcceptedBy.Auto, Integrate = new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule),
        });

        Assert.Matches("\"integrate\": \\{\\s*\"request\": \"owner\",\\s*\"source\": \"rule\"\\s*\\}", FileText());
        Assert.Matches("\"acceptedBy\": \"auto\",\\s*\"integrate\": \\{\\s*\"request\": \"done\",\\s*\"source\": \"rule\"", FileText());

        var read = new LandedBranches(Home).Of("engine", "feature/q1-fix")!;
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule), read.Integrate);
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule), Assert.Single(read.Advances).Integrate);

        // A plugin's answer rewrites the file whole: both stay, each equal to itself read twice.
        landings.Pushed("engine", "feature/q1-fix", new PluginLanding("acme.lands", true, null, "pushed"), "tip00002");
        var again = new LandedBranches(Home).Of("engine", "feature/q1-fix")!;
        Assert.Equal(read.Integrate, again.Integrate);
        Assert.Equal(read.Advances[0], again.Advances[0]);
    }

    /// <summary>
    /// A landing or an advance made where no <c>integrate</c> stood writes nothing, and a file from before the field, or one an older build
    /// rewrote and so dropped it, reads as a record that says neither.
    /// </summary>
    [Fact]
    public void A_record_without_the_field_says_neither_on_the_landing_and_its_advance()
    {
        var landings = new LandedBranches(Home);
        landings.Record(Entry("feature/q2-fix", "s3"));
        landings.Advanced("engine", "feature/q2-fix", new LandedAdvance("tip00001", "tip00002", At, "s4") { AcceptedBy = AcceptedBy.Person });

        Assert.DoesNotContain("\"integrate\"", FileText(), StringComparison.Ordinal);
        var unwritten = new LandedBranches(Home).Of("engine", "feature/q2-fix")!;
        Assert.Null(unwritten.Integrate);
        Assert.Null(Assert.Single(unwritten.Advances).Integrate);

        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Landings), """
            { "branches": [ { "repository": "engine", "workspace": "aurora", "branch": "feature/q9-old", "tip": "tip00002", "session": "s1",
              "landedAt": "2026-10-30T12:00:00.0000000+00:00", "acceptedBy": "person",
              "rule": { "autoAccept": false, "source": "repository" },
              "advances": [ { "from": "tip00001", "to": "tip00002", "at": "2026-10-30T12:00:00.0000000+00:00", "session": "s2",
                              "acceptedBy": "auto" } ] } ] }
            """);
        var older = new LandedBranches(Home).Of("engine", "feature/q9-old")!;
        Assert.Null(older.Integrate);
        Assert.Null(Assert.Single(older.Advances).Integrate);
    }

    /// <summary>
    /// A provenance whose shape does not read, half of it or not an object, says neither; a key it does not know is passed over, as an
    /// older reader passes over the whole field, and the values are kept as written for the reader that judges them.
    /// </summary>
    [Theory]
    [InlineData("\"owner\"", null, null)]
    [InlineData("{ \"request\": \"owner\" }", null, null)]
    [InlineData("{ \"source\": \"rule\" }", null, null)]
    [InlineData("{ \"request\": \"\", \"source\": \"rule\" }", null, null)]
    [InlineData("{ \"request\": 1, \"source\": \"rule\" }", null, null)]
    [InlineData("{ \"request\": \"steward\", \"source\": \"version\", \"later\": { \"kept\": true } }", "steward", "version")]
    [InlineData("{ \"request\": \"someday\", \"source\": \"rule\" }", "someday", "rule")]
    public void A_provenance_reads_whole_or_says_neither(string written, string? request, string? source)
    {
        File.WriteAllText(DaorisHome.Entry(Home, HomeEntry.Landings), $$"""
            { "branches": [ { "repository": "engine", "branch": "feature/q1", "tip": "tip00001", "session": "s1",
              "landedAt": "2026-10-30T12:00:00.0000000+00:00", "integrate": {{written}},
              "advances": [ { "from": "tip00000", "to": "tip00001", "at": "2026-10-30T12:00:00.0000000+00:00", "session": "s2",
                              "integrate": {{written}} } ] } ] }
            """);

        var read = new LandedBranches(Home).Of("engine", "feature/q1")!;
        var expected = request is null ? null : new LandedIntegrate(request, source!);
        Assert.Equal(expected, read.Integrate);
        Assert.Equal(expected, Assert.Single(read.Advances).Integrate);
    }

    /// <summary>
    /// What a landing writes (§17.4): under Current, where the effective rule carries <c>integrate</c>, its <c>request</c> resolved through
    /// the design's <i>Absent</i> column and the rule as its source; nothing where the rule carries none; and nothing of the rule's under a
    /// named version, whose own <c>integrate</c> step (row n) is the unit's process there.
    /// </summary>
    [Fact]
    public void The_provenance_is_the_rules_integrate_under_Current_and_none_where_none_stood()
    {
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Owner, LandedIntegrate.Rule), LandedIntegrate.Of(new LandingIntegrate(), named: false));
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Done, LandedIntegrate.Rule),
            LandedIntegrate.Of(new LandingIntegrate(Request: LandingIntegrate.Done, Gates: LandingIntegrate.Declared), named: false));
        Assert.Equal(new LandedIntegrate(LandingIntegrate.Steward, LandedIntegrate.Rule),
            LandedIntegrate.Of(new LandingIntegrate(Request: LandingIntegrate.Steward, Gates: LandingIntegrate.Declared), named: false));
        Assert.Null(LandedIntegrate.Of(null, named: false));
        Assert.Null(LandedIntegrate.Of(new LandingIntegrate(Request: LandingIntegrate.Owner), named: true));
    }
}
