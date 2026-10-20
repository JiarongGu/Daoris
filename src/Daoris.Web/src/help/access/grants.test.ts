import { describe, expect, it } from 'vitest';
import { GRANTS, grantState } from './grants';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId } from './wired';

// HELPMODE1c1 (D183 point 1; the modes design §4.1): restricted mode's configured access, read from the code. Each grant's
// operations and how each is chosen, its default, and where an agent cannot be held to it, so the screen never shows a grant
// in force that the agent would not keep.

const CLAUDE: HelpGrantAgent = { name: 'Claude Code', rules: true };
const CODEX: HelpGrantAgent = { name: 'Codex', rules: false };

const spec = (id: HelpGrantId) => GRANTS.find((grant) => grant.id === id)!;
const choice = (id: HelpGrantId, chosen: string[], extra: Partial<HelpGrantChoice> = {}): HelpGrantChoice => ({ id, chosen, ...extra });

describe('the grants', () => {
  it('are the table’s five, in its order, each operation chosen on its own and the checkouts’ reads one group', () => {
    expect(GRANTS.map((grant) => grant.id)).toEqual(['housekeeping', 'browser', 'checkouts', 'settings', 'shell']);
    expect(spec('housekeeping').operations).toEqual(['inspect', 'clean-up', 'discard-branch', 'drop-recovery']);
    expect(spec('checkouts').operations).toEqual(['read']);
    expect(spec('settings').operations).toEqual(['notify', 'language', 'timeout', 'cap', 'cooloff']);
    expect(spec('shell').operations).toEqual([]);
  });

  // HELPMODE1c3 (the modes design §4.1's decided note): a grant that configures a plugin is set on that plugin's own tab, so the
  // browser offers nothing to choose here; the others are Daoris's own and stay set here.
  it('set the browser on its plugin’s tab alone, with nothing chosen here', () => {
    expect(GRANTS.filter((grant) => grant.onPlugin).map((grant) => grant.id)).toEqual(['browser']);
    expect(spec('browser').operations).toEqual([]);
    expect(spec('browser').door).toBe('manage');
  });

  it('start off, the browser waiting for approval and the checkouts on, inherited', () => {
    expect(GRANTS.map((grant) => [grant.id, grant.byDefault])).toEqual([
      ['housekeeping', 'off'], ['browser', 'approval'], ['checkouts', 'inherited'], ['settings', 'off'], ['shell', 'off'],
    ]);
  });
});

describe('a grant’s state', () => {
  it('is on with any operation chosen, and off with none', () => {
    expect(grantState(spec('housekeeping'), choice('housekeeping', ['inspect']), CLAUDE)).toBe('on');
    expect(grantState(spec('housekeeping'), choice('housekeeping', []), CLAUDE)).toBe('off');
    expect(grantState(spec('settings'), undefined, CLAUDE)).toBe('off');
    expect(grantState(spec('checkouts'), choice('checkouts', ['read']), CLAUDE)).toBe('on');
  });

  it('needs approval until the browser’s reads are approved, and says a change since approval apart', () => {
    expect(grantState(spec('browser'), choice('browser', []), CLAUDE)).toBe('needs-approval');
    // A write chosen alone hands nothing: the server is approved with its reads.
    expect(grantState(spec('browser'), choice('browser', ['click']), CLAUDE)).toBe('needs-approval');
    expect(grantState(spec('browser'), choice('browser', ['reads', 'click']), CLAUDE)).toBe('on');
    expect(grantState(spec('browser'), choice('browser', ['reads'], { changed: true }), CLAUDE)).toBe('changed');
  });

  it('is unavailable where the agent takes no permission rules: the browser’s chosen writes and the checkouts’ scoped reads', () => {
    expect(grantState(spec('browser'), choice('browser', ['reads', 'click']), CODEX)).toBe('unavailable');
    expect(grantState(spec('checkouts'), choice('checkouts', ['read']), CODEX)).toBe('unavailable');
    // Daoris holds the typed tools itself, so they hold on every agent.
    expect(grantState(spec('housekeeping'), choice('housekeeping', ['inspect']), CODEX)).toBe('on');
    expect(grantState(spec('settings'), choice('settings', ['notify']), CODEX)).toBe('on');
  });

  it('is not available yet for the confined shell, on every agent', () => {
    expect(grantState(spec('shell'), undefined, CLAUDE)).toBe('later');
    expect(grantState(spec('shell'), undefined, CODEX)).toBe('later');
  });
});
