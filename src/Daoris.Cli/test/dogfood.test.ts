import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join, dirname, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { existsSync, readdirSync } from 'node:fs';
import { COMMANDS } from '../src/cli.ts';
import { readCanon } from '../src/canon.ts';
import { frontmatterEnd, parseFrontmatter, SKILL_FIELDS } from '../src/document.ts';
import { listFiles, readText } from '../src/fsx.ts';
import { readManifest, readLock } from '../src/config.ts';
import { inspect, commandCheck } from '../src/drift.ts';
import { ROLES } from '../src/documents.ts';

// This package is src/Daoris.Cli; the canon and daoris's own doctrine live at
// the workspace root, because they are the project's data rather than the CLI's.
const cliRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(cliRoot));

const isSkill = (source: string) => source.includes('/skills/');

test('every shipped canon file has complete frontmatter', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const core = canon.packs.get('core')!.files;
  assert.ok(core.length >= 6, `expected at least 6 core files, found ${core.length}`);

  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      if (isSkill(file.source)) continue;
      const { meta } = parseFrontmatter(readText(join(repoRoot, 'canon', file.source)));
      assert.ok(meta, `${file.source} is missing or has incomplete frontmatter`);
      assert.ok(meta!.name && meta!.applies_when && meta!.enforces);
      assert.equal(
        meta!.name,
        file.source.split('/').pop()!.replace(/\.md$/, ''),
        `${file.source}: frontmatter name must match the filename`,
      );
    }
  }
});

/**
 * What the harness needs of a canon's skills, as problems (DOC3).
 *
 * A skill is a FOLDER whose entry file is `SKILL.md`; everything else in the folder is supporting
 * material (a template, a script) that `canon.ts` ships with it on purpose. So the unit held here is
 * the folder: it must hold a `SKILL.md`, and only that entry file carries the frontmatter the harness
 * reads. `description` is the trigger it matches on, so a skill with none installs, costs bytes and
 * silently never fires; `name` must match the folder, since every entry file is called `SKILL.md`. A
 * file directly under `skills/` is not inside any folder, so no harness can ever invoke it.
 *
 * Holding every file under `/skills/` to be a `SKILL.md` failed the first skill with templates beside
 * it, which is the shape the canon ships by design.
 */
function skillProblems(sources: readonly string[], read: (source: string) => string): string[] {
  const problems: string[] = [];
  const folders = new Map<string, string[]>();
  for (const source of sources) {
    const tier = source.indexOf('/skills/') + '/skills/'.length;
    const slash = source.indexOf('/', tier);
    if (slash === -1) {
      problems.push(`${source}: a skill is a folder holding SKILL.md, and this file sits in no folder`);
      continue;
    }
    const folder = source.slice(0, slash);
    folders.set(folder, [...(folders.get(folder) ?? []), source]);
  }
  for (const [folder, files] of folders) {
    const entry = `${folder}/SKILL.md`;
    if (!files.includes(entry)) {
      problems.push(`${folder}: a skill's folder must hold SKILL.md`);
      continue;
    }
    const { meta } = parseFrontmatter(read(entry), SKILL_FIELDS);
    if (!meta) problems.push(`${entry} is missing 'name' or 'description'`);
    else if (meta.name !== folder.split('/').at(-1)) {
      problems.push(`${entry}: frontmatter name '${meta.name}' must match the skill's directory`);
    }
  }
  return problems;
}

test('a skill is held as a folder: its entry file, never its supporting files, and never a flat file', () => {
  const entry = (name: string) => `---\nname: ${name}\ndescription: Use when ${name} is wanted.\n---\n\nSteps.\n`;
  const files: Record<string, string> = {
    'core/skills/writer/SKILL.md': entry('writer'),
    'core/skills/writer/templates/row.md': '# A row\n\n<no frontmatter: a template, not a skill>\n',
    'core/skills/writer/run.sh': '#!/bin/sh\n',
  };
  const read = (source: string) => files[source] ?? '';

  assert.deepEqual(skillProblems(Object.keys(files), read), [], 'templates and scripts beside SKILL.md are supporting files');

  // The loud cases stay loud: a flat file, a folder with no entry file, and an entry file the harness cannot match.
  assert.match(skillProblems(['core/skills/flat.md'], () => entry('flat')).join('\n'), /flat\.md: a skill is a folder/);
  assert.match(skillProblems(['core/skills/empty/notes.md'], read).join('\n'), /core\/skills\/empty: a skill's folder must hold SKILL\.md/);
  assert.match(skillProblems(['core/skills/named/SKILL.md'], () => entry('other')).join('\n'), /must match the skill's directory/);
  assert.match(skillProblems(['core/skills/mute/SKILL.md'], () => '# no frontmatter\n').join('\n'), /missing 'name' or 'description'/);
});

test('every shipped canon skill carries the frontmatter the harness needs', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const skills = [...canon.packs.values()].flatMap((pack) => pack.files).filter((f) => isSkill(f.source));
  const entries = skills.filter((file) => file.source.endsWith('/SKILL.md'));
  assert.ok(entries.length >= 2, `expected at least 2 canon skills, found ${entries.length}`);
  assert.ok(skills.length > entries.length, 'no canon skill carries a supporting file, so the folder rule proved nothing here');

  assert.deepEqual(skillProblems(skills.map((file) => file.source), (source) => readText(join(repoRoot, 'canon', source))), []);
});

/**
 * A template beside a canon skill that its entry file never names, or one it names that is not there
 * (ORIENT2a). A skill's templates arrive in every adopter, and a session reaches one only through the
 * skill's own words: an unnamed template is shipped and never copied, and a named one that is gone sends
 * the session to a file that does not exist. Both are silent, so they are held here.
 */
function templateProblems(sources: readonly string[], read: (source: string) => string): string[] {
  const problems: string[] = [];
  const entries = sources.filter((source) => source.endsWith('/SKILL.md'));
  for (const entry of entries) {
    const folder = entry.slice(0, -'/SKILL.md'.length);
    const beside = sources.filter((source) => source.startsWith(`${folder}/templates/`))
      .map((source) => source.slice(folder.length + 1));
    const named = [...new Set([...read(entry).matchAll(/`(templates\/[^`<>\s]+)`/g)].map((match) => match[1]!))];
    for (const template of beside) {
      if (!named.includes(template)) problems.push(`${entry} never names ${template}, which ships beside it`);
    }
    for (const template of named) {
      if (!beside.includes(template)) problems.push(`${entry} names ${template}, which is not there`);
    }
  }
  return problems;
}

test('the template scan finds a template nobody names, and a name with no template', () => {
  const files: Record<string, string> = {
    'core/skills/writer/SKILL.md': '---\nname: writer\ndescription: d\n---\n\n| row | `templates/row.md` |\n| gone | `templates/gone.md` |\n',
    'core/skills/writer/templates/row.md': '# A row\n',
    'core/skills/writer/templates/stray.md': '# A stray\n',
    'core/skills/plain/SKILL.md': '---\nname: plain\ndescription: d\n---\n\nNo templates, and none named.\n',
  };
  assert.deepEqual(templateProblems(Object.keys(files), (source) => files[source]!), [
    'core/skills/writer/SKILL.md never names templates/stray.md, which ships beside it',
    'core/skills/writer/SKILL.md names templates/gone.md, which is not there',
  ]);
});

test('every template beside a canon skill is named by it, and every template it names is there', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const skills = [...canon.packs.values()].flatMap((pack) => pack.files).filter((file) => isSkill(file.source));
  const templates = skills.filter((file) => file.source.includes('/templates/'));
  assert.ok(templates.length >= 2, `the scan found ${templates.length} templates, so it proved nothing`);

  assert.deepEqual(templateProblems(skills.map((file) => file.source), (source) => readText(join(repoRoot, 'canon', source))), []);
});

/**
 * The roles a manifest may declare are the canon's (ORIENT2b; D122 §2.7, D151 point 4). `ROLES`' jobs
 * are the canon's roles table cut short, so a role the canon teaches that the CLI does not know is a
 * declaration every adopter is refused, and one the CLI knows that the canon does not teach is a row no
 * adopter has a reason to write. `knowledge` and `skill` are the canon's and never declared: the index
 * lists them from the target.
 */
test("the canon's roles table and the roles a manifest may declare name the same roles, in one order", () => {
  const text = readText(join(repoRoot, 'canon', 'core', 'knowledge', 'development-documents.md'));
  const table = text.slice(text.indexOf('### The roles'), text.indexOf('### Three ways a document is read'));
  const canonRoles = [...table.matchAll(/^\| \*\*([a-z]+)\*\* \|/gm)].map((match) => match[1]!);
  assert.ok(canonRoles.length >= 10, `the scan found ${canonRoles.length} roles in the canon's table, so it proved nothing`);

  assert.deepEqual(canonRoles.filter((role) => role !== 'knowledge' && role !== 'skill'), ROLES.map((row) => row.role));
});

/**
 * The skill files whose frontmatter pre-approves tools (D122, finding 3; DOC3).
 *
 * A project skill's `allowed-tools` is honoured for the turn that invokes it, and the harness's maker
 * documents that workspace trust never gates it, an untrusted folder in a pipe included. A canonical
 * skill carrying one would arrive by `sync` and widen every session in every adopter, unreviewed: the
 * side door the declaration of safe work exists to close. Read from the frontmatter only, in any of the
 * field's spellings and either of YAML's shapes (a line, or a list under the key); a sentence in the body
 * that names the field is prose. Every file of a skill's folder, not only its entry file: a template of
 * a skill copied into a repository would carry the field into that repository's own.
 */
function preApprovals(sources: readonly string[], read: (source: string) => string): string[] {
  return sources.filter((source) => {
    const text = read(source);
    const end = frontmatterEnd(text);
    return end !== -1 && /^allowed[-_]?tools\s*:/im.test(text.slice(0, end));
  });
}

test('the allowed-tools scan finds the field in frontmatter, and not in prose', () => {
  const files: Record<string, string> = {
    'core/skills/wide/SKILL.md': '---\nname: wide\ndescription: d\nallowed-tools: Bash(npm test)\n---\n\nSteps.\n',
    'core/skills/listed/SKILL.md': '---\nname: listed\ndescription: d\nallowed-tools:\n  - Read\n  - Bash(git push:*)\n---\n\nSteps.\n',
    'core/skills/camel/SKILL.md': '---\nname: camel\ndescription: d\nallowedTools: Read\n---\n\nSteps.\n',
    'core/skills/wide/templates/skill.md': '---\nname: t\ndescription: d\nallowed-tools: Read\n---\n',
    'core/skills/plain/SKILL.md': '---\nname: plain\ndescription: d\n---\n\nThis skill sets no allowed-tools: field on purpose.\n',
    'core/skills/plain/templates/row.md': '# A row\n\nallowed-tools: never in a body\n',
  };
  const found = preApprovals(Object.keys(files), (source) => files[source]!);
  assert.deepEqual(found.sort(), [
    'core/skills/camel/SKILL.md', 'core/skills/listed/SKILL.md', 'core/skills/wide/SKILL.md', 'core/skills/wide/templates/skill.md',
  ]);
});

test('no canonical skill carries allowed-tools', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const skills = [...canon.packs.values()].flatMap((pack) => pack.files)
    .filter((file) => isSkill(file.source) && file.source.endsWith('.md'));
  assert.ok(skills.length >= 2, `the scan found ${skills.length} skill files, so it proved nothing`);

  const found = preApprovals(skills.map((file) => file.source), (source) => readText(join(repoRoot, 'canon', source)));
  assert.deepEqual(found, [], 'a canonical skill pre-approves tools: it would widen every session in every adopter, '
    + 'unreviewed and untrusted. What a session may run is the gates file\'s declaration, which a person accepts (D122)');
});

test('every pack declares a description and ships at least one file', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const packs = [...canon.packs.values()].filter((pack) => pack.name !== 'core');
  assert.ok(packs.length >= 3, `expected at least 3 packs, found ${packs.length}`);
  for (const pack of packs) {
    assert.ok(pack.description, `pack '${pack.name}' has no description — init prints it`);
    assert.ok(pack.files.length, `pack '${pack.name}' ships no files`);
  }
});

test('no canon file names a private sibling project or a machine path', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  // Every separator a Windows home is spelled with — the devkit's sensitive gate's twin (REV3).
  const forbidden = /[A-Z]:(?:\\{1,2}|\/)Users(?:\\{1,2}|\/)|\/home\/[a-z]/i;
  for (const pack of canon.packs.values()) {
    for (const file of pack.files) {
      const text = readText(join(repoRoot, 'canon', file.source));
      assert.equal(forbidden.test(text), false, `${file.source} contains a machine path`);
    }
  }
});

/**
 * Doctrine must not hard-require the tool that shipped it (D48 §2a, CANON6).
 *
 * An adopted repository stays fully workable for contributors who do not run Daoris — their agents
 * included, because those agents load the same vendored markdown. Almost everything the canon names
 * survives that: `sync` COMMITS what it writes, so a generated index, a lock file and a vendored rule
 * are all still there for someone who never installed anything.
 *
 * A **service** is the exception, and the only one: publishing a quest needs something running. A rule
 * that says "publish a quest" and stops reads as a dead end to exactly the reader who cannot, so it
 * must name the tool-absent path in the same breath — the principle is canonical, the mechanism
 * degrades.
 *
 * This fails silently without a check, which is why there is one: nothing breaks, no gate goes red,
 * and a contributor's agent simply has nothing to do when it reaches that line.
 *
 * Two mechanisms since WSSETUP14a (D129 §2): a quest, and a search over the repository's knowledge
 * that a connected service answers by meaning. Each is held to its own carve-out, so a file that
 * degrades one cannot stand in for the other.
 */
const SERVICE_ONLY = [
  {
    what: 'a quest',
    // Deliberately not a broad vocabulary scan: the audit that wrote this rule first "fixed" a rule
    // naming the generated index, which needed nothing, because it matched on family words instead of
    // on what actually needs a process running.
    names: /\bquests?\b/i,
    // The carve-out, in any of the shapes it is reasonable to write. A rewording that drops all three
    // fails here — a loud false positive, which is the right trade for a guarantee that is otherwise
    // invisible, and it forces the next author to decide rather than drift.
    degrades: /where (?:none|it) (?:does|exists)|where none does not|where the quest system does not/i,
    absent: 'where no quest system exists',
  },
  {
    what: 'a connected search',
    // A search that is CONNECTED is a service: one sentence asks it beside the word searches a file
    // allows. A word search over the index or the folders needs nothing running and is not matched.
    names: /\bsearch\b[^.\n]{0,80}\bconnected\b|\bconnected\b[^.\n]{0,40}\bsearch\b/i,
    degrades: /where none is\b|where (?:none|no search) is connected/i,
    absent: 'where no such search is connected',
  },
] as const;

/** Each service-only mechanism a file names without its tool-absent path, as problems. */
function serviceOnlyProblems(files: readonly { source: string; text: string }[]): string[] {
  const problems: string[] = [];
  for (const { source, text } of files) {
    for (const mechanism of SERVICE_ONLY) {
      if (mechanism.names.test(text) && !mechanism.degrades.test(text)) {
        problems.push(`${source} instructs ${mechanism.what} without naming what to do ${mechanism.absent}`);
      }
    }
  }
  return problems;
}

test('the service-only scan finds a connected search without its tool-absent path, and passes one with it', () => {
  // D129 §4.2's doc-loader step 2, drafted without its last clause and then with it.
  const draft = '2. **The generated index.** Read it whole when it is a few dozen rows. When it is longer, search it '
    + "more than once. Where a search over this repository's knowledge is connected, ask it too, since it matches by "
    + 'meaning, which a word search cannot.';
  const whole = `${draft.slice(0, -1)}; where none is, the index searches are the whole step.`;

  assert.deepEqual(serviceOnlyProblems([{ source: 'draft', text: draft }]),
    ['draft instructs a connected search without naming what to do where no such search is connected']);
  assert.deepEqual(serviceOnlyProblems([{ source: 'whole', text: whole }]), []);
  // A word search needs nothing running, and a quest's carve-out is no carve-out for the search.
  assert.deepEqual(serviceOnlyProblems([{ source: 'words', text: 'Search the index for the task\'s words.' }]), []);
  assert.deepEqual(serviceOnlyProblems([{ source: 'both', text: `Publish a quest where it exists. ${draft}` }]),
    ['both instructs a connected search without naming what to do where no such search is connected']);
  assert.deepEqual(serviceOnlyProblems([{ source: 'quest', text: 'Publish a quest.' }]),
    ['quest instructs a quest without naming what to do where no quest system exists']);
});

test('no canon file instructs a service-only action without its tool-absent path', () => {
  const canon = readCanon(join(repoRoot, 'canon'));
  const files = [...canon.packs.values()].flatMap((pack) => pack.files)
    .map((file) => ({ source: file.source, text: readText(join(repoRoot, 'canon', file.source)) }));

  assert.deepEqual(serviceOnlyProblems(files), [],
    'Doctrine must not hard-require Daoris (D48 §2a) — name the alternative in the same breath.');
});

/**
 * The declared gates and the release workflow are two lists that must agree — and they silently did
 * not.
 *
 * `daoris.gates.json` declared `dotnet test src/Daoris.Service` (248 tests) and the workflow never ran
 * it; the driver's 130 were in neither. What hid it for eight landings is the most useful part: the
 * rehearsals drive the service AND the driver end to end, so the workflow looked thorough and every
 * release rehearsal passed. **An end-to-end pass is not a substitute for the judgement underneath
 * it** — a store rule or a payload guard can break without any rehearsal noticing, which is precisely
 * what those unit suites exist to catch.
 *
 * Substring rather than exact match, because the workflow appends runner flags (`--nologo`). One
 * direction only: the workflow legitimately runs more than the gates (both rehearsals), and a test
 * that forbade that would be wrong about the thing it is guarding.
 */
test('every declared gate is actually run by the release workflow', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  // 🔴 What the workflow RUNS, never what it says (REV3): a comment naming a command is not the command,
  // and deleting the Verify step left this green because a comment beside another step said
  // `npm run verify`. Full-line comments go; a step's own text stays.
  const workflow = readText(join(repoRoot, '.github', 'workflows', 'release.yml'))
    .split('\n').filter((line) => !/^\s*#/.test(line)).join('\n');
  // A gate `npm run verify` runs is run wherever verify is — the universal gates' one home (REV3
  // CLEAN1: the workflow ran them as a step of their own too, the same command twice).
  const verify = (JSON.parse(readText(join(repoRoot, 'package.json'))) as {
    scripts: { verify: string };
  }).scripts.verify;
  const runsVerify = /run:\s*npm run verify\s*$/m.test(workflow);

  assert.ok(gates.gates.length >= 4, 'the gate list emptied — that is not a pass');
  for (const gate of gates.gates) {
    assert.ok(
      workflow.includes(gate.run) || (runsVerify && verify.includes(gate.run)),
      `the '${gate.name}' gate is declared in daoris.gates.json but the release workflow never runs `
      + `it: ${gate.run}`);
  }

  // And a step that runs but whose failure is ignored runs nothing that gates.
  assert.doesNotMatch(workflow, /continue-on-error:\s*true/, 'a release step ignores its own failure');
  assert.doesNotMatch(workflow, /\|\|\s*true\b/, 'a release step swallows its exit code with `|| true`');
});

/**
 * The same gap one level further in (HTTP1). The service's gate is `dotnet test src/Daoris.Service`,
 * which runs the SOLUTION in that folder — so a suite added beside the others and left out of the
 * `.slnx` builds, passes when run by hand, and is run by no gate and no release. Every test project must
 * be reached by a declared gate: named directly, or a member of the solution a gated folder holds.
 * Found shallowly (`src/<tree>/<project>/<project>.csproj`) so no list of trees has to be kept here,
 * and by the test SDK reference, which is what makes `dotnet test` run a project: the devkit's suite
 * never says `IsTestProject`, and a search on that word missed it.
 */
test('every .NET test project is run by a declared gate', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  const targets = gates.gates
    .map((gate) => /^dotnet test (\S+)/.exec(gate.run)?.[1]?.replace(/\/+$/, ''))
    .filter((target): target is string => target !== undefined);
  const directories = (path: string) =>
    readdirSync(join(repoRoot, path), { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name);

  const projects = directories('src').flatMap((tree) => directories(`src/${tree}`)
    .map((project) => `src/${tree}/${project}/${project}.csproj`)
    .filter((file) => existsSync(join(repoRoot, file))
      && /<PackageReference\s+Include="Microsoft\.NET\.Test\.Sdk"/.test(readText(join(repoRoot, file)))));

  assert.ok(projects.length >= 5, `only ${projects.length} test projects were found — the search stopped reaching them`);
  assert.ok(projects.includes('src/Daoris.Service/Daoris.Service.Http.Tests/Daoris.Service.Http.Tests.csproj'),
    'the HTTP host suite is not found by this search — it has stopped asking about the project it was written for');
  for (const project of projects) {
    const reached = targets.some((target) => {
      if (project.slice(0, project.lastIndexOf('/')) === target) return true;
      if (!project.startsWith(`${target}/`)) return false;
      const member = project.slice(target.length + 1);
      return readdirSync(join(repoRoot, target))
        .filter((name) => /\.slnx?$/.test(name))
        .some((solution) => readText(join(repoRoot, target, solution)).replace(/\\/g, '/').includes(member));
    });
    assert.ok(reached, `${project} is a test project no declared gate runs — add it to its folder's solution, or declare it`);
  }
});

/**
 * One level further in again (MOD8). A desktop suite runs as two gates: the tests that start real
 * processes or run real ticks carry the `Process` category, a subagent's worktree runs the rest, and
 * the parent runs these one class at a time, by a run settings file. The test above counts a suite as
 * reached by either half, so losing the Process half would leave those tests run by no gate while
 * every check stayed green.
 */
test('a suite gated without its Process tests has a gate that runs them, one class at a time', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  const fast = gates.gates
    .map((gate) => /^dotnet test (\S+) --filter Category!=Process$/.exec(gate.run)?.[1])
    .filter((target): target is string => target !== undefined);
  const serial = gates.gates
    .map((gate) => /^dotnet test (\S+) --settings (\S+)$/.exec(gate.run))
    .filter((half): half is RegExpExecArray => half !== null)
    .map(([, target, settings]) => ({ target: target!, settings: readText(join(repoRoot, settings!)) }));

  assert.ok(fast.length >= 2, `${fast.length} gates leave the Process category out — this test has stopped reading the gates`);
  for (const target of fast) {
    const half = serial.find((gate) => gate.target === target);
    assert.ok(half, `${target} is gated without its Process tests, and no gate runs them`);
    assert.match(half.settings, /<TestCaseFilter>Category=Process<\/TestCaseFilter>/,
      `${target}'s settings gate does not run the Process tests`);
    assert.match(half.settings, /<ParallelizeTestCollections>false<\/ParallelizeTestCollections>/,
      `${target}'s Process tests are gated in parallel — load is what fails them (FLAKE1)`);
  }
});

/**
 * The release commit stages what release-prep rewrote, by a pathspec the workflow spells out — a
 * second list of the same files (REV3 CLEAN1). It had already missed the example manifests once, and
 * `verify` went red on main after a release. release-prep says what it writes; this holds the
 * pathspec to it. (The re-sync's writes are the workflow's own and are not read here.)
 */
test('the release commit stages every file release-prep writes', async () => {
  // @ts-expect-error — untyped workspace tooling; the same seam desktop-tool.test.ts documents
  const { written } = await import('../../../tools/release-prep.mjs') as { written: () => string[] };
  const workflow = readText(join(repoRoot, '.github', 'workflows', 'release.yml'));
  const staging = /git add -u -- ((?:[^\n]*\\\n)*[^\n]*)/.exec(workflow);
  assert.ok(staging, 'the release workflow no longer stages with `git add -u --` — this test must follow it');
  const pathspecs = staging[1]!.replace(/\\\n/g, ' ').trim().split(/\s+/);

  const files = written();
  assert.ok(files.some((file) => file.startsWith('examples/')), 'release-prep no longer finds the example manifests');
  for (const file of files) {
    assert.ok(
      pathspecs.some((spec) => file === spec || file.startsWith(`${spec}/`)),
      `release-prep writes ${file}, and the release commit's \`git add -u\` does not stage it`);
  }
});

/**
 * The same gap one list further in (SEN1): the universal gates were declared and in the workflow, and
 * red on this repository for a day, because nothing a session runs before committing ran them — the
 * workflow is dispatched by hand and the pre-commit hook was never installed. Placeholder home paths
 * reached six fixtures across four landings that way. The universal half is the one that belongs in
 * `verify`: it is fast, it judges facts (D54), and a leak is cheapest caught the moment it is written.
 */
test('the universal gates run in verify, not only at release', () => {
  const gates = JSON.parse(readText(join(repoRoot, 'daoris.gates.json'))) as {
    gates: { name: string; run: string }[];
  };
  const universal = gates.gates.find((gate) => gate.name === 'universal');
  const verify = (JSON.parse(readText(join(repoRoot, 'package.json'))) as {
    scripts: { verify: string };
  }).scripts.verify;

  assert.ok(universal, 'the universal gate is no longer declared — that is not a pass');
  assert.ok(verify.includes(universal.run), `npm run verify never runs the universal gates: ${universal.run}`);
});

/**
 * A decision recorded without what it beat invites re-litigation — which is the failure the decisions
 * record exists to prevent, so a decision that names no alternative has not finished being made.
 *
 * Adopted from deepseek-harness, where every active note carries a mandatory `## Alternatives
 * considered` (`docs/2026-09-21-dsh-evaluation.md` §3): two ecosystems keeping the same record for the
 * same reason, which is the D17 bar. Their rule comes with the discipline that makes it honest —
 * **alternatives are recorded, never invented** — and so does this one: the 41 entries written before
 * it are grandfathered rather than backfilled, because writing a plausible rejected alternative into
 * a decision nobody actually weighed would make the record worse while making the gate green.
 *
 * The line is the number, because the numbers only increase: every entry from D51 — the working-surface
 * arc onward — must say what it turned down. `Rejected` in any of its shapes, or a `Why not X?`
 * heading; both are already the house style, which is the evidence that the rule is describing
 * practice rather than imposing it.
 */
test('every decision entry from D51 on says what it rejected', () => {
  // One file per decision since DOC8a (D134): `docs/decisions/D<n>.md`, its number in its name, so a file
  // is an entry whole and no split at headings can drop one. The page left at `docs/DECISIONS.md` holds
  // none; read in their place, it was the guard below that refused to pass on its zero entries.
  const folder = join(repoRoot, 'docs', 'decisions');
  const GRANDFATHERED_THROUGH = 50;
  const entries = readdirSync(folder)
    .flatMap((name) => {
      const number = /^D(\d+)\.md$/.exec(name)?.[1];
      return number ? [{ number: Number(number), body: readText(join(folder, name)) }] : [];
    })
    .filter((entry) => entry.number > GRANDFATHERED_THROUGH);

  // Without this the test passes loudest exactly when the pattern stops matching headings at all.
  assert.ok(entries.length >= 3, `the walk found ${entries.length} entries after D${GRANDFATHERED_THROUGH}, so it proved nothing`);

  for (const entry of entries) {
    assert.match(
      entry.body,
      /Rejected\b|Why not\b/i,
      `D${entry.number} names no rejected alternative. A decision recorded without what it beat gets `
      + 're-litigated — say what was turned down and why, in the entry itself.');
  }
});

test('daoris holds its own doctrine and checks clean', () => {
  assert.equal(existsSync(join(repoRoot, 'daoris.json')), true, 'run: node bin/daoris.mjs init');
  const lock = readLock(repoRoot);
  assert.ok(lock, 'run: node bin/daoris.mjs sync');
  const report = inspect({ root: repoRoot, manifest: readManifest(repoRoot), lock });
  assert.equal(report.ok, true, JSON.stringify(report, null, 2));
});

/**
 * D8's offline guarantee, scoped to what it actually claims.
 *
 * The guarantee is about the DOCTRINE operations — `check` above all, because it runs inside build
 * gates and a gate that can fail on a network call is not a gate. The MANAGEMENT commands are a
 * different thing (D50): `connect`, `retire` and `import` are explicit, opt-in conversations with a
 * knowledge service, never run by a gate.
 *
 * This was briefly written as "nothing anywhere in the CLI may open a socket", which is a stronger
 * claim than D8 makes and would have made a client impossible. Two assertions replace it, and together
 * they are the real invariant: exactly one module may reach the network, and nothing on `check`'s path
 * may import it — directly or three modules deep.
 *
 * The first half survived the management class growing past one command because the network itself
 * stayed in one file. Had each verb opened its own socket, "the named management modules" would be a
 * list that grows, and a list that grows is one somebody eventually appends to without thinking.
 */
/** A module named by any import spelling: `from`, a bare `import`, a dynamic `import(`, a `require(`. */
const importing = (modules: string) => new RegExp(
  String.raw`(?:\bfrom|\bimport\s*\(?|\brequire\s*\()\s*['"](?:node:)?(?:${modules})['"]`);

/**
 * Widened by REV3: it held only `node:http(s)` and `require('http(s)')`, so a bare `'https'`, `net`,
 * `tls`, `http2`, `undici` or a dynamic `import()` walked straight past it.
 */
const NETWORK = new RegExp([
  String.raw`\b(?:fetch|XMLHttpRequest|WebSocket)\s*\(`,
  importing('https?|http2|net|tls|dgram|undici').source,
].join('|'));

/** The spawning primitive, in any spelling — held to one module the way the network is. */
const SPAWNING = importing('child_process');

/** The one module permitted a network primitive. Every management command speaks through it. */
const SERVICE_CLIENT = 'service.ts';

/**
 * A check you have not watched fail proves nothing — and a pattern is only as good as the spellings it
 * was shown. These are the realistic regressions, each of which the previous patterns missed.
 */
test('the guards recognise every spelling of the primitives they hold', () => {
  for (const line of [
    "import { request } from 'https';", "import { request } from \"node:https\";",
    "import net from 'node:net';", "import { connect } from 'tls';", "import * as h2 from 'node:http2';",
    "const { request } = await import('node:https');", "import { fetch as f } from 'undici';",
    "const h = require('http');", "await fetch(url);",
  ]) assert.ok(NETWORK.test(line), `NETWORK misses: ${line}`);

  for (const line of [
    "import { spawnSync } from 'node:child_process';", "import { execSync } from \"child_process\";",
    "const cp = await import('node:child_process');", "const { spawn } = require('child_process');",
  ]) assert.ok(SPAWNING.test(line), `SPAWNING misses: ${line}`);

  // And it is not a pattern that matches everything: the words alone are not the primitive.
  for (const line of ['const releaseFetcher = () => 1;', '// a network is not a socket', "import { x } from './nettle.ts';"]) {
    assert.equal(NETWORK.test(line), false, `NETWORK matches innocent: ${line}`);
  }
});

/** The one module permitted to spawn. Doctrine commands must not reach it either (below). */
const SPAWNS = 'toolchain.ts';

test('only the toolchain may spawn a process', () => {
  for (const dir of ['src', 'bin']) {
    for (const file of listFiles(join(cliRoot, dir), (n) => n.endsWith('.ts') || n.endsWith('.mjs'))) {
      if (file === SPAWNS) continue;
      assert.equal(
        SPAWNING.test(readText(join(cliRoot, dir, file))), false,
        `${dir}/${file} imports a spawning primitive — only ${SPAWNS} may`);
    }
  }
});

/**
 * One answer for Daoris and every child (TOOLS5, D121 §2.4): the toolchain starts every child in ONE place,
 * `startChild`, and that place hands the child the tools' environment through `tools.ts` — the driver's
 * `EveryChildIsHandedTheToolsTests` holds the same of its own starts. A second `spawnSync` would be a child that
 * runs whatever PATH it inherited.
 */
test('every child the toolchain starts is started in one place, with the tools’ environment', () => {
  const source = readText(join(cliRoot, 'src', SPAWNS));
  const calls = [...source.matchAll(/\bspawnSync\(/g)];
  assert.equal(calls.length, 1, `${SPAWNS} calls spawnSync ${calls.length} times — every child starts through startChild`);

  const start = source.indexOf('function startChild(');
  assert.ok(start >= 0 && start < calls[0]!.index!, 'the one spawnSync is inside startChild');
  const body = source.slice(start, calls[0]!.index!).replace(/\/\/[^\n]*/g, '');
  assert.match(body, /\bhandTools\(/, 'startChild hands the child the tools’ environment');
  assert.match(source, /import \{[^}]*\bhandTools\b[^}]*\} from '\.\/tools\.ts';/);
});

test('only the service client may touch the network', () => {
  for (const dir of ['src', 'bin']) {
    for (const file of listFiles(join(cliRoot, dir), (n) => n.endsWith('.ts') || n.endsWith('.mjs'))) {
      if (file === SERVICE_CLIENT) continue;
      const text = readText(join(cliRoot, dir, file));
      assert.equal(
        NETWORK.test(text), false,
        `${dir}/${file} reaches the network — only ${SERVICE_CLIENT} may`);
    }
  }
});

/**
 * The half that matters most. A gate would not realistically break by someone adding `fetch` to
 * `drift.ts`; it would break by an innocuous import three modules deep acquiring one for it.
 *
 * Walked from every DOCTRINE command's entry point, not only `check`'s: they all run offline, and a
 * test that named one of them would be silent the day `sync` grew a "just ask the service" shortcut.
 *
 * Since the commands became a table (MOD7), each doctrine command's row under `cli/` is an entry point
 * too, read from the table rather than listed, so a new doctrine verb is walked the day it lands.
 */
const DOCTRINE_ROWS = COMMANDS.filter((command) => command.kind === 'doctrine').map((command) => `cli/${command.name}.ts`);
const DOCTRINE = [
  'drift.ts', 'materialize.ts', 'indexgen.ts', 'upstream.ts', 'commands.ts', 'twins.ts', 'analyze.ts',
  ...DOCTRINE_ROWS,
];

test('the doctrine rows are read from the table, and check is one of them', () => {
  // Without this the rows could vanish from the walk (a renamed field, a class flipped) and every
  // assertion below would stay green over the handler modules alone.
  assert.ok(DOCTRINE_ROWS.includes('cli/check.ts'), `the doctrine rows were: ${DOCTRINE_ROWS.join(', ')}`);
  assert.ok(DOCTRINE_ROWS.includes('cli/sync.ts'), `the doctrine rows were: ${DOCTRINE_ROWS.join(', ')}`);
});

test('nothing a doctrine command reaches can import the service client', () => {
  for (const entry of DOCTRINE) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SERVICE_CLIENT), false,
      `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.ok(seen.size > 1, `the walk from ${entry} found nothing, so it proved nothing`);
  }
});

/**
 * The same guarantee, one door further out (D49 §4). `toolchain.ts` SPAWNS PROCESSES — each harness's
 * own installer, updater and login flow, which is the whole point of it — and a doctrine command that
 * reached it could run one of them inside a build gate.
 *
 * It is the same shape of rule as the network one and it is worth stating separately, because the
 * reasoning that makes spawning fine (opt-in, a person asked for it, never run by a gate) is exactly
 * the reasoning that stops holding the moment `check` can reach it.
 */
test('nothing a doctrine command reaches can spawn a harness', () => {
  for (const entry of DOCTRINE) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SPAWNS), false,
      `${entry} reaches the harness toolchain through: ${[...seen].sort().join(', ')}`);
  }
});

/**
 * Every module an entry point pulls in, transitively — both import spellings. Modules are named by
 * their path under `src/`, so a row under `cli/` importing `../drift.ts` reaches `drift.ts`.
 */
function reachableFrom(entry: string): Set<string> {
  const seen = new Set<string>();
  const walk = (module: string): void => {
    if (seen.has(module)) return;
    seen.add(module);
    const file = join(cliRoot, 'src', module);
    if (!existsSync(file)) return;
    // `from './x.ts'`, a bare `import './x.ts'` and a dynamic `import('./x.ts')`, in either quote — the
    // side-effect import was missed at first, and the dynamic one and the double quote until REV3. A
    // parent or nested path since MOD7, when the table's rows moved one folder down.
    for (const match of readText(file).matchAll(/(?:\bfrom|\bimport)\s*\(?\s*['"](\.{1,2}\/[\w./-]+\.ts)['"]/g)) {
      walk(posix.join(posix.dirname(module), match[1]!));
    }
  };

  walk(entry);
  return seen;
}

/**
 * Most of the management class needs no network at all (D50): `remote` edits the home's `remotes.json`
 * and `status --machine` reads it. The usage text says so out loud, and prose is the one surface with
 * no compiler — so the claim is held here.
 *
 * It is also what keeps the class from becoming a list. The offline guarantee survived management
 * growing past `connect` because the network itself stayed in ONE file; a verb that quietly acquired a
 * socket through a convenience import would make "the named management modules" something people
 * append to without thinking.
 */
test('the file-local management verbs reach no network module either', () => {
  for (const entry of ['remotes.ts', 'remotemap.ts', 'toolchain.ts', 'driverconfig.ts', 'plugins.ts', 'tools.ts']) {
    const seen = reachableFrom(entry);

    assert.equal(
      seen.has(SERVICE_CLIENT), false,
      `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.ok(seen.size > 1, `the walk from ${entry} found nothing, so it proved nothing`);
  }
});

/**
 * `tools.ts` answers "which program" and edits one file (TOOLS2, D121 §4.2): it stays pure. A tool's
 * version is asked where spawning lives, and a download reaches the network through the fetcher the
 * dispatcher hands in (TOOLS4) — so the module that holds the file's rules reaches neither on its own, and
 * neither does anything that reads the lists, unpacks an archive or lays a version out.
 */
test('the tools file reaches neither the toolchain nor the service client', () => {
  // TOOLS3: the resource lists are read and merged beside it, as pure. TOOLS4: the download, the archive readers and
  // the verb are pure too; only the dispatcher's row, below, hands them a fetcher.
  for (const entry of ['tools.ts', 'resources.ts', 'toolinstall.ts', 'zipfile.ts']) {
    const seen = reachableFrom(entry);
    assert.equal(seen.has(SPAWNS), false, `${entry} reaches the harness toolchain through: ${[...seen].sort().join(', ')}`);
    assert.equal(seen.has(SERVICE_CLIENT), false, `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.ok(seen.size > 1, `the walk from ${entry} found nothing, so it proved nothing`);
  }
  assert.ok(reachableFrom('toolinstall.ts').has('zipfile.ts'), 'the walk from the download no longer reaches the zip reader');
});

/**
 * `agent pin` DOES reach a network since AGT2b — a maker's release channel — and so does `agent update`
 * on a pinned agent since USE1a, asking the channel for its newest release; the usage text says they are
 * the verbs of their class that do. They reach it the way `connect` reaches a service:
 * through `service.ts`, which the DISPATCHER hands in. So the toolchain and everything that judges a
 * download (the channel, the signature, the archive) import no network module, and the one place the
 * two meet is a line a reviewer can read: the `agent` row of the dispatcher's table, `cli/agent.ts`
 * since MOD7.
 */
test('agent pin reaches a release channel only through the fetcher the dispatcher hands in', () => {
  for (const entry of ['channels.ts', 'openpgp.ts', 'tarball.ts']) {
    const seen = reachableFrom(entry);
    assert.equal(seen.has(SERVICE_CLIENT), false, `${entry} reaches the service client through: ${[...seen].sort().join(', ')}`);
    assert.equal(seen.has(SPAWNS), false, `${entry} reaches the harness toolchain through: ${[...seen].sort().join(', ')}`);
  }

  const row = readText(join(cliRoot, 'src', 'cli', 'agent.ts'));
  assert.match(row, /import \{ releaseFetcher \} from '\.\.\/service\.ts';/);
  assert.match(row, /run: \(args\) => commandHarness\(args, releaseFetcher\(\)\)/);
});

/**
 * The same shape for `daoris tool` (TOOLS4, D121 §4.2): `download`, `use … managed`, `update` and `look` reach a
 * tool's maker and a resource location through `service.ts`, handed in by the dispatcher's row with the address rule
 * every hop is held to. Everything that judges what arrived — the lists, the hash, the archive, the layout — imports
 * no network module (the test above this one), so the one place the two meet is the `tool` row, `cli/tool.ts`.
 */
test('tool download reaches a host only through the fetcher the dispatcher hands in, every hop held to the address rule', () => {
  const row = readText(join(cliRoot, 'src', 'cli', 'tool.ts'));
  assert.match(row, /import \{ releaseFetcher \} from '\.\.\/service\.ts';/);
  assert.match(row, /import \{ isAddress \} from '\.\.\/tools\.ts';/);
  assert.match(row, /run: \(args\) => commandTool\(args, releaseFetcher\(\{ hop: isAddress, redirects: REDIRECTS, bound: LOOK_BOUND_MS \}\)\)/);

  // The only rows that reach the service client are the ones that say so: a new row reaching it is a decision.
  const reaching = COMMANDS.map((command) => `cli/${command.name}.ts`).filter((entry) => reachableFrom(entry).has(SERVICE_CLIENT));
  assert.deepEqual(reaching.sort(), ['cli/agent.ts', 'cli/connect.ts', 'cli/driver.ts', 'cli/import.ts', 'cli/retire.ts', 'cli/tool.ts']);
});

/**
 * `driver retry <quest>` without `--at` reads this machine's session records since RETRY1b (D126's RETRY1b note), to mark
 * the quest at its failures as the driver counts them. The same shape again: the count (`strikes.ts`) and the verb
 * (`driverconfig.ts`) import no network module (the file-local test above), and the records arrive through
 * `service.ts`, handed in by the `driver` row, `cli/driver.ts`, the one line where the two meet. `driver review` reads the
 * registry's checkouts the same way since REVIEWENV1a, to look for a procedure in them (`reviews.ts` imports no network
 * module either).
 */
test('driver retry and review read the service only through the readers the dispatcher hands in', () => {
  const row = readText(join(cliRoot, 'src', 'cli', 'driver.ts'));
  assert.match(row, /import \{ registryCheckouts, sessionRecords \} from '\.\.\/service\.ts';/);
  // WORKFLOW1d hands the `workflow` verbs in after them, which read the registry through the same reader.
  assert.match(row, /run: \(args\) => commandDriver\(args, \(\) => sessionRecords\(\), \(\) => registryCheckouts\(\), \(each, context\) => commandWorkflow\(each, context\)\)/);
});

/**
 * `check` verifies against the LOCK, which already carries a hash per file — so it needs neither the
 * canon nor a network to answer. That is what makes it usable in a hook or an air-gapped build, and it
 * is the property most easily lost by an innocent-looking "just re-read the canon to compare" change.
 */
test('check passes with no canon present at all', () => {
  const previous = process.env.DAORIS_CANON;
  // Points the resolver at a path that does not exist rather than deleting the real canon: the
  // guarantee is about `check` never NEEDING the canon, and a test that had to destroy the tree to
  // prove it could not run beside the others.
  process.env.DAORIS_CANON = join(cliRoot, '_fixtures', 'no-such-canon');
  try {
    const out: string[] = [];
    // No `packageRoot` on purpose — commandCheck does not accept one, which is the guarantee stated
    // as a signature. The env var covers the other route to a canon, so a future version that grew
    // either one would fail here rather than quietly acquiring a dependency.
    const code = commandCheck({ root: repoRoot, write: (s: string) => out.push(s) });
    assert.equal(code, 0, out.join('\n'));
  } finally {
    if (previous === undefined) delete process.env.DAORIS_CANON;
    else process.env.DAORIS_CANON = previous;
  }
});
