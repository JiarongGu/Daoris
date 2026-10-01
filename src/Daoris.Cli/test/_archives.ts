import { deflateRawSync, gzipSync } from 'node:zlib';
import { crc32 } from '../src/zipfile.ts';
import { TAR_END, pax, tarEntry } from './_tar.ts';

/**
 * Archives built in the test from one row's words (TOOLS4, D121 §3.6), so a refusal table can say what an
 * archive holds without carrying one. The driver's `ToolInstallTests.Archive` reads the same words and builds
 * the same archive with its own code: the two share no code, and the twin tables are the contract.
 *
 * `entries` is items separated by `; `. An item is a folder, `<name>/`, or a file, `<name>=<text>`, followed by
 * any of these flags, each after a `!`:
 *
 *   stored     a zip entry stored, not deflated
 *   method12   a zip entry that says compression method 12, its bytes as they are
 *   encrypted  a zip entry whose flags say it is encrypted
 *   badcrc     a zip entry whose CRC-32 is wrong; a tar header whose checksum is wrong
 *   symlink    a symbolic link to <text>: a zip entry with a link's Unix mode; a tar entry of type 2
 *   hardlink   a tar entry of type 1, linking to <text>
 *   fifo       a tar entry of type 6
 *
 * `shape` is one of: '' (whole); `zip64` (a zip written with zip64's records); `pax` (every tar name carried in
 * an extended header); `cut-end` (a zip without its end record, a tar without its end blocks); `cut-data` (a zip
 * cut in half, a tar cut one byte into its first entry's data); `cut-gzip` (a tar.gz whose last 4 bytes are cut).
 */

export interface SpecEntry {
  name: string;
  /** Null for a folder. */
  text: string | null;
  flags: string[];
}

export function parseEntries(spec: string): SpecEntry[] {
  return spec.split('; ').filter((item) => item !== '').map((item) => {
    const [head, ...flags] = item.split('!') as [string, ...string[]];
    const equals = head.indexOf('=');
    return equals < 0 ? { name: head, text: null, flags } : { name: head.slice(0, equals), text: head.slice(equals + 1), flags };
  });
}

/** The archive a row names: `zip`, `tar.gz`, or any other kind, which is built as a zip and named as given. */
export function buildArchive(kind: string, entries: string, shape: string): Buffer {
  const parsed = parseEntries(entries);
  if (kind === 'tar.gz') {
    const tar = tarBytes(parsed, shape);
    const gz = gzipSync(tar);
    return shape === 'cut-gzip' ? gz.subarray(0, gz.length - 4) : gz;
  }

  const zip = zipBytes(parsed, shape === 'zip64');
  if (shape === 'cut-end') return zip.subarray(0, zip.length - 22);
  if (shape === 'cut-data') return zip.subarray(0, Math.floor(zip.length / 2));
  return zip;
}

function tarBytes(entries: SpecEntry[], shape: string): Buffer {
  const parts: Buffer[] = [];
  for (const entry of entries) {
    const folder = entry.text === null;
    const type = folder ? '5' : entry.flags.includes('symlink') ? '2' : entry.flags.includes('hardlink') ? '1'
      : entry.flags.includes('fifo') ? '6' : '0';
    const linked = type === '1' || type === '2';
    const body = type === '0' ? entry.text! : '';
    if (shape === 'pax') parts.push(tarEntry('PaxHeader', pax({ path: entry.name }), 'x'));
    const header = tarEntry(shape === 'pax' ? 'placeholder' : entry.name, body, type, folder ? 0o755 : 0o644, linked ? entry.text! : '');
    if (entry.flags.includes('badcrc')) header.write('000000\u0000 ', 148, 'binary');
    parts.push(header);
  }

  const tar = Buffer.concat(shape === 'cut-end' ? parts : [...parts, TAR_END]);
  return shape === 'cut-data' ? tar.subarray(0, 513) : tar;
}

const empty = Buffer.alloc(0);

/** Zip64's extended information: each value as eight bytes, in the order the format reads them. */
function zip64Extra(values: number[]): Buffer {
  const extra = Buffer.alloc(4 + 8 * values.length);
  extra.writeUInt16LE(0x0001, 0);
  extra.writeUInt16LE(8 * values.length, 2);
  values.forEach((value, at) => extra.writeBigUInt64LE(BigInt(value), 4 + 8 * at));
  return extra;
}

function zipBytes(entries: SpecEntry[], zip64: boolean): Buffer {
  const locals: Buffer[] = [];
  const centrals: Buffer[] = [];
  let offset = 0;

  for (const entry of entries) {
    const folder = entry.text === null;
    const data = Buffer.from(entry.text ?? '', 'utf8');
    const method = folder || entry.flags.includes('stored') ? 0 : entry.flags.includes('method12') ? 12 : 8;
    const body = method === 8 ? deflateRawSync(data) : data;
    const crc = entry.flags.includes('badcrc') ? (crc32(data) ^ 0xffffffff) >>> 0 : crc32(data);
    const flags = (entry.flags.includes('encrypted') ? 1 : 0) | 0x800;
    const mode = entry.flags.includes('symlink') ? 0o120777 : folder ? 0o040755 : 0o100644;
    const name = Buffer.from(entry.name, 'utf8');
    const version = zip64 ? 45 : 20;

    const localExtra = zip64 ? zip64Extra([data.length, body.length]) : empty;
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(version, 4);
    local.writeUInt16LE(flags, 6);
    local.writeUInt16LE(method, 8);
    local.writeUInt16LE(0, 10);
    local.writeUInt16LE(0x21, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(zip64 ? 0xffffffff : body.length, 18);
    local.writeUInt32LE(zip64 ? 0xffffffff : data.length, 22);
    local.writeUInt16LE(name.length, 26);
    local.writeUInt16LE(localExtra.length, 28);

    const centralExtra = zip64 ? zip64Extra([data.length, body.length, offset]) : empty;
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(0x0300 | version, 4);
    central.writeUInt16LE(version, 6);
    central.writeUInt16LE(flags, 8);
    central.writeUInt16LE(method, 10);
    central.writeUInt16LE(0, 12);
    central.writeUInt16LE(0x21, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(zip64 ? 0xffffffff : body.length, 20);
    central.writeUInt32LE(zip64 ? 0xffffffff : data.length, 24);
    central.writeUInt16LE(name.length, 28);
    central.writeUInt16LE(centralExtra.length, 30);
    central.writeUInt32LE((mode * 0x10000) >>> 0, 38);
    central.writeUInt32LE(zip64 ? 0xffffffff : offset, 42);

    locals.push(local, name, localExtra, body);
    centrals.push(central, name, centralExtra);
    offset += 30 + name.length + localExtra.length + body.length;
  }

  const directory = Buffer.concat(centrals);
  const tail: Buffer[] = [];
  if (zip64) {
    const record = Buffer.alloc(56);
    record.writeUInt32LE(0x06064b50, 0);
    record.writeBigUInt64LE(44n, 4);
    record.writeUInt16LE(45, 12);
    record.writeUInt16LE(45, 14);
    record.writeBigUInt64LE(BigInt(entries.length), 24);
    record.writeBigUInt64LE(BigInt(entries.length), 32);
    record.writeBigUInt64LE(BigInt(directory.length), 40);
    record.writeBigUInt64LE(BigInt(offset), 48);
    const locator = Buffer.alloc(20);
    locator.writeUInt32LE(0x07064b50, 0);
    locator.writeBigUInt64LE(BigInt(offset + directory.length), 8);
    locator.writeUInt32LE(1, 16);
    tail.push(record, locator);
  }

  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(zip64 ? 0xffff : entries.length, 8);
  end.writeUInt16LE(zip64 ? 0xffff : entries.length, 10);
  end.writeUInt32LE(zip64 ? 0xffffffff : directory.length, 12);
  end.writeUInt32LE(zip64 ? 0xffffffff : offset, 16);
  return Buffer.concat([...locals, directory, ...tail, end]);
}
