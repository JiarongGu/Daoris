#!/usr/bin/env node
/**
 * Publish the service hosts as standalone executables — the server the family actually runs, not a
 * source checkout with a build step (D43).
 *
 *   node tools/service-publish.mjs                # publish both hosts for this platform
 *   node tools/service-publish.mjs --install      # …and land them in $DAORIS_HOME/bin (D63), printing
 *                                                 #    the ready .mcp.json snippet with the root filled in
 *   node tools/service-publish.mjs --rid linux-x64
 *
 * Self-contained single-file: no .NET install on the consuming machine, and reflection stays intact
 * (the MCP SDK discovers tools by attribute, so trimming and AOT are deliberately NOT used here —
 * the devkit is the AOT artefact; this one values working over 3 MB).
 *
 * The HTTP host's web bundle travels as a wwwroot directory BESIDE its executable — the host resolves
 * its content root there when the working directory has no bundle.
 *
 * **The one recipe** (CONNECTOR1). `desktop-publish --service` lays both hosts under an install's `app/`
 * by the exports above the divider, each in the folder named for its binary that this script publishes
 * it into, so a host is built one way wherever it is published. The runner below the divider is guarded
 * (`isMain`), because importing this file must publish nothing.
 */
import { execSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { copyTree, isMain } from './fsx.mjs';

/** The two hosts, each published into a folder named for its binary. */
export const HOSTS = Object.freeze([
  Object.freeze({ project: 'src/Daoris.Service/Daoris.Service.Mcp', binary: 'daoris-knowledge' }),
  Object.freeze({ project: 'src/Daoris.Service/Daoris.Service.Http', binary: 'daoris-knowledge-http' }),
]);

/** A host's executable's name for a runtime identifier. */
export const executableFor = (binary, rid) => (rid.startsWith('win') ? `${binary}.exe` : binary);

/**
 * The recipe: the `dotnet publish` that makes `host` a self-contained single-file executable in `folder`, run from the
 * workspace root.
 *
 * IncludeNativeLibrariesForSelfExtract, because "single file" otherwise leaves e_sqlite3 BESIDE the executable — and an
 * installed copy that took only the exe dies on first store open with a DllNotFound. Found by running the installed
 * binary, not by reading the docs.
 */
export function publishCommand(host, rid, folder) {
  return `dotnet publish "${host.project}" -c Release -r ${rid} --self-contained `
    + `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "${folder}"`;
}

/**
 * Publish one host by the recipe, answering whether it was built. stdout is KEPT and shown when the publish fails (REV3):
 * MSBuild writes its errors to stdout, and a publish that discarded it failed with nothing but an exit code to read.
 */
export function publishHost(repoRoot, host, rid, folder, said = 'service-publish') {
  try {
    execSync(publishCommand(host, rid, folder), { cwd: repoRoot, stdio: ['ignore', 'pipe', 'inherit'], maxBuffer: 64 * 1024 * 1024 });
    return true;
  } catch (error) {
    process.stdout.write(error.stdout ?? '');
    console.error(`${said}: publishing ${host.binary} failed — MSBuild's own words are above`);
    return false;
  }
}

// ---------------------------------------------------------------------------------------------

function main() {
  const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
  const argv = process.argv.slice(2);
  const ridArg = argv.indexOf('--rid');
  const rid = ridArg !== -1 && argv[ridArg + 1]
    ? argv[ridArg + 1]
    : process.platform === 'win32' ? 'win-x64'
      : process.platform === 'darwin' ? (process.arch === 'arm64' ? 'osx-arm64' : 'osx-x64')
        : 'linux-x64';

  const out = join(repoRoot, 'publish', 'service', rid);
  const exe = (name) => executableFor(name, rid);

  rmSync(out, { recursive: true, force: true });

  for (const host of HOSTS) {
    console.log(`service-publish: ${host.binary} → ${rid}`);
    if (!publishHost(repoRoot, host, rid, join(out, host.binary))) process.exit(1);
  }

  console.log(`service-publish: published under ${out}`);

  if (argv.includes('--install')) {
    // The web bundle must exist to travel — a host installed without its page serves 404s for the UI.
    const wwwroot = join(out, 'daoris-knowledge-http', 'wwwroot');
    if (!existsSync(wwwroot)) {
      console.error('service-publish: the HTTP publish carries no wwwroot — build the web bundle first');
      console.error('  (npm --prefix src/Daoris.Web run build), then re-run with --install.');
      process.exit(1);
    }

    // Under the Daoris home, never under the profile (D63): the installed desktop sets `DAORIS_HOME`
    // for the account, and a machine with no home has nowhere Daoris may put a binary.
    const home = process.env.DAORIS_HOME?.trim();
    if (!home) {
      console.error('service-publish: --install needs DAORIS_HOME — the installed application\'s `data`');
      console.error('  folder (the desktop sets it for your account on first start). Daoris keeps nothing');
      console.error('  under the user profile, so there is no default to fall back to.');
      process.exit(1);
    }
    const bin = join(home, 'bin');
    mkdirSync(bin, { recursive: true });
    copyFileSync(join(out, 'daoris-knowledge', exe('daoris-knowledge')), join(bin, exe('daoris-knowledge')));
    // 🔴 REPLACED, not merged into. A copy over the old directory leaves every previous hashed bundle
    // in `wwwroot/assets` — harmless, because `index.html` names the current one, and actively
    // misleading to anybody trying to tell which build is live by listing the folder. That is exactly
    // how a stale deployment was diagnosed the slow way once.
    const installedHost = join(bin, 'daoris-knowledge-http');
    rmSync(installedHost, { recursive: true, force: true });
    // The tools' one recursive copy (REV3 CLEAN1): `fs.cpSync` has crashed on this platform.
    copyTree(join(out, 'daoris-knowledge-http'), installedHost);

    // The published binary has no workspace above it to walk to, so the root must be NAMED — this is
    // exactly the trap the README records, closed here by printing the snippet already filled in.
    const familyRoot = dirname(repoRoot);
    console.log(`service-publish: installed to ${bin}`);
    // CONNECTOR1: an install published with `--service` carries both hosts under its `app/`, and its sessions are
    // handed that connector ahead of this one; this copy serves a repository's own `.mcp.json` and a shell without them.
    console.log('  A desktop install published with --service runs the hosts it carries ahead of these.');
    console.log('');
    console.log('add to a repository\'s .mcp.json (its own file to write):');
    console.log(JSON.stringify({
      mcpServers: {
        'daoris-knowledge': {
          command: join(bin, exe('daoris-knowledge')),
          // The home named explicitly, because a harness spawns its servers with whatever environment
          // it was itself started with — and a terminal opened before the desktop set the variable
          // has none. The snippet and the binary move together either way.
          env: { DAORIS_HOME: home, DAORIS_KNOWLEDGE_ROOT: familyRoot },
        },
      },
    }, null, 2));
    console.log('');
    console.log(`the platform: ${join(bin, 'daoris-knowledge-http', exe('daoris-knowledge-http'))}  → http://localhost:5177`);
  }
}

// Guarded, because `desktop-publish.mjs` and its tests import the recipe above: importing this file must publish nothing.
if (isMain(import.meta.url)) main();
