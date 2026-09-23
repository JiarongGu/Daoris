/**
 * A tar writer, just enough to say what an archive holds (AGT2b). Written from the ustar layout, not
 * from the reader, so the two can disagree — which is what a test of the reader needs.
 */

export const TAR_END = Buffer.alloc(1024);

export function tarEntry(name: string, body: string | Buffer, type = '0', mode = 0o644, link = ''): Buffer {
  const data = Buffer.isBuffer(body) ? body : Buffer.from(body, 'utf8');
  const header = Buffer.alloc(512);
  header.write(name.slice(0, 100), 0, 'utf8');
  header.write(octal(mode, 7), 100);
  header.write(octal(0, 7), 108);
  header.write(octal(0, 7), 116);
  header.write(octal(data.length, 11), 124);
  header.write(octal(0, 11), 136);
  header.write('        ', 148);
  header.write(type, 156);
  header.write(link, 157);
  header.write('ustar\u000000', 257, 'binary');

  let sum = 0;
  for (const byte of header) sum += byte;
  header.write(`${sum.toString(8).padStart(6, '0')}\u0000 `, 148, 'binary');

  const padded = Buffer.alloc(Math.ceil(data.length / 512) * 512);
  data.copy(padded);
  return Buffer.concat([header, padded]);
}

/** A pax record set: each record is `<length> <key>=<value>\n`, the length counting itself. */
export function pax(records: Record<string, string>): string {
  return Object.entries(records).map(([key, value]) => {
    const tail = ` ${key}=${value}\n`;
    let length = tail.length + 1;
    while (`${length}${tail}`.length !== length) length += 1;
    return `${length}${tail}`;
  }).join('');
}

function octal(value: number, width: number): string {
  return `${value.toString(8).padStart(width, '0')}\u0000`;
}
