#!/usr/bin/env node
/**
 * Publish the service hosts as standalone executables — the server the family actually runs, not a
 * source checkout with a build step (D43).
 *
 *   node tools/service-publish.mjs                # publish both hosts for this platform
 *   node tools/service-publish.mjs --install      # …and land them in ~/.daoris/bin, printing the
 *                                                 #    ready .mcp.json snippet with the root filled in
 *   node tools/service-publish.mjs --rid linux-x64
 *
 * Self-contained single-file: no .NET install on the consuming machine, and reflection stays intact
 * (the MCP SDK discovers tools by attribute, so trimming and AOT are deliberately NOT used here —
 * the devkit is the AOT artefact; this one values working over 3 MB).
 *
 * The HTTP host's web bundle travels as a wwwroot directory BESIDE its executable — the host resolves
 * its content root there when the working directory has no bundle.
 */
import { execSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { homedir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const argv = process.argv.slice(2);
const ridArg = argv.indexOf('--rid');
const rid = ridArg !== -1 && argv[ridArg + 1]
  ? argv[ridArg + 1]
  : process.platform === 'win32' ? 'win-x64'
    : process.platform === 'darwin' ? (process.arch === 'arm64' ? 'osx-arm64' : 'osx-x64')
      : 'linux-x64';

const out = join(repoRoot, 'publish', 'service', rid);
const exe = (name) => (rid.startsWith('win') ? `${name}.exe` : name);

const HOSTS = [
  { project: 'src/Daoris.Service/Daoris.Service.Mcp', binary: 'daoris-knowledge' },
  { project: 'src/Daoris.Service/Daoris.Service.Http', binary: 'daoris-knowledge-http' },
];

rmSync(out, { recursive: true, force: true });

for (const host of HOSTS) {
  console.log(`service-publish: ${host.binary} → ${rid}`);
  // IncludeNativeLibrariesForSelfExtract, because "single file" otherwise leaves e_sqlite3 BESIDE
  // the executable — and an installed copy that took only the exe dies on first store open with a
  // DllNotFound. Found by running the installed binary, not by reading the docs.
  execSync(
    `dotnet publish "${host.project}" -c Release -r ${rid} --self-contained `
    + `-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "${join(out, host.binary)}"`,
    { cwd: repoRoot, stdio: ['ignore', 'ignore', 'inherit'] },
  );
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

  const bin = join(homedir(), '.daoris', 'bin');
  mkdirSync(bin, { recursive: true });
  cpSync(join(out, 'daoris-knowledge', exe('daoris-knowledge')), join(bin, exe('daoris-knowledge')));
  cpSync(join(out, 'daoris-knowledge-http'), join(bin, 'daoris-knowledge-http'), { recursive: true });

  // The published binary has no workspace above it to walk to, so the root must be NAMED — this is
  // exactly the trap the README records, closed here by printing the snippet already filled in.
  const familyRoot = dirname(repoRoot);
  console.log(`service-publish: installed to ${bin}`);
  console.log('');
  console.log('add to a repository\'s .mcp.json (its own file to write):');
  console.log(JSON.stringify({
    mcpServers: {
      'daoris-knowledge': {
        command: join(bin, exe('daoris-knowledge')),
        env: { DAORIS_KNOWLEDGE_ROOT: familyRoot },
      },
    },
  }, null, 2));
  console.log('');
  console.log(`the platform: ${join(bin, 'daoris-knowledge-http', exe('daoris-knowledge-http'))}  → http://localhost:5177`);
}
