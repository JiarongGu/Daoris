import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../ui';

/**
 * What a plugin proposal's plugin runs (PLUG9), each command as its manifest writes it, `${plugin}`
 * included: the bridge leaves a null out, so a plugin that speaks on no point arrives with no `command`,
 * and a switch with no `copied`.
 */
export type HelpPluginShown = {
  id: string;
  name: string;
  version: string;
  command?: string[] | null;
  points: string[];
  harnesses: { name: string; command: string[] }[];
  servers: { name: string; command: string[] }[];
  copied?: boolean;
  problem?: string | null;
  /** What one of the install's own plugins needs, in its README's words (PLUG9 d). */
  needs?: string[];
  /** What an update changes, each side as the manifests write it (PLUG9 c). */
  changes?: { what: string; was: string; now: string }[];
  /** Whether Apply replaces an installed plugin's folder from where it came from (an update). */
  replaced?: boolean;
};

/**
 * A proposal of Ask Daoris's the route takes (HELP1c): what it changes, and the command that does the same.
 * Since HELP6 a kind for each door built since: an agent's update or pin, an account's model and effort, a
 * delete of a record made by mistake, and a go to a screen, which has no command since it changes nothing.
 * Since PLUG9 a plugin that has landed, added from its folder, or one switched on or off, with what it runs.
 * Since WSR5b a branch a landing made, handed to a landing plugin, which pushes it. Since HELP10 Daoris's
 * browser's settings, a card as a setting's is, and bringing repositories up to date, which is looked at first.
 */
export type HelpProposal = {
  id: string;
  kind: 'setting' | 'ask' | 'agent' | 'account' | 'delete' | 'go' | 'plugin' | 'hand' | 'browser' | 'sync';
  describe: string;
  terminal: string;
  why: string;
  plugin?: HelpPluginShown | null;
  /** Bringing up to date (HELP10): whether the person has looked, and every row the look listed. */
  sync?: HelpSyncShown | null;
};

/**
 * What a bring-up-to-date card shows (HELP10, WSR6): whether the person has looked — the press that fetches (D109) —
 * and each row the look listed, in the terminal's words, and whether the press moves it.
 */
export type HelpSyncShown = {
  looked: boolean;
  rows: { key: string; step: 'line' | 'replay' | 'delete'; moves: boolean; says: string }[];
};

/** A command as a terminal takes it: a word holding a space quoted, as the driver spells it. */
const line = (command: string[]) => command.map((word) => (word.includes(' ') ? `"${word}"` : word)).join(' ');

/**
 * What a plugin runs, before Apply (PLUG9): its id, the process it starts and the points it speaks on, the
 * harnesses it declares and the servers it hands every session. Chrome translates; every name and command
 * is the manifest's, as it writes it.
 */
function PluginRuns({ plugin }: { plugin: HelpPluginShown }) {
  const { t } = useTranslation();
  const said = (text: string, key: string) => (
    <li key={key} className="m-0 text-meta text-ink-soft"><Inline text={text} /></li>
  );
  return (
    <ul aria-label={t('help.proposal.plugin.shows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
      {said(t('help.proposal.plugin.id', { id: plugin.id }), 'id')}
      {said(plugin.command && plugin.command.length > 0
        ? t('help.proposal.plugin.runs', { command: line(plugin.command) })
        : t('help.proposal.plugin.runsNothing'), 'runs')}
      {plugin.points.length > 0
        && said(t('help.proposal.plugin.points', { points: plugin.points.map((point) => `\`${point}\``).join(', ') }), 'points')}
      {plugin.harnesses.map((harness) =>
        said(t('help.proposal.plugin.harness', { name: harness.name, command: line(harness.command) }), `harness-${harness.name}`))}
      {plugin.servers.map((server) =>
        said(t('help.proposal.plugin.server', { name: server.name, command: line(server.command) }), `server-${server.name}`))}
      {/* PLUG9 (c): each side as the manifests write it; a side that is empty is none. */}
      {(plugin.changes ?? []).map((change) =>
        said(t('help.proposal.plugin.change', {
          what: t(`plugin.update.what.${change.what}`, { defaultValue: change.what }),
          was: change.was ? `\`${change.was}\`` : t('plugin.update.none'),
          now: change.now ? `\`${change.now}\`` : t('plugin.update.none'),
        }), `change-${change.what}`))}
      {/* PLUG9 (d): the README's own requirement lines, which the person sets up themselves. */}
      {(plugin.needs ?? []).map((need) => said(t('help.proposal.plugin.needs', { need }), `need-${need}`))}
    </ul>
  );
}

/**
 * One change Ask Daoris proposes (HELP1c, D89): what it changes, the terminal command that does the same
 * (D50), and why — with **apply** and **not now**, since every change is the person's press.
 *
 * @remarks
 * The description is the driver's sentence and renders verbatim, as every driver sentence does; only the
 * chrome around it translates. A molecule: the proposal arrives judged, a press goes out.
 *
 * **Each kind says what its press does** (HELP6): a delete is titled as one, says it cannot be undone as
 * the drawer's own confirmation does, and its press is *delete*; a go is a suggestion, not a change, so
 * it carries no command and its press is *go there*. A plugin (PLUG9) shows what will run before Apply,
 * and an add says its folder is copied in and that nothing starts at the press. A hand-off (WSR5b) says
 * before Apply that its plugin pushes the branch as the person, the one card whose press leads to a push.
 *
 * **Bringing up to date has the screen's two presses** (HELP10, D109): looking fetches as the person, so the
 * card's first press is *look for updates*, and only once the driver has listed what the press would do does it
 * show those rows, each as the terminal says it, with Apply, which does only the rows that move.
 */
export function ProposalCard({ proposal, pending = false, onApply, onDismiss }: {
  proposal: HelpProposal;
  pending?: boolean;
  onApply: (id: string) => void;
  onDismiss: (id: string) => void;
}) {
  const { t } = useTranslation();
  const deleting = proposal.kind === 'delete';
  const going = proposal.kind === 'go';
  const plugin = proposal.kind === 'plugin' ? proposal.plugin ?? null : null;
  const handing = proposal.kind === 'hand';
  const syncing = proposal.kind === 'sync' ? proposal.sync ?? { looked: false, rows: [] } : null;
  const looking = syncing !== null && !syncing.looked;

  return (
    <li className={deleting
      ? 'rounded-control border border-st-declined/50 bg-raised px-3 py-2.5'
      : 'rounded-control border border-accent/50 bg-raised px-3 py-2.5'}
    >
      <p className={deleting ? 'm-0 text-small font-semibold text-st-declined' : 'm-0 text-small font-semibold text-accent'}>
        {t(deleting ? 'help.proposal.titleDelete'
          : going ? 'help.proposal.titleGo'
            : proposal.kind === 'plugin' ? 'help.proposal.titlePlugin'
              : handing ? 'help.proposal.titleHand'
                : syncing ? 'help.proposal.titleSync' : 'help.proposal.title')}
      </p>
      <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>
      {plugin && <PluginRuns plugin={plugin} />}
      {looking && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.syncLook')}</p>}
      {syncing?.looked && (
        <>
          <ul aria-label={t('help.proposal.syncRows')} className="m-0 mt-1 flex list-none flex-col gap-0.5 p-0">
            {syncing.rows.map((row) => (
              <li key={`${row.step}:${row.key}`} className="m-0 text-meta text-ink-soft">
                <span className={row.moves ? 'font-semibold text-accent' : 'text-ink-faint'}>
                  {t(row.moves ? 'settings.sync.moves' : 'settings.sync.stays')}
                </span>{' '}
                {/* The driver's sentence, the terminal's, carried as it said it. */}
                <Inline text={row.says} />
              </li>
            ))}
          </ul>
          <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.syncNote')}</p>
        </>
      )}
      {plugin?.copied && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.copied')}</p>}
      {plugin?.replaced && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.replaced')}</p>}
      {/* The catalogue's sentence, carried through as a value: one copy of it, the driver's. */}
      {plugin?.problem && (
        <p className="m-0 mt-1 text-meta text-st-declined"><Inline text={t('help.proposal.plugin.problem', { problem: plugin.problem })} /></p>
      )}
      {deleting && <p className="m-0 mt-1 text-meta text-st-declined">{t('help.proposal.deleteNote')}</p>}
      {going && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>}
      {handing && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.handNote')}</p>}
      {proposal.terminal && (
        <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      )}
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant={deleting ? 'danger' : 'primary'} disabled={pending} onClick={() => onApply(proposal.id)}>
          {t(deleting ? 'help.proposal.delete' : going ? 'help.proposal.go' : looking ? 'help.proposal.look' : 'help.proposal.apply')}
        </Button>
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>{t('help.proposal.dismiss')}</Button>
      </div>
    </li>
  );
}
