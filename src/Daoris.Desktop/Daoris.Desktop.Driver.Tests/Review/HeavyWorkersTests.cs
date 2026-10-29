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

    /// <summary>
    /// HEAVYRUN1a3: <c>yarn test</c> and <c>yarn run &lt;script&gt;</c> at each yarn major read (1–4), which hands the script every word
    /// after its name: the bound at the end, with no <c>--</c>. The version is the <c>packageManager</c> field's, a corepack hash taken off.
    /// </summary>
    [Theory]
    [InlineData("yarn test", "test", "jest --ci", "yarn@1.22.22", "yarn test --maxWorkers=2", "Jest 29.7.0 through yarn 1.22.22's `test`")]
    [InlineData("yarn test --ci", "test", "jest", "yarn@2.4.2", "yarn test --ci --maxWorkers=2", "Jest 29.7.0 through yarn 2.4.2's `test`")]
    [InlineData("yarn run unit", "unit", "vitest run", "yarn@3.8.7", "yarn run unit --maxWorkers=2", "Vitest 5.0.1 through yarn 3.8.7's `unit`")]
    [InlineData("yarn run test:unit --reporter=dot", "test:unit", "vitest run", "yarn@4.18.1+sha512.abc123", "yarn run test:unit --reporter=dot --maxWorkers=2", "Vitest 5.0.1 through yarn 4.18.1's `test:unit`")]
    public void A_yarn_script_is_bounded_with_the_flag_at_its_end(string run, string script, string declared, string manager, string bound, string runs)
    {
        _fixture.Managed(_fixture.Checkout, manager, (script, declared));
        Install(_fixture.Checkout, "jest@29.7.0");
        Install(_fixture.Checkout, "vitest@5.0.1");

        var said = Bind(run);

        Assert.Equal(bound, said.Run);
        Assert.Equal($"`unit` runs {runs} script, so its workers are bounded to two (`--maxWorkers=2`).", said.Said);
    }

    /// <summary>
    /// HEAVYRUN1a3: pnpm as each major hands a script its words. pnpm 6 reads every flag as its own until its <c>--</c>; its test command
    /// does through pnpm 9, and <c>t</c> and <c>tst</c> still do; <c>run</c> from pnpm 7, and <c>test</c> spelled so from pnpm 10, hand
    /// the script every word after its name.
    /// </summary>
    [Theory]
    [InlineData("pnpm test", "pnpm@6.35.1", "pnpm test -- --maxWorkers=2", "Jest 29.7.0 through pnpm 6.35.1's `test`")]
    [InlineData("pnpm run unit", "pnpm@6.35.1", "pnpm run unit -- --maxWorkers=2", "Vitest 5.0.1 through pnpm 6.35.1's `unit`")]
    [InlineData("pnpm run unit", "pnpm@7.33.7", "pnpm run unit --maxWorkers=2", "Vitest 5.0.1 through pnpm 7.33.7's `unit`")]
    [InlineData("pnpm t -- --ci", "pnpm@8.15.9", "pnpm t -- --ci --maxWorkers=2", "Jest 29.7.0 through pnpm 8.15.9's `test`")]
    [InlineData("pnpm test", "pnpm@9.15.9", "pnpm test -- --maxWorkers=2", "Jest 29.7.0 through pnpm 9.15.9's `test`")]
    [InlineData("pnpm test --silent", "pnpm@9.15.9", "pnpm test --silent -- --maxWorkers=2", "Jest 29.7.0 through pnpm 9.15.9's `test`")]
    [InlineData("pnpm test", "pnpm@10.34.7", "pnpm test --maxWorkers=2", "Jest 29.7.0 through pnpm 10.34.7's `test`")]
    [InlineData("pnpm test --ci", "pnpm@11.28.6", "pnpm test --ci --maxWorkers=2", "Jest 29.7.0 through pnpm 11.28.6's `test`")]
    [InlineData("pnpm tst", "pnpm@11.28.6", "pnpm tst -- --maxWorkers=2", "Jest 29.7.0 through pnpm 11.28.6's `test`")]
    [InlineData("pnpm run-script unit --reporter=dot", "pnpm@10.34.7", "pnpm run-script unit --reporter=dot --maxWorkers=2", "Vitest 5.0.1 through pnpm 10.34.7's `unit`")]
    public void A_pnpm_script_is_bounded_as_its_major_hands_a_script_its_words(string run, string manager, string bound, string runs)
    {
        _fixture.Managed(_fixture.Checkout, manager, ("test", "jest"), ("unit", "vitest run"));
        Install(_fixture.Checkout, "jest@29.7.0");
        Install(_fixture.Checkout, "vitest@5.0.1");

        var said = Bind(run);

        Assert.Equal(bound, said.Run);
        Assert.Equal($"`unit` runs {runs} script, so its workers are bounded to two (`--maxWorkers=2`).", said.Said);
    }

    /// <summary>
    /// Without a <c>packageManager</c> field the install's own record is read: pnpm's <c>node_modules/.modules.yaml</c>, yarn's
    /// <c>.yarnrc.yml</c> release, and the <c>.yarn-integrity</c> only yarn 1 writes. The nearest field wins over a record.
    /// </summary>
    [Fact]
    public void Without_a_packageManager_field_the_install_s_own_record_is_read()
    {
        _fixture.Scripts(_fixture.Checkout, ("test", "jest"));
        Install(_fixture.Checkout, "jest@29.7.0");
        var modules = Path.Combine(_fixture.Checkout, "node_modules");

        File.WriteAllText(Path.Combine(modules, ".modules.yaml"), "hoistPattern:\n  - '*'\npackageManager: pnpm@9.15.9\nlayoutVersion: 5\n");
        Assert.Equal("pnpm test -- --maxWorkers=2", Bind("pnpm test").Run);

        _fixture.Managed(_fixture.Checkout, "pnpm@10.34.7", ("test", "jest"));
        Assert.Equal("pnpm test --maxWorkers=2", Bind("pnpm test").Run);

        _fixture.Scripts(_fixture.Checkout, ("test", "jest"));
        File.WriteAllText(Path.Combine(modules, ".yarn-integrity"), "{}");
        Assert.Equal("`unit` runs Jest 29.7.0 through yarn 1.x's `test` script, so its workers are bounded to two (`--maxWorkers=2`).", Bind("yarn test").Said);

        File.WriteAllText(Path.Combine(_fixture.Checkout, ".yarnrc.yml"), "nodeLinker: node-modules\nyarnPath: .yarn/releases/yarn-4.5.0.cjs\n");
        var berry = Bind("yarn test");
        Assert.Equal("yarn test --maxWorkers=2", berry.Run);
        Assert.Equal("`unit` runs Jest 29.7.0 through yarn 4.5.0's `test` script, so its workers are bounded to two (`--maxWorkers=2`).", berry.Said);
    }

    /// <summary>
    /// A yarn or pnpm whose version is recorded nowhere, or whose nearest <c>packageManager</c> names another tool, or whose version's
    /// forwarding was not read (pnpm 12 ships native; yarn 5, pnpm 5 and a prerelease were not read) runs as written, saying so.
    /// </summary>
    [Theory]
    [InlineData("yarn test", null, "`unit` runs yarn's `test` script, and which yarn version runs it is recorded neither by a `packageManager` field nor by its install, so it runs as written.")]
    [InlineData("pnpm test", null, "`unit` runs pnpm's `test` script, and which pnpm version runs it is recorded neither by a `packageManager` field nor by its install, so it runs as written.")]
    [InlineData("yarn test", "pnpm@9.15.9", "`unit` runs yarn's `test` script, and the package.json nearest it declares `pnpm@9.15.9` as its package manager, so it runs as written.")]
    [InlineData("pnpm test", "pnpm@12.5.1", "`unit` runs pnpm 12.5.1's `test` script, and how this pnpm version hands a script its words is not known here, so it runs as written.")]
    [InlineData("pnpm test", "pnpm@5.18.10", "`unit` runs pnpm 5.18.10's `test` script, and how this pnpm version hands a script its words is not known here, so it runs as written.")]
    [InlineData("pnpm test", "pnpm@10.0.0-rc.1", "`unit` runs pnpm 10.0.0-rc.1's `test` script, and how this pnpm version hands a script its words is not known here, so it runs as written.")]
    [InlineData("yarn test", "yarn@5.0.0", "`unit` runs yarn 5.0.0's `test` script, and how this yarn version hands a script its words is not known here, so it runs as written.")]
    public void A_yarn_or_pnpm_whose_forwarding_is_not_known_here_runs_as_written_saying_so(string run, string? manager, string said)
    {
        _fixture.Managed(_fixture.Checkout, manager, ("test", "jest"));
        Install(_fixture.Checkout, "jest@29.7.0");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>
    /// Each yarn and pnpm form that would not reach the runner as one bound runs as written, said: the tool's own options before its
    /// script, a shorthand that may name the tool's own command, a <c>--</c> after yarn's script, pnpm pointed at other packages, a
    /// <c>--</c> pnpm hands on, an owner's own bound, and a script that is not one runner command or cannot be read.
    /// </summary>
    [Theory]
    [InlineData("yarn --silent test", "yarn@4.18.1", "`unit` names yarn's own options before its script, so it runs as written.")]
    [InlineData("yarn run", "yarn@4.18.1", "`unit` runs `yarn run` with no script named after it, so it runs as written.")]
    [InlineData("yarn build", "yarn@4.18.1", "`unit` runs `yarn build`, which is not `yarn test` or `yarn run <script>`, so it runs as written.")]
    [InlineData("yarn test -- --ci", "yarn@1.22.22", "`unit` puts `--` after yarn's script, which yarn 1 takes away and yarn 2 and later hand on, so it runs as written.")]
    [InlineData("yarn test -w 1", "yarn@4.18.1", "`unit` names its own worker bound (`-w`), so it runs as written.")]
    [InlineData("pnpm -r test", "pnpm@9.15.9", "`unit` names pnpm's own options before its script, so it runs as written.")]
    [InlineData("pnpm --filter web test", "pnpm@9.15.9", "`unit` names pnpm's own options before its script, so it runs as written.")]
    [InlineData("pnpm vitest", "pnpm@9.15.9", "`unit` runs `pnpm vitest`, which is not `pnpm test` or `pnpm run <script>`, so it runs as written.")]
    [InlineData("pnpm run-script --silent", "pnpm@9.15.9", "`unit` runs `pnpm run-script` with no script named after it, so it runs as written.")]
    [InlineData("pnpm test -r", "pnpm@9.15.9", "`unit` points pnpm at other packages (`-r`), whose scripts are not read here, so it runs as written.")]
    [InlineData("pnpm test --filter=web", "pnpm@6.35.1", "`unit` points pnpm at other packages (`--filter=web`), whose scripts are not read here, so it runs as written.")]
    [InlineData("pnpm test -- --ci", "pnpm@10.34.7", "`unit` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    [InlineData("pnpm run unit -- src", "pnpm@7.33.7", "`unit` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    [InlineData("pnpm test -- --maxWorkers=4", "pnpm@9.15.9", "`unit` names its own worker bound (`--maxWorkers=4`), so it runs as written.")]
    [InlineData("pnpm run lint", "pnpm@9.15.9", "`unit`'s pnpm script `lint` runs `eslint`, neither Jest nor Vitest, so it runs as written.")]
    [InlineData("pnpm run e2e", "pnpm@9.15.9", "`unit` runs pnpm's `e2e` script, and its folder's package.json declares no `e2e` script, so it runs as written.")]
    [InlineData("yarn run chain", "yarn@1.22.22", "`unit`'s yarn script `chain` is more than one command, so it runs as written.")]
    [InlineData("yarn run ended", "yarn@4.18.1", "`unit`'s yarn script `ended` ends its runner's options with `--`, so a bound after them would not be read as one; it runs as written.")]
    public void A_yarn_or_pnpm_form_that_would_not_reach_the_runner_runs_as_written(string run, string manager, string said)
    {
        _fixture.Managed(
            _fixture.Checkout, manager, ("test", "jest"), ("unit", "vitest run"), ("lint", "eslint ."), ("chain", "tsc -b && jest"), ("ended", "jest --"));
        Install(_fixture.Checkout, "jest@29.7.0");
        Install(_fixture.Checkout, "vitest@5.0.1");

        var bound = Bind(run);

        Assert.Equal(run, bound.Run);
        Assert.Equal(said, bound.Said);
    }

    /// <summary>The tool's version is read from the gate's folder up to the checkout, never past it, the nearest field winning.</summary>
    [Fact]
    public void The_tool_s_version_is_read_from_the_gate_s_folder_up_to_the_checkout_and_never_past_it()
    {
        var web = Path.Combine(_fixture.Checkout, "web");
        _fixture.Managed(_fixture.Folder, "pnpm@10.34.7");
        _fixture.Scripts(web, ("test", "jest"));
        Install(_fixture.Checkout, "jest@29.7.0");
        var gate = new HeavyGate("unit", "pnpm test", "web", true);

        Assert.Equal("pnpm test", HeavyWorkers.Bind(gate, web, _fixture.Checkout).Run);

        _fixture.Managed(_fixture.Checkout, "pnpm@9.15.9");
        Assert.Equal("pnpm test -- --maxWorkers=2", HeavyWorkers.Bind(gate, web, _fixture.Checkout).Run);

        _fixture.Managed(web, "pnpm@10.34.7", ("test", "jest"));
        Assert.Equal("pnpm test --maxWorkers=2", HeavyWorkers.Bind(gate, web, _fixture.Checkout).Run);
    }

    private HeavyBound Bind(string run) => HeavyWorkers.Bind(new HeavyGate("unit", run, null, true), _fixture.Checkout, _fixture.Checkout);

    /// <summary><paramref name="installed"/> as <c>package@version</c>, installed under <paramref name="folder"/>.</summary>
    private void Install(string folder, string installed)
    {
        var at = installed.LastIndexOf('@');
        _fixture.Installed(folder, installed[..at], installed[(at + 1)..]);
    }
}
