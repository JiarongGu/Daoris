// An argument as a terminal hint prints it (ACCTQUOTE1, D125's ACCTQUOTE1 note). A hint is a command a person copies into
// whichever shell they have: PowerShell, Command Prompt or a POSIX shell, and on Windows the install's `daoris` is
// `daoris.cmd`, which PowerShell finds too (D124 §1.2). An account's name, an id `profile add` was given or a workspace's
// name may hold a character one of them reads, so a value printed bare can change the command it is pasted into: `R&D`
// pasted into Command Prompt runs `D`.
//
// One spelling serves every shell, in three kinds:
//
//   1. Bare, where every shell reads the value as itself: a letter or digit of any script or `_` first, then those and
//      `. + : @ / -`. Every id an account is made with now is one (`acct-1a2b3c4d`), and so is an email.
//   2. In double quotes, where the quotes keep it whole in all three: a space, `; , = ' ( ) # ~ { } [ ] * ?`, a leading
//      `@` or dash. PowerShell 5.1 hands a batch file the value without its quotes when it holds no space, so a character
//      Command Prompt reads bare is no use here even quoted.
//   3. A placeholder naming the argument (`<name>`), where no spelling holds in all three: `& | < > ^` (PowerShell 5.1
//      through `daoris.cmd`), `%` (Command Prompt expands `%name%` inside quotes), `$`, `` ` ``, `"`, `\` and `!` (a POSIX
//      shell or PowerShell reads them inside double quotes, and `\"` ends a quote for node on Windows), a quote PowerShell
//      reads as one (‘ ’ ‚ ‛ “ ” „), a control or format character, and nothing at all. The person types the value as
//      their shell spells it; pasted as it is, the placeholder reads as a redirection, as `<branch>` in a hint always has,
//      which PowerShell refuses and the others refuse unless a file of that name is in the folder.
//
// It is the CLI's twin of the page's `shellWord.ts`; `shellword.test.ts` and the page's `shellWord.test.ts` read one
// table, `test/fixtures/shell-words.json`, row for row. It changes no naming rule: a name the contract allows is still
// allowed, and only its hint is spelled.

/** Every shell reads it as itself. */
const BARE = /^[\p{L}\p{N}_][\p{L}\p{N}_.+:@/-]*$/u;

/** No one spelling keeps it whole in PowerShell 5.1 (through `daoris.cmd`), Command Prompt and a POSIX shell. */
const UNSPELLABLE = /[&|<>^%$`"\\!\u2018-\u201E\p{Cc}\p{Cf}\p{Zl}\p{Zp}]/u;

/**
 * `value` as a hint prints it: bare, in double quotes, or `placeholder` where no spelling holds in every shell.
 *
 * @param placeholder What the argument is, as the hint names it where the value cannot be printed: `<name>`, `<account>`,
 *   `<workspace>`.
 */
export function shellWord(value: string, placeholder: string): string {
  if (BARE.test(value)) return value;
  if (value.length > 0 && !UNSPELLABLE.test(value)) return `"${value}"`;
  return placeholder;
}

/** Each of `values` as a hint prints it, a space between. */
export function shellWords(values: readonly string[], placeholder: string): string {
  return values.map((value) => shellWord(value, placeholder)).join(' ');
}
