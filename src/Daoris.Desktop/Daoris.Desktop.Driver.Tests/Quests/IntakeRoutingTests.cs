using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AUTONOMY1c (D184 points 2, 3 and 5): intake routing follows the access setup of the repository the intake chooses. Under
/// <c>person</c>, today's, an intake whose declarations do not settle an ask publishes nothing and asks the person, who accepts a
/// route; under <c>intake</c>, and so under full access, it publishes to its best choice among the repositories set so, saying in
/// the body that the route was its own, and the person may route it again. On the install an ask waited for its route to be
/// accepted before its session started.
/// </summary>
public sealed class IntakeRoutingTests
{
    private const string Today =
        "When they do not settle it — nothing points at one repository, or more than one could — do not guess, and publish "
        + "nothing. End by saying plainly what you would need to know; the person decides.";

    private const string Chooses =
        "the person has set them to take your choice of route without accepting it first";

    private static readonly string[] Circle = ["media-api", "storefront", "importer"];

    private static AskView Ask() => new("a1b2c3", "work", "the video field name is hard coded", "Proposed", "declarations")
    {
        Proposed = ["media-api"],
    };

    private static string Flat(string prompt) => System.Text.RegularExpressions.Regex.Replace(prompt, @"\s+", " ");

    private static AccessSetting Routing(string value) =>
        new() { Pauses = new Dictionary<string, string>(StringComparer.Ordinal) { [AccessRules.Routing] = value } };

    private static DriverConfig Config(
        IReadOnlyDictionary<string, AccessSetting>? repositories = null, IReadOnlyDictionary<string, AccessSetting>? workspaces = null,
        AccessSetting? machine = null) =>
        DriverConfig.Empty with
        {
            Access = repositories ?? new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase),
            WorkspaceAccess = workspaces ?? new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase),
            MachineAccess = machine,
        };

    private static string Composed(DriverConfig config) =>
        Flat(IntakePrompt.Compose(Ask(), routedByIntake: IntakePrompt.RoutedByIntake(config, "work", Circle)));

    [Fact]
    public void Person_asks_the_person_where_the_declarations_do_not_settle_it()
    {
        var prompt = Composed(Config());

        Assert.Contains(Today, prompt);
        Assert.DoesNotContain(Chooses, prompt);
        Assert.Empty(IntakePrompt.RoutedByIntake(Config(), "work", Circle));
        // Nothing set reads exactly as it did.
        Assert.Equal(IntakePrompt.Compose(Ask()), IntakePrompt.Compose(Ask(), routedByIntake: []));
    }

    [Fact]
    public void Intake_publishes_to_its_choice_among_the_repositories_set_so_and_says_it_was_its_own()
    {
        var config = Config(new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["media-api"] = Routing(AccessRules.Intake) });
        var prompt = Composed(config);

        Assert.Equal(["media-api"], IntakePrompt.RoutedByIntake(config, "work", Circle));
        Assert.Contains($"`media-api`: {Chooses}.", prompt);
        Assert.Contains(
            "Where the one that fits best is among them, publish to it as above, and begin the body by saying the route was your "
            + "choice, why, and which other repositories could own it: the person may route it again.",
            prompt);
        // Any other is still the person's to settle.
        Assert.Contains("Otherwise do not guess, and publish nothing. End by saying plainly what you would need to know; the person decides.", prompt);
        Assert.DoesNotContain(Today, prompt);
    }

    [Fact]
    public void Full_access_routes_by_the_intake_and_the_chosen_repositorys_own_setup_counts()
    {
        var workspaceFull = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["work"] = AccessSetting.FullAccess };
        var storefrontAsks = new Dictionary<string, AccessSetting>(StringComparer.OrdinalIgnoreCase) { ["storefront"] = Routing(AccessRules.Person) };

        Assert.Equal(Circle, IntakePrompt.RoutedByIntake(Config(machine: AccessSetting.FullAccess), "work", Circle));
        Assert.Equal(Circle, IntakePrompt.RoutedByIntake(Config(workspaces: workspaceFull), "work", Circle));
        // The repository's own choice outranks its workspace's full access: the route to it is the person's.
        Assert.Equal(["media-api", "importer"], IntakePrompt.RoutedByIntake(Config(storefrontAsks, workspaceFull), "work", Circle));
        Assert.Contains($"`media-api`, `storefront`, `importer`: {Chooses}.", Composed(Config(machine: AccessSetting.FullAccess)));
        // Another workspace's setup is not this one's.
        Assert.Empty(IntakePrompt.RoutedByIntake(Config(workspaces: workspaceFull), "home", Circle));
    }
}
