using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACCOUNTID1 (D66's and D125's ACCOUNTID1 notes): one email may sign in to two organisations, so the status answer's
/// organisation and plan are read beside who signed in, on a yes only, and none where the agent names none. Read fresh and
/// written nowhere: the roster holds them in memory, as it holds who.
/// </summary>
/// <remarks>Nothing here starts a process: each answer is read as text, by the declaration the probe asks with.</remarks>
public sealed class SignInOrgTests
{
    /// <summary>The shape <c>claude auth status</c> answers in: keys measured on the real binary (2026-09-23), values invented.</summary>
    private const string Claude = """
        {
          "loggedIn": true,
          "authMethod": "claude.ai",
          "email": "person@example.invalid",
          "orgName": "Org A",
          "subscriptionType": "max"
        }
        """;

    private static LoginQuestion ClaudeQuestion => new ClaudeCodeAdapter().Toolchain!.LoginCheck!;

    [Fact]
    public void Claude_code_s_answer_reads_who_the_organisation_and_the_plan()
    {
        Assert.Equal(new LoginRead(LoginState.In, "person@example.invalid", "Org A", "max"), ClaudeQuestion.Read(Claude));
    }

    /// <summary>An older CLI, a key's answer, or a stand-in answering only the boolean: no organisation, so none is shown.</summary>
    [Theory]
    [InlineData("""{ "loggedIn": true }""")]
    [InlineData("""{ "loggedIn": true, "authMethod": "api_key", "orgName": null, "subscriptionType": null }""")]
    public void An_answer_naming_no_organisation_reads_none(string answer)
    {
        var said = ClaudeQuestion.Read(answer);

        Assert.Equal(LoginState.In, said.Login);
        Assert.Null(said.Org);
        Assert.Null(said.Plan);
    }

    /// <summary>A signed-out home names nobody, and so no organisation either, whatever the tool prints beside its "no".</summary>
    [Fact]
    public void A_signed_out_answer_names_no_organisation_whatever_it_prints()
    {
        Assert.Equal(
            new LoginRead(LoginState.Out, null),
            ClaudeQuestion.Read(Claude.Replace("\"loggedIn\": true", "\"loggedIn\": false", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The stub's way to say one (ACCOUNTID1), so a test and a rehearsal can sign one email in to two organisations with no
    /// account: a line of its own for each, after who; an answer saying neither reads as before.
    /// </summary>
    [Theory]
    [InlineData("logged-in as person@example.invalid\norg: Org B\nplan: pro\n", "Org B", "pro")]
    [InlineData("logged-in as person@example.invalid\r\norg: Org B\r\nplan: pro\r\n", "Org B", "pro")]
    [InlineData("logged-in as person@example.invalid\norg: Org B\n", "Org B", null)]
    [InlineData("logged-in as person@example.invalid", null, null)]
    public void The_stub_says_an_organisation_and_a_plan_on_lines_of_their_own(string answer, string? org, string? plan)
    {
        Assert.Equal(
            new LoginRead(LoginState.In, "person@example.invalid", org, plan),
            new StubAdapter().Toolchain!.LoginCheck!.Read(answer));
    }
}
