/**
 * The moments the tool writes and says: a record's instant to the second, and a summary's minute in UTC.
 */
/** A moment as people read it in a summary: minutes, in UTC, which is how the record keeps it. */
export const when = (iso: unknown): string => (typeof iso === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(iso) ? `${iso.slice(0, 10)} ${iso.slice(11, 16)} UTC` : 'an unknown time');

export const nowIso = (): string => new Date().toISOString().replace(/\.\d+Z$/, 'Z');
