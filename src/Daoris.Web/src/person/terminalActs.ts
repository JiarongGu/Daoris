// The record of the terminal's tier as `STATE`'s `terminalActs` says it (the driver's `TerminalActsNow`): who changed the tier, and
// what a terminal pressed under act, newest first, kept across a restart.
import { clockOf } from '../locales/format';

/** Who changed the tier, and by which door. */
export const TERMINAL_TIER_BY = ['window', 'card', 'ask-daoris', 'session', 'terminal', 'alone'] as const;
export type TerminalTierBy = (typeof TERMINAL_TIER_BY)[number];

/** How a terminal's press under act stands: under way, done, not done, answered with its end not known, or cut off by a stop. */
export const TERMINAL_PRESS_OUTCOMES = ['running', 'done', 'failed', 'unknown', 'interrupted'] as const;
export type TerminalPressOutcome = (typeof TERMINAL_PRESS_OUTCOMES)[number];

/** One change of the tier: the tier set, when, who set it, and the session that pressed it. */
export type TerminalTierChange = { tier: 'ask' | 'act'; at: string; by: TerminalTierBy; session: string | null };

/** One press a terminal made under act: its route, what it named, the command it is, its admission and its end. */
export type TerminalPress = {
  id: string;
  module: string;
  route: string;
  scope: string | null;
  target: string | null;
  command: string | null;
  at: string;
  outcome: TerminalPressOutcome;
  /** The refusal's code a press not done answered. */
  code: string | null;
  ended: string | null;
};

export type TerminalActsNow = { changes: TerminalTierChange[]; presses: TerminalPress[] };

const text = (raw: unknown): string | null => (typeof raw === 'string' && raw.length > 0 ? raw : null);
const shapedOf = (raw: unknown): Record<string, unknown> | null => (typeof raw === 'object' && raw !== null ? raw as Record<string, unknown> : null);
const listOf = (raw: unknown): unknown[] => (Array.isArray(raw) ? raw : []);

/** The record field by field; a change or press it cannot read is left out. */
export function terminalActsOf(raw: unknown): TerminalActsNow {
  const shaped = shapedOf(raw);
  const changes = listOf(shaped?.changes).flatMap((each): TerminalTierChange[] => {
    const change = shapedOf(each);
    const tier = change?.tier === 'act' || change?.tier === 'ask' ? change.tier : null;
    const by = TERMINAL_TIER_BY.find((known) => known === change?.by);
    const at = text(change?.at);
    return tier && by && at ? [{ tier, at, by, session: text(change?.session) }] : [];
  });
  const presses = listOf(shaped?.presses).flatMap((each): TerminalPress[] => {
    const press = shapedOf(each);
    const id = text(press?.id);
    const route = text(press?.route);
    const at = text(press?.at);
    const outcome = TERMINAL_PRESS_OUTCOMES.find((known) => known === press?.outcome);
    if (!press || id === null || route === null || at === null || !outcome) return [];
    return [{
      id, module: text(press.module) ?? 'DAORIS.DRIVER', route, scope: text(press.scope), target: text(press.target), command: text(press.command),
      at, outcome, code: text(press.code), ended: text(press.ended),
    }];
  });
  return { changes, presses };
}

/** The scopes a press's target opens in the window. */
export const TERMINAL_TARGET_SCOPES = ['quest', 'ask', 'session', 'repository'] as const;

/**
 * Who set the tier held and when, in the window's words: the newest change where it set that tier; under act with none, said as set
 * outside Daoris; under ask with none, nothing.
 */
export function tierSetWords(
  t: (key: string, values?: Record<string, string>) => string, language: string, acting: boolean, changes: TerminalTierChange[],
): string | null {
  const newest = changes[0];
  if (newest && newest.tier === (acting ? 'act' : 'ask')) {
    return t(acting ? 'notifications.terminalTier.setAct' : 'notifications.terminalTier.setAsk', {
      by: t(`notifications.terminalTier.by.${newest.by}`, { session: newest.session ?? '' }),
      when: clockOf(newest.at, undefined, language),
    });
  }
  return acting ? t('notifications.terminalTier.unrecorded') : null;
}
