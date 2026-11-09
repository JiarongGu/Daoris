/**
 * The widths and counts the tool's lines are laid out with.
 */
export const pad = (text, width) => (text.length >= width ? `${text} ` : text.padEnd(width));

export const count = (n, word) => `${n} ${word}${n === 1 ? '' : 's'}`;

export const classCount = (n) => `${n} class${n === 1 ? '' : 'es'}`;
