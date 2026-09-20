/**
 * The one filesystem helper the tooling shares. It existed five times — both rehearsals, the package
 * stager, and the web e2e host — each copy carrying the same one-line justification.
 */
import { copyFileSync, mkdirSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

/** Recursive copy. Deliberately not fs.cpSync — it has crashed on this platform. */
export function copyTree(from, to) {
  mkdirSync(to, { recursive: true });
  for (const entry of readdirSync(from, { withFileTypes: true })) {
    const source = join(from, entry.name);
    const target = join(to, entry.name);
    if (entry.isDirectory()) copyTree(source, target);
    else copyFileSync(source, target);
  }
}
