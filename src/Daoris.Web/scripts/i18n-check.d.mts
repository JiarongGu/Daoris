// The types of `i18n-check.mjs`, for the test that plants a catalogue (D222). The script is plain JavaScript so the build runs it on any
// Node the package supports; this declares what it exports.

/** The languages present under `root` (a folder each): English first, then the rest in order. */
export function languagesIn(root?: string): string[];

/** Every disagreement between the catalogues under `root`, `en` the source, and the summary line of a clean run. */
export function checkLocales(root?: string): { problems: string[]; summary: string };
