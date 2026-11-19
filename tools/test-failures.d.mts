/** Types of `tools/test-failures.mjs` for the merge tool's TypeScript: the lines of a log that are, and are not, a failure. */
export declare function notAFailure(line: string): boolean;
export declare function failingTestLine(line: string): boolean;
export declare function noneFailed(exit?: number | null): string;
