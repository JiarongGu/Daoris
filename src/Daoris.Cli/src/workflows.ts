// A repository's Current workflow (WORKFLOW1a, D157 point 7, the workflow design §2.7, §3.2): how its work moves, drawn from
// the rules as they stand, each step with the rule it was read from and the limit the runtime has. A view over the keys, never
// a migration: nothing here writes, and every gate goes on reading the rules as it did.
//
// 🔴 A TWIN with the driver's `WorkflowCurrent.cs`: the CLI and the driver share no code, so both hold ONE table, the driver's
// `Daoris.Desktop.Driver.Tests/fixtures/workflow-current.json`, cell for cell, the version included (`.claude/knowledge/twins.md`).
// The rules are read here as the driver resolves them: the landing as `LandingRules.Choose` (the repository's name matched as
// given, untrimmed), the review and the opinion as `reviewFor` and `opinionFor` (trimmed), the standing answer as
// `standingFor` reads it.
//
// Pure: it reads no file, reaches no network and spawns nothing. The door hands it the plugins it reads beside its file.

import { createHash } from 'node:crypto';
import { atName, findName, sameName } from './casefold.ts';
import type { DriverChoices, LandingRule } from './driverconfig.ts';
import { OPINION_DECLARED_ONLY, opinionFor } from './opinions.ts';
import type { PluginCatalog } from './plugins.ts';
import { normalizeWorkspace } from './remotemap.ts';
import { reviewFor } from './reviews.ts';

/** One plugin switched on and sound, as the derivation reads it: its id and the points it speaks on, in catalogue order. */
export interface WorkflowPlugin {
  id: string;
  points: string[];
}

/** Where a step was read from: the rule, or none for the work, and the level its rule stood at. */
export interface WorkflowSource {
  rule: 'landing' | 'review' | 'opinion' | null;
  level: 'repository' | 'workspace' | 'default';
}

/** A setting's value as a step carries it: text or none, a switch, or a list of names. */
export type WorkflowValue = string | null | boolean | string[];

/** One step of a workflow (design §3.1, §3.2): what it is, who takes part and who acts, where it was read, and its limit. */
export interface WorkflowStep {
  /** Its kind, under Current: each kind is drawn at most once. */
  id: string;
  kind: 'work' | 'opinion' | 'look' | 'landing' | 'pull-request';
  participation: 'agent' | 'agent-and-you' | 'you' | 'automatic';
  executor: 'agent' | 'daoris' | 'plugin';
  /** The person's one named press on it, where the step has one. */
  press: 'reviewed' | 'accept' | 'merge' | null;
  source: WorkflowSource;
  /** What it is set to, in the order the version reads them, the driver's order. */
  settings: Record<string, WorkflowValue>;
  runtime: 'built' | 'partial' | 'declared';
  /** A code of `WORKFLOW_LIMITS`, or null where the runtime runs it whole. */
  limit: string | null;
}

/** A repository's, or a workspace's, Current: the plugins that may hold a start, the steps in order, and the digest of both. */
export interface CurrentWorkflow {
  startHolds: string[];
  steps: WorkflowStep[];
  /** The first 12 hex characters of the SHA-256 of `workflowCanonical`'s text. */
  version: string;
}

/** The rule maps the derivation reads: `driver.json`'s, as `readDriverChoices` reads them. */
export type WorkflowRules = Pick<DriverChoices,
  'landings' | 'workspaceLandings' | 'reviews' | 'workspaceReviews' | 'opinions' | 'workspaceOpinions' | 'standing'>;

/**
 * Each limit a step may carry, said where the step is drawn (design §3.9), in the table's order — the driver's
 * `WorkflowLimits.Table`, word for word. The opinion's is its rule's own *declared only*, until XAGENT1f's gate reads it.
 */
export const WORKFLOW_LIMITS: Readonly<Record<string, string>> = {
  'opinion-declared': OPINION_DECLARED_ONLY,
  'look-partial': 'Partial: work here lands only once you say it is reviewed or skip the review, and a set-up step\'s build is '
    + 'shown in Daoris\'s browser from its session\'s end until then; no set-up step is composed for you yet, and nothing runs a '
    + 'process of its own for one.',
  'plugin-unready': 'Its plugin cannot land work on this machine now: it is not installed, is switched off, contributes '
    + 'nothing, or speaks on no `work/land` point. `daoris plugin list` says which.',
  'pull-request-unread': 'Its plugin answers no `work/state`, so Daoris never reads whether the pull request was merged or '
    + 'abandoned.',
  'pull-request-at-clean-up': 'Its state is asked of its plugin only when branches are cleaned up, never while someone waits on it.',
};

/** The points a plugin speaks on that Current reads — the driver's `HookPoints`. */
const CONSIDER_POINT = 'quest/consider';
const LAND_POINT = 'work/land';
const STATE_POINT = 'work/state';

/**
 * The landing rule standing for a repository: its own, else its workspace's (one in no workspace being in `default`), else the
 * merge — the driver's `LandingRules.Choose`, which matches the repository's name in any case as given, never trimmed.
 */
export function landingFor(
  rules: Pick<WorkflowRules, 'landings' | 'workspaceLandings'>, repository: string, workspace: string | null,
): { rule: LandingRule; source: WorkflowSource['level'] } {
  const own = atName(rules.landings, repository);
  if (own !== undefined) return { rule: own, source: 'repository' };
  const shared = atName(rules.workspaceLandings, normalizeWorkspace(workspace));
  return shared !== undefined ? { rule: shared, source: 'workspace' } : { rule: { form: 'merge' }, source: 'default' };
}

/** The plugins a door hands the derivation: those switched on and sound, in its catalogue's order, each with its points. */
export function workflowPluginsOf(catalog: PluginCatalog): WorkflowPlugin[] {
  return catalog.contributing.map((entry) => ({ id: entry.manifest.id, points: [...(entry.manifest.hooks?.points ?? [])] }));
}

/**
 * A repository's Current (design §2.7): the plugins that may hold a start; the work, with its standing answer; the second
 * opinion where a rule stands that is not `false`, with its own occasions; the look where the review rule is required, in its
 * default environment; the landing as its rule says; and the pull request where a branch rule names a plugin. A null
 * repository draws the workspace's Current, which no repository's rule and no standing answer reach.
 *
 * @param plugins The plugins switched on and sound, in catalogue order (`workflowPluginsOf`).
 */
export function currentWorkflow(
  rules: WorkflowRules, repository: string | null, workspace: string | null, plugins: readonly WorkflowPlugin[],
): CurrentWorkflow {
  // The workspace's Current reads the same resolvers with nothing set for any repository.
  const read: WorkflowRules = repository === null ? { ...rules, landings: {}, reviews: {}, opinions: {}, standing: {} } : rules;
  const named = repository ?? '';

  const startHolds = plugins.filter((plugin) => plugin.points.includes(CONSIDER_POINT)).map((plugin) => plugin.id);
  // `standingFor`'s reading, the name trimmed and matched in any case: imported, it would make `driverconfig.ts` a cycle.
  const standingKey = findName(Object.keys(read.standing), named.trim());
  const steps: WorkflowStep[] = [{
    id: 'work', kind: 'work', participation: 'agent', executor: 'agent', press: null,
    source: { rule: null, level: 'default' },
    settings: { standing: standingKey === null ? null : read.standing[standingKey]!.says },
    runtime: 'built', limit: null,
  }];

  const opinion = opinionFor(read, named, workspace);
  if (opinion !== null && opinion.rule !== false) {
    steps.push({
      id: 'opinion', kind: 'opinion', participation: 'agent', executor: 'agent', press: null,
      source: { rule: 'opinion', level: opinion.source },
      settings: {
        reviewers: [...opinion.rule.reviewers],
        on: [...opinion.rule.on],
        required: opinion.rule.required === true,
        recheck: opinion.rule.recheck !== false,
      },
      runtime: 'declared', limit: 'opinion-declared',
    });
  }

  const review = reviewFor(read, named, workspace);
  if (review !== null && review.rule !== false && review.rule.required === true) {
    steps.push({
      id: 'look', kind: 'look', participation: 'agent-and-you', executor: 'agent', press: 'reviewed',
      source: { rule: 'review', level: review.source },
      settings: { environment: review.rule.environments[0]!.name },
      runtime: 'partial', limit: 'look-partial',
    });
  }

  const landing = landingFor(read, named, workspace);
  const plugin = landing.rule.form === 'branch' ? landing.rule.plugin ?? null : null;
  // The plugin a rule names, as the driver's `PluginProblem` finds it: the first of that id in any case.
  const speaker = plugin === null ? undefined : plugins.find((each) => sameName(each.id, plugin));
  const lands = speaker !== undefined && speaker.points.includes(LAND_POINT);
  const automatic = landing.rule.form === 'branch' && landing.rule.autoAccept === true;
  steps.push({
    id: 'landing', kind: 'landing', participation: automatic ? 'automatic' : 'you', executor: 'daoris',
    press: automatic ? null : 'accept',
    source: { rule: 'landing', level: landing.source },
    settings: {
      form: landing.rule.form,
      accept: automatic ? 'automatic' : 'you',
      pattern: landing.rule.form === 'branch' ? landing.rule.pattern ?? null : null,
      plugin,
      tidy: landing.rule.tidy === true,
    },
    runtime: 'built', limit: plugin !== null && !lands ? 'plugin-unready' : null,
  });

  if (plugin !== null) {
    steps.push({
      id: 'pull-request', kind: 'pull-request', participation: 'you', executor: 'plugin', press: 'merge',
      source: { rule: 'landing', level: landing.source },
      settings: { plugin },
      runtime: 'partial',
      limit: !lands ? 'plugin-unready' : speaker!.points.includes(STATE_POINT) ? 'pull-request-at-clean-up' : 'pull-request-unread',
    });
  }

  const drawn = { startHolds, steps, version: '' };
  return { ...drawn, version: createHash('sha256').update(workflowCanonical(drawn), 'utf8').digest('hex').slice(0, 12) };
}

/**
 * The text a version is the digest of, built by hand so both runtimes write the same bytes (the driver's
 * `WorkflowCurrent.Canonical`): `current`; `holds` and the plugins that may hold a start; then each step's line, its id, kind,
 * participation, executor, press, rule, level, runtime and limit, each followed by its settings, a line each in order. Text is
 * quoted, with `\`, `"` and each control character escaped; none is `-`; a list is its quoted items between brackets.
 */
export function workflowCanonical(workflow: Pick<CurrentWorkflow, 'startHolds' | 'steps'>): string {
  const lines = ['current', `holds ${value(workflow.startHolds)}`];
  for (const step of workflow.steps) {
    lines.push(['step', step.id, step.kind, step.participation, step.executor, step.press, step.source.rule, step.source.level,
      step.runtime, step.limit].map((cell, index) => (index === 0 ? cell : value(cell))).join(' '));
    for (const [name, setting] of Object.entries(step.settings)) lines.push(`  ${name}=${value(setting)}`);
  }

  return lines.join('\n');
}

function value(setting: WorkflowValue): string {
  if (setting === null) return '-';
  if (typeof setting === 'boolean') return setting ? 'true' : 'false';
  if (Array.isArray(setting)) return `[${setting.map(quoted).join(',')}]`;
  return quoted(setting);
}

function quoted(text: string): string {
  return `"${text.replace(/[\\"\u0000-\u001f]/g, (c) => (c === '\\' ? '\\\\' : c === '"' ? '\\"'
    : `\\u${c.charCodeAt(0).toString(16).padStart(4, '0')}`))}"`;
}

/** Names in backticks, in the order they are tried: `a`, `a, else b`. */
function orElse(names: readonly string[]): string {
  return names.map((name) => `\`${name}\``).join(', else ');
}

/** Where a step was read, as a door says it. */
function sourceSaid(source: WorkflowSource): string {
  if (source.rule === null) return 'Always.';
  const rule = { landing: 'landing rule', review: 'review rule', opinion: 'opinion rule' }[source.rule];
  return source.level === 'repository' ? `This repository's ${rule}.`
    : source.level === 'workspace' ? `The workspace's ${rule}.` : 'Daoris\'s default.';
}

/** A step's line as the terminal draws it: its title, who takes part and who acts, the person's press, what it is set to and its source. */
function stepSaid(step: WorkflowStep): string[] {
  const participation = { agent: 'Agent alone', 'agent-and-you': 'Agent + you', you: 'You', automatic: 'Automatic' }[step.participation];
  const executor = step.executor === 'plugin' ? `\`${step.settings['plugin']}\`` : step.executor === 'daoris' ? 'Daoris' : 'an agent';
  const press = step.press === null ? '' : ` · your press: ${{ reviewed: 'Reviewed', accept: 'Accept', merge: 'Merge' }[step.press]}`;
  const s = step.settings;
  let title: string;
  let set: string;
  switch (step.kind) {
    case 'work':
      title = 'The work';
      set = 'The chain\'s steps in this repository; a done that departs from your words or lacks its evidence waits for your yes.';
      break;
    case 'opinion': {
      title = 'Second opinion';
      const on = s['on'] as string[];
      const when = on.includes('landing') ? (on.includes('steps') ? 'before it lands and before each next step' : 'before it lands')
        : 'before each next step';
      set = `Read by ${orElse(s['reviewers'] as string[])} ${when}; ${s['required'] ? 'required' : 'not required'}; `
        + `its answers ${s['recheck'] ? '' : 'not '}read again.`;
      break;
    }
    case 'look':
      title = 'Your look';
      set = `In \`${s['environment']}\`.`;
      break;
    case 'landing':
      title = 'The landing';
      set = s['form'] === 'merge' ? 'Merged into its line'
        : `On a branch named \`${s['pattern']}\`${s['accept'] === 'automatic' ? ', accepted automatically' : ''}`
          + (s['plugin'] === null ? ', for you to push' : `, pushed by \`${s['plugin']}\``);
      set += `${s['tidy'] ? '; its tree and branch go once it lands' : ''}.`;
      break;
    default:
      title = 'The pull request';
      set = 'Opened by its plugin at the landing; you merge it on the platform.';
  }

  const lines = [`  ${title} — ${participation} · ${executor}${press}. ${set} ${sourceSaid(step.source)}`];
  if (step.kind === 'work' && s['standing'] !== null) lines.push(`      Your standing answer, handed to each session: "${s['standing']}"`);
  if (step.limit !== null) lines.push(`      ${WORKFLOW_LIMITS[step.limit]}`);
  return lines;
}

/**
 * What `driver workflow show` says of a Current (design §4.7): whose it is and its version, the plugins that may hold a
 * start, then each step with its source and its limit.
 *
 * @param whose `` `web-app` `` or `` the workspace `work` ``, as the header names it.
 * @param where What the header adds of where it was read, such as the workspace a repository is in.
 */
export function workflowSaid(workflow: CurrentWorkflow, whose: string, where: string): string[] {
  return [
    `daoris: ${whose} follows Current: ${where}, read from the rules as they stand at each gate (version ${workflow.version}).`,
    workflow.startHolds.length === 0
      ? '  No plugin here may hold a start.'
      : `  Plugins that may hold a start: ${workflow.startHolds.map((id) => `\`${id}\``).join(', ')}.`,
    ...workflow.steps.flatMap(stepSaid),
  ];
}
