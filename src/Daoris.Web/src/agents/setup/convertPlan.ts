// The home's one conversion door as the driver answers it (ONEWAY1ae; `CONVERT_PLAN`, `CONVERT_APPLY` and `CONVERT_UNDO`, the oneway design §1.1, the
// cleanup design §4): every area's part, the workflow part's own plan among them, the row's two choices and the asks they make. The bridge leaves
// a null out.

/** The row's two choices: what a place whose access setup composed the switch becomes, and what an `automatic` landing saved becomes. */
export type ConvertChoices = { composed: 'none' | 'review'; landAutomatic: 'review' | 'none' };

/** The defaults that lower nothing: as it landed, and with your review. */
export const DEFAULT_CHOICES: ConvertChoices = { composed: 'none', landAutomatic: 'review' };

export type ConvertSave = { id: string; name: string; mode: 'append' | 'new'; shape: string; said: string; base?: number | null; places: string[] };

export type ConvertUse = { place: string; isWorkspace: boolean; workflow?: string | null; kind?: string | null; said: string };

export type ConvertRemoval = { place: string; isWorkspace: boolean; keys: string[] };

export type ConvertKept = { code: string; subject: string; said: string };

export type ConvertCensusPlace = { place: string; isWorkspace: boolean; workspace: string; shapes: string[]; workCycle: boolean; openUnits: boolean };

export type ConvertRun = { run: string; repository: string; workflow: string; version?: number | null; engine: string; door: string };

export type ConvertLeftover = { place: string; isWorkspace: boolean; keys: string[]; why: string };

/** The workflow part's own plan (WORKFLOWCLEAN1 1e): every save, choice, policy and removal, what it keeps, and its census. */
export type WorkflowsPlan = {
  saves: ConvertSave[];
  uses: ConvertUse[];
  defaultWorkflow?: string | null;
  policies: { place: string; isWorkspace: boolean }[];
  removals: ConvertRemoval[];
  kept: ConvertKept[];
  lowers: string[];
  census: {
    ended: boolean;
    versions: { workflow: string; version: number; shape: string }[];
    places: ConvertCensusPlace[];
    runs: ConvertRun[];
    leftovers: ConvertLeftover[];
  };
};

/** Finished history a part clears for good: counted, dated and sized, and never undone. */
export type ConvertClear = { said: string; count: number; from?: string | null; to?: string | null; bytes?: number | null };

/** One part's plan: its digest, whether it writes nothing, the old records it holds, its terminal lines and clears, and the workflow part's own plan. */
export type ConvertPart = {
  part: string;
  digest: string;
  empty: boolean;
  count: number;
  ended: boolean;
  lines: string[];
  clears: ConvertClear[];
  workflows?: WorkflowsPlan | null;
};

/** A part the plan could not read, and why: its source did not answer. */
export type ConvertUnread = { part: string; said: string };

export type ConvertPlanned = {
  digest: string;
  /** No part that read writes anything: what is left is kept, held or counted. */
  empty: boolean;
  /** Every part read and has nothing old left. */
  ended: boolean;
  /** The old records every part that read holds. */
  count: number;
  choices: ConvertChoices;
  parts: ConvertPart[];
  unread: ConvertUnread[];
};

export type ConvertOperation = { id: string; kind: string; said: string; outcome: string; note?: string | null; clear?: boolean };

/** A part's entry in the receipt: its outcome (`done`, `failed`, `moved`, `left`, `uncertain`) and its sentence. */
export type ConvertPartOutcome = { part: string; digest: string; outcome: string; said?: string | null; operations: ConvertOperation[] };

/** What a press came to: `refused` is a code (`moved` where the plan no longer holds), and a refusal wrote nothing. */
export type ConvertApplied = {
  done: boolean;
  refused?: string | null;
  message: string;
  digest: string;
  operations: ConvertOperation[];
  parts?: ConvertPartOutcome[];
  plan?: ConvertPlanned | null;
};

/** The undo's plan: offered only where `refusal` is null; `lines` say what it puts back. */
export type ConvertUndoPlan = { undo: true; digest?: string | null; refusal?: string | null; lines: string[] };

/** A press: the plan's digest and the choices, or a failed part of that receipt left (*Leave it*). */
export type ConvertApplyAsk = (ConvertChoices & { digest: string }) | { digest: string; leave: string };

/** The ask a plan is read with. */
export type ConvertAsk = ConvertChoices | { undo: true };

/** The workflow part's own plan, where it read. */
export const workflowsOf = (plan: ConvertPlanned | null): WorkflowsPlan | null =>
  plan?.parts.find((each) => each.part === 'workflows')?.workflows ?? null;

/** Whether a place follows an old landing rule: a repository or a workspace the workflow part's census lists. */
export const followsOf = (plan: ConvertPlanned | null, place: { repository?: string | null; workspace?: string | null }): boolean =>
  (workflowsOf(plan)?.census.places ?? []).some((each) => (place.repository
    ? !each.isWorkspace && each.place === place.repository && (!place.workspace || each.workspace === place.workspace)
    : each.isWorkspace && each.place === place.workspace));

/** The group the workflow part's saves fall in, in the order the page draws them. */
export const SHAPES = ['steps', 'handed', 'land-accept', 'current', 'copy', 'default'] as const;

/** The workflow part's saves by their shape, each save once with its places. */
export const savesByShape = (plan: WorkflowsPlan): { shape: string; saves: ConvertSave[] }[] => {
  const shapes = [...new Set([...SHAPES, ...plan.saves.map((each) => each.shape)])];
  return shapes.map((shape) => ({ shape, saves: plan.saves.filter((each) => each.shape === shape) })).filter((each) => each.saves.length > 0);
};

/** The parts other than the workflows' that write something, each with its lines. */
export const otherParts = (plan: ConvertPlanned): ConvertPart[] => plan.parts.filter((each) => each.part !== 'workflows' && !each.empty);

/** Every part's clears, each with its part. */
export const clearsOf = (plan: ConvertPlanned): (ConvertClear & { part: string })[] =>
  plan.parts.flatMap((each) => each.clears.map((clear) => ({ ...clear, part: each.part })));

/** The terminal's twins of the plan, the apply and the undo (`daoris convert`). */
export function terminalOf(plan: ConvertPlanned): string[] {
  const { composed, landAutomatic } = plan.choices;
  const flags = composed === DEFAULT_CHOICES.composed && landAutomatic === DEFAULT_CHOICES.landAutomatic ? '' : ` --composed ${composed} --land-automatic ${landAutomatic}`;
  return [
    `daoris convert --plan${flags}`,
    `daoris convert --apply ${plan.digest}${flags}`,
    'daoris convert --undo',
  ];
}
