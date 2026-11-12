/** The sentences a merge's summary adds for its flakes and for a gate that died of memory. */

type Named = { gate: { name: string }; cause?: string };

const count = (n: number, word: string): string => `${n} ${word}${n === 1 ? '' : 's'}`;

/**
 * What a summary says of its flakes, empty when none: a test's flake is recorded, and one of memory is
 * the machine's, not the change's, and said so.
 */
export function flakeNote(flakes: Named[]): string {
  const of = (list: Named[]): string => list.map((result) => result.gate.name).join(', ');
  const tests = flakes.filter((result) => result.cause !== 'memory');
  const memory = flakes.filter((result) => result.cause === 'memory');
  const parts: string[] = [];
  if (tests.length) parts.push(`${count(tests.length, 'flake')} (${of(tests)}): record it under FLAKE1`);
  if (memory.length) {
    parts.push(`${count(memory.length, 'flake')} of memory (${of(memory)}): ${memory.length === 1 ? 'it' : 'each'} died of memory and passed run again alone, the machine's and not the change's`);
  }
  return parts.length ? `, with ${parts.join('; and ')}` : '';
}

/** The line a failed run's summary adds when a gate of it died of memory, or null. */
export function memoryLine(failed: Named[]): string | null {
  const memory = failed.filter((result) => result.cause === 'memory').map((result) => result.gate.name);
  if (!memory.length) return null;
  return `  Of memory, not of a check: ${memory.join(', ')} died of memory; run ${memory.length === 1 ? 'it' : 'them'} again when the machine has memory to spare.`;
}
