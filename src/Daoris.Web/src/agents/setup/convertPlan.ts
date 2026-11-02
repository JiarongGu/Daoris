// The conversion of the old workflow records as the driver answers it (WORKFLOWCLEAN1 1e; `CONVERT_PLAN`, `CONVERT_APPLY` and `CONVERT_UNDO`, the
// cleanup design §4): the shapes the review draws, the row's two choices and the ask they make. The bridge leaves a null out.

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

export type ConvertPlanned = {
  digest: string;
  /** The plan writes nothing: what is left is kept, held or counted. */
  empty: boolean;
  choices: ConvertChoices;
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

export type ConvertOperation = { id: string; kind: string; said: string; outcome: string; note?: string | null };

/** What a press came to: `refused` is a code (`moved` where the plan no longer holds), and a refusal wrote nothing. */
export type ConvertApplied = { done: boolean; refused?: string | null; message: string; digest: string; operations: ConvertOperation[]; plan?: ConvertPlanned | null };

/** The undo's plan: offered only where `refusal` is null; `lines` say what it puts back. */
export type ConvertUndoPlan = { undo: true; digest?: string | null; refusal?: string | null; lines: string[] };

export type ConvertApplyAsk = ConvertChoices & { digest: string };

/** The ask a plan is read with. */
export type ConvertAsk = ConvertChoices | { undo: true };

/** Whether a place follows an old landing rule: a repository or a workspace the census lists. */
export const followsOf = (plan: ConvertPlanned | null, place: { repository?: string | null; workspace?: string | null }): boolean =>
  (plan?.census.places ?? []).some((each) => (place.repository
    ? !each.isWorkspace && each.place === place.repository && (!place.workspace || each.workspace === place.workspace)
    : each.isWorkspace && each.place === place.workspace));

/** The group the plan's saves fall in, in the order the page draws them. */
export const SHAPES = ['steps', 'handed', 'land-accept', 'current', 'copy', 'default'] as const;

/** The saves by their shape, each save once with its places. */
export const savesByShape = (plan: ConvertPlanned): { shape: string; saves: ConvertSave[] }[] => {
  const shapes = [...new Set([...SHAPES, ...plan.saves.map((each) => each.shape)])];
  return shapes.map((shape) => ({ shape, saves: plan.saves.filter((each) => each.shape === shape) })).filter((each) => each.saves.length > 0);
};

/** The terminal's twins of the plan, the apply and the undo (`daoris-driver workflow convert`). */
export function terminalOf(plan: ConvertPlanned): string[] {
  const { composed, landAutomatic } = plan.choices;
  const flags = composed === DEFAULT_CHOICES.composed && landAutomatic === DEFAULT_CHOICES.landAutomatic ? '' : ` --composed ${composed} --land-automatic ${landAutomatic}`;
  return [
    `daoris-driver workflow convert --plan${flags}`,
    `daoris-driver workflow convert --apply ${plan.digest}${flags}`,
    'daoris-driver workflow convert --undo',
  ];
}
