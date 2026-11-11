import { useMutation } from '@tanstack/react-query';
import { call } from './call';
import type { PluginCommandPress, PluginCommandRound } from '../plugins/interfaces';

export type { PluginCommandPress, PluginCommandRound };

/** The driver waits a minute for a round; the window waits a little longer, so the driver's own sentence arrives first. */
const ROUND_WAIT = 90_000;

const text = (value: unknown): string | null => (typeof value === 'string' && value !== '' ? value : null);

/**
 * A round as an older or a stranger driver may answer it, read defensively: lines that are no text are dropped, an ask with no name
 * asks nothing, and a round that asks nothing and names no exit ended as not done, so the card never waits on nothing.
 */
export function roundOf(raw: unknown): PluginCommandRound {
  const answer = raw !== null && typeof raw === 'object' ? raw as Record<string, unknown> : {};
  const asked = answer.ask !== null && typeof answer.ask === 'object' ? answer.ask as Record<string, unknown> : null;
  const name = text(asked?.name);
  const ask = asked && name ? { name, prompt: text(asked.prompt) ?? name, masked: asked.masked === true } : null;
  return {
    run: text(answer.run) ?? '',
    said: Array.isArray(answer.said) ? answer.said.filter((line): line is string => typeof line === 'string') : [],
    ask,
    open: text(answer.open),
    code: text(answer.code),
    then: answer.then ?? null,
    data: answer.data ?? null,
    exit: typeof answer.exit === 'number' ? answer.exit : ask ? 0 : 1,
  };
}

/**
 * The window's door onto a plugin's commands: one round per press, run in a process of the plugin's own. A masked answer is sent
 * with the round and kept nowhere here; the card that asked holds the run's answers until it ends.
 */
export const usePluginCommand = () => useMutation({
  mutationFn: async (press: PluginCommandPress) => roundOf(await call('PLUGIN_COMMAND', press, { timeoutMs: ROUND_WAIT })),
});
