/**
 * Relax the shadow stack's context validation in the desktop's executable.
 *
 *   node tools/cet-relax.mjs <exe>      sets the flag in place; says what it did
 *
 * The executable is the engine's launcher, built with Windows' hardware stack protection and its strict check on the
 * instruction a handled exception resumes at. A shell extension that raises and handles an exception of its own (a
 * context menu handler under a file picker's right-click) resumes somewhere that check refuses, and Windows ends the
 * whole process with fail-fast 0xc0000409, code 0x30. The shadow stack stays on; only that check is relaxed.
 * Run by the desktop project after its build and its publish lay the executable out.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { isMain } from './fsx.mjs';

const DEBUG_DIRECTORY = 6;
const EX_DLLCHARACTERISTICS = 20;
const CET_COMPAT = 0x1;
const RELAXED_CONTEXT_VALIDATION = 0x4;

/** The offset in the image of the extended characteristics' flags, or -1 where it holds none. */
function flagsOffset(image) {
  const pe = image.readUInt32LE(0x3c);
  const sections = image.readUInt16LE(pe + 6);
  const optional = pe + 24;
  const table = optional + (image.readUInt16LE(optional) === 0x20b ? 112 : 96);
  const rva = image.readUInt32LE(table + DEBUG_DIRECTORY * 8);
  const size = image.readUInt32LE(table + DEBUG_DIRECTORY * 8 + 4);
  const first = optional + image.readUInt16LE(pe + 20);
  const toOffset = (at) => {
    for (let i = 0; i < sections; i++) {
      const section = first + i * 40;
      const start = image.readUInt32LE(section + 12);
      if (at >= start && at < start + image.readUInt32LE(section + 8)) return image.readUInt32LE(section + 20) + at - start;
    }
    return -1;
  };

  const directory = rva === 0 ? -1 : toOffset(rva);
  if (directory < 0) return -1;
  for (let i = 0; i < size / 28; i++) {
    const entry = directory + i * 28;
    if (image.readUInt32LE(entry + 12) === EX_DLLCHARACTERISTICS && image.readUInt32LE(entry + 16) >= 4) return image.readUInt32LE(entry + 24);
  }

  return -1;
}

/** The extended characteristics' flags of a PE image; 0 where it has none. */
export function cetFlags(image) {
  const at = flagsOffset(image);
  return at < 0 ? 0 : image.readUInt32LE(at);
}

/** Set the relaxed flag on an image that is CET compatible, in place; whether it changed. */
export function relaxCetContextValidation(image) {
  const at = flagsOffset(image);
  if (at < 0) return false;
  const flags = image.readUInt32LE(at);
  if ((flags & CET_COMPAT) === 0 || (flags & RELAXED_CONTEXT_VALIDATION) !== 0) return false;
  image.writeUInt32LE(flags | RELAXED_CONTEXT_VALIDATION, at);
  return true;
}

if (isMain(import.meta.url)) {
  const file = process.argv[2];
  if (!file) {
    console.error('usage: node tools/cet-relax.mjs <exe>');
    process.exit(2);
  }

  const image = readFileSync(file);
  if (relaxCetContextValidation(image)) {
    writeFileSync(file, image);
    console.log(`${file}: context validation relaxed`);
  } else {
    console.log(`${file}: nothing to relax`);
  }
}
