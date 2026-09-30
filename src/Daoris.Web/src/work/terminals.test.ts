import { describe, expect, it } from 'vitest';
import { folderName, type OpenTerminal, terminalNames } from './terminals';

// CONSOLE4c: a tab is named by its shell and where it started, and a name that repeats says which it is.

const terminal = (id: string, shell: string, cwd: string): OpenTerminal => ({ id, shell, cwd, exit: null });
const name = (row: OpenTerminal) => `${row.shell} · ${folderName(row.cwd)}`;
const nth = (base: string, n: number) => `${base} (${n})`;

describe('a terminal\'s tab name', () => {
  it('is the shell and the folder it started in', () => {
    expect(terminalNames([terminal('a', 'pwsh', 'C:\\somewhere\\engine')], name, nth)).toEqual({ a: 'pwsh · engine' });
  });

  it('numbers a name that repeats in the order the terminals opened, leaving the first as it was', () => {
    const list = [
      terminal('a', 'pwsh', 'C:/somewhere/engine'),
      terminal('b', 'cmd', 'C:/somewhere/engine'),
      terminal('c', 'pwsh', 'C:/somewhere/engine/'),
      terminal('d', 'pwsh', 'D:/elsewhere/engine'),
    ];
    expect(terminalNames(list, name, nth)).toEqual({
      a: 'pwsh · engine', b: 'cmd · engine', c: 'pwsh · engine (2)', d: 'pwsh · engine (3)',
    });
  });

  it('takes a folder\'s last segment whichever way its path is written', () => {
    expect(folderName('C:\\somewhere\\engine\\')).toBe('engine');
    expect(folderName('/home/someone/engine')).toBe('engine');
    expect(folderName('C:\\')).toBe('C:');
  });
});
