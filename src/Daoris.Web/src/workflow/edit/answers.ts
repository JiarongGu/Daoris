// What the step editor reads and presses (WORKFLOWEDIT1a's reads, WORKFLOWEDIT1b's save and use): the answers of `WORKFLOW_LIST`,
// `WORKFLOW_SHOW`, `WORKFLOW_PALETTE`, `WORKFLOW_SAVE` and `WORKFLOW_USE` as the driver gives them. Kept beside the editor, which a
// bridge domain may not be imported by, and re-exported by `bridge/repositories/workflowEdit.ts`. A twin of the driver's
// `WorkflowOperations.Listing`, `Show`, `SaveAsync` and `WorkflowPalette` (.claude/knowledge/twins.md).

/** A place that chooses a saved workflow: `repository` or `workspace`, its name, and the role it holds there. */
export type WorkflowChosen = { level: 'repository' | 'workspace'; name: string; role: 'default' | 'kind' | 'landed'; kind: string | null };

/** A saved workflow: its versions, and the newest version's schema (1 steps, 2 graph), digest and composed line. */
export type SavedWorkflow = {
  id: string; name: string | null; problem: string | null; versions: number[]; schema: 1 | 2 | null;
  digest: string | null; line: string | null; pipeline: string | null; chosen: WorkflowChosen[];
};

/**
 * A preset built in, never stored until saved: a graph's `body` as a version saves it, which the step editor starts from, and a default's
 * names in both languages (WORKFLOWEDIT1b); a driver before them leaves both out.
 */
export type PresetWorkflow = {
  id: string; name: string; graph: boolean; line: string; preset: true; body?: unknown; names?: { en: string; zh: string } | null;
};

export type WorkflowListAnswer = { workflows: SavedWorkflow[]; presets: PresetWorkflow[] };

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
 * works a role takes it by (WORKFLOWEDIT1b), none where no agent session takes it.
 */
export type PaletteKind = {
  kind: string; reach: string; reachBy: string; failureBy: string | null; personJudges: boolean; personSkips: boolean; mayEnd: boolean;
  evidenced: boolean; skipBy: string | null; options: OptionField[]; leaves: string[]; roleTakes: string[];
};

/** A wire kind: whether it makes its target eligible, and the kinds that may leave it (null: any node). */
export type PaletteWire = { kind: string; eligibility: boolean; from: string[] | null; to: string | null };

/** A plugin's step as its manifest declares it, which a graph's node copies, with the wire kinds that may leave it. */
export type PaletteStep = {
  kind: string; version: number; reach: string; rejectable: boolean; skippable: boolean; inputs: Record<string, string>;
  outputs: Record<string, string>; evidence: string | null; outcomes: string[]; leaves: string[];
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
export type WorkflowSaveAsk = { id: string; graph: unknown; base?: number; name?: string; authorize?: string[]; plan?: boolean };

/**
 * What `WORKFLOW_SAVE` answers: whether it was (or would be) written, the version, the places an append reaches, and a refused graph's
 * problems by their codes at the steps and wires they name (WORKFLOWEDIT1b), absent from a driver before them.
 */
export type WorkflowSaveAnswer = {
  id: string; done: boolean; refused: string | null; message: string | null; version: number | null; written: boolean; appended: boolean;
  scopes: WorkflowScopeRow[]; reach: WorkflowReachRow[]; problems?: GraphProblem[];
};

/** A place's choice of a saved workflow (`WORKFLOW_USE`): `version` is the newest the person read, refused `stale` once it moved. */
export type WorkflowUseAsk = ({ repository: string } | { workspace: string }) & { workflow: string; version?: number };

export type WorkflowUseAnswer = { done: boolean; refused: string | null; message: string | null; changed: boolean; reach: WorkflowReachRow[] };

/** A working role a step may name, from the roles' structure: its id, its names, and the works it takes. */
export type EditorRole = { id: string; name: { en: string; zh: string } | null; takes: string[] };
