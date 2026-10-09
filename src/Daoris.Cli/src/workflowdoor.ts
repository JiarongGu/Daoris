// `daoris driver workflow` — the terminal's door to workflows (D50; D157, the workflow design §4.7).
//
// `show --repository|--workspace [--kind]` draws the workflow work there follows: Current, read from the rules as they stand
// (WORKFLOW1a), or the named workflow a choice gives (WORKFLOW1e). The rest are named workflows (WORKFLOW1d), one file each under
// the home's `workflows/` (D63), beside `driver.json` as every door derives the home: `list`, `show <id>[@<version>]`, `new`,
// `edit`, `apply`, `export` and `import`. A writer prints the change as Ask Daoris's card draws it (design §8.2) and saves a new
// version, never editing one; `--plan` prints it and saves nothing. Every write is atomic, BOM-less and LF, and a file this door
// cannot read is never written over. `use` and `kind` edit the choice in `driver.json` (WORKFLOW1e, design §4.7), through
// `workflowchoice.ts`, the driver's twin on one table.
//
// 🔴 A choice names a workflow, and work binds its newest version at its first start (the driver's `WorkflowRunBindings`); the gate
// that reads that version is WORKFLOW1f's, so every verb that names a workflow says work still lands as Current says. What a
// workflow may say, its digest, its diff and the presets are `namedworkflows.ts`'s; this door adds the terminal's words and the
// change language.
//
// A MANAGEMENT verb, offline: it reads and writes files under the home, and reads the registry only through the reader it is
// handed, as `review` does.

import { existsSync, readdirSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { flagValue } from './args.ts';
import { atName } from './casefold.ts';
import { DaorisError, type ExitCode } from './errors.ts';
import { readText, writeTextAtomic } from './fsx.ts';
import { readPlugins } from './plugins.ts';
import { normalizeWorkspace } from './remotemap.ts';
import { inScope, type CheckoutsReader } from './reviews.ts';
import { writeDriverChoices, type DriverChoices } from './driverconfig.ts';
import {
  CURRENT, applyWorkflowEdit, readKeptVersions, resolveWorkflow, type WorkflowChoiceEdit, type WorkflowSelected,
} from './workflowchoice.ts';
import type { CommandArgs } from './types.ts';
import { WORKFLOW_LIMITS, currentWorkflow, workflowPluginsOf, workflowSaid, type CurrentWorkflow } from './workflows.ts';
import {
  WORKFLOW_PRESETS, field, isWorkflowId, kindOf, planAddVersion, readSteps, readWorkflowFile, rendered, versionDigest,
  withGates, workflowDiff, workflowIdProblem, workflowLine, type NamedStep, type NamedVersion, type SavedStep, type WorkflowRead,
} from './namedworkflows.ts';

const USAGE = '`driver workflow` takes list; show <id>[@<version>]; show --repository <name>|--workspace <name> [--kind <kind>]; '
  + 'new <id> --from <preset>|current:<repository>|<id>[@<version>] [--with-opinion] [--with-look] [--name "…"]; '
  + 'edit <id> [--base <version>] <change>…; apply <id> <file> [--base <version>]; export <id> [--to <file>]; import <file>; '
  + 'use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>]; '
  + 'kind <workspace> <kind> --label "…" [--paths <path,…>]|--drop. '
  + 'new, edit, apply, import, use and kind print their change, and with --plan save nothing — e.g. '
  + '`daoris driver workflow new docs --from pull-request-no-press --plan`.';

const CHANGES = 'a change is add <kind> --after <step> [--id <id>] [<field>=<value>…], remove <step>, set <step> '
  + '<field>=<value>…, move <step> --after <step>, or on <step> failed=wait|send-back|stop — e.g. `daoris driver workflow '
  + 'edit docs --base 1 remove look set landing accept=automatic`.';

/**
 * Said by every verb that names a named workflow (WORKFLOW1e): a choice names one and work binds its newest version, and until the
 * gate reads that version (WORKFLOW1f) work lands as Current says.
 */
export const WHAT_CHOOSES = '  A choice names a workflow (`daoris driver workflow use <id> --repository <name>|--workspace <name> '
  + '[--kind <kind>]`), and work binds its newest version at its first start; nothing at a gate reads it yet, so work still lands '
  + 'as Current says.';

/** Said by every verb that edits the choice (WORKFLOW1e), until the gate reads what a run bound (WORKFLOW1f). */
export const NOT_GATED = '  Nothing at a gate reads a chosen workflow yet: work still lands as Current says until it does.';

/** What the door is handed by `commandDriver`: the file it read, its choices, the registry's reader, and the clock. */
export interface WorkflowDoorContext {
  /** `driver.json`; the workflows are in the `workflows` folder beside it. */
  path: string;
  choices: DriverChoices;
  checkouts: CheckoutsReader;
  now?: () => Date;
}

/** A workflow's file as found: the file as parsed (undefined where it is not JSON) and what it reads as. */
interface Found {
  id: string;
  path: string;
  file: unknown;
  read: WorkflowRead;
}

/** `daoris driver workflow …` (design §4.7). */
export function commandWorkflow({ root, argv, write }: CommandArgs, context: WorkflowDoorContext): ExitCode | Promise<ExitCode> {
  const folder = join(dirname(context.path), 'workflows');
  const now = context.now ?? (() => new Date());
  const verb = argv[1];
  switch (verb) {
    case 'list':
      flags('list', 2, new Set(), new Set());
      return list();
    case 'show':
      return argv.includes('--repository') || argv.includes('--workspace') ? showChosen() : show();
    case 'use':
      return use();
    case 'kind':
      return kind();
    case 'new':
      return create();
    case 'edit':
      return edit();
    case 'apply':
      return apply();
    case 'export':
      return exportFile();
    case 'import':
      return importFile();
    default:
      throw new DaorisError(USAGE);
  }

  /** Refuses a flag the verb does not take, and an operand past the ones it names. */
  function flags(name: string, operands: number, valued: ReadonlySet<string>, switches: ReadonlySet<string>): void {
    let seen = 0;
    for (let at = 2; at < argv.length; at += 1) {
      const token = argv[at]!;
      if (valued.has(token)) at += 1;
      else if (switches.has(token)) continue;
      else if (token.startsWith('--')) throw new DaorisError(`\`${token}\` is not a flag \`driver workflow ${name}\` takes — ${USAGE}`);
      else if (++seen > operands - 2) throw new DaorisError(USAGE);
    }
    if (seen < operands - 2) throw new DaorisError(USAGE);
  }

  function load(id: string): Found | null {
    // An id is the file's name, so one that is no id would name a path outside the folder.
    if (!isWorkflowId(id)) throw new DaorisError(workflowIdProblem(id)!, 1);
    const path = join(folder, `${id}.json`);
    if (!existsSync(path)) return null;
    let file: unknown;
    try {
      file = JSON.parse(readText(path));
    } catch {
      return { id, path, file: undefined, read: { id: null, name: null, versions: [], problem: `\`${id}.json\` is not readable JSON.` } };
    }
    const read = readWorkflowFile(file);
    if (read.problem === null && read.id !== id) {
      read.problem = `\`${id}.json\` holds the workflow \`${read.id}\`; a workflow's file is named by its id.`;
    }
    return { id, path, file, read };
  }

  function required(id: string): Found {
    const found = load(id);
    if (found === null) throw new DaorisError(`no workflow \`${id}\` here — \`daoris driver workflow list\` names them.`);
    return found;
  }

  /** `<id>` or `<id>@<version>`. */
  function named(token: string | undefined): { id: string; version: number | null } {
    if (token === undefined || token.startsWith('--')) throw new DaorisError(USAGE);
    const at = token.lastIndexOf('@');
    if (at === -1) return { id: token, version: null };
    const version = token.slice(at + 1);
    if (!/^[1-9][0-9]{0,8}$/.test(version)) throw new DaorisError(`\`${token}\` names no version — \`<id>@<version>\`, such as \`docs-to-pr@2\`.`);
    return { id: token.slice(0, at), version: Number(version) };
  }

  function list(): ExitCode {
    const ids = existsSync(folder)
      ? readdirSync(folder).filter((name) => name.endsWith('.json')).map((name) => name.slice(0, -5)).filter(isWorkflowId).sort()
      : [];
    if (ids.length === 0) write(`daoris: no workflow is named in ${folder} yet.`);
    else write(`daoris: ${ids.length === 1 ? '1 workflow' : `${ids.length} workflows`} named in ${folder}:`);
    for (const id of ids) {
      const { read } = load(id)!;
      if (read.problem !== null) {
        write(`  \`${id}\` — cannot be read: ${read.problem}`);
        continue;
      }
      const newest = read.versions[read.versions.length - 1]!;
      if (newest.steps !== null) {
        write(`  \`${id}\` — ${read.name}: v${newest.version}, ${newest.steps.length} steps. ${workflowLine(newest.steps)}`);
        continue;
      }
      const reads = [...read.versions].reverse().find((each) => each.steps !== null);
      write(`  \`${id}\` — ${read.name}: v${newest.version} cannot be read here (${newest.problem})`
        + (reads === undefined ? '; no version of it reads here.' : `; v${reads.version} is the newest that reads.`));
    }
    write('Presets built in, for `daoris driver workflow new <id> --from <preset>` (`--with-opinion`, `--with-look`):');
    for (const preset of WORKFLOW_PRESETS) {
      write(`  \`${preset.id}\` — ${preset.name}. ${workflowLine(readSteps(preset.steps, 1).steps!)}`);
    }
    write(WHAT_CHOOSES);
    return 0;
  }

  function show(): ExitCode {
    flags('show', 3, new Set(), new Set());
    const { id, version } = named(argv[2]);
    const { read } = required(id);
    if (read.problem !== null) throw new DaorisError(`\`${id}\` cannot be read: ${read.problem}`, 1);
    const shown = version === null ? read.versions[read.versions.length - 1]! : read.versions.find((each) => each.version === version);
    if (shown === undefined) {
      throw new DaorisError(`\`${id}\` keeps no v${version} — it keeps ${read.versions.map((each) => `v${each.version}`).join(', ')}.`);
    }
    if (shown.steps === null) {
      throw new DaorisError(`\`${id}\` v${shown.version} cannot be read here: ${shown.problem} The file keeps it as written.`, 1);
    }

    const newest = shown === read.versions[read.versions.length - 1];
    write(`daoris: \`${id}\` — ${read.name}: v${shown.version}${newest ? ', its newest' : ''}, saved ${shown.at} ${doorSaid(shown.door!)} `
      + `(digest ${shown.digest}).`);
    write(`  ${workflowLine(shown.steps)}`);
    for (const step of shown.steps) for (const line of stepSaid(step)) write(line);
    write(`  Versions kept: ${read.versions.map((each) => `v${each.version}${each.steps === null ? ' (cannot be read here)' : ''}`).join(', ')}.`);
    write(WHAT_CHOOSES);
    return 0;
  }

  /**
   * The workflow a repository's or a workspace's work follows (WORKFLOW1a, WORKFLOW1e; design §4.1, §4.7): §4.1 resolved for the
   * kind named, as a task of that kind would be. Current is drawn from the rules as they stand; a named workflow, its newest
   * version, which new work binds. A workspace's is read from the file alone; a repository's workspace from the registry.
   */
  function showChosen(): ExitCode | Promise<ExitCode> {
    const usage = '`driver workflow show` takes --repository <name>|--workspace <name>, and --kind <kind> for a kind of task: '
      + 'the workflow its work follows, Current or the one chosen — e.g. `daoris driver workflow show --repository web-app`; or a '
      + 'named workflow\'s <id>[@<version>].';
    const valued = new Set(['--repository', '--workspace', '--kind']);
    for (let at = 2; at < argv.length; at += 1) {
      const token = argv[at]!;
      if (valued.has(token)) at += 1;
      else if (token.startsWith('--')) throw new DaorisError(`\`${token}\` is not a flag \`driver workflow show\` takes — ${usage}`);
      else throw new DaorisError(usage);
    }

    const repository = flagValue(argv, '--repository');
    const workspace = flagValue(argv, '--workspace');
    const kind = flagValue(argv, '--kind');
    if ((repository === undefined) === (workspace === undefined)) throw new DaorisError(usage);
    const task = kind === undefined ? null : { kind, workflow: null };
    if (workspace !== undefined) {
      const selected = resolveWorkflow(context.choices, null, workspace, task);
      const whose = `the workspace \`${workspace}\`${kindSaid(selected)}`;
      if (selected.workflow !== CURRENT) return named(selected, whose, workspace, null, []);
      const said = workflowSaid(currentWorkflow(context.choices, null, workspace, plugins()), whose, 'for each repository there that sets none of its own');
      for (const line of withChoice(said, selected, workspace, null, [])) write(line);
      return 0;
    }

    return currentOf(repository!).then(({ drawn, where, notes, inWorkspace }) => {
      const selected = resolveWorkflow(context.choices, repository!, inWorkspace, task);
      const whose = `\`${repository}\`${kindSaid(selected)}`;
      if (selected.workflow !== CURRENT) return named(selected, whose, normalizeWorkspace(inWorkspace), repository!, notes);
      for (const line of withChoice(workflowSaid(drawn, whose, where), selected, normalizeWorkspace(inWorkspace), repository!, notes)) write(line);
      return 0 as ExitCode;
    });

    /** Current's lines, with the registry's notes and, where a choice or a kind is read, what chose it under the header. */
    function withChoice(said: string[], selected: WorkflowSelected, circle: string, owner: string | null, notes: string[]): string[] {
      const [header, ...rest] = said;
      const chosen = selected.level === 'current' && kind === undefined ? [] : [`  Chosen: ${whyChosen(selected, owner, circle)}.`];
      return [header!, ...notes, ...chosen, ...undeclaredSaid(selected, circle), ...rest];
    }

    /** A named workflow chosen: its newest version, which new work binds, or why it cannot be read here. */
    function named(selected: WorkflowSelected, whose: string, circle: string, owner: string | null, notes: string[]): ExitCode {
      const why = whyChosen(selected, owner, circle);
      const found = load(selected.workflow);
      const newest = found?.read.problem === null ? found.read.versions[found.read.versions.length - 1]! : null;
      if (newest?.steps == null) {
        const problem = found === null ? 'no workflow by that id is saved here'
          : found.read.problem ?? `v${newest!.version} cannot be read here: ${newest!.problem}`;
        write(`daoris: ${whose} chooses \`${selected.workflow}\`, ${why}, which cannot be read here: ${problem.replace(/\.$/, '')}.`);
        for (const line of [...notes, ...undeclaredSaid(selected, circle)]) write(line);
        write(WHAT_CHOOSES);
        return 1;
      }
      write(`daoris: ${whose} follows \`${selected.workflow}\` — ${found!.read.name}, v${newest.version} (digest ${newest.digest}), ${why}.`);
      for (const line of [...notes, ...undeclaredSaid(selected, circle)]) write(line);
      write(`  ${workflowLine(newest.steps)}`);
      for (const step of newest.steps) for (const line of stepSaid(step)) write(line);
      write(WHAT_CHOOSES);
      return 0;
    }
  }

  /** ` for kind `docs` (Documentation)`, where a kind is read; nothing where none is. */
  function kindSaid(selected: WorkflowSelected): string {
    return selected.kind === null ? '' : `, for kind \`${selected.kind}\`${selected.label === null ? '' : ` (${selected.label})`},`;
  }

  /** A kind named that the workspace does not declare, read as none. */
  function undeclaredSaid(selected: WorkflowSelected, circle: string): string[] {
    return selected.undeclared === null ? []
      : [`  \`${selected.undeclared}\` is not a kind the workspace \`${circle}\` declares, so it is read as none.`];
  }

  /** The workspace a repository is in, as the registry says, and whether it was read. */
  async function workspaceOf(name: string): Promise<{ read: true; workspace: string | null } | { read: false; unread: string }> {
    const read = await context.checkouts();
    if ('unread' in read) return { read: false, unread: read.unread };
    return { read: true, workspace: read.checkouts.find((each) => inScope(each, { repository: name }))?.workspace ?? null };
  }

  /**
   * `use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>]` (design §4.7): which workflow a repository's
   * or a workspace's work follows, or a kind's. A named workflow is one saved here whose newest version reads (§3.9); a
   * repository's kind is one its workspace declares, where the registry says which that is.
   */
  function use(): ExitCode | Promise<ExitCode> {
    const usage = '`driver workflow use` takes <id>|current|--clear, then --repository <name>|--workspace <name>, and --kind <kind> '
      + 'for a kind of task — e.g. `daoris driver workflow use docs-to-pr --workspace work --kind docs`.';
    const valued = new Set(['--repository', '--workspace', '--kind']);
    const operands: string[] = [];
    for (let at = 2; at < argv.length; at += 1) {
      const token = argv[at]!;
      if (valued.has(token)) at += 1;
      else if (token === '--clear' || token === '--plan') continue;
      else if (token.startsWith('--')) throw new DaorisError(`\`${token}\` is not a flag \`driver workflow use\` takes — ${usage}`);
      else operands.push(token);
    }
    const repository = flagValue(argv, '--repository');
    const workspace = flagValue(argv, '--workspace');
    const kind = flagValue(argv, '--kind');
    const clear = argv.includes('--clear');
    if ((repository === undefined) === (workspace === undefined) || operands.length !== (clear ? 0 : 1)) throw new DaorisError(usage);
    const chosen = clear ? null : operands[0]!;

    // A named workflow is chosen only where it reads here (design §3.9): refused where it would be chosen, naming why.
    if (chosen !== null && chosen !== CURRENT && isWorkflowId(chosen)) {
      const found = load(chosen);
      if (found === null) {
        throw new DaorisError(`no workflow \`${chosen}\` is saved here — \`daoris driver workflow list\` names them, and \`new\` names one.`);
      }
      if (found.read.problem !== null) throw new DaorisError(`\`${chosen}\` cannot be read: ${found.read.problem}`, 1);
      const newest = found.read.versions[found.read.versions.length - 1]!;
      if (newest.steps === null) {
        throw new DaorisError(`\`${chosen}\` v${newest.version} cannot be read here: ${newest.problem} It can be chosen once a `
          + 'version that reads is saved after it.', 1);
      }
    }

    const apply = (edit: WorkflowChoiceEdit, unchecked: string | null): ExitCode => {
      const edited = applyWorkflowEdit(context.choices, edit);
      if (edited.refusal !== null) {
        const declare = kind !== undefined && edited.refusal.includes('declares no kind')
          ? ` \`daoris driver workflow kind <workspace> ${kind} --label "…"\` declares one.` : '';
        throw new DaorisError(`${edited.refusal}${declare} Nothing was written.`, 1);
      }
      const owner = repository ?? null;
      const name = (repository ?? workspace)!;
      const whose = repository !== undefined ? `\`${repository}\`` : `the workspace \`${workspace}\``;
      const scope = repository !== undefined ? edited.maps!.workflows : edited.maps!.workspaceWorkflows;
      const before = repository !== undefined ? context.choices.workflows : context.choices.workspaceWorkflows;
      const after = atName(scope, name.trim());
      const was = atName(before, name.trim());
      if (JSON.stringify(after) === JSON.stringify(was)) {
        write(`daoris: ${whose} ${chosen === null ? 'chose no workflow' : `chooses \`${chosen}\` already`}`
          + `${kind === undefined ? ' as its default' : ` for kind \`${kind}\``}: nothing was written.`);
        return 0;
      }

      const forWhat = kind === undefined ? ' as its default' : ` for kind \`${kind}\``;
      if (chosen === null) write(`daoris: ${whose} no longer chooses a workflow${forWhat}.`);
      else if (chosen === CURRENT) write(`daoris: ${whose} now chooses Current${forWhat}: its rules as they stand, read at each gate.`);
      else {
        const found = load(chosen)!;
        const newest = found.read.versions[found.read.versions.length - 1]!;
        write(`daoris: ${whose} now chooses \`${chosen}\`${forWhat} — ${found.read.name}, v${newest.version} now: ${workflowLine(newest.steps!)}`);
      }
      if (owner !== null) {
        write(after === undefined
          ? `  \`${owner}\` names no workflow now, so its workspace's choice reaches it again.`
          : `  This replaces the workspace's for every kind: \`${owner}\` follows its own kinds and its own default, and Current `
            + 'where it names none.');
      }
      if (unchecked !== null) {
        write(`  The registry was not read (${unchecked}), so kind \`${kind}\` was not checked against the kinds \`${owner}\`'s `
          + 'workspace declares.');
      }
      if (chosen !== null) write('  Each new piece of work binds what is chosen at its first start; work already started keeps what it bound.');
      return savedChoice({ ...context.choices, ...edited.maps! });
    };

    const base = { use: chosen, ...(kind !== undefined ? { kind } : {}) };
    if (workspace !== undefined) return apply({ ...base, workspace }, null);
    if (kind === undefined || chosen === null) return apply({ ...base, repository: repository! }, null);
    return workspaceOf(repository!).then((found) => found.read
      ? apply({ ...base, repository: repository!, in: found.workspace }, null)
      : apply({ ...base, repository: repository! }, found.unread));
  }

  /**
   * `kind <workspace> <kind> --label "…" [--paths <path,…>]` and `kind <workspace> <kind> --drop` (design §4.2, §4.7): a kind of
   * task a workspace declares, with its label and the paths work of that kind keeps to; declared again, both are replaced.
   */
  function kind(): ExitCode {
    const usage = '`driver workflow kind` takes <workspace> <kind> --label "…" [--paths <path,…>], or <workspace> <kind> --drop — '
      + 'e.g. `daoris driver workflow kind work docs --label "Documentation" --paths "docs/**,**/*.md"`.';
    const valued = new Set(['--label', '--paths']);
    const operands: string[] = [];
    for (let at = 2; at < argv.length; at += 1) {
      const token = argv[at]!;
      if (valued.has(token)) at += 1;
      else if (token === '--drop' || token === '--plan') continue;
      else if (token.startsWith('--')) throw new DaorisError(`\`${token}\` is not a flag \`driver workflow kind\` takes — ${usage}`);
      else operands.push(token);
    }
    const label = flagValue(argv, '--label');
    const drop = argv.includes('--drop');
    if (operands.length !== 2 || drop === (label !== undefined) || (drop && argv.includes('--paths'))) throw new DaorisError(usage);
    const [workspace, id] = operands as [string, string];
    const paths = (flagValue(argv, '--paths') ?? '').split(',').map((each) => each.trim()).filter((each) => each.length > 0);

    const edited = applyWorkflowEdit(context.choices, drop ? { drop: id, workspace } : { declare: id, workspace, label: label!, paths });
    if (edited.refusal !== null) throw new DaorisError(`${edited.refusal} Nothing was written.`, 1);
    if (drop) {
      write(`daoris: the workspace \`${workspace}\` no longer declares kind \`${id}\`.`);
      const mapping = Object.entries(context.choices.workflows)
        .filter(([, choice]) => choice.kinds.some(([each]) => each === id)).map(([name]) => `\`${name}\``);
      if (mapping.length > 0) {
        write(`  ${mapping.join(', ')} still map${mapping.length === 1 ? 's' : ''} it; no task of that kind reaches `
          + `${mapping.length === 1 ? 'that mapping' : 'those mappings'} until \`${id}\` is declared again.`);
      }
    } else {
      const shared = atName(edited.maps!.workspaceWorkflows, workspace.trim());
      const declared = shared?.kinds.find(([each]) => each === id)?.[1];
      write(`daoris: the workspace \`${workspace}\` declares kind \`${id}\` — ${label}, `
        + `${paths.length === 0 ? 'with no paths' : `its paths ${paths.map((each) => `\`${each}\``).join(', ')}`}.`);
      write(declared?.workflow
        ? `  It maps to \`${declared.workflow}\` there.`
        : `  It maps to no workflow there: \`daoris driver workflow use <id> --workspace ${workspace} --kind ${id}\` maps one, and `
          + '`--repository <name>` maps it for one repository.');
    }
    return savedChoice({ ...context.choices, ...edited.maps! });
  }

  /** The choices written, or not with `--plan`, and said either way, with what reads them: no gate yet. */
  function savedChoice(choices: DriverChoices): ExitCode {
    if (argv.includes('--plan')) {
      write('  --plan: nothing was written.');
    } else {
      writeDriverChoices(context.path, choices);
      write(`  Written to ${context.path}.`);
    }
    write(NOT_GATED);
    return 0;
  }

  /** The plugins beside the file, as the driver reads them, the landing's and the opinion's doors with it. */
  function plugins() {
    return workflowPluginsOf(readPlugins(dirname(context.path)));
  }

  /**
   * A repository's Current: its workspace is the registry's, read as `review` reads it; unread, it is drawn only where no rule
   * in the file is set for a workspace, since none could then reach it.
   */
  async function currentOf(name: string): Promise<{ drawn: CurrentWorkflow; where: string; notes: string[]; inWorkspace: string | null }> {
    const read = await context.checkouts();
    const notes: string[] = [];
    let inWorkspace: string | null = null;
    let where: string;
    if ('unread' in read) {
      const { choices } = context;
      const shared = [choices.workspaceLandings, choices.workspaceReviews, choices.workspaceOpinions, choices.workspaceWorkflows];
      if (shared.some((map) => Object.keys(map).length > 0)) {
        throw new DaorisError(`cannot say which workspace's rules reach \`${name}\`: the registry could not be read — `
          + `${read.unread} — and this file sets rules for a workspace. Nothing was drawn. \`--workspace <name>\` draws a `
          + 'workspace\'s Current from this file alone.');
      }
      where = 'its workspace not read';
      notes.push(`  The registry was not read (${read.unread}); no rule here is set for a workspace, so none reaches it.`);
    } else {
      const checkout = read.checkouts.find((each) => inScope(each, { repository: name }));
      inWorkspace = checkout?.workspace ?? null;
      where = `in the workspace \`${normalizeWorkspace(inWorkspace)}\``;
      if (checkout === undefined) {
        notes.push(`  \`${name}\` is not in the registry, so it is read as in no workspace: the \`${normalizeWorkspace(null)}\` `
          + 'workspace\'s rules reach it.');
      }
    }
    return { drawn: currentWorkflow(context.choices, name, inWorkspace, plugins()), where, notes, inWorkspace };
  }

  function create(): ExitCode | Promise<ExitCode> {
    flags('new', 3, new Set(['--from', '--name']), new Set(['--with-opinion', '--with-look', '--plan']));
    const id = argv[2]!;
    const idProblem = workflowIdProblem(id);
    if (idProblem !== null) throw new DaorisError(idProblem, 1);
    const from = flagValue(argv, '--from');
    if (from === undefined) throw new DaorisError(`\`driver workflow new\` needs --from: a preset, \`current:<repository>\` or a saved workflow — ${USAGE}`);
    const existing = load(id);
    if (existing !== null) {
      const at = existing.read.versions[existing.read.versions.length - 1];
      throw new DaorisError(`\`${id}\` is a workflow here already${at === undefined ? '' : `, at v${at.version}`}: \`daoris driver `
        + `workflow edit ${id} …\` adds a version to it.`, 1);
    }
    const gates = { opinion: argv.includes('--with-opinion'), look: argv.includes('--with-look') };
    const gated = [gates.opinion ? 'with a second opinion' : null, gates.look ? 'with your look first' : null].filter(Boolean).join(' and ');

    const save = (steps: SavedStep[], source: string, name: string): ExitCode => {
      const at = moment(now());
      const plan = planAddVersion(null, { id, name: flagValue(argv, '--name') ?? name, door: 'terminal', at, steps: withGates(steps, gates) });
      if (plan.problem !== null) throw new DaorisError(plan.problem, 1);
      const version = readSteps((plan.result!['versions'] as { steps: unknown }[])[0]!.steps, 1).steps!;
      write(`daoris: \`${id}\` v1, a new workflow — ${plan.result!['name'] as string}, from ${source}${gated ? `, ${gated}` : ''}:`);
      said(version, workflowDiff(null, version).said);
      return saved(join(folder, `${id}.json`), plan.result!);
    };

    if (from.startsWith('current:')) {
      const repository = from.slice('current:'.length);
      if (repository.length === 0) throw new DaorisError(`\`current:\` names a repository, such as \`current:web-app\` — ${USAGE}`);
      return currentOf(repository).then(({ drawn }) => save(stepsOfCurrent(drawn), `\`${repository}\`'s Current (version ${drawn.version})`,
        `${repository}'s Current`));
    }
    const preset = from.includes('@') ? undefined : WORKFLOW_PRESETS.find((each) => each.id === from);
    if (preset !== undefined) {
      // Short enough that every preset's name with both stays within a name's 60 characters.
      const name = `${preset.name}${gates.opinion ? ' + second opinion' : ''}${gates.look ? ' + your look' : ''}`;
      return save(preset.steps.map((step) => ({ ...step })), `the preset \`${preset.id}\``, name);
    }
    const source = named(from);
    const found = load(source.id);
    if (found === null) {
      throw new DaorisError(`\`${from}\` is no preset, no saved workflow and no \`current:<repository>\` — the presets are `
        + `${WORKFLOW_PRESETS.map((each) => `\`${each.id}\``).join(', ')}.`);
    }
    const { version, raw } = versionOf(found, source.version);
    return save(raw, `\`${source.id}\` v${version.version}`, found.read.name!);
  }

  /** A saved workflow's version that reads, and its steps as the file holds them; the newest where none is named. */
  function versionOf(found: Found, wanted: number | null): { version: NamedVersion; raw: SavedStep[] } {
    if (found.read.problem !== null) throw new DaorisError(`\`${found.id}\` cannot be read: ${found.read.problem}`, 1);
    const versions = found.read.versions;
    const version = wanted === null ? versions[versions.length - 1]! : versions.find((each) => each.version === wanted);
    if (version === undefined) {
      throw new DaorisError(`\`${found.id}\` keeps no v${wanted} — it keeps ${versions.map((each) => `v${each.version}`).join(', ')}.`);
    }
    if (version.steps === null) throw new DaorisError(`\`${found.id}\` v${version.version} cannot be read here: ${version.problem}`, 1);
    const index = versions.indexOf(version);
    const raw = ((found.file as { versions: { steps: unknown }[] }).versions[index]!.steps);
    return { version, raw: JSON.parse(JSON.stringify(raw)) as SavedStep[] };
  }

  /** The base a change is made against: the newest version, which `--base` must name where it is given (design §8.1). */
  function baseOf(found: Found): { version: NamedVersion; raw: SavedStep[] | null } {
    if (found.read.problem !== null) throw new DaorisError(`\`${found.id}\` cannot be read: ${found.read.problem}`, 1);
    const newest = found.read.versions[found.read.versions.length - 1]!;
    const base = flagValue(argv, '--base');
    if (base !== undefined && base !== String(newest.version)) {
      throw new DaorisError(`\`${found.id}\` is at v${newest.version} now, not v${base}: read it again (\`daoris driver workflow `
        + `show ${found.id}\`) and make the change against v${newest.version}.`, 1);
    }
    return { version: newest, raw: newest.steps === null ? null : versionOf(found, newest.version).raw };
  }

  function edit(): ExitCode {
    const id = named(argv[2]).id;
    const found = required(id);
    const { version, raw } = baseOf(found);
    if (raw === null) {
      throw new DaorisError(`\`${id}\` v${version.version} cannot be read here: ${version.problem} \`daoris driver workflow apply `
        + `${id} <file>\` saves a whole version after it.`, 1);
    }
    const tokens: string[] = [];
    for (let at = 3; at < argv.length; at += 1) {
      const token = argv[at]!;
      if (token === '--base') at += 1;
      else if (token !== '--plan') tokens.push(token);
    }
    if (tokens.length === 0) throw new DaorisError(`\`driver workflow edit\` needs a change: ${CHANGES}`);
    return change(found, version, applyChanges(raw, tokens, version.version, found.read));
  }

  function apply(): ExitCode {
    flags('apply', 4, new Set(['--base']), new Set(['--plan']));
    const id = named(argv[2]).id;
    const found = required(id);
    const { version } = baseOf(found);
    const path = fileOperand(argv[3]!);
    const held = parsed(path);
    const steps = Array.isArray(held) ? held
      : held !== null && typeof held === 'object' && Array.isArray((held as Record<string, unknown>)['steps']) ? (held as Record<string, unknown>)['steps']
        : held !== null && typeof held === 'object' && Array.isArray((held as Record<string, unknown>)['versions'])
          ? ((held as { versions: unknown[] }).versions[(held as { versions: unknown[] }).versions.length - 1] as Record<string, unknown> | undefined)?.['steps']
          : undefined;
    if (steps === undefined) {
      throw new DaorisError(`\`${argv[3]}\` holds no steps: a list of steps, a version's \`steps\`, or a workflow's \`versions\`, its newest taken.`, 1);
    }
    return change(found, version, steps as SavedStep[]);
  }

  /** A new version made from steps, against the base: validated, compared, said, and saved unless `--plan`. */
  function change(found: Found, base: NamedVersion, steps: SavedStep[]): ExitCode {
    // Every version a bound run names is kept beyond the newest (design §2.6): the driver binds them (WORKFLOW1e).
    const plan = planAddVersion(found.file, { id: found.id, name: null, door: 'terminal', at: moment(now()), steps }, readKeptVersions(folder, found.id));
    if (plan.problem !== null) throw new DaorisError(plan.problem, 1);
    const next = readSteps(steps, plan.version!, found.read.versions.filter((each) => each.steps !== null)).steps!;
    if (base.steps !== null && base.digest === versionDigest(next)) {
      write(`daoris: \`${found.id}\` v${base.version} already says this: nothing was written.`);
      return 0;
    }
    write(`daoris: \`${found.id}\` v${base.version} → v${plan.version} — ${found.read.name}${base.steps === null
      ? ` (v${base.version} cannot be read here, so the change is drawn from nothing)` : ''}:`);
    said(next, workflowDiff(base.steps, next).said);
    return saved(found.path, plan.result!);
  }

  function exportFile(): ExitCode {
    flags('export', 3, new Set(['--to']), new Set());
    const id = named(argv[2]).id;
    const found = required(id);
    if (found.file === undefined) throw new DaorisError(`\`${id}\` cannot be exported: ${found.read.problem}`, 1);
    const text = `${JSON.stringify(found.file, null, 2)}\n`;
    const to = flagValue(argv, '--to');
    if (to === undefined) {
      write(text.slice(0, -1));
      return 0;
    }
    const path = fileOperand(to);
    if (existsSync(path)) throw new DaorisError(`\`${to}\` exists already: export writes a new file, and never over one.`);
    writeTextAtomic(path, text);
    write(`daoris: \`${id}\` written to ${path}, with ${found.read.versions.length === 1 ? 'its version' : `its ${found.read.versions.length} versions`}:`
      + ` \`daoris driver workflow import <file>\` reads it on another machine.`);
    return 0;
  }

  function importFile(): ExitCode {
    flags('import', 3, new Set(), new Set(['--plan']));
    const from = argv[2]!;
    const file = parsed(fileOperand(from));
    const read = readWorkflowFile(file);
    if (read.problem !== null) throw new DaorisError(`\`${from}\` cannot be imported: ${read.problem}`, 1);
    const unread = read.versions.find((each) => each.steps === null);
    if (unread !== undefined) throw new DaorisError(`\`${from}\` cannot be imported: ${unread.problem}`, 1);
    const id = read.id!;
    const newest = read.versions[read.versions.length - 1]!;
    const here = load(id);
    if (here !== null) {
      const same = here.read.problem === null && here.read.versions.length === read.versions.length
        && here.read.versions.every((each, index) => each.version === read.versions[index]!.version && each.digest === read.versions[index]!.digest);
      if (same) {
        write(`daoris: \`${id}\` is here already, with the same versions: nothing was written.`);
        return 0;
      }
      const at = here.read.versions[here.read.versions.length - 1];
      throw new DaorisError(`\`${id}\` is a workflow here already${at === undefined ? '' : `, at v${at.version}`}, and the file's `
        + `versions differ: \`daoris driver workflow apply ${id} ${from}\` adds the file's newest version to it.`, 1);
    }

    write(`daoris: \`${id}\` — ${read.name}, from ${from}: ${read.versions.length === 1 ? 'one version' : `${read.versions.length} versions`}, `
      + `its newest v${newest.version}:`);
    said(newest.steps!, workflowDiff(null, newest.steps!).said);
    return saved(join(folder, `${id}.json`), file as Record<string, unknown>);
  }

  /** The new version's line, then its change as the card draws it, under the header. */
  function said(steps: readonly NamedStep[], lines: readonly string[]): void {
    write(`  ${workflowLine(steps)}`);
    for (const line of lines) write(`  ${line}`);
  }

  /** Written, or not with `--plan`, and said either way, with what reads it: nothing yet. */
  function saved(path: string, file: Record<string, unknown>): ExitCode {
    if (argv.includes('--plan')) {
      write('  --plan: nothing was written.');
    } else {
      writeTextAtomic(path, `${JSON.stringify(file, null, 2)}\n`);
      write(`  Written to ${path}.`);
    }
    write(WHAT_CHOOSES);
    return 0;
  }

  function fileOperand(token: string): string {
    if (token === undefined || token.startsWith('--')) throw new DaorisError(USAGE);
    return isAbsolute(token) ? token : resolve(root, token);
  }

  function parsed(path: string): unknown {
    if (!existsSync(path)) throw new DaorisError(`there is no file \`${path}\`.`);
    try {
      return JSON.parse(readText(path));
    } catch {
      throw new DaorisError(`\`${path}\` is not readable JSON.`, 1);
    }
  }
}

/** A moment as a version keeps it: UTC, to the second. */
export function moment(at: Date): string {
  return `${new Date(Math.floor(at.getTime() / 1000) * 1000).toISOString().slice(0, 19)}Z`;
}

/**
 * Which level chose a workflow, in words (design §4.1, *the run says why*): *chosen by `web-app` for Documentation* — the words the
 * driver's look says a binding in (`WorkflowRunBindings.Why`).
 */
export function whyChosen(selected: WorkflowSelected, repository: string | null, workspace: string): string {
  const kind = selected.label ?? selected.kind;
  switch (selected.level) {
    case 'task': return 'chosen by its ask';
    case 'repository-kind': return `chosen by \`${repository}\` for ${kind}`;
    case 'repository': return `\`${repository}\`'s default`;
    case 'workspace-kind': return `chosen by the workspace \`${workspace}\` for ${kind}`;
    case 'workspace': return `the workspace \`${workspace}\`'s default`;
    default: return 'since nothing names a workflow';
  }
}

/** Which door saved a version, as `show` says it (design §2.5): never *the person*, since a terminal's edit cannot be told from an agent's. */
function doorSaid(door: string): string {
  if (door === 'screen') return 'on the screen';
  if (door === 'terminal') return 'at a terminal';
  return `through Ask Daoris (\`${door.slice('ask-daoris:'.length)}\`)`;
}

/** A named step as `show` draws it: who takes part and the person's press, what it is set to, and its limit. */
function stepSaid(step: NamedStep): string[] {
  const part = {
    work: 'Agent alone', opinion: 'Agent alone', look: 'Agent + you · your press: Reviewed',
    landing: field(step, 'accept') === 'you' ? 'You · your press: Accept' : 'Automatic · Daoris',
    'pull-request': 'You · your press: Merge', 'go-ahead': 'You · your answer',
  }[step.kind] ?? 'Automatic';
  const set = step.fields.map(([name, value]) => `${name} ${rendered(value)}`).join('; ');
  const lines = [`  ${step.id} · ${step.kind} — ${part}.${set === '' ? '' : ` ${set}.`}`];
  const limit = kindOf(step.kind)!.limit;
  if (limit !== null) lines.push(`      ${WORKFLOW_LIMITS[limit]}`);
  return lines;
}

/**
 * Current copied into steps (design §2.7's *Save as a workflow…*), each as Current draws it so the two read the same: the
 * opinion with its reviewers and switches, the look in its environment, the landing with its form, its press, and on a branch
 * its pattern and plugin, `none` where it names none. The standing answer and the landing's `tidy` stay declarations (§2.3).
 */
export function stepsOfCurrent(current: CurrentWorkflow): SavedStep[] {
  return current.steps.map((step): SavedStep => {
    const s = step.settings;
    switch (step.kind) {
      case 'opinion':
        return { id: 'opinion', kind: 'opinion', reviewers: s['reviewers'], required: s['required'], steps: (s['on'] as string[]).includes('steps'), recheck: s['recheck'] };
      case 'look':
        return { id: 'look', kind: 'look', environment: s['environment'] };
      case 'landing':
        return s['form'] === 'merge'
          ? { id: 'landing', kind: 'landing', form: 'merge', accept: 'you' }
          : { id: 'landing', kind: 'landing', form: 'branch', accept: s['accept'], pattern: s['pattern'], plugin: s['plugin'] ?? 'none' };
      default:
        return { id: step.kind, kind: step.kind };
    }
  });
}

/**
 * The change language (design §4.7) applied to a version's steps as saved, in order, all making one version: `add <kind> --after
 * <step> [--id <id>] [<field>=<value>…]`, `remove <step>`, `set <step> <field>=<value>…`, `move <step> --after <step>`, `on
 * <step> failed=…`. A value is read by its field's type (a switch, a list of names by commas); an empty value clears the field.
 * What the steps then say is the version's validation's to judge.
 */
export function applyChanges(base: readonly SavedStep[], tokens: readonly string[], version: number, read: WorkflowRead): SavedStep[] {
  const steps = base.map((step) => ({ ...step }));
  const KEYWORDS = new Set(['add', 'remove', 'set', 'move', 'on']);
  const at = (id: string | undefined): number => {
    const index = steps.findIndex((step) => step.id === id);
    if (id === undefined || index === -1) {
      throw new DaorisError(`no step \`${id ?? ''}\` in v${version} — its steps are ${steps.map((step) => `\`${step.id}\``).join(', ')}.`, 1);
    }
    return index;
  };
  const assign = (step: SavedStep, token: string): void => {
    const equals = token.indexOf('=');
    if (equals <= 0) throw new DaorisError(`\`${token}\` is no <field>=<value> — ${CHANGES}`);
    const name = token.slice(0, equals);
    const value = token.slice(equals + 1);
    if (name === 'id' || name === 'kind') throw new DaorisError(`a step's \`${name}\` is not changed — a new step takes a new id: ${CHANGES}`);
    if (value === '') {
      delete step[name];
      return;
    }
    const type = kindOf(step.kind)?.fields.find((each) => each.name === name)?.type;
    if (type === 'flag' && (value === 'true' || value === 'false')) step[name] = value === 'true';
    else if (type === 'names' || (type === 'checks' && value !== 'required')) step[name] = value.split(',').map((each) => each.trim());
    else step[name] = value;
  };
  const usedFor = (id: string, kind: string): boolean => steps.some((step) => step.id === id)
    || read.versions.some((each) => each.steps?.some((step) => step.id === id && step.kind !== kind));

  let i = 0;
  const next = (): string | undefined => tokens[i++];
  while (i < tokens.length) {
    const word = next()!;
    const operands: string[] = [];
    while (i < tokens.length && !KEYWORDS.has(tokens[i]!)) {
      const token = next()!;
      if (token.startsWith('--') && token !== '--after' && token !== '--id') {
        throw new DaorisError(`\`${token}\` is not a flag a change takes — ${CHANGES}`);
      }
      operands.push(token);
      if ((token === '--after' || token === '--id') && i < tokens.length) operands.push(next()!);
    }
    const option = (flag: string): string | undefined => {
      const index = operands.indexOf(flag);
      return index === -1 ? undefined : operands[index + 1];
    };
    const plain = operands.filter((token, index) => !token.startsWith('--') && operands[index - 1] !== '--after' && operands[index - 1] !== '--id');
    switch (word) {
      case 'add': {
        const [kind, ...fields] = plain;
        const after = option('--after');
        if (kind === undefined || after === undefined) throw new DaorisError(`\`add\` names a kind and --after <step> — ${CHANGES}`);
        let id = option('--id') ?? kind;
        for (let n = 2; option('--id') === undefined && usedFor(id, kind); n += 1) id = `${kind}-${n}`;
        const step: SavedStep = { id, kind };
        for (const token of fields) assign(step, token);
        steps.splice(at(after) + 1, 0, step);
        break;
      }
      case 'remove':
        if (plain.length !== 1) throw new DaorisError(`\`remove\` names one step — ${CHANGES}`);
        steps.splice(at(plain[0]), 1);
        break;
      case 'set':
      case 'on': {
        const [id, ...fields] = plain;
        if (fields.length === 0 || (word === 'on' && (fields.length !== 1 || !fields[0]!.startsWith('failed=')))) {
          throw new DaorisError(`\`${word}\` names a step and ${word === 'on' ? 'failed=wait|send-back|stop' : '<field>=<value>…'} — ${CHANGES}`);
        }
        const step = steps[at(id)]!;
        for (const token of fields) assign(step, token);
        break;
      }
      case 'move': {
        const after = option('--after');
        if (plain.length !== 1 || after === undefined || after === plain[0]) throw new DaorisError(`\`move\` names a step and --after another — ${CHANGES}`);
        const [step] = steps.splice(at(plain[0]), 1);
        steps.splice(at(after) + 1, 0, step!);
        break;
      }
      default:
        throw new DaorisError(`\`${word}\` is not a change — ${CHANGES}`);
    }
  }
  return steps;
}
