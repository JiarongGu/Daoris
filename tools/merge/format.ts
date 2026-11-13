/**
 * The widths and counts the tool's lines are laid out with.
 */
export const pad = (text: string, width: number): string => (text.length >= width ? `${text} ` : text.padEnd(width));

export const count = (n: number, word: string): string => `${n} ${word}${n === 1 ? '' : 's'}`;

export const classCount = (n: number): string => `${n} class${n === 1 ? '' : 'es'}`;
