import { describe, expect, it } from 'vitest';
import { fileLines, fileName, namedLines, treePath } from './preview';

// A file's preview (PREVIEW1, D111): the derivations the tool card, the review and the preview share.

describe('the path a preview asks for', () => {
  const tree = 'C:\\daoris\\data\\trees\\default\\engine\\daoris-s1';

  it('is a path inside the tree, relative to it, with forward slashes', () => {
    expect(treePath('C:\\daoris\\data\\trees\\default\\engine\\daoris-s1\\src\\chunk.ts', tree)).toBe('src/chunk.ts');
    // git answers in forward slashes, and so does a harness sometimes; a drive letter's case is the same place.
    expect(treePath('c:/daoris/data/trees/default/engine/daoris-s1/src/chunk.ts', tree)).toBe('src/chunk.ts');
  });

  it('is a relative path as it came, since the agent works in the tree', () => {
    expect(treePath('src/chunk.ts', tree)).toBe('src/chunk.ts');
    expect(treePath('src\\chunk.ts', tree)).toBe('src/chunk.ts');
    expect(treePath('./src/chunk.ts', tree)).toBe('src/chunk.ts');
  });

  it('is nothing for a path the page can see is outside the tree: that door could only refuse', () => {
    expect(treePath('C:\\daoris\\data\\trees\\default\\game\\src\\a.ts', tree)).toBeNull();
    // 🔴 A prefix is not a folder: the tree's sibling whose name begins with the tree's.
    expect(treePath('C:\\daoris\\data\\trees\\default\\engine\\daoris-s10\\a.ts', tree)).toBeNull();
    expect(treePath('/etc/passwd', '/home/me/engine')).toBeNull();
    expect(treePath('../game/a.ts', tree)).toBeNull();
    // The tree itself is not a file.
    expect(treePath(tree, tree)).toBeNull();
  });

  it('is nothing under git\'s own folder, which the preview refuses', () => {
    expect(treePath('.git/config', tree)).toBeNull();
    expect(treePath(`${tree}\\.git\\HEAD`, tree)).toBeNull();
    expect(treePath('.gitignore', tree)).toBe('.gitignore');
  });

  it('is a relative path alone when the tree is not known, and never an absolute one', () => {
    expect(treePath('src/chunk.ts', null)).toBe('src/chunk.ts');
    expect(treePath('C:\\somewhere\\src\\chunk.ts', null)).toBeNull();
    expect(treePath('', tree)).toBeNull();
  });
});

describe('the lines a tool call named', () => {
  it('are a read\'s offset and limit, from the call\'s own input', () => {
    expect(namedLines('{"file_path":"src/chunk.ts","offset":120,"limit":40}')).toEqual({ from: 120, to: 159 });
  });

  it('start at the first line when only a limit is given, and are one line when only an offset is', () => {
    expect(namedLines('{"file_path":"a.ts","limit":25}')).toEqual({ from: 1, to: 25 });
    expect(namedLines('{"file_path":"a.ts","offset":7}')).toEqual({ from: 7, to: 7 });
    expect(namedLines('{"file_path":"a.ts","offset":0,"limit":3}')).toEqual({ from: 1, to: 3 });
  });

  it('are nothing where the input names none, is not JSON, or was cut on the way', () => {
    expect(namedLines('{"file_path":"a.ts"}')).toBeNull();
    expect(namedLines('{"file_path":"a.ts","offset":"x"}')).toBeNull();
    expect(namedLines('{"file_path":"a.ts","offset":')).toBeNull();
    expect(namedLines(null)).toBeNull();
    expect(namedLines('[1,2]')).toBeNull();
  });
});

describe('a file\'s lines', () => {
  it('are its text split at each line\'s end, the last newline ending a line rather than starting one', () => {
    expect(fileLines('a\nb\n')).toEqual(['a', 'b']);
    expect(fileLines('a\r\nb')).toEqual(['a', 'b']);
    expect(fileLines('')).toEqual([]);
    expect(fileLines('\n')).toEqual(['']);
  });
});

describe('a file\'s name', () => {
  it('is the last segment of its path', () => {
    expect(fileName('src/work/PatchView.tsx')).toBe('PatchView.tsx');
    expect(fileName('README.md')).toBe('README.md');
  });
});
