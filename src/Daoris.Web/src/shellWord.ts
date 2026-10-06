// An argument as a terminal hint prints it (ACCTQUOTE1, D125's ACCTQUOTE1 note). A twin on the page is a command a person
// copies into whichever shell they have: PowerShell, Command Prompt or a POSIX shell, and on Windows the install's `daoris`
// is `daoris.cmd`, which PowerShell finds too (D124 §1.2). An account's name, an id or a workspace's name may hold a
// character one of them reads, so a value printed bare can change the command it is pasted into: `R&D` pasted into
// Command Prompt runs `D`. One spelling serves every shell:
//
//   1. Bare, where every shell reads the value as itself: a letter or digit of any script or `_` first, then those and
//      `. + : @ / -`.
//   2. In double quotes, where they keep it whole in all three: a space, `; , = ' ( ) # ~ { } [ ] * ?`, a leading `@` or
//      dash.
//   3. A placeholder naming the argument (`<name>`), where no spelling holds in all three: `& | < > ^ % $`, a backtick, a
//      double quote, `\`, `!`, a quote PowerShell reads as one, a control or format character, and nothing at all. The
//      person types the value as their shell spells it; pasted as it is, the placeholder reads as a redirection, as
//      `<branch>` in a twin always has.
//
// It is the page's twin of the CLI's `shellword.ts`, which carries the reasons beside each kind; `shellWord.test.ts` and
// the CLI's `shellword.test.ts` read one table, the CLI's `test/fixtures/shell-words.json`, row for row, and the CLI's
// suite holds that table against the shells themselves.

/** Every shell reads it as itself. */
const BARE = /^[\p{L}\p{N}_][\p{L}\p{N}_.+:@/-]*$/u;

/** No one spelling keeps it whole in PowerShell 5.1 (through `daoris.cmd`), Command Prompt and a POSIX shell. */
const UNSPELLABLE = /[&|<>^%$`"\\!\u2018-\u201E\p{Cc}\p{Cf}\p{Zl}\p{Zp}]/u;

/**
 * `value` as a terminal twin prints it: bare, in double quotes, or `placeholder` where no spelling holds in every shell.
 *
 * @param placeholder What the argument is, as the twin names it where the value cannot be printed: `<name>`, `<account>`,
 *   `<workspace>`.
 */
export function shellWord(value: string, placeholder: string): string {
  if (BARE.test(value)) return value;
  if (value.length > 0 && !UNSPELLABLE.test(value)) return `"${value}"`;
  return placeholder;
}

/** Each of `values` as a twin prints it, a space between. */
export function shellWords(values: readonly string[], placeholder: string): string {
  return values.map((value) => shellWord(value, placeholder)).join(' ');
}
