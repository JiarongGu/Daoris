import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { gzipSync } from 'node:zlib';
import {
  CLAUDE_RELEASE_FINGERPRINT, CLAUDE_RELEASE_KEY, CLAUDE_RELEASES, CODEX_GITHUB, CODEX_RELEASES,
  claudePlatform, codexTarget, installFromChannel, refuseVersion,
} from '../src/channels.ts';
import type { Fetcher } from '../src/channels.ts';
import { dearmor, readPublicKeys } from '../src/openpgp.ts';
import { captureError, makeFixture } from './_fixture.ts';
import { signDetached, strangerKey } from './_openpgp.ts';
import { TAR_END, tarEntry } from './_tar.ts';

/**
 * A managed install from the vendor's own channel (AGT2b). Claude Code: the release bucket's manifest,
 * verified against its detached signature under the PINNED release key, and only then its SHA-256
 * trusted for the binary. Codex: the release's package, by the hash its metadata publishes and the
 * hash its SUMS file publishes, both.
 *
 * 🔴 **No test here touches the network.** Every byte arrives from a fetcher the test holds, and the
 * vendor's own files are served from `fixtures/vendor/`, fetched once and kept byte for byte. The
 * refusals are the point: each one is a way a download could be something other than what the vendor
 * published, and each leaves nothing behind — not the pin's directory, not its staging.
 */

const VENDOR = fileURLToPath(new URL('./fixtures/vendor/', import.meta.url));
const REAL_MANIFEST = readFileSync(join(VENDOR, 'claude-code', '2.1.281', 'manifest.json'));
const REAL_SIGNATURE = readFileSync(join(VENDOR, 'claude-code', '2.1.281', 'manifest.json.sig'));
const REAL_RELEASE = readFileSync(join(VENDOR, 'codex', '0.156.1', 'release.json'));
const REAL_SUMS = readFileSync(join(VENDOR, 'codex', '0.156.1', 'codex-package_SHA256SUMS'));

/** A fetcher over a table of URLs — and the URLs it was asked for, in order. */
function served(files: Record<string, string | Buffer>): { fetcher: Fetcher; asked: string[] } {
  const asked: string[] = [];
  const body = (url: string) => (files[url] === undefined ? null : Buffer.from(files[url]!));
  return {
    asked,
    fetcher: {
      async bytes(url) {
        asked.push(url);
        return body(url);
      },
      async save(url, to) {
        asked.push(url);
        const bytes = body(url);
        if (!bytes) return null;
        writeFileSync(to, bytes);
        return { sha256: sha256(bytes), size: bytes.length };
      },
    },
  };
}

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

function lines(): { write: (line: string) => void; said: string[] } {
  const said: string[] = [];
  return { said, write: (line) => said.push(line) };
}

// ——— What the pin is: the key, the fingerprint, the versions and the platforms.

test('the release key Daoris carries IS the vendor’s published key, and reads as the pinned fingerprint', () => {
  const published = readFileSync(join(VENDOR, 'claude-code', 'claude-code.asc'), 'utf8');
  assert.deepEqual(dearmor(CLAUDE_RELEASE_KEY, 'PUBLIC KEY BLOCK'), dearmor(published, 'PUBLIC KEY BLOCK'));
  assert.equal(CLAUDE_RELEASE_FINGERPRINT, '31DDDE24DDFAB679F42D7BD2BAA929FF1A7ECACE');
  assert.equal(
    readPublicKeys(dearmor(CLAUDE_RELEASE_KEY, 'PUBLIC KEY BLOCK')).find((key) => key.primary)?.fingerprint,
    CLAUDE_RELEASE_FINGERPRINT);
});

test('each machine maps to the build the vendor publishes for it, and an unpublished one is refused', () => {
  assert.equal(claudePlatform('win32', 'x64', false), 'win32-x64');
  assert.equal(claudePlatform('win32', 'arm64', false), 'win32-arm64');
  assert.equal(claudePlatform('darwin', 'arm64', false), 'darwin-arm64');
  assert.equal(claudePlatform('linux', 'x64', false), 'linux-x64');
  assert.equal(claudePlatform('linux', 'arm64', true), 'linux-arm64-musl');
  assert.match(captureError(() => claudePlatform('freebsd', 'x64', false)).message, /freebsd-x64/);

  assert.equal(codexTarget('win32', 'x64'), 'x86_64-pc-windows-msvc');
  assert.equal(codexTarget('win32', 'arm64'), 'aarch64-pc-windows-msvc');
  assert.equal(codexTarget('darwin', 'x64'), 'x86_64-apple-darwin');
  // Linux is published as musl builds only, which run on either libc.
  assert.equal(codexTarget('linux', 'arm64'), 'aarch64-unknown-linux-musl');
  assert.match(captureError(() => codexTarget('linux', 'ia32')).message, /linux-ia32/);
});

/**
 * 🔴 **Before 2.1.89 there is no signature to check** — the vendor's `.sig` for 2.1.87 answers 404. A
 * managed install that took an unsigned manifest would be trusting the one file the signature exists
 * to vouch for, so an earlier version is refused by name, and the way to run one anyway is said.
 */
test('a Claude Code version from before signed manifests is refused, naming the first signed one', () => {
  const error = captureError(() => refuseVersion('claude-code-releases', '2.1.87'));
  assert.match(error.message, /2\.1\.89/);
  assert.match(error.message, /PATH/);
  refuseVersion('claude-code-releases', '2.1.89');
  refuseVersion('claude-code-releases', '2.1.281');
  refuseVersion('claude-code-releases', '3.0.0');
  assert.match(captureError(() => refuseVersion('claude-code-releases', '1.9.999')).message, /2\.1\.89/);
});

test('a version is a version, not a pointer or a tag', () => {
  for (const version of ['latest', 'stable', '2.1', '2.1.281-beta', 'v2.1.281']) {
    assert.match(captureError(() => refuseVersion('claude-code-releases', version)).message, /version/);
  }
  refuseVersion('codex-releases', '0.156.1');
  refuseVersion('codex-releases', '0.157.0-alpha.3');
  for (const version of ['latest', 'rust-v0.156.1', 'v0.156.1', '0.156']) {
    assert.match(captureError(() => refuseVersion('codex-releases', version)).message, /version/);
  }
});

// ——— Claude Code: the signed manifest, then the binary it vouches for.

/** A release signed by a key the test holds — the only way to reach the end of a successful install. */
function signedRelease(version: string, platform: string, binary: Buffer, name = platform.startsWith('win32') ? 'claude.exe' : 'claude') {
  const stranger = strangerKey();
  const manifest = Buffer.from(`${JSON.stringify({
    version,
    platforms: { [platform]: { binary: name, checksum: sha256(binary), size: binary.length } },
  }, null, 2)}\n`);
  return {
    trust: { key: stranger.armoured, fingerprint: stranger.fingerprint },
    files: {
      [`${CLAUDE_RELEASES}/${version}/manifest.json`]: manifest,
      [`${CLAUDE_RELEASES}/${version}/manifest.json.sig`]: signDetached(manifest, stranger.privateKey, stranger.fingerprint),
      [`${CLAUDE_RELEASES}/${version}/${platform}/${name}`]: binary,
    },
  };
}

test('a verified release lands as bin/<binary>, byte for byte, and its staging is gone', async () => {
  const fx = makeFixture('channel-claude-ok');
  const binary = Buffer.from('MZ-not-really-a-binary');
  const release = signedRelease('2.1.300', 'win32-x64', binary);
  const { fetcher, asked } = served(release.files);
  const where = join(fx.root, 'toolchain', 'claude-code', '2.1.300');
  const out = lines();

  const executable = await installFromChannel({
    channel: 'claude-code-releases', version: '2.1.300', where, fetcher, write: out.write,
    platform: 'win32', arch: 'x64', trust: release.trust,
  });

  assert.equal(executable, join(where, 'bin', 'claude.exe'));
  assert.deepEqual(readFileSync(executable), binary);
  assert.equal(existsSync(`${where}.part`), false);
  // The manifest and its signature, THEN the binary — never the binary first.
  assert.deepEqual(asked, [
    `${CLAUDE_RELEASES}/2.1.300/manifest.json`,
    `${CLAUDE_RELEASES}/2.1.300/manifest.json.sig`,
    `${CLAUDE_RELEASES}/2.1.300/win32-x64/claude.exe`,
  ]);
  assert.match(out.said.join('\n'), /signature/);
  assert.match(out.said.join('\n'), /SHA-256/);
  fx.cleanup();
});

test('the vendor’s real manifest verifies — and a binary that is not the one it names is refused', async () => {
  const fx = makeFixture('channel-claude-real');
  const { fetcher, asked } = served({
    [`${CLAUDE_RELEASES}/2.1.281/manifest.json`]: REAL_MANIFEST,
    [`${CLAUDE_RELEASES}/2.1.281/manifest.json.sig`]: REAL_SIGNATURE,
    [`${CLAUDE_RELEASES}/2.1.281/win32-x64/claude.exe`]: 'something else entirely',
  });
  const where = join(fx.root, 'toolchain', 'claude-code', '2.1.281');
  const out = lines();

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.281', where, fetcher, write: out.write,
      platform: 'win32', arch: 'x64',
    }),
    /SHA-256/);

  // It got as far as the binary, so the real signature verified under the real pinned key.
  assert.equal(asked.length, 3);
  assert.match(out.said.join('\n'), /31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE/);
  assert.equal(existsSync(where), false);
  assert.equal(existsSync(`${where}.part`), false);
  fx.cleanup();
});

test('a manifest with one changed byte is refused before any binary is fetched', async () => {
  const fx = makeFixture('channel-claude-tampered');
  const tampered = Buffer.from(REAL_MANIFEST);
  const at = tampered.indexOf('"checksum"') + '"checksum": "'.length;
  tampered[at] = tampered[at] === 0x61 ? 0x62 : 0x61;
  const { fetcher, asked } = served({
    [`${CLAUDE_RELEASES}/2.1.281/manifest.json`]: tampered,
    [`${CLAUDE_RELEASES}/2.1.281/manifest.json.sig`]: REAL_SIGNATURE,
  });
  const where = join(fx.root, 'toolchain', 'claude-code', '2.1.281');

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.281', where, fetcher, write: () => {},
      platform: 'win32', arch: 'x64',
    }),
    /did not verify/);
  assert.equal(asked.length, 2);
  assert.equal(existsSync(where), false);
  fx.cleanup();
});

/**
 * A genuine signed manifest, served for a version it is not. The signature is perfect — it is the
 * vendor's — so only reading the version it signed stops an old release being installed as a new one.
 */
test('a real signed manifest served as another version is refused as the version it really is', async () => {
  const fx = makeFixture('channel-claude-replay');
  const { fetcher } = served({
    [`${CLAUDE_RELEASES}/2.1.290/manifest.json`]: REAL_MANIFEST,
    [`${CLAUDE_RELEASES}/2.1.290/manifest.json.sig`]: REAL_SIGNATURE,
  });

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.290', where: join(fx.root, 'v'), fetcher,
      write: () => {}, platform: 'win32', arch: 'x64',
    }),
    /2\.1\.281, not 2\.1\.290/);
  fx.cleanup();
});

test('a signature by a key that is not the pinned one is refused, even over a well-formed release', async () => {
  const fx = makeFixture('channel-claude-stranger');
  // Signed by the stranger, verified against the VENDOR's pin: exactly what a hijacked host would serve.
  const release = signedRelease('2.1.300', 'linux-x64', Buffer.from('elf'));
  const { fetcher, asked } = served(release.files);

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.300', where: join(fx.root, 'v'), fetcher,
      write: () => {}, platform: 'linux', arch: 'x64', musl: false,
    }),
    /did not verify/);
  assert.equal(asked.length, 2);
  fx.cleanup();
});

test('a manifest that names a binary other than the tool’s own is refused, not followed', async () => {
  const fx = makeFixture('channel-claude-name');
  const release = signedRelease('2.1.300', 'linux-x64', Buffer.from('elf'), '../../claude');

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.300', where: join(fx.root, 'v'), fetcher: served(release.files).fetcher,
      write: () => {}, platform: 'linux', arch: 'x64', musl: false, trust: release.trust,
    }),
    /names `\.\.\/\.\.\/claude`/);
  fx.cleanup();
});

test('a version the bucket does not hold is refused, saying that versions skip', async () => {
  const fx = makeFixture('channel-claude-missing');

  await assert.rejects(
    installFromChannel({
      channel: 'claude-code-releases', version: '2.1.290', where: join(fx.root, 'v'), fetcher: served({}).fetcher,
      write: () => {}, platform: 'win32', arch: 'x64',
    }),
    /no Claude Code 2\.1\.290[\s\S]*skip/);
  fx.cleanup();
});

// ——— Codex: the package, by both of its published hashes, unpacked whole.

/** A release in the vendor's metadata shape, over a package built here. */
function codexRelease(version: string, target: string, windows: boolean) {
  const exe = windows ? '.exe' : '';
  const pkg = gzipSync(Buffer.concat([
    tarEntry('codex-package.json', '{"layout":1}\n'),
    tarEntry(`bin/codex${exe}`, 'codex', '0', 0o755),
    tarEntry(`codex-path/rg${exe}`, 'rg', '0', 0o755),
    TAR_END,
  ]));
  const name = `codex-package-${target}.tar.gz`;
  const sums = Buffer.from(`${'0'.repeat(64)}  codex-app-server-package-${target}.tar.gz\n${sha256(pkg)}  ${name}\n`);
  const base = `${CODEX_RELEASES}/${version}`;
  const release = {
    tag_name: `rust-v${version}`,
    assets: [
      { name, digest: `sha256:${sha256(pkg)}`, browser_download_url: `${base}/${name}` },
      { name: 'codex-package_SHA256SUMS', digest: `sha256:${sha256(sums)}`, browser_download_url: `${base}/codex-package_SHA256SUMS` },
    ],
  };
  return {
    pkg, sums, release,
    files: {
      [`${base}/release.json`]: JSON.stringify(release),
      [`${base}/${name}`]: pkg,
      [`${base}/codex-package_SHA256SUMS`]: sums,
    } as Record<string, string | Buffer>,
  };
}

test('a verified Codex package is unpacked whole, and bin/codex is what runs', async () => {
  const fx = makeFixture('channel-codex-ok');
  const release = codexRelease('0.156.1', 'x86_64-pc-windows-msvc', true);
  const { fetcher, asked } = served(release.files);
  const where = join(fx.root, 'toolchain', 'codex', '0.156.1');
  const out = lines();

  const executable = await installFromChannel({
    channel: 'codex-releases', version: '0.156.1', where, fetcher, write: out.write, platform: 'win32', arch: 'x64',
  });

  assert.equal(executable, join(where, 'bin', 'codex.exe'));
  // Whole: the helpers it finds through its own layout came with it.
  assert.ok(existsSync(join(where, 'codex-path', 'rg.exe')));
  assert.ok(existsSync(join(where, 'codex-package.json')));
  assert.equal(existsSync(`${where}.part`), false);
  // The SUMS file before the package: the package's hash is checked against both.
  assert.deepEqual(asked, [
    `${CODEX_RELEASES}/0.156.1/release.json`,
    `${CODEX_RELEASES}/0.156.1/codex-package_SHA256SUMS`,
    `${CODEX_RELEASES}/0.156.1/codex-package-x86_64-pc-windows-msvc.tar.gz`,
  ]);
  fx.cleanup();
});

test('the vendor’s real metadata and SUMS agree — and a package that is neither’s hash is refused', async () => {
  const fx = makeFixture('channel-codex-real');
  const base = `${CODEX_RELEASES}/0.156.1`;
  const { fetcher, asked } = served({
    [`${base}/release.json`]: REAL_RELEASE,
    [`${base}/codex-package_SHA256SUMS`]: REAL_SUMS,
    [`${base}/codex-package-x86_64-pc-windows-msvc.tar.gz`]: 'not the package',
  });
  const where = join(fx.root, 'toolchain', 'codex', '0.156.1');

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.156.1', where, fetcher, write: () => {}, platform: 'win32', arch: 'x64',
    }),
    /a2e017db9807e6a2269a26fea0e1d9546469cef4d472a33016bc9f3ad7d3b733/);
  // It reached the package, so the real SUMS file matched the real metadata's digest for it.
  assert.equal(asked.length, 3);
  assert.equal(existsSync(where), false);
  assert.equal(existsSync(`${where}.part`), false);
  fx.cleanup();
});

test('a SUMS file with one changed byte is refused before the package is fetched', async () => {
  const fx = makeFixture('channel-codex-sums');
  const tampered = Buffer.from(REAL_SUMS);
  tampered[0] = tampered[0] === 0x61 ? 0x62 : 0x61;
  const base = `${CODEX_RELEASES}/0.156.1`;
  const { fetcher, asked } = served({
    [`${base}/release.json`]: REAL_RELEASE,
    [`${base}/codex-package_SHA256SUMS`]: tampered,
  });

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.156.1', where: join(fx.root, 'v'), fetcher, write: () => {},
      platform: 'win32', arch: 'x64',
    }),
    /SHA256SUMS/);
  assert.equal(asked.length, 2);
  fx.cleanup();
});

test('metadata and SUMS that disagree about the package are refused — two published hashes must be one', async () => {
  const fx = makeFixture('channel-codex-disagree');
  const release = codexRelease('0.156.1', 'x86_64-unknown-linux-musl', false);
  const sums = Buffer.from(`${'f'.repeat(64)}  codex-package-x86_64-unknown-linux-musl.tar.gz\n`);
  release.release.assets[1]!.digest = `sha256:${sha256(sums)}`;
  const base = `${CODEX_RELEASES}/0.156.1`;
  const { fetcher, asked } = served({
    ...release.files,
    [`${base}/release.json`]: JSON.stringify(release.release),
    [`${base}/codex-package_SHA256SUMS`]: sums,
  });

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.156.1', where: join(fx.root, 'v'), fetcher, write: () => {},
      platform: 'linux', arch: 'x64',
    }),
    /disagree/);
  assert.equal(asked.length, 2);
  fx.cleanup();
});

test('with no release.json, the GitHub release is asked instead — and verified the same way', async () => {
  const fx = makeFixture('channel-codex-github');
  const release = codexRelease('0.156.1', 'aarch64-apple-darwin', false);
  const base = `${CODEX_RELEASES}/0.156.1`;
  const { [`${base}/release.json`]: metadata, ...rest } = release.files;
  const { fetcher, asked } = served({ ...rest, [`${CODEX_GITHUB}/rust-v0.156.1`]: metadata! });
  const where = join(fx.root, 'toolchain', 'codex', '0.156.1');

  const executable = await installFromChannel({
    channel: 'codex-releases', version: '0.156.1', where, fetcher, write: () => {}, platform: 'darwin', arch: 'arm64',
  });

  assert.equal(executable, join(where, 'bin', 'codex'));
  assert.deepEqual(asked.slice(0, 2), [`${base}/release.json`, `${CODEX_GITHUB}/rust-v0.156.1`]);
  fx.cleanup();
});

test('metadata for another version is refused as that version', async () => {
  const fx = makeFixture('channel-codex-tag');
  const release = codexRelease('0.156.1', 'x86_64-pc-windows-msvc', true);
  const { fetcher } = served({
    [`${CODEX_RELEASES}/0.157.0/release.json`]: JSON.stringify(release.release),
  });

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.157.0', where: join(fx.root, 'v'), fetcher, write: () => {},
      platform: 'win32', arch: 'x64',
    }),
    /rust-v0\.156\.1/);
  fx.cleanup();
});

test('a download address that is not HTTPS is refused before anything is fetched from it', async () => {
  const fx = makeFixture('channel-codex-http');
  const release = codexRelease('0.156.1', 'x86_64-pc-windows-msvc', true);
  release.release.assets[0]!.browser_download_url = 'http://releases.example.invalid/pkg.tar.gz';
  const { fetcher, asked } = served({
    ...release.files,
    [`${CODEX_RELEASES}/0.156.1/release.json`]: JSON.stringify(release.release),
  });

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.156.1', where: join(fx.root, 'v'), fetcher, write: () => {},
      platform: 'win32', arch: 'x64',
    }),
    /HTTPS/);
  assert.ok(!asked.includes('http://releases.example.invalid/pkg.tar.gz'));
  fx.cleanup();
});

test('a package with no bin/codex in it is refused, and nothing is left where the pin points', async () => {
  const fx = makeFixture('channel-codex-empty');
  const release = codexRelease('0.156.1', 'x86_64-pc-windows-msvc', true);
  const pkg = gzipSync(Buffer.concat([tarEntry('codex-package.json', '{}'), TAR_END]));
  const name = 'codex-package-x86_64-pc-windows-msvc.tar.gz';
  const sums = Buffer.from(`${sha256(pkg)}  ${name}\n`);
  release.release.assets[0]!.digest = `sha256:${sha256(pkg)}`;
  release.release.assets[1]!.digest = `sha256:${sha256(sums)}`;
  const base = `${CODEX_RELEASES}/0.156.1`;
  const where = join(fx.root, 'toolchain', 'codex', '0.156.1');

  await assert.rejects(
    installFromChannel({
      channel: 'codex-releases', version: '0.156.1', where, write: () => {}, platform: 'win32', arch: 'x64',
      fetcher: served({
        [`${base}/release.json`]: JSON.stringify(release.release),
        [`${base}/${name}`]: pkg,
        [`${base}/codex-package_SHA256SUMS`]: sums,
      }).fetcher,
    }),
    /bin\/codex\.exe/);
  assert.equal(existsSync(where), false);
  assert.equal(existsSync(`${where}.part`), false);
  fx.cleanup();
});
