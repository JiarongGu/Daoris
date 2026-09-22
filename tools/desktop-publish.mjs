#!/usr/bin/env node
/**
 * Publish the desktop shell as an installed application — the thing a person runs, not a checkout
 * with a build step. The sibling of `service-publish.mjs`, and it exists for the same reason: a
 * workspace build is not a deployment, and the difference only shows up once you try.
 *
 *   node tools/desktop-publish.mjs --to <folder>          publish the shell into <folder>
 *   node tools/desktop-publish.mjs --to <folder> --service  …and the HTTP host beside it, with its bundle
 *
 * 🔴 **`--to` is required and has no default.** A machine path in a tracked file is exactly what
 * `sensitive-info` forbids, and a default would be one — the same rule `testbed.mjs` follows, for the
 * same reason.
 *
 * **Framework-dependent on purpose.** The shell already requires a Windows desktop runtime and the
 * WebView2 runtime; a self-contained publish would add ~150 MB to carry a .NET that this machine has
 * and would still not carry WebView2. The service hosts are the opposite case and are self-contained
 * — they are what a *server* runs, possibly without .NET at all (D43).
 *
 * **What a deployed shell finds.** Nothing is wired into it: with no `DAORIS_*` overrides it uses the
 * real `~/.daoris` — the machine's registry, quests and drivable set — and locates the HTTP host
 * through `ServiceHostLocator`, which looks at `~/.daoris/bin` before any workspace. `--service`
 * publishes a copy beside the shell as well, so the folder is self-sufficient.
 */
import { execSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const argv = process.argv.slice(2);
const flag = (name) => argv.includes(name);
const value = (name) => {
  const at = argv.indexOf(name);
  return at !== -1 && argv[at + 1] && !argv[at + 1].startsWith('--') ? argv[at + 1] : null;
};

const toArg = value('--to');
if (!toArg) {
  console.error('desktop-publish: --to <folder> is required.\n'
    + '  It has no default on purpose: a machine path does not belong in a tracked file.');
  process.exit(2);
}
const to = isAbsolute(toArg) ? toArg : resolve(process.cwd(), toArg);

const APP = 'src/Daoris.Desktop/Daoris.Desktop.App';
const HTTP = 'src/Daoris.Service/Daoris.Service.Http';
const WEB = 'src/Daoris.Web';

const run = (command) => execSync(command, { cwd: repoRoot, stdio: ['ignore', 'inherit', 'inherit'] });

/** What says this folder is ours to overwrite. Written on every publish; read before any. */
const MARKER = 'INSTALLED.md';
const MARKER_HEADER = '# Daoris — installed desktop';

/**
 * 🔴 The guard: this writes a whole directory tree, so it refuses a folder holding anything it did
 * not put there. A publish into somebody's documents folder and a publish into an install folder
 * look identical to `cpSync`.
 *
 * **Decided by a marker, not by a list of extensions.** The first version allowlisted the file types
 * a publish emits and refused its own second run, because `dotnet publish` also drops `.xml` doc
 * files — and an allowlist is a list people append to until it allows everything. A marker this
 * script writes is decidable: either we published here or we did not. It is the same provenance test
 * the dsh patch layer uses, for the same reason.
 */
if (existsSync(to)) {
  const held = readdirSync(to);
  const marker = join(to, MARKER);
  const ours = existsSync(marker)
    && readFileSync(marker, 'utf8').startsWith(MARKER_HEADER);

  if (held.length > 0 && !ours) {
    // Naming what it found, not the first few things it listed: a refusal that points at the wrong
    // file sends you to look in the wrong place, which is worse than one that says nothing.
    console.error(`desktop-publish: \`${to}\` is not a folder this script published — it holds `
      + `${held.length} item(s) and no \`${MARKER}\`: ${held.slice(0, 6).join(', ')}`
      + `${held.length > 6 ? ', …' : ''}\n  Refusing to write into it. Point --to at an empty folder, `
      + 'or one this script installed to before.');
    process.exit(2);
  }
}

// The dependency order, and it is not cosmetic: the platform builds INTO the host's wwwroot, so a
// host published before the bundle carries the previous one — and the window shows it.
console.log('desktop-publish: building the platform bundle…');
run(`npm --prefix ${WEB} run build`);

console.log('desktop-publish: publishing the shell…');
run(`dotnet publish "${APP}" -c Release -o "${to}" --nologo`);

if (flag('--service')) {
  console.log('desktop-publish: publishing the HTTP host beside it…');
  const host = join(to, 'daoris-knowledge-http');
  run(`dotnet publish "${HTTP}" -c Release -r win-x64 --self-contained `
    + `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "${host}"`);

  // The bundle travels BESIDE the executable — a host installed without its page answers every API
  // call and serves 404 for the UI, which reads as a broken app rather than a missing file.
  const bundle = join(host, 'wwwroot');
  if (!existsSync(bundle)) {
    console.error('desktop-publish: the HTTP publish carries no wwwroot — the shell would serve no page.');
    process.exit(1);
  }
}

// What this folder is, for whoever opens it in six months. Written here rather than tracked, because
// it names a machine path — the file belongs to the install, not to the repository.
mkdirSync(to, { recursive: true });
writeFileSync(join(to, MARKER), `${MARKER_HEADER}

Published from a Daoris workspace by \`tools/desktop-publish.mjs\`.

- \`daoris-desktop.exe\` — the shell. It brings up the local HTTP host, carries the platform in its
  window, and runs the driver loop in-process.
- It uses this machine's real \`~/.daoris\`: the registry, the quests, the drivable set. Starting it
  starts the driver loop, so **a drivable repository with an open quest gets a real agent session.**
- \`daoris driver list\` and the Machine view are two doors onto the same choices.

Re-publish over this folder to update it; nothing here is edited by hand.
`);

console.log(`\ndesktop-publish: installed to ${to}`);
console.log('  It runs against your real ~/.daoris — starting it starts the driver loop.');
