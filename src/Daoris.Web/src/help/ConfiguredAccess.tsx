import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { HelpGrantAgent, HelpGrantChoice, HelpGrantId, HelpGrantState } from './wired';
import { Button, CheckField, Icon, Inline, Pill } from '../design/ui';
import { cn } from '../lib/cn';
import { GRANTS, grantState, type GrantSpec } from './grants';

// Restricted mode's configured access (HELPMODE1c1, D183 point 1; the modes design §4.1 and §5's Settings sketch), drawn from
// props, first for the owner's review before any grant was built; it presses nothing itself. It lives in help/ so both
// Settings → Daoris's own AI → Ask Daoris and the panel can draw it. Not `HelpGrants`, which is the driver's name for the
// plugin approvals the browser's grant reads.

/** A state's pill: granted in the done hue, waiting on the person's approval in the waiting hue, the rest quiet. */
const TONE: Record<HelpGrantState, 'neutral' | 'open' | 'done'> = {
  off: 'neutral', on: 'done', 'needs-approval': 'open', changed: 'open', unavailable: 'neutral', later: 'neutral',
};

/** The browser's writes, chosen each on its own after its reads (§4.1). */
const READS = 'reads';

/**
 * Ask Daoris's configured access in restricted mode: the five grants of §4.1 in its order. Each says what it reaches in a
 * sentence, its default and its state, and opens its operations from its door (*Choose operations*, *Manage*, *Choose
 * settings*): each a box of its own where the table chooses them apart, one box for a group. A grant this agent cannot be
 * held to says *unavailable on this agent* and why, its boxes disabled, whatever is chosen; the browser says signed-in
 * pages may be affected; and a choice the conversation in view did not start with is marked as taking its next start, since
 * *What it has* keeps what the start was handed (§4.1's traps).
 */
export function ConfiguredAccess({ agent, grants, opened = [], busy = false, onChoose }: {
  /** The agent Ask Daoris runs on: whether it takes permission rules decides what it can be held to. */
  agent: HelpGrantAgent;
  /** What this machine has chosen; a grant not named has nothing chosen. */
  grants: readonly HelpGrantChoice[];
  /** The grants whose operations show from the start: a door that opens this to them, or a story. */
  opened?: readonly HelpGrantId[];
  busy?: boolean;
  onChoose: (grant: HelpGrantId, operation: string, chosen: boolean) => void;
}) {
  const { t } = useTranslation();
  const title = useId();

  return (
    <section aria-labelledby={title} className="grid min-w-0 gap-2">
      <h3 id={title} className="m-0 text-small font-semibold text-ink-faint">{t('help.grants.title')}</h3>
      <p className="m-0 text-small text-ink-soft">{t('help.grants.lead')}</p>
      <ul className="m-0 grid min-w-0 list-none p-0">
        {GRANTS.map((spec) => (
          <Grant
            key={spec.id}
            spec={spec}
            choice={grants.find((grant) => grant.id === spec.id)}
            agent={agent}
            open={opened.includes(spec.id)}
            busy={busy}
            onChoose={onChoose}
          />
        ))}
      </ul>
    </section>
  );
}

/** One grant's row: its name, state and door; its reach and default; why it is unavailable; and, opened, its operations. */
function Grant({ spec, choice, agent, open: openAtFirst, busy, onChoose }: {
  spec: GrantSpec; choice: HelpGrantChoice | undefined; agent: HelpGrantAgent; open: boolean; busy: boolean;
  onChoose: (grant: HelpGrantId, operation: string, chosen: boolean) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(openAtFirst);
  const panel = useId();
  const state = grantState(spec, choice, agent);
  const name = t(`help.grants.name.${spec.id}`);
  const chosen = choice?.chosen ?? [];
  const held = state !== 'unavailable';
  // Why it is not in force, in a sentence: the agent's, the plugin's update's, or the shell's not being built.
  const why = state === 'unavailable' ? t(`help.grants.why.${spec.id}`, { agent: agent.name })
    : state === 'changed' ? t('help.grants.why.changed')
      : state === 'later' ? t('help.grants.why.shell') : null;

  const box = (operation: string) => (
    <CheckField
      key={operation}
      checked={chosen.includes(operation)}
      disabled={busy || !held}
      onChange={(checked) => onChoose(spec.id, operation, checked)}
      label={t(`help.grants.op.${spec.id}.${operation}`)}
      // A box the agent cannot be held to reads as disabled, its sentence too, while it keeps what was chosen.
      className={cn('text-small', !held && 'cursor-not-allowed opacity-60')}
    />
  );

  return (
    <li aria-label={name} className="grid min-w-0 gap-1 border-t border-line py-2.5 first:border-t-0 first:pt-0 last:pb-0">
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <span className="text-body font-medium text-ink">{name}</span>
        <Pill tone={TONE[state]}>{t(`help.grants.state.${state}`)}</Pill>
        {choice?.pending && <Pill title={t('help.grants.pendingWhy')}>{t('help.grants.pending')}</Pill>}
        {spec.door && (
          <Button
            variant="ghost"
            className="ml-auto text-small text-accent"
            aria-expanded={open}
            aria-controls={panel}
            aria-label={t('help.grants.doorFor', { door: t(`help.grants.door.${spec.door}`), grant: name })}
            onClick={() => setOpen((was) => !was)}
          >
            {t(`help.grants.door.${spec.door}`)}
            <Icon name="chevronDown" size={12} className={cn('transition-transform duration-(--speed)', open && 'rotate-180')} />
          </Button>
        )}
      </div>
      <p className="m-0 text-small text-ink-soft"><Inline text={t(`help.grants.reach.${spec.id}`)} /></p>
      {!spec.later && <p className="m-0 text-meta text-ink-faint">{t(`help.grants.default.${spec.byDefault}`)}</p>}
      {why && (
        <p className="m-0 flex gap-1.5 text-small text-ink-soft">
          <Icon name="info" size={13} className="mt-0.5 text-ink-faint" />
          <span className="min-w-0"><Inline text={why} /></span>
        </p>
      )}
      {spec.id === 'browser' && (
        <p className="m-0 border-l-[3px] border-warn pl-2.5 text-small text-ink">{t('help.grants.signedIn')}</p>
      )}
      {open && spec.operations.length > 0 && (
        <div id={panel} className="mt-1 grid min-w-0 gap-1.5 rounded-control border border-line bg-raised px-3 py-2">
          {spec.id === 'browser' ? (
            <>
              {box(READS)}
              <p className="m-0 text-small text-ink-soft">{t('help.grants.writes')}</p>
              <div className="flex min-w-0 flex-wrap gap-x-4 gap-y-1.5">
                {spec.operations.filter((operation) => operation !== READS).map(box)}
              </div>
            </>
          ) : (
            <div className="grid min-w-0 gap-1.5">{spec.operations.map(box)}</div>
          )}
          <p className="m-0 text-meta text-ink-faint"><Inline text={t(`help.grants.never.${spec.id}`)} /></p>
        </div>
      )}
    </li>
  );
}
