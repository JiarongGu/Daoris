// The basic block, extension A and the compatibility block; the later extensions are surrogate pairs, two characters already.
const HAN = /^[一-鿿㐀-䶿豈-﫿]$/;

/**
 * Whether these words are enough to search a session by (ASKHIST1d2, RAILSRCH1b): two characters, or one Han character,
 * which is a word on its own (区, 圈). The driver's own rule (`SessionEvents.Searchable`): one letter of any other script
 * would find nearly everything. The rail, a conversation's find, the bridge hook and Ask Daoris's history all ask this.
 */
export function searchable(words: string): boolean {
  const wanted = words.trim();
  return wanted.length >= 2 || HAN.test(wanted);
}
