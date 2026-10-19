using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HEAVYRUN1a2 (D180's HEAVYRUN1 note): a heavy gate's test runner is held to two workers where the repository declares it Jest or
/// Vitest, at an installed version whose worker flag is known, and runs as written, saying why, everywhere else; an owner's own bound
/// is never doubled. Reading the repository's files starts no process, so these are the fast half's; <see cref="HeavyRunProcessTests"/>
/// shows the bound reaching the runner through npm.
/// </summary>
public sealed class HeavyWorkersTests : IDisposable
{
    private readonly HeavyFixture _fixture = new("heavy-workers");

    public void Dispose() => _fixture.Dispose();

    /// <summary>A runner the gate names itself, or through <c>npx</c>, at each version whose flag was read: the flag at its end.</summary>
    [Theory]
    [InlineData("jest", "jest@27.5.1", "jest --maxWorkers=2", "Jest 27.5.1")]
    [InlineData("jest --ci", "jest@29.7.0", "jest --ci --maxWorkers=2", "Jest 29.7.0")]
    [InlineData("npx jest", "jest@30.5.2", "npx jest --maxWorkers=2", "Jest 30.5.2")]
    [InlineData("JEST \"src/a&b\"", "jest@30.3.0", "JEST \"src/a&b\" --maxWorkers=2", "Jest 30.3.0")]
    [InlineData("vitest run", "vitest@2.1.9", "vitest run --maxWorkers=2", "Vitest 2.1.9")]
    [InlineData("vitest run --coverage", "vitest@3.2.4", "vitest run --coverage --maxWorkers=2", "Vitest 3.2.4")]
    [InlineData("npx vitest run", "vitest@4.1.11", "npx vitest run --maxWorkers=2", "Vitest 4.1.11")]
    [InlineData("vitest run  ", "vitest@5.0.1", "vitest run --maxWorkers=2", "Vitest 5.0.1")]
    public void A_runner_the_gate_names_is_bounded_at_a_version_whose_flag_is_known(string run, string installed, string bound, string runs)
    {
        Install(_fixture.Checkout, installed);

        var said = Bind(run);

        Assert.Equal(bound, said.Run);
        Assert.Equal($"`unit` runs {runs}, so its workers are bounded to two (`--maxWorkers=2`).", said.Said);
    }

    /// <summary>
    /// <c>npm test</c> and <c>npm run &lt;script&gt;</c> are read through the folder's own <c>scripts</c>: one command whose first word is
    /// the runner is bounded through npm, after its <c>--</c>, or after the one the gate already has.
    /// </summary>
    [Theory]
    [InlineData("npm test", "test", "jest --ci", "jest@29.7.0", "npm test -- --maxWorkers=2", "Jest 29.7.0", "test")]
    [InlineData("npm t", "test", "jest", "jest@30.5.2", "npm t -- --maxWorkers=2", "Jest 30.5.2", "test")]
    [InlineData("npm run test:unit", "test:unit", "vitest run", "vitest@5.0.1", "npm run test:unit -- --maxWorkers=2", "Vitest 5.0.1", "test:unit")]
    [InlineData("npm run-script unit --silent", "unit", "vitest", "vitest@4.1.5", "npm run-script unit --silent -- --maxWorkers=2", "Vitest 4.1.5", "unit")]
    [InlineData("npm test -- --coverage", "test", "jest", "jest@27.5.1", "npm test -- --coverage --maxWorkers=2", "Jest 27.5.1", "test")]
    public void An_npm_script_that_is_one_runner_command_is_bounded_through_npm(
        string run, string script, string declared, string installed, string bound, string runs, string named)
    {
        _fixture.Scripts(_fixture.Checkout, (script, declared));
        Install(_fixture.Checkout, installed);

        var said = Bind(run);

        Assert.Equal(bound, said.Run);
        Assert.Equal($"`unit` runs {runs} through npm's `{named}` script, so its workers are bounded to two (`--maxWorkers=2`).", said.Said);
    }

    /// <summary>
    /// A version whose worker flag was not read here runs as written, saying so: one between or beyond those read, and a prerelease,
    /// which promises nothing of its major's flags.
    /// </summary>
    [Theory]
    [InlineData("jest", "jest@28.1.3", "Jest 28.1.3", "Jest")]
    [InlineData("jest", "jest@26.6.3", "Jest 26.6.3", "Jest")]
    [InlineData("jest", "jest@31.0.0", "Jest 31.0.0", "Jest")]
    [InlineData("vitest run", "vitest@1.6.0", "Vitest 1.6.0", "Vitest")]
    [InlineData("vitest run", "vitest@6.0.0", "Vitest 6.0.0", "Vitest")]
    [InlineData("vitest run", "vitest@5.0.0-beta.2", "Vitest 5.0.0-beta.2", "Vitest")]
    [InlineData("vitest run", "vitest@next", "Vitest next", "Vitest")]
    public void A_version_whose_worker_flag_is_not_known_here_runs_as_written_saying_so(string run, string installed, string runs, string runner)
    {
        Install(_fixture.Checkout, installed);

        var said = Bind(run);

        Assert.Equal(run, said.Run);
        Assert.Equal($"`unit` runs {runs}, and this {runner} version's worker flag is not known here, so it runs as written.", said.Said);
    }

    /// <summary>The same through an npm script: the version is the one installed where the script runs.</summary>
    [Fact]
    public void An_npm_script_s_runner_at_an_unknown_version_runs_as_written_saying_so()
    {
        _fixture.Scripts(_fixture.Checkout, ("test", "jest --ci"));
        Install(_fixture.Checkout, "jest@28.1.3");

        var said = Bind("npm test");

        Assert.Equal("npm test", said.Run);
        Assert.Equal(
            "`unit` runs Jest 28.1.3 through npm's `test` script, and this Jest version's worker flag is not known here, so it runs as written.",
            said.Said);
    }

    /// <summary>
    /// 🔴 Never doubled: a command or a script that already names a worker bound or a pool keeps the owner's, untouched, at a version
    /// whose flag is known. <c>npm test -- --maxWorkers=2</c> is the bound an owner writes into this machine's mapping (D180's HEAVYRUN1b2a note).
    /// </summary>
    [Theory]
    [InlineData("jest --maxWorkers=4", null, "`unit` names its own worker bound (`--maxWorkers=4`), so it runs as written.")]
    [InlineData("jest --maxWorkers 4", null, "`unit` names its own worker bound (`--maxWorkers`), so it runs as written.")]
    [InlineData("jest -w 1", null, "`unit` names its own worker bound (`-w`), so it runs as written.")]
    [InlineData("jest -w3", null, "`unit` names its own worker bound (`-w3`), so it runs as written.")]
    [InlineData("vitest run --max-workers=3", null, "`unit` names its own worker bound (`--max-workers=3`), so it runs as written.")]
    [InlineData("vitest run --pool=forks", null, "`unit` names its own worker bound (`--pool=forks`), so it runs as written.")]
    [InlineData("vitest run --poolOptions.forks.singleFork", null, "`unit` names its own worker bound (`--poolOptions.forks.singleFork`), so it runs as written.")]
    [InlineData("npm test -- --maxWorkers=2", "jest", "`unit` names its own worker bound (`--maxWorkers=2`), so it runs as written.")]
    [InlineData("npm test", "jest --maxWorkers=50%", "`unit`'s npm script `test` names its own worker bound (`--maxWorkers=50%`), so it runs as written.")]
    [InlineData("npm test", "vitest run --pool threads", "`unit`'s npm script `test` names its own worker bound (`--pool`), so it runs as written.")]
    public void An_owner_s_own_bound_is_kept_and_never_doubled(string run, string? script, string said)
    {
        if (script is not null) _fixture.Scripts(_fixture.Checkout, ("test", script));
        Install(_fixture.Checkout, "jest@29.7.0");
        Install(_fixture.Checkout, "vitest@5.0.1");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>A chain, in the gate or its script, runs as written: which of its commands a bound added at its end would reach is not told.</summary>
    [Theory]
    [InlineData("npm ci && npm test", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("jest || exit 0", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("jest; echo done", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("jest | more", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("jest & echo done", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("jest > out.txt", null, "`unit` is more than one command, so it runs as written.")]
    [InlineData("npm test", "tsc -b && jest", "`unit`'s npm script `test` is more than one command, so it runs as written.")]
    [InlineData("npm test", "vitest run | tee out.txt", "`unit`'s npm script `test` is more than one command, so it runs as written.")]
    public void A_chain_in_the_gate_or_its_script_runs_as_written(string run, string? script, string said)
    {
        if (script is not null) _fixture.Scripts(_fixture.Checkout, ("test", script));
        Install(_fixture.Checkout, "jest@29.7.0");
        Install(_fixture.Checkout, "vitest@5.0.1");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>Angular's <c>ng test</c> and every other runner, in the gate or its script, run as written, named.</summary>
    [Theory]
    [InlineData("", null, "`unit` names no command, so it runs as written.")]
    [InlineData("dotnet test", null, "`unit` runs `dotnet`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("node tools/check.mjs", null, "`unit` runs `node`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("npx playwright test", null, "`unit` runs `npx playwright`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("npm ci", null, "`unit` runs `npm ci`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("ng test --watch=false", null, "`unit` runs Angular's `ng test`, whose builder takes its own flags, so it runs as written.")]
    [InlineData("npx ng test", null, "`unit` runs Angular's `ng test`, whose builder takes its own flags, so it runs as written.")]
    [InlineData("npm test", "ng test --watch=false", "`unit`'s npm script `test` runs Angular's `ng test`, whose builder takes its own flags, so it runs as written.")]
    [InlineData("npm test", "cross-env CI=1 jest", "`unit`'s npm script `test` runs `cross-env`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("npm test", "mocha", "`unit`'s npm script `test` runs `mocha`, neither Jest nor Vitest, so it runs as written.")]
    public void Angular_and_every_other_runner_run_as_written_named(string run, string? script, string said)
    {
        if (script is not null) _fixture.Scripts(_fixture.Checkout, ("test", script));
        Install(_fixture.Checkout, "jest@29.7.0");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>A script that cannot be read is never guessed at: no package.json, one that is not JSON, or one that declares no such script.</summary>
    [Fact]
    public void A_script_that_cannot_be_read_runs_as_written_saying_why()
    {
        Install(_fixture.Checkout, "jest@29.7.0");

        Assert.Equal(
            "`unit` runs npm's `test` script, and its folder has no package.json, so it runs as written.",
            Bind("npm test").Said);

        File.WriteAllText(Path.Combine(_fixture.Checkout, "package.json"), "{ \"scripts\": ");
        Assert.Equal(
            "`unit` runs npm's `test` script, and its folder's package.json is not valid JSON, so it runs as written.",
            Bind("npm test").Said);

        _fixture.Scripts(_fixture.Checkout, ("lint", "eslint ."));
        var none = Bind("npm run unit");
        Assert.Equal("npm run unit", none.Run);
        Assert.Equal("`unit` runs npm's `unit` script, and its folder's package.json declares no `unit` script, so it runs as written.", none.Said);

        Assert.Equal("`unit` runs `npm run` with no script named after it, so it runs as written.", Bind("npm run --silent").Said);
    }

    /// <summary>npm pointed at another package's script reads a package.json that is not the folder's, so it runs as written.</summary>
    [Theory]
    [InlineData("npm test -w web", "-w")]
    [InlineData("npm test --workspace=web", "--workspace=web")]
    [InlineData("npm run unit --workspaces", "--workspaces")]
    [InlineData("npm test --prefix web", "--prefix")]
    public void Npm_pointed_at_another_package_runs_as_written(string run, string flag)
    {
        _fixture.Scripts(_fixture.Checkout, ("test", "jest"), ("unit", "jest"));
        Install(_fixture.Checkout, "jest@29.7.0");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal($"`unit` points npm at another package (`{flag}`), whose script is not read here, so it runs as written.", bound.Said);
    }

    /// <summary>A runner whose options end with <c>--</c> would read a bound after it as a test's name, so it runs as written.</summary>
    [Theory]
    [InlineData("jest -- src", null, "`unit` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    [InlineData("npm test -- --ci -- src", "jest", "`unit` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    [InlineData("npm test", "jest --", "`unit`'s npm script `test` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    public void A_runner_whose_options_end_with_dashes_runs_as_written(string run, string? script, string said)
    {
        if (script is not null) _fixture.Scripts(_fixture.Checkout, ("test", script));
        Install(_fixture.Checkout, "jest@29.7.0");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>
    /// The install is read where the gate runs, then each folder above it up to the checkout, never past it: a runner installed nowhere
    /// there, or whose package.json gives no version, runs as written.
    /// </summary>
    [Fact]
    public void The_install_is_read_from_the_gate_s_folder_up_to_the_checkout_and_never_past_it()
    {
        var web = Path.Combine(_fixture.Checkout, "src", "web");
        Directory.CreateDirectory(web);
        var gate = new HeavyGate("unit", "jest", "src/web", true);

        // Above the checkout is another project's install, never this repository's.
        Install(_fixture.Folder, "jest@29.7.0");
        var nowhere = HeavyWorkers.Bind(gate, web, _fixture.Checkout);
        Assert.Equal("jest", nowhere.Run);
        Assert.Equal(
            "`unit` runs Jest, which is not installed in its folder or above it in the checkout, so its version is not known and it runs as written.",
            nowhere.Said);

        Install(_fixture.Checkout, "jest@30.5.2");
        Assert.Equal("jest --maxWorkers=2", HeavyWorkers.Bind(gate, web, _fixture.Checkout).Run);

        // The nearest install wins, as node resolves it.
        Install(web, "jest@28.1.3");
        Assert.Equal("jest", HeavyWorkers.Bind(gate, web, _fixture.Checkout).Run);

        _fixture.Installed(web, "jest", null);
        Assert.Equal(
            "`unit` runs Jest, whose installed package.json gives no readable version, so it runs as written.",
            HeavyWorkers.Bind(gate, web, _fixture.Checkout).Said);
    }

    /// <summary>An npm script's runner is read through the gate folder's own package.json, its install from there up to the checkout.</summary>
    [Fact]
    public void An_npm_script_is_read_in_the_gate_s_folder()
    {
        var web = Path.Combine(_fixture.Checkout, "web");
        _fixture.Scripts(_fixture.Checkout, ("test", "dotnet test"));
        _fixture.Scripts(web, ("test", "vitest run"));
        Install(_fixture.Checkout, "vitest@5.0.1");

        var bound = HeavyWorkers.Bind(new HeavyGate("unit", "npm test", "web", true), web, _fixture.Checkout);

        Assert.Equal("npm test -- --maxWorkers=2", bound.Run);
        Assert.Equal("`unit` runs Vitest 5.0.1 through npm's `test` script, so its workers are bounded to two (`--maxWorkers=2`).", bound.Said);
    }

    private HeavyBound Bind(string run) => HeavyWorkers.Bind(new HeavyGate("unit", run, null, true), _fixture.Checkout, _fixture.Checkout);

    /// <summary><paramref name="installed"/> as <c>package@version</c>, installed under <paramref name="folder"/>.</summary>
    private void Install(string folder, string installed)
    {
        var at = installed.LastIndexOf('@');
        _fixture.Installed(folder, installed[..at], installed[(at + 1)..]);
    }
}
