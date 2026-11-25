/**
 * The types of `time-why.mjs`, for the typed modules under `tools/merge/` that import it. Every export of the script is declared
 * here and nothing else is: `tools/merge/declarations.test.ts` holds the two to the same names.
 */
export type Cause = 'process' | 'disk' | 'network' | 'clock' | 'scan' | 'load' | 'unclassed';

/** One time breach: what breached, its time and limit, its cause and what it was read from, the row that owns the fix, and whether it fails the run. */
export type Breach = {
  kind: 'test' | 'census' | 'hang' | 'gate'; name: string; ms: number; limit: number; cause: Cause; evidence: string; fix: string; fails: boolean;
  unit?: string; domain?: string;
};
export type Separation = { domain: string; cause: Cause; classes: string[]; fix: string };
/** What a test started, opened and waited on, in counts: the runtime ledger's entry. */
export type Acts = { process?: number; git?: number; disk?: number; network?: number; clock?: number };
export type Source = { file: string; text: string };
export type StepTime = { step: string; ms: number; open?: true };
export type LoadSeam = { sample: () => { peak: () => number; stop: () => void }; free: () => boolean; sleep: (ms: number) => Promise<void>; now: () => number };

export declare function causeOf(given: { text?: string | undefined; file?: string | undefined; record?: string; acts?: Acts | null }): { cause: Cause; evidence: string; real?: true };
export declare const LEDGER_END: string;
export type Ledger = (key: string, seam?: { own?: boolean }) => Acts | null;
export declare function readLedger(dir: string, seam?: { since?: number | null }): Ledger;
export declare function domainOf(file: string): string;
export declare function sourceReader(root: string, seams?: { files?: string[] | null; read?: (path: string) => string }): (key: string) => Source | null;
type Over = Record<string, { ms: number; limit: number }>;
export declare function ratchetBreaches(given: {
  found: Over; inherited?: Over; loaded?: Over; listedAt?: (key: string) => number | undefined; records?: () => Map<string, string>; source?: (key: string) => Source | null;
  acts?: (key: string) => Acts | null;
}): Breach[];
export declare function hangBreaches(log: string | null | undefined, source: (key: string) => Source | null, peak?: number | null, seam?: { alone?: boolean; acts?: (key: string) => Acts | null }): Breach[];
export declare function hungUnderLoad(breaches: readonly Breach[]): boolean;
export declare function testBreach(given: {
  kind?: 'test' | 'census' | 'hang'; key: string; ms: number; limit: number; fails: boolean; record?: string; source?: Source | null; loaded?: boolean; note?: string; acts?: Acts | null;
}): Breach;
export declare function gateBreach(given: { gate: string; ms: number; budget: number; steps?: readonly StepTime[]; peak?: number | null; fails: boolean }): Breach;
export declare function separations(breaches: readonly Breach[], least?: number): Separation[];
export declare const sayBreach: (breach: Breach) => string;
export declare function parseBreaches(text: string | null | undefined): Breach[];
export declare const isBreachLine: (line: string) => boolean;
export declare function breachLine(breach: Breach): string;
export declare function breachLines(breaches: readonly Breach[], found?: Separation[], options?: { file?: string | null; max?: number }): string[];
export declare function writeBreaches(dir: string, breaches: readonly Breach[], found?: Separation[]): string | null;
export declare function loadSeam(home: string | null, root: string): LoadSeam;
