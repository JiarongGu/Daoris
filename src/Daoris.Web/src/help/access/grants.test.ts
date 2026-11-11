import { describe, expect, it } from 'vitest';
import { GRANTS, grantState } from './grants';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId } from './wired';

// Restricted mode's configured access: the screen never shows a grant in force that the agent would not keep.

const CLAUDE: HelpGrantAgent = { name: 'Claude Code', rules: true };
const CODEX: HelpGrantAgent = { name: 'Codex', rules: false };

const spec = (id: HelpGrantId) => GRANTS.find((grant) => grant.id === id)!;
const choice = (id: HelpGrantId, chosen: string[], extra: Partial<HelpGrantChoice> = {}): HelpGrantChoice => ({ id, chosen, ...extra });

describe('the grants', () => {
  it('are the table’s four, in its order, the acts granted by family and the checkouts the driver’s to name', () => {
    expect(GRANTS.map((grant) => grant.id)).toEqual(['browser', 'checkouts', 'acts', 'shell']);
    expect(spec('acts').operations).toEqual(['pause', 'clean-up', 'discard', 'settings']);
    // One box a checkout, by its repository's name, as the driver serves them.
    expect(spec('checkouts').operations).toEqual([]);
    expect(GRANTS.filter((grant) => grant.served).map((grant) => grant.id)).toEqual(['checkouts']);
    expect(spec('shell').operations).toEqual([]);
  });

  // A grant that configures a plugin is set on that plugin's own tab, so the
  // browser offers nothing to choose here; the others are Daoris's own and stay set here.
  it('set the browser on its plugin’s tab alone, with nothing chosen here', () => {
    expect(GRANTS.filter((grant) => grant.onPlugin).map((grant) => grant.id)).toEqual(['browser']);
    expect(spec('browser').operations).toEqual([]);
    expect(spec('browser').door).toBe('manage');
  });

  it('start off, the browser waiting for approval and the checkouts on, inherited', () => {
    expect(GRANTS.map((grant) => [grant.id, grant.byDefault])).toEqual([
      ['browser', 'approval'], ['checkouts', 'inherited'], ['acts', 'off'], ['shell', 'off'],
    ]);
  });
});

describe('a grant’s state', () => {
  it('is on with any operation chosen, and off with none', () => {
    expect(grantState(spec('acts'), choice('acts', ['clean-up']), CLAUDE)).toBe('on');
    expect(grantState(spec('acts'), choice('acts', []), CLAUDE)).toBe('off');
    expect(grantState(spec('acts'), undefined, CLAUDE)).toBe('off');
    expect(grantState(spec('checkouts'), choice('checkouts', ['engine']), CLAUDE)).toBe('on');
    expect(grantState(spec('checkouts'), choice('checkouts', []), CLAUDE)).toBe('off');
  });

  it('needs approval until the browser’s reads are approved, and says a change since approval apart', () => {
    expect(grantState(spec('browser'), choice('browser', []), CLAUDE)).toBe('needs-approval');
    // A write chosen alone hands nothing: the server is approved with its reads.
    expect(grantState(spec('browser'), choice('browser', ['click']), CLAUDE)).toBe('needs-approval');
    expect(grantState(spec('browser'), choice('browser', ['reads', 'click']), CLAUDE)).toBe('on');
    expect(grantState(spec('browser'), choice('browser', ['reads'], { changed: true }), CLAUDE)).toBe('changed');
  });

  it('is unavailable where the agent takes no permission rules: the browser’s chosen writes alone', () => {
    expect(grantState(spec('browser'), choice('browser', ['reads', 'click']), CODEX)).toBe('unavailable');
    // Daoris holds the typed tools itself, so they hold on every agent: the checkouts' read too.
    expect(grantState(spec('checkouts'), choice('checkouts', ['engine']), CODEX)).toBe('on');
    expect(grantState(spec('acts'), choice('acts', ['settings']), CODEX)).toBe('on');
  });

  it('is not available yet for the confined shell, on every agent', () => {
    expect(grantState(spec('shell'), undefined, CLAUDE)).toBe('later');
    expect(grantState(spec('shell'), undefined, CODEX)).toBe('later');
  });
});
