using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1l (the agent-provider design §11 row l, §3.2's <c>ModelRefused</c>): a model the protocol open refuses is one reading on the
/// run's sink, said before the open's re-ask walks on, and the offers store's part keeps it as the account's refusal, so the walk the re-ask
/// runs passes that account for that model while another account of the same door and version is tried.
/// </summary>
public sealed class OfferRefusalsTests : IDisposable
{
    private static readonly ModelRefusal Refused = new("model", "opus-9", "not-offered");

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-offer-refusals-" + Guid.NewGuid().ToString("N")[..8]);

    public OfferRefusalsTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private sealed class Heard(List<string> order) : IReadingSink
    {
        public bool Watched => false;

        public void Read(WireReading reading)
        {
            if (reading is WireReading.ModelRefused refused) order.Add("reading " + refused.Refusal.Value);
        }
    }

    private sealed class Walk(List<string> order) : IChoiceReask
    {
        public Task<ActorAsked?> ReaskAsync(ModelRefusal refusal)
        {
            order.Add("walk " + refusal.Value);
            return Task.FromResult<ActorAsked?>(null);
        }
    }

    [Fact]
    public async Task The_open_s_refused_model_is_a_reading_said_before_its_re_ask_walks_on()
    {
        List<string> order = [];
        var port = RefusalReading.Of(new Walk(order), new Heard(order))!;

        Assert.Null(await port.ReaskAsync(Refused));

        Assert.Equal(["reading opus-9", "walk opus-9"], order);
        Assert.Null(RefusalReading.Of(null, new Heard(order)));
    }

    [Fact]
    public void The_offers_store_s_part_keeps_the_refusal_on_its_account_alone()
    {
        var part = new OfferRefusals(_home, "claude-code-acp", "1.2.0", "work");

        part.Read(new WireReading.ModelRefused(Refused));
        part.Read(new WireReading.ModelRefused(Refused with { Value = "replayed" }) { Replayed = true });

        var readings = AgentOffers.Read(_home);
        var now = DateTimeOffset.UtcNow;
        Assert.NotNull(AgentOffers.Offers(readings, "claude-code-acp", "1.2.0", "work", now).Session.Judge("opus-9"));
        Assert.Null(AgentOffers.Offers(readings, "claude-code-acp", "1.2.0", "home", now).Session.Judge("opus-9"));
        Assert.Null(AgentOffers.Offers(readings, "claude-code-acp", "1.2.0", "work", now).Session.Judge("replayed"));
    }
}
