// The types of `machine-read.mjs`, for the merge tool's TypeScript: the machine's process table and free memory, read with no PowerShell and no WMI.

export type ProcessRow = { pid: number; ppid: number; created: number; name: string; path: string | null; command: string };
type Seam = { platform?: string; run?: (command: string, args: string[], options: never) => { status: number | null; stdout?: unknown; stderr?: unknown; error?: Error }; helper?: string | (() => string) };

export declare const HELPER_SOURCES: string[];
export declare const BUILD_FOLDER: string;
export declare const READ_TIMEOUT_MS: number;
export declare const BUILD_TIMEOUT_MS: number;
export declare function sourceHash(root?: string, read?: (path: string) => string): string;
export declare function helperStanding(root?: string, seam?: { read?: (path: string) => string; stamp?: (path: string) => string | null; exists?: (path: string) => boolean }): { hash: string; exe: string; stale: boolean };
export declare function helperPath(root?: string, seam?: { run?: unknown; look?: (root: string) => { hash: string; exe: string; stale: boolean }; exists?: (path: string) => boolean; build?: boolean }): string;
export declare function parseProcesses(text: string): ProcessRow[];
export declare function parseMemory(text: string): { physical: number; commit: number; total: number };
export declare function readProcesses(options?: Seam & { pids?: number[]; timeoutMs?: number; build?: boolean }): ProcessRow[];
export declare function freeMemory(options?: Seam & { timeoutMs?: number }): { physical: number; commit: number; total: number };
export declare function processStartOf(pid: number, options?: Seam): number | null;
