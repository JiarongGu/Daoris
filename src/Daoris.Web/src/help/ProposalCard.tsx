import { useTranslation } from 'react-i18next';
import { ago } from '../format';
import { RequirementItem } from '../quests/Requirements';
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
 * Since DRIFT1d2 the person's yes to a done's departure, with each departure shown before the press.
 */
export type HelpProposal = {
  id: string;
  /** A move to a workspace (`repository`, ENTRY1d1) has no card of its own: its sentence, Apply and Not now. */
  kind: 'setting' | 'ask' | 'agent' | 'account' | 'delete' | 'go' | 'plugin' | 'hand' | 'browser' | 'sync' | 'accept' | 'repository';
  describe: string;
  terminal: string;
  why: string;
  plugin?: HelpPluginShown | null;
  /** Bringing up to date (HELP10): whether the person has looked, and every row the look listed. */
  sync?: HelpSyncShown | null;
  /** A yes to a departure (DRIFT1d2): the quest, and each departure its done answered. */
  accept?: HelpAcceptShown | null;
};

/**
 * What an accept card shows (DRIFT1d2, D133 §4), as the driver's judge read the quest: each departure by its requirement's
 * number, the person's words the requirement quotes and its check, the done's reason, and the person's words the reason
 * relied on — each as the service answered it.
 */
export type HelpAcceptShown = {
  quest: string;
  title: string;
  departures: { requirement: number; quote: string; check: string; departed: string; words: string }[];
};

/**
 * What a bring-up-to-date card shows (HELP10, WSR6): whether the person has looked — the press that fetches (D109) —
 * and each row the look listed, in the terminal's words, and whether the press moves it. Since LEFT3, what the rows do
 * not say, kept by the look (WSR7, D112): each repository whose line was not fetched, and those left apart. A host
 * older than LEFT3 answers neither.
 */
export type HelpSyncShown = {
  looked: boolean;
  rows: { key: string; step: 'line' | 'replay' | 'delete'; moves: boolean; says: string }[];
  /** Each repository whose line the look did not fetch: git's reason, when it last heard from origin (never, as none), and how origin is reached. */
  notFetched?: { repository: string; fetch: string; lastFetch?: string | null; reach?: string | null }[];
  /** The repositories with a checkout here that the look left apart, holding no branch of Daoris's. */
  apart?: string[];
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
 * What a sync card's look did not fetch (LEFT3 b, WSR7), said once, after the rows, as the screen's note says it: how
 * many of the lines it looked at, grouped by git's reason, when each last heard from origin — which its row is judged
 * against — and what the git Daoris runs needs to reach an origin over SSH or HTTPS. Git's words are content.
 */
function SyncNotFetched({ failed, lines }: { failed: NonNullable<HelpSyncShown['notFetched']>; lines: number }) {
  const { t } = useTranslation();
  const reasons = [...new Set(failed.map((line) => line.fetch))];
  const when = (line: (typeof failed)[number]) => (line.lastFetch
    ? t('settings.sync.notFetched.when', { repository: line.repository, when: ago(line.lastFetch) })
    : t('settings.sync.notFetched.never', { repository: line.repository }));
  return (
    <div role="note" aria-label={t('settings.sync.notFetched.label')} className="m-0 mt-1 text-meta text-ink-soft">
      <p className="m-0">{t('settings.sync.notFetched.head', { count: failed.length, total: Math.max(lines, failed.length) })}</p>
      <ul className="m-0 list-none p-0">
        {reasons.map((reason) => (
          <li key={reason} className="m-0 mt-0.5">
            <span className="break-words"><Inline text={reason} /></span>
            <span className="block">{failed.filter((line) => line.fetch === reason).map(when).join(' · ')}</span>
          </li>
        ))}
      </ul>
      {failed.some((line) => line.reach === 'ssh') && (
        <p className="m-0 mt-0.5"><Inline text={t('settings.sync.notFetched.ssh')} /></p>
      )}
      {failed.some((line) => line.reach === 'https' || line.reach === 'http') && (
        <p className="m-0 mt-0.5">{t('settings.sync.notFetched.https')}</p>
      )}
    </div>
  );
}

/**
 * The repositories a sync card's look left apart (LEFT3 b, D112), collapsed as the screen's list is: they hold no branch
 * of Daoris's, so nothing of theirs was fetched or moves, and a card cannot tick one, so it says how one is included.
 */
function SyncApart({ apart }: { apart: string[] }) {
  const { t } = useTranslation();
  return (
    <details role="group" aria-label={t('settings.sync.apart.label')} className="m-0 mt-1 text-meta text-ink-soft">
      <summary className="cursor-pointer">{t('settings.sync.apart.summary', { count: apart.length })}</summary>
      <p className="m-0 mt-0.5">
        <Inline text={t('help.proposal.syncApart', { names: apart.map((name) => `\`${name}\``).join(', ') })} />
      </p>
    </details>
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
 * show those rows, each as the terminal says it, with Apply, which does only the rows that move. Beside them it says
 * what they do not (LEFT3): the lines the look could not fetch, and the repositories it left apart (D112).
 *
 * **A yes to a departure shows each departure first** (DRIFT1d2, D133 §4): its requirement in the person's words, its
 * check, the done's reason and the words that reason relied on, drawn as the quest's page draws them (`RequirementItem`),
 * so the person reads what they say yes to; its press is *accept*, the quest page's word.
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
  const accepting = proposal.kind === 'accept';
  const departures = accepting ? proposal.accept?.departures ?? [] : [];

  return (
    <li className={deleting
      ? 'rounded-control border border-st-declined/50 bg-raised px-3 py-2.5'
      : 'rounded-control border border-accent/50 bg-raised px-3 py-2.5'}
    >
      <p className={deleting ? 'm-0 text-small font-semibold text-ink-danger' : 'm-0 text-small font-semibold text-accent'}>
        {t(deleting ? 'help.proposal.titleDelete'
          : going ? 'help.proposal.titleGo'
            : proposal.kind === 'plugin' ? 'help.proposal.titlePlugin'
              : handing ? 'help.proposal.titleHand'
                : syncing ? 'help.proposal.titleSync'
                  : accepting ? 'help.proposal.titleAccept' : 'help.proposal.title')}
      </p>
      <p className="m-0 mt-1 text-small text-ink"><Inline text={proposal.describe} /></p>
      {plugin && <PluginRuns plugin={plugin} />}
      {departures.length > 0 && (
        <ol aria-label={t('help.proposal.departures')} className="m-0 mt-2 grid list-none gap-2.5 p-0">
          {departures.map((departure) => (
            <RequirementItem
              key={departure.requirement}
              number={departure.requirement}
              requirement={{ quote: departure.quote, check: departure.check }}
              answer={{ requirement: departure.requirement, departed: departure.departed, quote: departure.words }}
              waiting
            />
          ))}
        </ol>
      )}
      {accepting && <p className="m-0 mt-2 text-meta text-ink-soft">{t('help.proposal.acceptNote')}</p>}
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
          {/* LEFT3 b: what the rows do not say, on the card itself, as the look's message says it too. */}
          {(syncing.notFetched ?? []).length > 0 && (
            <SyncNotFetched failed={syncing.notFetched!} lines={syncing.rows.filter((row) => row.step === 'line').length} />
          )}
          {(syncing.apart ?? []).length > 0 && <SyncApart apart={syncing.apart!} />}
          <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.syncNote')}</p>
        </>
      )}
      {plugin?.copied && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.copied')}</p>}
      {plugin?.replaced && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.plugin.replaced')}</p>}
      {/* The catalogue's sentence, carried through as a value: one copy of it, the driver's. */}
      {plugin?.problem && (
        <p className="m-0 mt-1 text-meta text-ink-danger"><Inline text={t('help.proposal.plugin.problem', { problem: plugin.problem })} /></p>
      )}
      {deleting && <p className="m-0 mt-1 text-meta text-ink-danger">{t('help.proposal.deleteNote')}</p>}
      {going && <p className="m-0 mt-1 text-meta text-ink-faint">{t('help.proposal.goNote')}</p>}
      {handing && <p className="m-0 mt-1 text-meta text-ink-soft">{t('help.proposal.handNote')}</p>}
      {proposal.terminal && (
        <p className="m-0 mt-1 text-meta text-ink-faint"><Inline text={t('help.proposal.command', { command: proposal.terminal })} /></p>
      )}
      {/* The agent's reason quotes names as code, as the description does, so it renders as it does. */}
      {proposal.why && <p className="m-0 mt-1 text-small text-ink-soft"><Inline text={t('help.proposal.why', { why: proposal.why })} /></p>}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant={deleting ? 'danger' : 'primary'} disabled={pending} onClick={() => onApply(proposal.id)}>
          {t(deleting ? 'help.proposal.delete'
            : going ? 'help.proposal.go'
              : looking ? 'help.proposal.look'
                : accepting ? 'help.proposal.accept' : 'help.proposal.apply')}
        </Button>
        <Button variant="ghost" disabled={pending} onClick={() => onDismiss(proposal.id)}>{t('help.proposal.dismiss')}</Button>
      </div>
    </li>
  );
}
