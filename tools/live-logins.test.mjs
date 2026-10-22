/**
 * LIVELOGIN1: the live-logins census (no test starts a real sign-in or runs a real agent binary).
 *
 *   node --test tools/live-logins.test.mjs
 *
 * `checkLiveLogins` reads a source tree. Each case builds a small tree in a gitignored scratch folder of the repository, with a
 * toolchain declaring `claude` and `codex`, plants one test source and holds the verdict: a real login command or an agent command
 * that falls back to PATH is refused, naming its file and line; a stub, a refusal's words and a marked guard line pass.
 */
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkLiveLogins, isTest } from './live-logins.mjs';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(root, 'local', 'scratch', 'live-logins-test');
rmSync(scratch, { recursive: true, force: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

const TOOLCHAIN = 'public static class T {\n  A = new(Binary: ["claude"]);\n  B = new(Binary: ["codex"]);\n}\n';

let n = 0;
/** A fresh tree holding the toolchain and `{ 'src/A.Tests/X.cs': 'source' }`; returns its root. */
function tree(files) {
  const dir = join(scratch, `t${n++}`);
  for (const [rel, text] of Object.entries({ 'src/Daoris.Desktop/Daoris.Desktop.Driver/T.cs': TOOLCHAIN, ...files })) {
    const full = join(dir, rel);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, text);
  }
  return dir;
}

const cs = (body) => ({ 'src/X.Tests/X.cs': `namespace N;\npublic class X {\n${body}\n}\n` });

test('the tree passes', () => {
  assert.deepEqual(checkLiveLogins(root).problems, []);
});

test('the agents are read from the toolchains', () => {
  assert.deepEqual(checkLiveLogins(tree({})).binaries, ['claude', 'codex']);
});

for (const [name, body] of [
  ['an argument list of claude auth login', 'var info = new ProcessStartInfo("claude") { ArgumentList = { "claude", "auth", "login" } };'],
  ['an argument list of codex login', "const run = ['codex', 'login', '--device-auth'];"],
  ['a whole command handed to a spawn', 'execSync("codex login");'],
  ['a HarnessCommand on the bare binary', 'var run = new HarnessCommand("claude-code", ["claude"], null, null);'],
  ['a HarnessCommand on a toolchain\'s binary', 'var run = new HarnessCommand("codex", toolchain.Binary, null, null);'],
  ['a configured command of the bare binary', 'File.WriteAllText(path, """{ "commands": { "claude-code": ["claude", "-p"] } }""");'],
  ['a configured dictionary entry of the bare binary', 'var c = new Dictionary<string, IReadOnlyList<string>> { ["codex"] = ["codex.exe"] };'],
  ['a door named without its account agent', 'File.WriteAllText(path, """{ "commands": { "claude-code-acp": ["node", "acp.mjs"] } }""");'],
]) {
  test(`${name} is refused, naming the file and line`, () => {
    const { problems } = checkLiveLogins(tree(cs(body)));
    assert.equal(problems.length, 1, problems.join('\n'));
    assert.match(problems[0], /^src\/X\.Tests\/X\.cs:3: /);
    assert.match(problems[0], /a real login command|an agent command that falls back to PATH/);
  });
}

test('a planted case in a TypeScript test is refused too', () => {
  const { problems } = checkLiveLogins(tree({ 'src/Daoris.Cli/test/x.test.ts': "spawnSync('claude', ['auth', 'login']);\nspawn(\"claude auth login\");\n" }));
  assert.equal(problems.length, 1, problems.join('\n'));
  assert.match(problems[0], /x\.test\.ts:2: a real login command/);
});

for (const [name, body] of [
  ['a stub script', 'var run = new HarnessCommand("claude-code", ["node", script], null, null);'],
  ['a stub laid at a path named like the real one', 'var run = new HarnessCommand("claude-code", [Path.Combine(home, "claude.cmd")], null, null);'],
  ['a stub answering its login', "} else if (words === 'auth login') {"],
  ['a refusal\'s words naming the command in backticks', 'Assert.Contains("`codex login` waits for your browser on port 1455", refusal);'],
  ['the words handed to a replace', "held.replace('`codex login`', 'codex login');"],
  ['a door named beside its account agent', '"""{ "commands": { "codex-acp": ["node", "a.mjs"], "codex": ["node", "c.mjs"] } }"""'],
  ['a comment', '// runs `codex login` as ["codex", "login"]'],
  ['a guard line marked', 'var real = new HarnessCommand("claude-code", ["claude"], null, null); // live-logins: refused, never started'],
]) {
  test(`${name} passes`, () => {
    assert.deepEqual(checkLiveLogins(tree(cs(body))).problems, []);
  });
}

test('the marked lines are counted', () => {
  const body = 'var real = new HarnessCommand("claude-code", ["claude"], null, null); // live-logins: refused, never started';
  assert.equal(checkLiveLogins(tree(cs(body))).exempt, 1);
});

test('a source outside the tests is not read', () => {
  assert.deepEqual(checkLiveLogins(tree({ 'src/X/Run.cs': 'var run = new HarnessCommand("codex", ["codex"], null, null);' })).problems, []);
  assert.equal(isTest('src/X/Run.cs'), false);
  assert.equal(isTest('src/A.Tests/B/C.cs'), true);
  assert.equal(isTest('src/Daoris.Web/src/agents/SignIn.test.tsx'), true);
  assert.equal(isTest('src/Daoris.Cli/tool-test/merge-branch.test.ts'), true);
});
