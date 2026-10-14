import { describe, expect, it } from 'vitest';
import { admit, isImage, linksOf, MAX_FILES, MAX_FILE_BYTES, toUpload } from './attachments';

// What the composer does with files and links BEFORE the service judges them (D65 §2). The service
// is the authority — these only keep the form from offering an ask it would refuse, and say why.

const file = (name: string, size: number, text = 'x') => {
  const made = new File([text], name);
  Object.defineProperty(made, 'size', { value: size });
  return made;
};

describe('admit', () => {
  it('adds what fits, in the order it came', () => {
    const { files, refused } = admit([file('a.png', 10)], [file('b.txt', 20), file('c.log', 30)]);
    expect(files.map((f) => f.name)).toEqual(['a.png', 'b.txt', 'c.log']);
    expect(refused).toBeNull();
  });

  it(`refuses the ones past ${MAX_FILES} and says so, keeping what was already there`, () => {
    const current = Array.from({ length: MAX_FILES }, (_, i) => file(`f${i}.txt`, 1));
    const { files, refused } = admit(current, [file('one-more.txt', 1)]);
    expect(files).toHaveLength(MAX_FILES);
    expect(refused).toBe('tooMany');
  });

  it('refuses what would take the total past the limit — a link is the better thing to carry', () => {
    const { files, refused } = admit([file('a.bin', MAX_FILE_BYTES - 5)], [file('b.bin', 10), file('c.bin', 2)]);
    expect(files.map((f) => f.name)).toEqual(['a.bin', 'c.bin']);
    expect(refused).toBe('tooLarge');
  });
});

describe('linksOf', () => {
  it('reads one address per line or per space, trimmed, once each', () => {
    expect(linksOf(' https://a.example \n\nhttps://b.example https://a.example ')).toEqual([
      'https://a.example', 'https://b.example',
    ]);
  });

  it('is empty for nothing', () => {
    expect(linksOf('   \n ')).toEqual([]);
  });
});

describe('toUpload', () => {
  it('carries the name and the bytes as base64 — what a byte array is in JSON', async () => {
    const upload = await toUpload(new File(['stack'], 'trace.log'));
    expect(upload).toEqual({ name: 'trace.log', content: btoa('stack') });
  });
});

describe('isImage', () => {
  it('knows the kinds a drawer can show as a picture', () => {
    expect(isImage('before.png')).toBe(true);
    expect(isImage('SHOT.JPG')).toBe(true);
    expect(isImage('notes.txt')).toBe(false);
    // An SVG is a document that can carry script — shown as a file, never inlined.
    expect(isImage('diagram.svg')).toBe(false);
  });
});
