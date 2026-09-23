import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dearmor, fingerprintOf, readPublicKeys, readSignature, verifyDetached } from '../src/openpgp.ts';
import { signDetached, strangerKey } from './_openpgp.ts';

/**
 * The signature that stands between a vendor's release manifest and the binary Daoris installs from it
 * (AGT2b). Every fixture here is the vendor's own file, fetched once and kept byte for byte
 * (`.gitattributes`): the manifest of a real release, its detached signature, and the published
 * release key. No test touches the network.
 *
 * The two refusals that matter are the ones a verifier that merely LOOKED right would pass: a changed
 * byte, and a signature made by some other key that says it is the release key.
 */

const VENDOR = fileURLToPath(new URL('./fixtures/vendor/claude-code/', import.meta.url));
const manifest = readFileSync(`${VENDOR}2.1.281/manifest.json`);
const signature = readFileSync(`${VENDOR}2.1.281/manifest.json.sig`, 'utf8');
const releaseKey = readFileSync(`${VENDOR}claude-code.asc`, 'utf8');

/** The vendor's published fingerprint, as the vendor writes it. */
const PINNED = '31DD DE24 DDFA B679 F42D  7BD2 BAA9 29FF 1A7E CACE';

test('the release key reads as the fingerprint the vendor publishes', () => {
  const keys = readPublicKeys(dearmor(releaseKey, 'PUBLIC KEY BLOCK'));
  const primary = keys.find((key) => key.primary);
  assert.equal(primary?.fingerprint, fingerprintOf(PINNED));
  assert.equal(primary?.fingerprint, '31DDDE24DDFAB679F42D7BD2BAA929FF1A7ECACE');
});

test('the signature is the shape a release manifest is signed in: v4, binary document, RSA, SHA-512', () => {
  const read = readSignature(dearmor(signature, 'SIGNATURE'));
  assert.equal(read.type, 0x00);
  assert.equal(read.publicKeyAlgorithm, 1);
  assert.equal(read.hashAlgorithm, 10);
});

test('a real release manifest verifies against the pinned release key', () => {
  assert.deepEqual(verifyDetached(manifest, signature, releaseKey, PINNED), {
    ok: true,
    fingerprint: fingerprintOf(PINNED),
  });
});

test('one changed byte in the manifest is refused — the hash it would then trust is not the one signed', () => {
  const tampered = Buffer.from(manifest);
  // The first digit of a checksum: exactly the byte an attacker would want to change.
  const at = tampered.indexOf('"checksum"') + '"checksum": "'.length;
  tampered[at] = tampered[at] === 0x61 ? 0x62 : 0x61;

  const verdict = verifyDetached(tampered, signature, releaseKey, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /not what the release key signed/);
});

test('a key that is not the pinned one is refused, naming both — whatever it would have verified', () => {
  const verdict = verifyDetached(manifest, signature, strangerKey().armoured, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE/);
  assert.match(!verdict.ok ? verdict.reason : '', /the key given is/);
});

test('a signature by another key that CLAIMS to be the release key is refused by the RSA check itself', () => {
  // A forger controls every byte of a signature packet, including the issuer it names. The only
  // thing it cannot produce is RSA over the pinned key's modulus — so this is the test that fails if
  // verification ever stops at the parts a forger writes.
  const forged = signDetached(manifest, strangerKey().privateKey, fingerprintOf(PINNED));

  const verdict = verifyDetached(manifest, forged, releaseKey, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /not what the release key signed/);
});

test('a signature that names a different signer is refused before any arithmetic', () => {
  const stranger = strangerKey();
  const theirs = signDetached(manifest, stranger.privateKey, stranger.fingerprint);

  const verdict = verifyDetached(manifest, theirs, releaseKey, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /was made by/);
});

test('a stranger pinning their own key verifies their own signature — the pin is the whole trust root', () => {
  // The positive control for the forgery above: the same forged packet, verified against the key that
  // really made it, passes. Without it, the refusal above could be a parser that refuses everything.
  const stranger = strangerKey();
  const theirs = signDetached(manifest, stranger.privateKey, stranger.fingerprint);

  assert.deepEqual(
    verifyDetached(manifest, theirs, stranger.armoured, stranger.fingerprint),
    { ok: true, fingerprint: stranger.fingerprint });
});

test('an armour that fails its own checksum is refused as damaged, not mis-verified', () => {
  const lines = signature.split('\n');
  const at = lines.findIndex((line) => line.startsWith('='));
  lines[at] = lines[at] === '=AAAA' ? '=AAAB' : '=AAAA';

  const verdict = verifyDetached(manifest, lines.join('\n'), releaseKey, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /checksum/);
});

test('something that is not a signature at all is refused by name', () => {
  const verdict = verifyDetached(manifest, '<html>Not Found</html>', releaseKey, PINNED);
  assert.equal(verdict.ok, false);
  assert.match(!verdict.ok ? verdict.reason : '', /not an armoured PGP signature/);
});
