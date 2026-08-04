import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * BOM-less, LF. Every comparison in this tool happens on normalized text, so a
 * checkout that converted line endings is not mistaken for a local edit.
 */
export function normalize(text) {
  return text.replace(/^﻿/, '').replace(/\r\n/g, '\n');
}

export function readText(file) {
  return normalize(readFileSync(file, 'utf8'));
}

/**
 * Write beside the target, then rename — a crash never leaves a half-written
 * rule on disk. Node writes UTF-8 without a BOM, and the text is already LF.
 */
export function writeTextAtomic(file, text) {
  mkdirSync(dirname(file), { recursive: true });
  const tmp = `${file}.daoris-tmp`;
  writeFileSync(tmp, text, 'utf8');
  renameSync(tmp, file);
}

export function sha256(text) {
  return createHash('sha256').update(normalize(text), 'utf8').digest('hex');
}

/** Sorted, '/'-separated, recursive, .md only. An absent directory yields []. */
export function listMarkdown(dir) {
  if (!existsSync(dir)) return [];
  const out = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      out.push(...listMarkdown(join(dir, entry.name)).map((path) => `${entry.name}/${path}`));
    } else if (entry.name.endsWith('.md')) {
      out.push(entry.name);
    }
  }
  return out.sort();
}
