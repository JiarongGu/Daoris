import { createHash, generateKeyPairSync, sign, type JsonWebKey } from 'node:crypto';
import { dearmor, readPublicKeys } from '../src/openpgp.ts';

/**
 * A key and a signature made HERE, in the vendor's format (AGT2b) — so a test can say "signed by
 * somebody else" without a second vendor file, and can run a whole install against a key it holds.
 * Built from the RFC, not from the verifier: a helper that borrowed the verifier's own parsing would
 * agree with it by construction.
 */

let pair: { publicKey: JsonWebKey; privateKey: string } | null = null;

/** One RSA key per test run, generated once — the stranger every test means. */
export function strangerKey(): { publicKey: JsonWebKey; privateKey: string; armoured: string; fingerprint: string } {
  pair ??= (() => {
    const made = generateKeyPairSync('rsa', { modulusLength: 2048 });
    return {
      publicKey: made.publicKey.export({ format: 'jwk' }),
      privateKey: made.privateKey.export({ format: 'pem', type: 'pkcs8' }).toString(),
    };
  })();

  const armoured = armouredPublicKey(pair.publicKey);
  const fingerprint = readPublicKeys(dearmor(armoured, 'PUBLIC KEY BLOCK'))[0]!.fingerprint;
  return { ...pair, armoured, fingerprint };
}

export function armouredPublicKey(jwk: JsonWebKey): string {
  const body = Buffer.concat([
    Buffer.from([4, 0x60, 0, 0, 0, 1]),
    mpi(Buffer.from(jwk.n!, 'base64url')),
    mpi(Buffer.from(jwk.e!, 'base64url')),
  ]);
  return armour('PUBLIC KEY BLOCK', packet(6, body));
}

/**
 * A detached v4 signature over `data`, RSA/SHA-512, naming `issuer` in its hashed area — which a
 * forger may set to anything, and that is the point of being able to.
 */
export function signDetached(data: Buffer, privateKey: string, issuer: string): string {
  const hashed = Buffer.concat([
    Buffer.from([5, 2, 0x68, 0, 0, 0]),
    Buffer.from([22, 33, 4]), Buffer.from(issuer, 'hex'),
  ]);
  const hashedPart = Buffer.concat([Buffer.from([4, 0x00, 1, 10, hashed.length >> 8, hashed.length & 0xff]), hashed]);
  const trailer = Buffer.from([4, 0xff, 0, 0, 0, 0]);
  trailer.writeUInt32BE(hashedPart.length, 2);
  const signed = Buffer.concat([data, hashedPart, trailer]);

  const left16 = createHash('sha512').update(signed).digest().subarray(0, 2);
  const value = sign('sha512', signed, privateKey);
  const body = Buffer.concat([hashedPart, Buffer.from([0, 0]), left16, mpi(value)]);
  return armour('SIGNATURE', packet(2, body));
}

function mpi(value: Buffer): Buffer {
  let start = 0;
  while (start < value.length - 1 && value[start] === 0) start++;
  const trimmed = value.subarray(start);
  const bits = (trimmed.length - 1) * 8 + (32 - Math.clz32(trimmed[0]!));
  return Buffer.concat([Buffer.from([bits >> 8, bits & 0xff]), trimmed]);
}

function packet(tag: number, body: Buffer): Buffer {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(body.length);
  return Buffer.concat([Buffer.from([0xc0 | tag, 0xff]), length, body]);
}

/** Armour without the optional checksum line, which RFC 9580 no longer asks a writer to emit. */
function armour(kind: string, bytes: Buffer): string {
  const base64 = bytes.toString('base64').match(/.{1,64}/g)!.join('\n');
  return `-----BEGIN PGP ${kind}-----\n\n${base64}\n-----END PGP ${kind}-----\n`;
}
