#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { DaorisError } from '../src/errors.mjs';
import { commandIndex } from '../src/indexgen.mjs';
import { commandSync } from '../src/materialize.mjs';
import { commandCheck } from '../src/drift.mjs';
import { commandUpstream } from '../src/upstream.mjs';
import { commandInit, commandStatus } from '../src/commands.mjs';
import { commandDoctor } from '../src/twins.mjs';

export const packageRoot = dirname(dirname(fileURLToPath(import.meta.url)));

const USAGE = `daoris <command> [options]

  init                 detect what this repo has, write daoris.json
  sync                 materialize the manifest's packs; write daoris.lock
  check                drift, staleness, index freshness, core budget (offline)
  upstream <file>      promote a locally-edited canonical file back to the canon
  index                regenerate RULES_INDEX.md from what is on disk
  status               human summary of packs, drift, and local files
  doctor               report local documents that look like canonical ones
                       under a different name (advisory; never fails)

Options:
  --dry-run            print the plan; write nothing
  --force              overwrite locally-drifted files (sync only)
  --all                promote every drifted file (upstream only)
  --help, --version`;

/** Commands are registered here as they land. @returns {number} process exit code */
const commands = {
  index: commandIndex,
  sync: commandSync,
  check: commandCheck,
  upstream: commandUpstream,
  init: commandInit,
  status: commandStatus,
  doctor: commandDoctor,
};

export function runCli(argv, cwd, write = console.log) {
  try {
    if (argv.includes('--help')) {
      write(USAGE);
      return 0;
    }
    if (argv.includes('--version')) {
      write(JSON.parse(readFileSync(join(packageRoot, 'package.json'), 'utf8')).version);
      return 0;
    }

    const command = argv[0];
    if (!command) {
      write(USAGE);
      return 2;
    }

    const handler = commands[command];
    if (!handler) throw new DaorisError(`unknown command '${command}' — run 'daoris --help'`);
    return handler({ root: cwd, argv: argv.slice(1), write, packageRoot });
  } catch (error) {
    if (error instanceof DaorisError) {
      write(`daoris: ${error.message}`);
      return error.exitCode;
    }
    throw error;
  }
}

if (process.argv[1] && process.argv[1].endsWith('daoris.mjs')) {
  process.exit(runCli(process.argv.slice(2), process.cwd()));
}
