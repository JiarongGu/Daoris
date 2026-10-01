// A managed version of a tool: downloaded, verified, unpacked and laid out (TOOLS4, D121;
// `docs/2026-10-01-tools-design.md` §3.6, §3.7).
//
// 🔴 A TWIN of the driver's `ToolInstall.cs`. The two share no code — the LAYOUT and the refusals are the
// contract — and each carries the same tables (`toolinstall.test.ts` here, `ToolInstallTests.cs` there), row for
// row. A rule changed here is changed there, in the same commit.
//
// NO NETWORK HERE, and nothing spawned. Every byte arrives through a `Fetcher` the caller hands in —
// `service.ts` is still the one module that may open a socket — so a doctrine command that somehow reached this
// file still could not fetch anything (the dogfood tests hold both).

import { chmodSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { RefusalError } from './errors.ts';
import { extractTarGz } from './tarball.ts';
import { extractZip } from './zipfile.ts';

/**
 * Unpack a verified archive whole into `into`, and find the executable its list names there (§3.6). @returns the
 * executable's path.
 *
 * @throws RefusalError naming its check: the archive's own (`zipfile.ts`, `tarball.ts`), `archive` for a kind this
 * build does not unpack, or `exe` when the executable is not a file in it.
 */
export async function unpackPackage(archive: string, kind: string, into: string, exe: string): Promise<string> {
  if (kind === 'zip') await extractZip(archive, into);
  else if (kind === 'tar.gz') await extractTarGz(archive, into);
  else throw new RefusalError('archive', `\`${kind}\` is not an archive this build unpacks — zip or tar.gz`);

  const file = join(into, ...exe.split('/'));
  if (!isFile(file)) {
    throw new RefusalError('exe', `the archive holds no file at \`${exe}\`, the executable its list names — nothing of it is kept`);
  }
  // Nothing is patched (§3.6): the executable bit off Windows is the one change, and it is the file system's.
  if (process.platform !== 'win32') chmodSync(file, 0o755);
  return file;
}

/** A file, as .NET's `File.Exists` answers: a folder is not one. */
function isFile(path: string): boolean {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}
