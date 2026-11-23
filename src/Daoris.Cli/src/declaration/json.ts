/** A value as JSON text the way the folder's files are written: two spaces, a list of plain values on one line where it fits, one to a line where it does not. */
const WIDTH = 110;

const isPlain = (value: unknown): boolean => value === null || typeof value !== 'object';

function lines(value: unknown, indent: string): string {
  if (Array.isArray(value)) {
    if (value.length === 0) return '[]';
    if (value.every(isPlain)) {
      const inline = `[${value.map((each) => JSON.stringify(each)).join(', ')}]`;
      if (indent.length + inline.length <= WIDTH) return inline;
    }
    const next = `${indent}  `;
    return `[\n${value.map((each) => `${next}${lines(each, next)}`).join(',\n')}\n${indent}]`;
  }
  if (value !== null && typeof value === 'object') {
    const members = Object.entries(value as Record<string, unknown>).filter(([, each]) => each !== undefined);
    if (members.length === 0) return '{}';
    const next = `${indent}  `;
    return `{\n${members.map(([key, each]) => `${next}${JSON.stringify(key)}: ${lines(each, next)}`).join(',\n')}\n${indent}}`;
  }
  return JSON.stringify(value);
}

export const formatJson = (value: unknown): string => `${lines(value, '')}\n`;
