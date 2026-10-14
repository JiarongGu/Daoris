// A detached OpenPGP signature, verified against one PINNED key (AGT2b) — what stands between a
// vendor's release manifest and the binary Daoris installs from it.
//
// Deliberately small, and deliberately narrow. It reads exactly what a vendor's release signing
// produces — an armoured, detached, binary-document signature, version 4, RSA — and refuses anything
// else by name rather than half-supporting it. It is not a keyring and not a web of trust: the trust
// root is a fingerprint written into this repository, reviewed like any other line, and a key whose
// primary fingerprint is not that one verifies nothing. Revocation and expiry are not read, because
// the pin is the decision; a vendor that rotates its key changes the pin through a reviewed change.
//
// No dependency: parsing is a few packet headers, and the signature itself is the platform's own RSA
// (`node:crypto`), which the zero-runtime-dependency guarantee already counts on. No network either —
// the bytes arrive from the caller.

import { createHash, createPublicKey, verify as rsaVerify, type KeyObject } from 'node:crypto';

/** A public key or subkey, as a key packet describes it. */
export interface PublicKeyInfo {
  /** The v4 fingerprint, forty upper-case hex characters. */
  fingerprint: string;
  primary: boolean;
  key: KeyObject;
}

/** The fields of a v4 signature packet that verification reads. */
export interface SignatureInfo {
  type: number;
  publicKeyAlgorithm: number;
  hashAlgorithm: number;
  /** The issuer's fingerprint from the hashed area, when it says (subpacket 33). */
  issuerFingerprint: string | null;
  /** The issuer's key id, from either area (subpacket 16), when it says. */
  issuerKeyId: string | null;
  /** Version through the hashed subpackets — what the trailer hashes. */
  hashedPart: Buffer;
  left16: Buffer;
  signature: Buffer;
}

/** Why a signature did not verify — a sentence, because it reaches a person as the refusal. */
export type Verdict = { ok: true; fingerprint: string } | { ok: false; reason: string };

const HASHES: Record<number, string> = { 8: 'sha256', 9: 'sha384', 10: 'sha512' };

/** Normalise a fingerprint as people write it — spaced, any case — to forty hex characters. */
export function fingerprintOf(text: string): string {
  return text.replace(/\s+/g, '').toUpperCase();
}

/**
 * Verify a detached signature over `data`, made by the key whose PRIMARY fingerprint is `pinned`.
 *
 * @remarks
 * 🔴 The signer must be the pinned primary key itself. A subkey would need its binding signature
 * checked too, and a verifier that trusted any subkey sitting in the key block would trust whatever
 * key the block's author chose to add — so a signature by a subkey is refused here, by name.
 */
export function verifyDetached(data: Buffer, armouredSignature: string, armouredKey: string, pinned: string): Verdict {
  let keys: PublicKeyInfo[];
  let signature: SignatureInfo;
  try {
    keys = readPublicKeys(dearmor(armouredKey, 'PUBLIC KEY BLOCK'));
    signature = readSignature(dearmor(armouredSignature, 'SIGNATURE'));
  } catch (error) {
    return { ok: false, reason: (error as Error).message };
  }

  const want = fingerprintOf(pinned);
  const primary = keys.find((key) => key.primary);
  if (!primary || primary.fingerprint !== want) {
    return {
      ok: false,
      reason: `the key given is ${primary ? spaced(primary.fingerprint) : 'no key at all'}, and the pinned `
        + `release key is ${spaced(want)}`,
    };
  }

  const named = signature.issuerFingerprint
    ?? (signature.issuerKeyId ? keys.find((key) => key.fingerprint.endsWith(signature.issuerKeyId!))?.fingerprint ?? null : null);
  if (named !== primary.fingerprint) {
    return {
      ok: false,
      reason: `the signature was made by ${named ? spaced(named) : 'a key it does not name'}, not by the `
        + `pinned release key ${spaced(want)}`,
    };
  }

  if (signature.type !== 0x00) {
    return { ok: false, reason: `the signature is of type 0x${hex(signature.type)}, and a release manifest is signed as a binary document (0x00)` };
  }
  if (signature.publicKeyAlgorithm !== 1 && signature.publicKeyAlgorithm !== 3) {
    return { ok: false, reason: `the signature uses public-key algorithm ${signature.publicKeyAlgorithm}, and only RSA is read here` };
  }
  const hash = HASHES[signature.hashAlgorithm];
  if (!hash) {
    return { ok: false, reason: `the signature uses hash algorithm ${signature.hashAlgorithm}, and only SHA-256, SHA-384 and SHA-512 are read here` };
  }

  // What v4 hashes: the document, then the hashed part, then a trailer naming the hashed part's length.
  const trailer = Buffer.alloc(6);
  trailer[0] = 0x04;
  trailer[1] = 0xff;
  trailer.writeUInt32BE(signature.hashedPart.length, 2);
  const signed = Buffer.concat([data, signature.hashedPart, trailer]);

  const digest = createHash(hash).update(signed).digest();
  if (!digest.subarray(0, 2).equals(signature.left16)) {
    return { ok: false, reason: 'the signature does not match these bytes — they are not what the release key signed' };
  }

  const modulusBytes = (primary.key.asymmetricKeyDetails?.modulusLength ?? 0) / 8;
  const padded = signature.signature.length >= modulusBytes
    ? signature.signature
    : Buffer.concat([Buffer.alloc(modulusBytes - signature.signature.length), signature.signature]);
  const valid = rsaVerify(hash, signed, primary.key, padded);
  return valid
    ? { ok: true, fingerprint: primary.fingerprint }
    : { ok: false, reason: 'the signature does not match these bytes — they are not what the release key signed' };
}

/** The bytes inside an ASCII armour of the named kind, with its checksum checked. */
export function dearmor(text: string, kind: 'SIGNATURE' | 'PUBLIC KEY BLOCK'): Buffer {
  const lines = text.replace(/\r\n?/g, '\n').split('\n');
  const begin = lines.findIndex((line) => line.trim() === `-----BEGIN PGP ${kind}-----`);
  const end = lines.findIndex((line) => line.trim() === `-----END PGP ${kind}-----`);
  if (begin < 0 || end < begin) throw new Error(`not an armoured PGP ${kind.toLowerCase()}`);

  // Headers (`Comment: …`) run to the first blank line; the body follows.
  let at = begin + 1;
  while (at < end && lines[at]!.trim() !== '') at++;
  const body = lines.slice(at + 1, end).map((line) => line.trim()).filter((line) => line.length > 0);
  const checksumLine = body.length > 0 && body[body.length - 1]!.startsWith('=') ? body.pop()! : null;

  const bytes = Buffer.from(body.join(''), 'base64');
  if (checksumLine) {
    const expected = Buffer.from(checksumLine.slice(1), 'base64');
    const actual = crc24(bytes);
    if (expected.length !== 3 || expected.readUIntBE(0, 3) !== actual) {
      throw new Error(`the armoured ${kind.toLowerCase()} fails its own checksum — it was damaged on the way`);
    }
  }

  return bytes;
}

/** Every packet in a byte string, as its tag and body. */
export function packets(bytes: Buffer): { tag: number; body: Buffer }[] {
  const found: { tag: number; body: Buffer }[] = [];
  let at = 0;
  while (at < bytes.length) {
    const first = bytes[at]!;
    if ((first & 0x80) === 0) throw new Error('not an OpenPGP packet');

    let tag: number;
    let length: number;
    if (first & 0x40) {
      tag = first & 0x3f;
      const octet = bytes[at + 1]!;
      if (octet < 192) {
        length = octet;
        at += 2;
      } else if (octet < 224) {
        length = ((octet - 192) << 8) + bytes[at + 2]! + 192;
        at += 3;
      } else if (octet === 255) {
        length = bytes.readUInt32BE(at + 2);
        at += 6;
      } else {
        throw new Error('a partial-length packet, which a key or a detached signature never uses');
      }
    } else {
      tag = (first >> 2) & 0x0f;
      const type = first & 0x03;
      if (type === 0) {
        length = bytes[at + 1]!;
        at += 2;
      } else if (type === 1) {
        length = bytes.readUInt16BE(at + 1);
        at += 3;
      } else if (type === 2) {
        length = bytes.readUInt32BE(at + 1);
        at += 5;
      } else {
        throw new Error('an indeterminate-length packet, which a key or a detached signature never uses');
      }
    }

    if (at + length > bytes.length) throw new Error('a packet runs past the end of its data');
    found.push({ tag, body: bytes.subarray(at, at + length) });
    at += length;
  }

  return found;
}

/** The public key and subkeys in a key block, each with its fingerprint. Only v4 RSA is read. */
export function readPublicKeys(bytes: Buffer): PublicKeyInfo[] {
  const keys: PublicKeyInfo[] = [];
  for (const { tag, body } of packets(bytes)) {
    if (tag !== 6 && tag !== 14) continue;
    if (body[0] !== 4) throw new Error(`a version ${body[0]} key, and only version 4 is read here`);
    const algorithm = body[5]!;
    if (algorithm !== 1 && algorithm !== 2 && algorithm !== 3) continue; // Not RSA: never a signer we can check.

    const [n, afterN] = mpi(body, 6);
    const [e] = mpi(body, afterN);
    const prefix = Buffer.from([0x99, (body.length >> 8) & 0xff, body.length & 0xff]);
    keys.push({
      fingerprint: createHash('sha1').update(prefix).update(body).digest('hex').toUpperCase(),
      primary: tag === 6,
      key: createPublicKey({ key: { kty: 'RSA', n: base64url(n), e: base64url(e) }, format: 'jwk' }),
    });
  }

  if (!keys.some((key) => key.primary)) throw new Error('the key block holds no RSA primary key');
  return keys;
}

/** The one signature packet a detached signature is. */
export function readSignature(bytes: Buffer): SignatureInfo {
  const signatures = packets(bytes).filter((packet) => packet.tag === 2);
  if (signatures.length !== 1) throw new Error(`a detached signature holds one signature packet, and this holds ${signatures.length}`);

  const body = signatures[0]!.body;
  if (body[0] !== 4) throw new Error(`a version ${body[0]} signature, and only version 4 is read here`);

  const hashedLength = body.readUInt16BE(4);
  const hashedPart = body.subarray(0, 6 + hashedLength);
  const hashed = body.subarray(6, 6 + hashedLength);
  const unhashedLength = body.readUInt16BE(6 + hashedLength);
  const unhashed = body.subarray(8 + hashedLength, 8 + hashedLength + unhashedLength);
  const rest = 8 + hashedLength + unhashedLength;

  let issuerFingerprint: string | null = null;
  let issuerKeyId: string | null = null;
  for (const [area, trusted] of [[hashed, true], [unhashed, false]] as const) {
    for (const { type, data } of subpackets(area)) {
      // The fingerprint is believed only from the HASHED area, which the signature covers.
      if (type === 33 && trusted && data[0] === 4) issuerFingerprint = data.subarray(1).toString('hex').toUpperCase();
      if (type === 16) issuerKeyId = data.toString('hex').toUpperCase();
    }
  }

  const [signature] = mpi(body, rest + 2);
  return {
    type: body[1]!,
    publicKeyAlgorithm: body[2]!,
    hashAlgorithm: body[3]!,
    issuerFingerprint,
    issuerKeyId,
    hashedPart,
    left16: body.subarray(rest, rest + 2),
    signature,
  };
}

function subpackets(area: Buffer): { type: number; data: Buffer }[] {
  const found: { type: number; data: Buffer }[] = [];
  let at = 0;
  while (at < area.length) {
    const octet = area[at]!;
    let length: number;
    if (octet < 192) {
      length = octet;
      at += 1;
    } else if (octet < 255) {
      length = ((octet - 192) << 8) + area[at + 1]! + 192;
      at += 2;
    } else {
      length = area.readUInt32BE(at + 1);
      at += 5;
    }

    found.push({ type: area[at]! & 0x7f, data: area.subarray(at + 1, at + length) });
    at += length;
  }

  return found;
}

/** A multiprecision integer: its bytes, and where the next field starts. */
function mpi(bytes: Buffer, at: number): [Buffer, number] {
  const bits = bytes.readUInt16BE(at);
  const length = Math.ceil(bits / 8);
  return [bytes.subarray(at + 2, at + 2 + length), at + 2 + length];
}

/** The armour's own checksum (RFC 4880 §6.1). */
function crc24(bytes: Buffer): number {
  let crc = 0xb704ce;
  for (const byte of bytes) {
    crc ^= byte << 16;
    for (let bit = 0; bit < 8; bit++) {
      crc <<= 1;
      if (crc & 0x1000000) crc ^= 0x1864cfb;
    }
  }

  return crc & 0xffffff;
}

function base64url(bytes: Buffer): string {
  return bytes.toString('base64url');
}

function hex(value: number): string {
  return value.toString(16).padStart(2, '0');
}

/** A fingerprint as people read it: ten groups of four. */
export function spaced(fingerprint: string): string {
  return fingerprint.match(/.{1,4}/g)?.join(' ') ?? fingerprint;
}
