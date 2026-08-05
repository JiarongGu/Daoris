import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * BOM-less, LF. Every comparison in this tool happens on normalized text, so a
 * checkout that converted line endings is not mistaken for a local edit.
 */
export function normalize(text: string): string {
  return text.replace(/^﻿/, '').replace(/\r\n/g, '\n');
}

export function readText(file: string): string {
  return normalize(readFileSync(file, 'utf8'));
}

/**
 * Write beside the target, then rename — a crash never leaves a half-written
 * rule on disk. Node writes UTF-8 without a BOM, and the text is already LF.
 */
export function writeTextAtomic(file: string, text: string): void {
  mkdirSync(dirname(file), { recursive: true });
  const tmp = `${file}.daoris-tmp`;
  writeFileSync(tmp, text, 'utf8');
  renameSync(tmp, file);
}

export function sha256(text: string): string {
  return createHash('sha256').update(normalize(text), 'utf8').digest('hex');
}

/** Sorted, '/'-separated, recursive. An absent directory yields []. */
export function listFiles(dir: string, keep: (name: string) => boolean = () => true): string[] {
  if (!existsSync(dir)) return [];
  const out: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) {
      out.push(...listFiles(join(dir, entry.name), keep).map((path) => `${entry.name}/${path}`));
    } else if (keep(entry.name)) {
      out.push(entry.name);
    }
  }
  return out.sort();
}

export const listMarkdown = (dir: string): string[] => listFiles(dir, (name) => name.endsWith('.md'));
