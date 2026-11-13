export type AdmissionEntry = { id: string; kind: 'queue' | 'session'; pid: number; what: string; since: string; beat: string; session?: string; repository?: string; tree?: string };
export type AdmissionHold = { id: string; inside: boolean; release(): void };
export type Admission = { take(options?: { what?: string; session?: string; repository?: string; tree?: string; halted?: () => boolean }): Promise<AdmissionHold | null> };
export function createAdmission(options?: {
  daorisHome?: string | null; kind?: 'queue' | 'session'; capacity?: number | null; alive?: (pid: number) => boolean; now?: () => number;
  sleep?: (ms: number) => Promise<void>; say?: (line: string) => void; pollMs?: number; sayMs?: number; beatMs?: number; env?: Record<string, string | undefined>;
}): Admission;
export function admissionFile(daorisHome: string): string;
export function readState(file: string): { holds: AdmissionEntry[]; waits: AdmissionEntry[] };
