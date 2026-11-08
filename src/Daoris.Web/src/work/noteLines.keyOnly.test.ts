import { expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { NotePart } from '../data/api';
import { noteLines } from './noteLines';

// The notes of an agent held by a key alone: no sign-in, so each says the key's way, in both languages.

const t = i18n.getFixedT('en');
const coded = (code: string, text: string, values: Record<string, unknown> = {}): NotePart => ({ code, values, text });

it('words a missing key where the roster refuses an account', () => {
  const keyless = [coded('held.key-missing', '…', { owner: 'dsh', account: 'a-1', profile: 'a-1' })];
  expect(noteLines(t, { parts: keyless }, 'en')[0].text).toBe(
    "The `dsh` account `a-1` has no API key its provider accepts, so a session would have nothing to run as — `daoris agent key dsh --account a-1` reads one from standard input, or Agents → the agent's page → Accounts.",
  );
  expect(noteLines(t, { parts: keyless }, 'zh')[0].text).toContain('daoris agent key dsh --account a-1');
});

it('words the wait for a key, as the wait for a sign-in', () => {
  const keyed = [
    coded('held.no-key', '…', { owner: 'dsh' }),
    coded('held.key-sooner', '…', { logins: ['daoris agent key dsh --account a'] }),
    coded('held.key', '…', { logins: ['daoris agent key dsh --account a'] }),
  ];
  expect(noteLines(t, { parts: keyed }, 'en').map((line) => line.text)).toEqual([
    "No `dsh` account of Daoris's own has an API key, and Daoris never starts its sessions in your own `dsh` sign-in — set one for Daoris on the agent's page under Agents, or with `daoris agent key dsh`, which reads it from standard input. The start waits until then.",
    "A key starts it sooner: `daoris agent key dsh --account a`, or Agents → the agent's page → Accounts.",
    "A key starts it: `daoris agent key dsh --account a`, or Agents → the agent's page → Accounts.",
  ]);
  for (const line of noteLines(t, { parts: keyed }, 'zh')) expect(line.text).toContain('agent key dsh');
});
