/** The terminals a line is offered for (the this-Daoris design §4). */
export type TerminalShell = 'powershell' | 'cmd' | 'bash';

export const TERMINAL_SHELLS: readonly TerminalShell[] = ['powershell', 'cmd', 'bash'];

/** What a terminal needs to reach this Daoris: its home, the service's address and the install's folder of launchers; a missing one leaves its part out. */
export interface TerminalFacts { home: string | null; url: string | null; bin: string | null }

/** `C:\Tools\x` as Git Bash spells it: `/c/Tools/x`. A path that is not a drive path is only given forward slashes. */
const forBash = (path: string): string => path.replace(/^([A-Za-z]):/, (_, drive: string) => `/${drive.toLowerCase()}`).replaceAll('\\', '/');

const powershell = (text: string): string => `'${text.replaceAll("'", "''")}'`;
const bash = (text: string): string => `'${text.replaceAll("'", `'\\''`)}'`;

/**
 * The line a person pastes into a terminal to reach this Daoris from it: `DAORIS_HOME` and `DAORIS_SERVICE_URL` for that
 * terminal only, and the install's launchers first on its `PATH`, so a script or a terminal from before the address file reaches this
 * install. It names this machine's paths and goes nowhere but the clipboard; nothing is changed on the account.
 */
export function terminalLine(shell: TerminalShell, facts: TerminalFacts): string {
  const { home, url, bin } = facts;
  if (shell === 'powershell') {
    return [
      home && `$env:DAORIS_HOME = ${powershell(home)}`,
      url && `$env:DAORIS_SERVICE_URL = ${powershell(url)}`,
      bin && `$env:PATH = ${powershell(`${bin};`)} + $env:PATH`,
    ].filter(Boolean).join('; ');
  }
  if (shell === 'cmd') {
    return [
      home && `set "DAORIS_HOME=${home}"`,
      url && `set "DAORIS_SERVICE_URL=${url}"`,
      bin && `set "PATH=${bin};%PATH%"`,
    ].filter(Boolean).join(' && ');
  }
  return [
    home && `export DAORIS_HOME=${bash(forBash(home))}`,
    url && `export DAORIS_SERVICE_URL=${bash(url)}`,
    bin && `export PATH=${bash(forBash(bin))}:"$PATH"`,
  ].filter(Boolean).join('; ');
}
