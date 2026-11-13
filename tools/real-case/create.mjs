import { parseList } from './reads.mjs';

const CREATE_WAIT_MS = 10 * 60_000;

/** The installed verb's refusal or last sentence: its `repository create:` line, else whatever it said. */
const sentenceOf = (r) => {
  const text = `${r.stdout}\n${r.stderr}`.split('\n').map(l => l.trim()).filter(Boolean);
  return text.filter(l => l.startsWith('repository create:')).at(-1) ?? text.at(-1) ?? `exit ${r.code}`;
};

/**
 * The scratch repository through the installed command's own verb, which opens the folder, registers it, drives it and files its
 * set-up quest (the person's Confirm on the window's card is the verb's own wait, not this tool's press). Then the set-up quest's
 * outcome is waited for. Each step marks its stage; a refusal or a set-up that does not finish ends the case with the command's
 * sentence.
 */
export async function createAndSetUp({ installed, name, purpose, workspace, workspaceDir, line = 'main', mark, clock, sleep, pollMs, timeoutMs }) {
  if (!workspaceDir) throw new Error('--workspace-dir names the folder the scratch repository is made in');
  const args = ['repository', 'create', name, '--purpose', purpose, '--under', workspaceDir, '--line', line];
  if (workspace) args.push('--workspace', workspace);
  const made = installed(args, { timeoutMs: CREATE_WAIT_MS });
  const said = sentenceOf(made);
  if (made.code !== 0) return { ok: false, outcome: 'could-not-create', said };
  mark('created');

  const quest = /^\s*quest\s+#(\S+)\s+for\s/m.exec(made.stdout)?.[1] ?? null;
  if (!quest) { mark('set-up', 'the verb filed no set-up quest'); return { ok: true, said, quest }; }
  const t0 = clock();
  for (;;) {
    const row = (parseList(installed(['quest', 'list', '--repository', name, '--json']).stdout) ?? []).find(q => q.id === quest);
    const status = String(row?.status ?? '').toLowerCase();
    if (status === 'done') { mark('set-up'); return { ok: true, said, quest }; }
    if (status === 'declined') return { ok: false, outcome: 'set-up-declined', said: `the set-up quest #${quest} was declined${row.note ? `: ${row.note}` : ''}`, quest };
    if (clock() - t0 > timeoutMs) return { ok: false, outcome: 'set-up-timeout', said: `the set-up quest #${quest} was still ${status || 'unread'} after the wait`, quest };
    await sleep(pollMs);
  }
}
