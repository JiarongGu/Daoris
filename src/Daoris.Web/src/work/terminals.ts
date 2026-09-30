/**
 * The person's own terminals (CONSOLE4, D96), as the page holds them: the shape lives here, beside the
 * view that draws it, so the view can name it without importing the bridge (SURF6's rule for shapes).
 */

/** One terminal the page opened: its shell, where it started, and how it ended once it has. */
export type OpenTerminal = {
  id: string;
  /** The shell's id: `pwsh`, `powershell`, `cmd` or `bash`. */
  shell: string;
  /** Where it started, as the module answered: the folder asked for, or the home when that was not here. */
  cwd: string;
  /** The shell's exit code once it ended on its own; null while it runs. */
  exit: number | null;
};

/** What a new terminal asks for. The module decides whatever is left out. */
export type TerminalOpening = { shell?: string; cwd?: string; cols?: number; rows?: number };

/** The shells this machine has, and the one a terminal opens with when none is chosen. */
export type TerminalShellChoice = { shells: string[]; default: string | null };

/**
 * The terminals and what can be done to them, held by the frame so they outlive the view that shows
 * them: the view unmounts whenever it moves or another view is shown, and a shell must not end with it.
 */
export type Terminals = {
  list: OpenTerminal[];
  /** The tab shown (CONSOLE4c): the one opened last, until the person picks another. */
  selected: string | null;
  select: (id: string) => void;
  /** Whether an open is on its way; a second one waits for it. */
  opening: boolean;
  /** Why the last open was refused, as the bridge rejected it, or null. */
  refused: unknown;
  open: (opening?: TerminalOpening) => void;
  input: (id: string, data: string) => void;
  resize: (id: string, cols: number, rows: number) => void;
  /** Close its tab: the shell and everything it started end. */
  close: (id: string) => void;
  /** Hear its output: what came while nothing listened first, then each batch as it arrives. */
  listen: (id: string, sink: (data: string) => void) => () => void;
  /** Null until asked for. */
  shells: TerminalShellChoice | null;
  askShells: () => void;
};

/** A folder's last segment, for a tab's name: the full path is the tip's. */
export function folderName(path: string): string {
  const parts = path.split(/[\\/]+/).filter(Boolean);
  return parts[parts.length - 1] ?? path;
}

/**
 * Each terminal's tab name (CONSOLE4c): its shell and where it started, and, where that name repeats,
 * which of them it is — the first plain, the next numbered in the order they opened, so a name already
 * on a tab never changes under the person.
 */
export function terminalNames(
  list: readonly OpenTerminal[],
  name: (terminal: OpenTerminal) => string,
  nth: (name: string, n: number) => string,
): Record<string, string> {
  const seen = new Map<string, number>();
  return Object.fromEntries(list.map((terminal) => {
    const base = name(terminal);
    const count = (seen.get(base) ?? 0) + 1;
    seen.set(base, count);
    return [terminal.id, count === 1 ? base : nth(base, count)];
  }));
}
