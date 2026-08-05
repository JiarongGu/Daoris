#!/usr/bin/env node
// The published entry point, and the only file `package.json`'s `bin` names.
//
// It stays `.mjs` and stays this thin on purpose: every consumer executes it, and their Node may be
// 22, which does not strip types on its own. The dispatcher lives in `src/cli.ts` and is compiled to
// `dist/` at pack time — so this resolves whichever of the two is present, preferring the built one.
import { existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const built = join(packageRoot, 'dist', 'cli.js');
const entry = existsSync(built) ? built : join(packageRoot, 'src', 'cli.ts');

const { runCli } = await import(pathToFileURL(entry).href);
process.exit(await runCli(process.argv.slice(2), process.cwd()));
