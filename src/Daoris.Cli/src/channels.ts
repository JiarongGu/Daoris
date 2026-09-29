// A managed install from the vendor's OWN channel (AGT2b) — what `daoris agent pin` does for the two
// agents whose makers publish one, instead of going through npm. `docs/2026-09-24-agt2b-channel-
// evidence.md` is where every URL, shape and name below was checked; nothing here is a guess about
// somebody else's release host, and what that document lists as unconfirmed is not relied on.
//
//   Claude Code  the release bucket's manifest, verified against its detached OpenPGP signature under
//                the PINNED release key — and only then its SHA-256 trusted for the binary, which is
//                fetched directly. Never the bootstrap and never `claude install`: the bootstrap always
//                fetches latest, and what the subcommand verifies is undocumented.
//   Codex        the release's package tarball, by the digest its metadata publishes AND the line its
//                SUMS file publishes (the SUMS file itself checked against the metadata first), then
//                unpacked whole — its executable finds its helpers through the package's layout.
//
// 🔴 **The binary is installed and run as published** (the vendor's terms for Claude Code): the bytes
// that verified are the bytes that land, and nothing is patched, wrapped or repacked. Setting the
// executable bit is the only change, and it is the file system's, not the binary's.
//
// NO NETWORK HERE. Every byte arrives through a `Fetcher` the caller hands in — `service.ts` is still
// the one module that may open a socket, and a test hands in a table instead. So a doctrine command
// that somehow reached this file still could not fetch anything.

import { chmodSync, existsSync, mkdirSync, renameSync, rmSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import { DaorisError } from './errors.ts';
import { fingerprintOf, spaced, verifyDetached } from './openpgp.ts';
import { extractTarGz } from './tarball.ts';

/** How the channel's bytes arrive. `null` means the host answered that there is nothing there. */
export interface Fetcher {
  bytes(url: string): Promise<Buffer | null>;
  /** Stream a download to `to`, hashing on the way. */
  save(url: string, to: string): Promise<{ sha256: string; size: number } | null>;
}

/** The channels this build installs from — one per maker that publishes one. */
export type Channel = 'claude-code-releases' | 'codex-releases';

export const CLAUDE_RELEASES = 'https://downloads.claude.ai/claude-code-releases';
export const CODEX_RELEASES = 'https://releases.openai.com/codex/releases';
/** The documented fallback when a Codex release's own metadata does not answer. */
export const CODEX_GITHUB = 'https://api.github.com/repos/openai/codex/releases/tags';

/**
 * Where each channel names its newest release (USE1a), per the channel evidence: Claude Code's
 * `latest` answers a version as plain text, and Codex's `channels/latest` answers the newest
 * release's own metadata. Claude Code's undocumented `stable` pointer is not used.
 */
export const CLAUDE_LATEST = `${CLAUDE_RELEASES}/latest`;
export const CODEX_LATEST = 'https://releases.openai.com/codex/channels/latest';

/**
 * The release key's fingerprint, as the vendor publishes it — **the trust root**, and a line of this
 * repository reviewed like any other. A vendor that rotates its key changes this through a change
 * somebody reads.
 */
export const CLAUDE_RELEASE_FINGERPRINT = fingerprintOf('31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE');

/**
 * The first Claude Code version whose manifest is signed. The `.sig` for 2.1.87 answers 404, and
 * 2.1.88 was never published.
 */
export const FIRST_SIGNED = '2.1.89';

/**
 * The release key itself, carried rather than fetched: one fetch fewer, and a key that cannot be
 * swapped in transit. `verifyDetached` still checks it against the fingerprint above, so an edit to
 * this block that is not the vendor's key verifies nothing. The published copy is
 * `https://downloads.claude.ai/keys/claude-code.asc`; a test holds the two equal.
 */
export const CLAUDE_RELEASE_KEY = `-----BEGIN PGP PUBLIC KEY BLOCK-----

mQINBGnK73ABEACnbytJXkjweYrwIr0aLEFRlH+C0nF44KxFc7gQmJ6PjSPMGZAD
dxZcaixU7zZl8WxEpVO0wLmIH8cf2zGOdyuZg1Yaugk1vHb2b8WBhAGCQJdPgB8W
XquedepEYtk56uP/gCoTjJDUZluEGBHnlnuujSJ4orxEdhSykEoAUfJZGEILPpMd
bphFt/Sn+Eb/TxM5jpKPdwnv8AShNF/1mZU1fWTQq9tRKJUakZj04gdaDFElQXak
CtTij+GT6yoYCARSHwGO+PC/Pr6q4tc+D7LRjxSBvUWDoFSmlqb/PJ1hj9D/7I2O
e4XXniAPWMR56KvxHlzOzrNQdJujbJdSkCwh1ZijkSd3y8ayW5WYUTGdRab99NUw
agzlabe/VVF6kzJ0Scn5q3PihB2Y9Bwo0CKnkYk7a7KT77EWv0Kkq+VHmOtqX3a2
hhX+b6a6ve9rzJ1qZYGj+obv/C3Sx1LzUjAfqVy7RJDf2uAoP5t2g8u/TkSpUxhM
VEjZBkSxYZhMyzQM6t8IgkUfnSrIPTHixbDWARZ4beMOBjxyPZK1nP7OOrNR3TkK
JtwLMQAabURCDnL0PjS0iwBTU4jtumBD1XSULyWuoTvMljrpQr1nV1oDyOt0OLqa
KA2McWtd9PdXhC8y2EIg7TmrTlJLfHYbdmkiCYj4J49Q8HWkN/6WE+RTUwARAQAB
tD5BbnRocm9waWMgQ2xhdWRlIENvZGUgUmVsZWFzZSBTaWduaW5nIDxzZWN1cml0
eUBhbnRocm9waWMuY29tPokCUQQTAQoAOxYhBDHd3iTd+rZ59C170rqpKf8afsrO
BQJpyu9wAhsPBQsJCAcCAiICBhUKCQgLAgQWAgMBAh4HAheAAAoJELqpKf8afsrO
l5IP/2I8X1dFy5xYczWB/coIxGjuzS/V6ByZGZZEJsbr04pmuHiFUykJqPGWGQ6q
U0YF5iEwvEkaagS5m7DzhSEf3FM3Cgafax/6d70tar9Vr1D+w6uPfxetu7u/WYJp
aolIsdh5fTrBh9zSM1Njl8FM8wG8CwZQjS33Oa7d8cwRkgdUWbt6LXgz+cTQNuBn
BgW6Ks7oZFI25dfu0ojDR+aDFJg4+4wZoyDLPvJz1SIrJ5WFGs67zsx9SfS3yZnf
XKmBe+f0dUy+GJ2nFZrXFf99+c0dPEHYO8DCeAHZizjkFrdYtUHdDU0YDYEGkLJa
bE+pgcpkHf5EvsZzHsyDbl95W/eh7pcXMbwkN+W4CBYUE9X4uHhqzWaC5yAVRWUA
1BJ9V4LjZfHPLEJt0I3TxzXiEg9/BVeaTYq9RjaxIFo9Nfk158HqJY6SA5jslBlx
Gv/No8u+xVcze2UJyGVfEIUfm92+0UAIkny3+5cuVV0ICzJxXlXj0CnLM9Lt50wE
p3suVwuBEviCbZ08eAH1Ht8gbBdSsiOkIU8CX3v/scwHHx5q0+NBL6xLrQObg13a
tRXBlKObfElkPN3lTUbUnJOW4U8uSjH8VRP+AujKWMDFe7x0zCs+iYY1mTOvbrTS
9n3CmZUmbynZ+E/QWNENpW/pDNZdWFy43PASmML5FHu4m9Sn
=oqMI
-----END PGP PUBLIC KEY BLOCK-----
`;

/** One install: which channel, which version, into where, fetched how, told to whom. */
export interface ChannelInstall {
  channel: Channel;
  version: string;
  /** The managed directory the pin points at. Staging happens beside it, at `<where>.part`. */
  where: string;
  fetcher: Fetcher;
  write: (line: string) => void;
  /** The machine to install for — this one, unless a test says otherwise. */
  platform?: NodeJS.Platform | string;
  arch?: string;
  musl?: boolean;
  /** Who signs Claude Code's manifests — the vendor's pinned key, unless a test holds its own. */
  trust?: { key: string; fingerprint: string };
}

/** The Claude Code build for a machine, in the release bucket's own names. */
export function claudePlatform(platform: string = process.platform, arch: string = process.arch, musl = isMusl()): string {
  const os = { win32: 'win32', darwin: 'darwin', linux: 'linux' }[platform];
  const cpu = { x64: 'x64', arm64: 'arm64' }[arch];
  if (!os || !cpu) {
    throw new DaorisError(
      `Claude Code publishes no build for ${platform}-${arch}, so there is nothing to pin here. Install `
      + 'it with its own tooling if it runs on this machine; unpinned, Daoris runs whatever is on PATH.');
  }

  return `${os}-${cpu}${os === 'linux' && musl ? '-musl' : ''}`;
}

/** The Codex package target for a machine, in the release's own names. Linux builds are musl only. */
export function codexTarget(platform: string = process.platform, arch: string = process.arch): string {
  const os = { win32: 'pc-windows-msvc', darwin: 'apple-darwin', linux: 'unknown-linux-musl' }[platform];
  const cpu = { x64: 'x86_64', arm64: 'aarch64' }[arch];
  if (!os || !cpu) {
    throw new DaorisError(
      `Codex publishes no package for ${platform}-${arch}, so there is nothing to pin here. Install it `
      + 'with its own tooling if it runs on this machine; unpinned, Daoris runs whatever is on PATH.');
  }

  return `${cpu}-${os}`;
}

/** Whether this Linux runs musl rather than glibc — Node's own report says which libc it was given. */
export function isMusl(): boolean {
  if (process.platform !== 'linux') return false;
  const report = process.report?.getReport() as { header?: { glibcVersionRuntime?: string } } | undefined;
  return !report?.header?.glibcVersionRuntime;
}

/**
 * Refuse a version the channel cannot install verifiably, before anything is fetched.
 *
 * @remarks
 * 🔴 **A Claude Code version before 2.1.89 is refused, not installed unverified.** Its manifest has
 * no signature, so the one file the signature exists to vouch for would be taken on trust — and a
 * fall back to npm would be the same trust by another road. The way to run one is the way it always
 * was: install it with its own tooling, and Daoris runs it from PATH.
 */
export function refuseVersion(channel: Channel, version: string): void {
  if (channel === 'claude-code-releases') {
    if (!/^\d+\.\d+\.\d+$/.test(version)) {
      throw new DaorisError(
        `\`${version}\` is not a Claude Code version — a pin names one exact release, like 2.1.281, `
        + 'never a pointer such as latest.');
    }
    if (compare(version, FIRST_SIGNED) < 0) {
      throw new DaorisError(
        `Claude Code ${version} was published before its release manifests were signed (the first is `
        + `${FIRST_SIGNED}), so Daoris cannot verify it and does not install it. Pin ${FIRST_SIGNED} or later — `
        + 'or install that version with its own tooling, and unpinned, Daoris runs it from PATH.');
    }
    return;
  }

  if (!/^\d+\.\d+\.\d+(?:-(?:alpha|beta)\.\d+)?$/.test(version)) {
    throw new DaorisError(
      `\`${version}\` is not a Codex version — a pin names one exact release, like 0.156.1, not its `
      + 'tag and not a pointer.');
  }
}

/**
 * The exact version a channel names as its newest release (USE1a) — what `agent update` pins.
 *
 * @remarks
 * 🔴 **The pointer only chooses a version; it vouches for nothing.** It is not signed, so it is read as
 * one exact version or refused, and the version it names is then installed through
 * `installFromChannel`, verified exactly as a version a person typed would be. A pin is never the
 * pointer itself: a pin that meant "whatever is newest today" would change under a running
 * arrangement, which is what pinning exists to prevent.
 */
export async function latestVersion(channel: Channel, fetcher: Fetcher): Promise<string> {
  const url = channel === 'claude-code-releases' ? CLAUDE_LATEST : CODEX_LATEST;
  const body = await fetcher.bytes(url);
  if (!body) {
    throw new DaorisError(
      `nothing answered at ${url}, where the channel names its newest release — nothing was fetched or pinned.`);
  }

  const said = channel === 'claude-code-releases' ? body.toString('utf8').trim() : codexTag(body);
  try {
    refuseVersion(channel, said);
  } catch {
    const shown = said.length > 60 ? `${said.slice(0, 60)}…` : said;
    throw new DaorisError(
      `the channel's newest-release pointer at ${url} answered \`${shown}\`, which is not a version — `
      + 'nothing was fetched or pinned.');
  }

  return said;
}

/** The version a Codex release's metadata is tagged with (`rust-v<version>`), or what it said instead. */
function codexTag(body: Buffer): string {
  let parsed: unknown;
  try {
    parsed = JSON.parse(body.toString('utf8'));
  } catch {
    return body.toString('utf8').trim();
  }

  const tag = parsed && typeof parsed === 'object' ? (parsed as { tag_name?: unknown }).tag_name : undefined;
  if (typeof tag !== 'string') return '';
  return tag.startsWith('rust-v') ? tag.slice('rust-v'.length) : tag;
}

/**
 * Install one version from its vendor's channel into `where`, verified end to end.
 *
 * @returns the executable, inside `where`.
 * @remarks
 * Staged at `<where>.part` and moved into place only once everything verified, so the managed
 * directory holds a verified install or nothing — `managedBinary` finding one is the proof. Every
 * refusal removes the staging, and nothing is ever left at `where` by a refused install.
 */
export async function installFromChannel(install: ChannelInstall): Promise<string> {
  refuseVersion(install.channel, install.version);

  const staging = `${install.where}.part`;
  rmSync(staging, { recursive: true, force: true });
  mkdirSync(join(staging, 'package'), { recursive: true });

  try {
    const executable = install.channel === 'claude-code-releases'
      ? await claude(install, staging)
      : await codex(install, staging);

    if (existsSync(install.where)) {
      // Daoris's own directory, holding no runnable install (the caller asked `managedBinary` first):
      // a download that stopped before this change, or an npm install that did not finish.
      install.write(`  replacing ${install.where}, which held nothing that runs`);
      rmSync(install.where, { recursive: true, force: true });
    }
    mkdirSync(join(install.where, '..'), { recursive: true });
    renameSync(join(staging, 'package'), install.where);
    return join(install.where, executable);
  } finally {
    rmSync(staging, { recursive: true, force: true });
  }
}

/** The release bucket: the signed manifest, then the binary it vouches for. @returns the executable, relative. */
async function claude(install: ChannelInstall, staging: string): Promise<string> {
  const { version, fetcher, write } = install;
  const platform = claudePlatform(install.platform, install.arch, install.musl ?? isMusl());
  const binary = platform.startsWith('win32') ? 'claude.exe' : 'claude';
  const trust = install.trust ?? { key: CLAUDE_RELEASE_KEY, fingerprint: CLAUDE_RELEASE_FINGERPRINT };

  const manifestUrl = `${CLAUDE_RELEASES}/${version}/manifest.json`;
  write(`  fetching Claude Code ${version}'s release manifest for ${platform}, and its signature`);
  const manifest = await fetcher.bytes(manifestUrl);
  if (!manifest) {
    throw new DaorisError(
      `the release bucket holds no Claude Code ${version} — nothing answered at ${manifestUrl}. Versions `
      + 'skip numbers; the release notes name the ones that exist.');
  }
  const signature = await fetcher.bytes(`${manifestUrl}.sig`);
  if (!signature) {
    throw new DaorisError(
      `Claude Code ${version}'s manifest has no signature beside it, so Daoris cannot verify it and did `
      + 'not install it. Nothing was downloaded beyond the manifest.');
  }

  const verdict = verifyDetached(manifest, signature.toString('utf8'), trust.key, trust.fingerprint);
  if (!verdict.ok) {
    throw new DaorisError(
      `Claude Code ${version}'s manifest did not verify: ${verdict.reason}. Nothing was installed — `
      + 'a manifest that does not verify vouches for no binary.');
  }
  write(`  the signature verifies: signed by the release key ${spaced(verdict.fingerprint)}`);

  let parsed: { version?: unknown; platforms?: Record<string, { binary?: unknown; checksum?: unknown; size?: unknown }> };
  try {
    parsed = JSON.parse(manifest.toString('utf8'));
  } catch {
    throw new DaorisError(`Claude Code ${version}'s manifest verified and is not JSON — nothing was installed.`);
  }

  // 🔴 A genuine manifest served for another version verifies perfectly. Only the version it SIGNED
  // stops an old release being installed under a new release's name.
  if (parsed.version !== version) {
    throw new DaorisError(
      `the signed manifest at ${manifestUrl} is for Claude Code ${String(parsed.version)}, not ${version} — `
      + 'a genuine manifest served for the wrong version. Nothing was installed.');
  }

  const entry = parsed.platforms?.[platform];
  if (!entry) {
    throw new DaorisError(`Claude Code ${version}'s signed manifest lists no build for ${platform}, so there is nothing to install.`);
  }
  if (entry.binary !== binary) {
    throw new DaorisError(
      `Claude Code ${version}'s signed manifest names \`${String(entry.binary)}\` for ${platform}, where the `
      + `tool's own binary is \`${binary}\` — refused rather than followed.`);
  }
  if (typeof entry.checksum !== 'string' || !/^[0-9a-f]{64}$/i.test(entry.checksum)
    || typeof entry.size !== 'number' || !Number.isSafeInteger(entry.size) || entry.size <= 0) {
    throw new DaorisError(`Claude Code ${version}'s signed manifest carries no usable SHA-256 and size for ${platform}.`);
  }

  const url = `${CLAUDE_RELEASES}/${version}/${platform}/${binary}`;
  const into = join(staging, 'package', 'bin');
  mkdirSync(into, { recursive: true });
  write(`  downloading ${url} (${megabytes(entry.size)})`);
  const saved = await fetcher.save(url, join(into, binary));
  if (!saved) throw new DaorisError(`nothing answered at ${url}, which Claude Code ${version}'s signed manifest names.`);

  if (saved.sha256 !== entry.checksum.toLowerCase() || saved.size !== entry.size) {
    throw new DaorisError(
      `the binary downloaded from ${url} is not the one the signed manifest names — its SHA-256 is `
      + `${saved.sha256} (${saved.size} bytes), and the manifest says ${entry.checksum.toLowerCase()} `
      + `(${entry.size} bytes). It was deleted, and nothing was installed.`);
  }
  write(`  the binary's SHA-256 matches the signed manifest: ${saved.sha256}`);

  if (!platform.startsWith('win32')) chmodSync(join(into, binary), 0o755);
  return join('bin', binary);
}

/** A Codex release: the metadata, the SUMS file it vouches for, the package both vouch for. */
async function codex(install: ChannelInstall, staging: string): Promise<string> {
  const { version, fetcher, write } = install;
  const target = codexTarget(install.platform, install.arch);
  const windows = target.endsWith('windows-msvc');
  const packageName = `codex-package-${target}.tar.gz`;
  const sumsName = 'codex-package_SHA256SUMS';

  write(`  fetching Codex ${version}'s release metadata`);
  const metadata = await releaseMetadata(fetcher, version);
  if (metadata.tag_name !== `rust-v${version}`) {
    throw new DaorisError(
      `the release metadata answered for Codex ${version} is for \`${String(metadata.tag_name)}\` — metadata `
      + 'for another release. Nothing was installed.');
  }

  const pkg = asset(metadata, packageName, version);
  const sumsAsset = asset(metadata, sumsName, version);

  const sums = await fetcher.bytes(sumsAsset.url);
  if (!sums) throw new DaorisError(`nothing answered at ${sumsAsset.url}, which Codex ${version}'s metadata names.`);
  const sumsHash = createHash('sha256').update(sums).digest('hex');
  if (sumsHash !== sumsAsset.sha256) {
    throw new DaorisError(
      `Codex ${version}'s ${sumsName} is not the file its release metadata publishes — its SHA-256 is `
      + `${sumsHash}, and the metadata says ${sumsAsset.sha256}. Nothing was installed.`);
  }

  const listed = sums.toString('utf8').split(/\r?\n/)
    .map((line) => /^([0-9a-f]{64})\s+\*?(.+)$/i.exec(line.trim()))
    .find((match) => match?.[2] === packageName)?.[1]?.toLowerCase();
  if (!listed) {
    throw new DaorisError(`Codex ${version}'s ${sumsName} lists no ${packageName}, so there is no second hash to hold it to.`);
  }
  if (listed !== pkg.sha256) {
    throw new DaorisError(
      `Codex ${version}'s two published hashes for ${packageName} disagree — the metadata says ${pkg.sha256}, `
      + `the ${sumsName} file says ${listed}. Nothing was downloaded.`);
  }
  write(`  the release publishes ${packageName} as ${pkg.sha256}, in its metadata and its SUMS file both`);

  write(`  downloading ${pkg.url}`);
  const archive = join(staging, packageName);
  const saved = await fetcher.save(pkg.url, archive);
  if (!saved) throw new DaorisError(`nothing answered at ${pkg.url}, which Codex ${version}'s metadata names.`);
  if (saved.sha256 !== pkg.sha256) {
    throw new DaorisError(
      `the package downloaded from ${pkg.url} is not the one Codex ${version} publishes — its SHA-256 is `
      + `${saved.sha256}, and the release says ${pkg.sha256}. It was deleted, and nothing was installed.`);
  }
  write(`  the package's SHA-256 matches: ${saved.sha256}`);

  await extractTarGz(archive, join(staging, 'package'));
  const executable = join('bin', windows ? 'codex.exe' : 'codex');
  if (!existsSync(join(staging, 'package', executable))) {
    throw new DaorisError(
      `Codex ${version}'s package holds no bin/${windows ? 'codex.exe' : 'codex'} — its layout is not the one `
      + 'Daoris was written against, so nothing was installed.');
  }
  if (!windows) chmodSync(join(staging, 'package', executable), 0o755);
  return executable;
}

/** The release's own metadata, or the GitHub release when that does not answer. */
async function releaseMetadata(fetcher: Fetcher, version: string): Promise<Record<string, unknown>> {
  const own = `${CODEX_RELEASES}/${version}/release.json`;
  const github = `${CODEX_GITHUB}/rust-v${version}`;

  let body: Buffer | null = null;
  let why = 'nothing answered there';
  try {
    body = await fetcher.bytes(own);
  } catch (error) {
    why = (error as Error).message;
  }
  // Only an ANSWER stops the fallback: metadata that answered and then fails a check is refused, never
  // traded for a second opinion.
  body ??= await fetcher.bytes(github);
  if (!body) {
    throw new DaorisError(
      `no release of Codex ${version} was found — not at ${own} (${why}), and not at ${github}. `
      + 'A pin names a version the release page lists.');
  }

  try {
    const parsed = JSON.parse(body.toString('utf8')) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed as Record<string, unknown>;
  } catch {
    // Refused below.
  }
  throw new DaorisError(`Codex ${version}'s release metadata is not JSON — nothing was installed.`);
}

/** One named asset: its HTTPS address and its published SHA-256. */
function asset(metadata: Record<string, unknown>, name: string, version: string): { url: string; sha256: string } {
  const assets = Array.isArray(metadata.assets) ? metadata.assets as Record<string, unknown>[] : [];
  const found = assets.find((candidate) => candidate?.name === name);
  if (!found) throw new DaorisError(`Codex ${version}'s release publishes no ${name}, so there is nothing to install.`);

  const digest = typeof found.digest === 'string' ? /^sha256:([0-9a-f]{64})$/i.exec(found.digest)?.[1] : undefined;
  if (!digest) {
    throw new DaorisError(`Codex ${version}'s release publishes ${name} with no SHA-256, and a download with no hash to hold it to is not installed.`);
  }

  const url = typeof found.browser_download_url === 'string' ? found.browser_download_url : '';
  if (!url.startsWith('https://')) {
    throw new DaorisError(`Codex ${version}'s release names ${name} at \`${url}\`, which is not HTTPS — refused before anything was fetched from it.`);
  }

  return { url, sha256: digest.toLowerCase() };
}

function compare(a: string, b: string): number {
  const left = a.split('.').map(Number);
  const right = b.split('.').map(Number);
  for (let at = 0; at < 3; at++) {
    if (left[at]! !== right[at]!) return left[at]! - right[at]!;
  }
  return 0;
}

function megabytes(bytes: number): string {
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
