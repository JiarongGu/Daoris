import { standing, writeContracts } from '../contracts.mjs';

/**
 * The page's generated contracts made fresh before the web bundle builds against them. They are ignored files written in each
 * checkout, so a checkout's can predate a route the C# declares, and the bundle's typecheck then refuses the page that sends it.
 * The page's `prebuild` script asks the same, but only where npm runs pre-scripts; the publish asks it itself, as the merge does
 * before its gates, and says what it found. `look`, `build` and `say` are a test's. Answers the generator's exit code.
 */
export function freshContracts(root, { look = standing, build, say = console.log } = {}) {
  const { why } = look(root);
  if (!why) {
    say('desktop-publish: the page\'s generated contracts are fresh.');
    return 0;
  }
  say(`desktop-publish: ${why.replace(/[.:] [Rr]un `[^`]*`$/, '')}; regenerating the page's contracts before the bundle…`);
  const code = writeContracts(root, '--if-stale', build ? { look, build } : { look });
  say(code === 0
    ? 'desktop-publish: regenerated the page\'s contracts.'
    : 'desktop-publish: could not regenerate the page\'s contracts (the generator\'s words are above); nothing was published.');
  return code;
}
