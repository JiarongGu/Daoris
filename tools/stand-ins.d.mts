// The types of `stand-ins.mjs`, for the CLI's tool test that reads the stand-ins' files through it. The reader is plain JavaScript so
// the family rehearsal imports it unbuilt; this declares what it exports.

export type DoorKind = 'native' | 'protocol';

/** One door of a `provider.json`: its id, its kind and, for a stand-in, the arguments that choose it in its program. */
export interface StandInDoor { id: string; kind: DoorKind; arguments: string[] }

/** A line of a provider's `pending.json`: a case it cannot pass yet, on a door or every door, with its row and why. */
export interface PendingLine { case: string; door: string; row: string; why: string }

/** A provider as its files declare it, its folder's name the path under the providers' folder in a source and a build alike. */
export interface ProviderRead {
  id: string;
  standIn: boolean;
  folder: string;
  program: { entry: string } | null;
  doors: StandInDoor[];
  pending: PendingLine[];
}

/** A case of the scenario contract: its name, the door kinds it applies to, what it says, and where it is read if no drive reaches it. */
export interface Scenario { case: string; kinds: readonly DoorKind[]; says: string; elsewhere?: string }

/** A row of the phase's plan: a case on a door, held by a pending line, read elsewhere, or driven. */
export interface PlanRow {
  provider: string;
  door: string;
  kind: DoorKind;
  case: string;
  says: string;
  held: PendingLine | null;
  elsewhere: string | null;
}

export const PROGRAM_FOLDER: string;
export const PROVIDER_FILE: string;
export const PENDING_FILE: string;
export const KINDS: readonly DoorKind[];
export const EVERY_DOOR: string;
export const SCENARIO_CONTRACT: readonly Scenario[];

export function providersFolder(repoRoot: string): string;
export function readProvider(file: string, folder: string): { provider: Omit<ProviderRead, 'pending'> | null; problems: string[] };
export function readPending(file: string, folder: string): { lines: PendingLine[]; problems: string[] };
export function readProviders(folder: string): { providers: ProviderRead[]; problems: string[] };
export function readStandIns(folder: string): { standIns: ProviderRead[]; problems: string[] };
export function programOf(standIn: ProviderRead, root: string): string;
export function doorCommand(standIn: ProviderRead, door: StandInDoor, root: string, more?: string[]): string[];
export function heldBy(lines: PendingLine[], name: string, door: string): PendingLine | null;
export function scenarioPlan(standIns: ProviderRead[]): PlanRow[];
