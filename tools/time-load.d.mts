/** Types of `tools/time-load.mjs` for the merge tool's TypeScript: the load under way as a gate's timing records are taken. */
export declare const ALONE_HOLDS: number;
export declare const FOREIGN_EVERY: number;
export declare function heldNow(home?: string, seam?: object): number;
export declare function readMarks(dir: string): Record<string, { holds: number; at: number }>;
export declare function isLoaded(marks: Record<string, { holds: number; at: number }>, file: string, at: number): boolean;
export declare function stampLoad(dir: string, holds: number, since: number): string[];
export declare function readProcesses(): { name: string; commandLine: string }[];
export declare function isTestProcess(process: { name: string; commandLine: string }): boolean;
export declare function treeOf(path: string, main: string): string | null;
export declare function foreignTrees(root?: string, seam?: { processes?: () => { name: string; commandLine: string }[]; now?: () => number }): number;
export declare function loadSampler(options?: { every?: number; count?: () => number }): { peak: () => number; stop: () => void };
