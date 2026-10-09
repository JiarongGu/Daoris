// A repository's or a workspace's Current workflow as the shell answers it (WORKFLOW1b; D157 point 7, the workflow design
// §2.7, §3.2): the driver's `WorkflowCurrent.Derive`, read from the machine's driver file, plugins and registry exactly as
// `daoris driver workflow show` reads them. The page draws it and derives nothing: what a step is, who takes part, who acts,
// where it was read and what it cannot do yet are the driver's, and only the words are the page's.

/** The person's part in a step (design §3.1), said apart from who acts in it. */
export type WorkflowParticipation = 'agent' | 'agent-and-you' | 'you' | 'automatic';

/** Who acts in a step: an agent, Daoris itself, or a plugin speaking for a service. */
export type WorkflowExecutor = 'agent' | 'daoris' | 'plugin';

/** The person's one named press on a step, where it has one. */
export type WorkflowPress = 'reviewed' | 'accept' | 'merge';

/** How much of a kind this build runs (design §3.2, §3.9): whole, in part, or declared with nothing acting on it. */
export type WorkflowRuntime = 'built' | 'partial' | 'declared';

/** A setting's value as a step carries it: text or none, a switch, or a list of names. */
export type WorkflowValue = string | boolean | string[] | null;

/** The rule a step was read from, or none for the work, and the level it stood at. */
export interface WorkflowSource {
  rule: 'landing' | 'review' | 'opinion' | null;
  level: 'repository' | 'workspace' | 'default';
}

/** One step, as the shared table's cell (the driver's `WorkflowCurrent.ToJson`). */
export interface WorkflowStep {
  /** Its kind, under Current: each kind is drawn at most once. */
  id: string;
  /** `work`, `opinion`, `look`, `landing` or `pull-request` today; a kind this page cannot word is drawn as unknown. */
  kind: string;
  participation: WorkflowParticipation;
  executor: WorkflowExecutor;
  press: WorkflowPress | null;
  source: WorkflowSource;
  /** What it is set to, in the order the version reads them. */
  settings: Record<string, WorkflowValue>;
  runtime: WorkflowRuntime;
  /** A limit's code, or null where the runtime runs it whole. */
  limit: string | null;
}

/** A repository whose workspace's Current is drawn: whether its own rules make it follow something else. */
export interface WorkflowRepository {
  repository: string;
  /** It sets a rule of its own, so its Current is not the workspace's. */
  own: boolean;
}

/** What `WORKFLOW_CURRENT` answers. */
export interface CurrentWorkflow {
  /** The repository drawn, or null for a workspace's Current. */
  repository: string | null;
  /** The workspace whose rules reach it, as the driver names it: one in no workspace is in `default`. */
  workspace: string;
  /** A repository's: whether the registry holds it. One it does not is read as in no workspace. */
  registered?: boolean;
  /** The plugins that may hold a start (`quest/consider`), never a step. */
  startHolds: string[];
  steps: WorkflowStep[];
  /** The first 12 hex characters of the digest of what is drawn: it changes when a rule does. */
  version: string;
  /** Each limit a step carries, by its code, in the driver's own words: what the page says where it has no words of its own. */
  limits: Record<string, string>;
  /**
   * A workspace's: its repositories, each with whether it sets rules of its own. Absent for a repository's, and null where
   * the registry was not read.
   */
  repositories?: WorkflowRepository[] | null;
}

/** What the page asks for: a repository's Current, or a workspace's. */
export type WorkflowScope = { repository: string } | { workspace: string };

/** The kinds this page words; anything else is drawn as a step it cannot word, never dropped. */
export const KNOWN_KINDS = ['work', 'opinion', 'look', 'landing', 'pull-request'] as const;

/** The limits this page words; a code it does not know is said in the driver's words, marked as such. */
export const KNOWN_LIMITS = [
  'opinion-declared', 'look-partial', 'plugin-unready', 'pull-request-unread', 'pull-request-at-clean-up',
] as const;

/**
 * The steps before the landing and after it (design §2.2, §6.2): before it the order is the runtime's, after it the
 * person's, and the landing is the hinge, drawn last before. A workflow with no landing draws everything before.
 */
export function aroundTheLanding(steps: readonly WorkflowStep[]): { before: WorkflowStep[]; after: WorkflowStep[] } {
  const at = steps.findIndex((step) => step.kind === 'landing');
  if (at < 0) return { before: [...steps], after: [] };
  return { before: steps.slice(0, at + 1), after: steps.slice(at + 1) };
}

/** The Setup section that sets a step, on the page that draws it: its repository's or its workspace's. */
export type SetupDoor =
  | { where: 'repository'; section: 'work' | 'sessions' }
  | { where: 'workspace'; section: 'defaults' };

/**
 * Where the rule a step was read from is set (design §6.1, §4.7's Setup rows): on a repository's page, its own Setup's
 * *Line and landing* for a rule set here or set nowhere, and its workspace's Setup for one its workspace set; on a
 * workspace's page, its own Setup's defaults. The work is set by no rule, so it has no door; its standing answer does.
 */
export function setupDoorOf(step: WorkflowStep, page: 'repository' | 'workspace'): SetupDoor | null {
  if (step.source.rule === null) return null;
  if (page === 'workspace') return { where: 'workspace', section: 'defaults' };
  return step.source.level === 'workspace' ? { where: 'workspace', section: 'defaults' } : { where: 'repository', section: 'work' };
}

/** A setting read as text, or null where it is none or not text. */
export function textOf(step: WorkflowStep, name: string): string | null {
  const value = step.settings[name];
  return typeof value === 'string' && value.length > 0 ? value : null;
}

/** A setting read as a list of names, empty where it is none or not one. */
export function namesOf(step: WorkflowStep, name: string): string[] {
  const value = step.settings[name];
  return Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : [];
}

/** A setting read as a switch: only `true` is on. */
export function flagOf(step: WorkflowStep, name: string): boolean {
  return step.settings[name] === true;
}

/** One of the person's presses, in the order the work reaches them, with what each press names. */
export type YourPress = { press: WorkflowPress; environment?: string };

/**
 * The person's part, composed from the steps (design §2.5: no summary is typed, so it cannot drift from them): each
 * press in order, the look's with its environment. Empty is a part too: work moves on with no press of theirs.
 */
export function yourPart(steps: readonly WorkflowStep[]): YourPress[] {
  return steps.flatMap((step): YourPress[] => {
    if (step.press === null) return [];
    const environment = step.press === 'reviewed' ? textOf(step, 'environment') ?? undefined : undefined;
    return [environment ? { press: step.press, environment } : { press: step.press }];
  });
}
