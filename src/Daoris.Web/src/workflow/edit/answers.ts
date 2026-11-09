// What the step editor reads and presses: the answers of `WORKFLOW_LIST`,
// `WORKFLOW_SHOW`, `WORKFLOW_PALETTE`, `WORKFLOW_SAVE` and `WORKFLOW_USE` as the driver gives them. Kept beside the editor, which a
// bridge domain may not be imported by, and re-exported by `bridge/repositories/workflowEdit.ts`. A twin of the driver's
// `WorkflowOperations.Listing`, `Show`, `SaveAsync` and `WorkflowPalette` (.claude/knowledge/twins.md).
import type { SettingsChoices } from '../../agents/tools';
import type { ActorRow, RoleCatalogue, RoleChangePress } from '../../data/wiring';
import type { Authored } from '../../locales/authored';

/** A place that chooses a saved workflow: `repository` or `workspace`, its name, and the role it holds there. */
export type WorkflowChosen = { level: 'repository' | 'workspace'; name: string; role: 'default' | 'kind' | 'landed'; kind: string | null };

/** A saved workflow: its versions, and the newest version's schema (1 steps, 2 graph), digest and composed line. */
export type SavedWorkflow = {
  id: string; name: string | null; problem: string | null; versions: number[]; schema: 1 | 2 | null;
  digest: string | null; line: string | null; pipeline: string | null; chosen: WorkflowChosen[];
};

/**
 * A preset built in, never stored until saved: a graph's `body` as a version saves it, which the step editor starts from. A default's
 * `name` is the driver's English one (the page words it from its catalogue by `id`, `presetWords.ts`); it also says what it is `for`
 * (English, likewise), its `order`, whether the place's set-up offers it (`setUp`) and its `delivery`. A driver before that
 * leaves them out, and a preset that is no default carries them null. The data holds no per-language field.
 */
export type PresetWorkflow = {
  id: string; name: string; graph: boolean; line: string; preset: true; body?: unknown;
  for?: string | null; order?: number | null; setUp?: boolean | null; delivery?: PresetDelivery | null;
};

/** The `land` options a default's delivery sets, with its own `id`, English name and purpose, which are not the workflow's. */
export type PresetDelivery = {
  id: string; method: string; accept: 'you' | 'automatic'; plugin?: string | null; request?: string | null; name: string; for: string;
};

/** `use` lists what `WORKFLOW_USE` can be asked beside a plain press (`plan`), absent from a driver before it. */
export type WorkflowListAnswer = { workflows: SavedWorkflow[]; presets: PresetWorkflow[]; use?: string[] };

/** A problem the graph has, by its code, with the wire or node it is about. */
export type GraphProblem = { code: string; wire: string | null; node: string | null; words: string };

/** One version as saved: `body` is its steps (schema 1) or its graph (schema 2) exactly as the file keeps it. */
export type WorkflowShownAnswer = {
  id: string; name: string; version: number; newest: boolean; versions: number[]; at: string | null; door: string | null;
  digest: string | null; schema: 1 | 2; line: string; body: unknown; problems: GraphProblem[];
};

export type OptionField = { name: string; type: 'choice' | 'text' | 'switch' | 'names'; choices: string[]; required: boolean };

/**
 * A built-in kind with who reaches and fails it, what the person may do to it, and its options; the wire kinds that may leave it, and the
 * works a role takes it by, none where no agent session takes it.
 */
export type PaletteKind = {
  kind: string; reach: string; reachBy: string; failureBy: string | null; personJudges: boolean; personSkips: boolean; mayEnd: boolean;
  evidenced: boolean; skipBy: string | null; options: OptionField[]; leaves: string[]; roleTakes: string[];
  /** How the editor offers the kind: `alone`, `tail:<method>` for a step a landing's method brings. Absent: `alone`. */
  offered?: string;
  /** The outcomes a step of the kind is reached with, which an added step declares (a monitor's). Absent or empty: none. */
  outcomes?: string[];
  inputs?: Record<string, string>;
  outputs?: Record<string, string>;
  /** Each output carried on some outcomes alone, with them (a rebase's `commit` on `ready`); absent from a driver before it. */
  outputsOn?: Record<string, string[]>;
};

/** A wire kind: whether it makes its target eligible, and the kinds that may leave it (null: any node). */
export type PaletteWire = { kind: string; eligibility: boolean; from: string[] | null; to: string | null };

/**
 * A plugin's step as its manifest declares it, which a graph's node copies, with the wire kinds that may leave it. `outputsOn`
 * names, for an output carried on some outcomes alone, those outcomes; an output it leaves out is carried on every reach, and it is absent
 * from a driver before it. `title` and `about` are the manifest's words as authored text: one text, or language tags
 * to text; null or absent where it declares none. A page shows them with `authoredText`.
 */
export type StepWords = Authored;

export type PaletteStep = {
  kind: string; version: number; reach: string; rejectable: boolean; skippable: boolean; inputs: Record<string, string>;
  outputs: Record<string, string>; evidence: string | null; outcomes: string[]; outputsOn?: Record<string, string[]>; leaves: string[];
  title?: StepWords | null; about?: StepWords | null;
};

export type PalettePlugin = { plugin: string; steps: PaletteStep[] };

export type WorkflowPaletteAnswer = { kinds: PaletteKind[]; wires: PaletteWire[]; plugins: PalettePlugin[] };

/** One repository a save or a choice reaches (the integration design §17.7): why it cannot start the version, and whether its line has no gates. */
export type WorkflowReachRow = { repository: string; workflow: string | null; cannots: string[]; noGates: boolean; said: string | null };

/** A place a saved workflow is chosen for, which an append reaches at its next start, by the key the person authorizes it with. */
export type WorkflowScopeRow = WorkflowChosen & { key: string };

/**
 * A version saved, or planned (`plan: true` writes nothing): under a fresh `id`, or appended to a saved one against `base`, the version the
 * person read, with `authorize` naming every place that chooses it.
 */
export type WorkflowSaveAsk = {
  id: string; graph: unknown; base?: number; name?: string; authorize?: string[]; plan?: boolean;
  /** The shipped default an appended version updates its copy to: the version keeps it as where it came from. */
  updates?: string;
};

/**
 * What `WORKFLOW_SAVE` answers: whether it is (or would be) written, the version, the places an append reaches, and a refused graph's
 * problems by their codes at the steps and wires they name, absent from a driver before them.
 */
export type WorkflowSaveAnswer = {
  id: string; done: boolean; refused: string | null; message: string | null; version: number | null; written: boolean; appended: boolean;
  scopes: WorkflowScopeRow[]; reach: WorkflowReachRow[]; problems?: GraphProblem[];
};

/**
 * A place's choice of a saved workflow (`WORKFLOW_USE`): `version` is the newest the person read, refused `stale` once it moved. `plan: true`
 * reads the choice and writes nothing, only where `WORKFLOW_LIST` announces `use: ['plan']`; `expected`, the plan's token, is refused `moved`
 * where anything the plan says has changed since. `workflow` is absent to clear the default.
 */
export type WorkflowUseAsk = ({ repository: string } | { workspace: string }) & {
  workflow?: string; version?: number; kind?: string; plan?: boolean; expected?: string;
};

/** A repository whose workflow a choice changes: what it follows now and after, and what lowers the person's part there. */
export type WorkflowUsePlace = { repository: string; was: string; now: string; lowers: string[] };

/** What clearing a default leaves to the kinds the place still maps: the driver's sentence, and each kind with its workflow. */
export type WorkflowUseKeeps = { said: string; kinds: { kind: string; workflow: string }[] };

/**
 * What `WORKFLOW_USE` answers. A plan (`plan: true`) adds what it would do: `lowers`, the `places` it changes, what a cleared default's kinds
 * `keeps`, and the `expected` token a press carries; `done` is false, since nothing is written. A driver before it leaves these out.
 */
export type WorkflowUseAnswer = {
  done: boolean; refused: string | null; message: string | null; changed: boolean; reach: WorkflowReachRow[];
  plan?: boolean; lowers?: string[]; places?: WorkflowUsePlace[]; keeps?: WorkflowUseKeeps | null; expected?: string;
};

/**
 * A working role a step may name, from the roles' structure: its id, its names, and the works it takes; where it is placed, and whether that
 * place covers the one the editor opened from. A driver or a test that leaves the place out has a role of this machine, which
 * covers every place.
 */
export type EditorRole = {
  id: string; name: Authored | null; takes: string[]; scope?: { kind: string; id: string | null }; revision?: number; covers?: boolean;
};

/** The press of a role's change finished: it landed, or the driver's sentence. */
export type RoleAnswered = { done: () => void; refused: (sentence: string) => void };

/**
 * What a step's *Taken by* needs to open the one role editor beside the step: the catalogue and the order of each role, the agents
 * this machine has and what each offers, and the press over `SET_ROLE`. Read only once the editor shows.
 */
export type StepStaffing = {
  catalogue: RoleCatalogue | null;
  read: 'ok' | 'loading' | 'failed';
  actors: readonly ActorRow[];
  agents: readonly { value: string; label: string }[];
  offers: Readonly<Record<string, SettingsChoices | null | undefined>>;
  busy: boolean;
  onSet: (press: RoleChangePress, answered: RoleAnswered) => void;
};
