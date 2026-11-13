type Command = { label?: string; cwd: string; command: string; args: string[] };
type Unit = { file: string; unit: string };

export declare function withReport(each: Command, name: string): { args: string[]; report: string };
export declare function passesOf<T extends Unit>(group: { runner: string; units: T[] }, exitCode: number | null, report: string | null, root: string): T[];
