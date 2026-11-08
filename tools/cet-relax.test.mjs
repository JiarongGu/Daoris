/**
 * The desktop's executable relaxes the shadow stack's context validation: Windows kills a CET-enforced process
 * (fail-fast 0xc0000409, code 0x30) when an exception a shell extension raises under the file picker's context menu is
 * handled, so the executable carries the relaxed-mode flag beside its CET flag.
 *
 *   node --test tools/cet-relax.test.mjs
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cetFlags, relaxCetContextValidation } from './cet-relax.mjs';

/** A minimal PE32+ image: one section holding a debug directory with one extended-characteristics entry. */
function image(flags) {
  const b = Buffer.alloc(0x400);
  b.writeUInt32LE(0x80, 0x3c);
  b.write('PE\0\0', 0x80, 'latin1');
  b.writeUInt16LE(1, 0x80 + 6); // sections
  b.writeUInt16LE(240, 0x80 + 20); // optional header size
  const opt = 0x80 + 24;
  b.writeUInt16LE(0x20b, opt);
  const dd = opt + 112;
  b.writeUInt32LE(0x1000, dd + 6 * 8); // debug directory rva
  b.writeUInt32LE(28, dd + 6 * 8 + 4);
  const sec = opt + 240;
  b.writeUInt32LE(0x200, sec + 8); // virtual size
  b.writeUInt32LE(0x1000, sec + 12); // virtual address
  b.writeUInt32LE(0x200, sec + 20); // raw pointer
  const entry = 0x200;
  b.writeUInt32LE(20, entry + 12); // IMAGE_DEBUG_TYPE_EX_DLLCHARACTERISTICS
  b.writeUInt32LE(4, entry + 16);
  b.writeUInt32LE(0x300, entry + 24);
  b.writeUInt32LE(flags, 0x300);
  return b;
}

test('a CET-compatible image gains the relaxed context validation flag and keeps its shadow stack', () => {
  const b = image(0x1);
  assert.equal(relaxCetContextValidation(b), true);
  assert.equal(cetFlags(b), 0x5);
});

test('an image already relaxed, or with no CET flag at all, is left as it is', () => {
  const relaxed = image(0x5);
  assert.equal(relaxCetContextValidation(relaxed), false);
  const none = image(0);
  assert.equal(relaxCetContextValidation(none), false);
  assert.equal(cetFlags(none), 0);
});
