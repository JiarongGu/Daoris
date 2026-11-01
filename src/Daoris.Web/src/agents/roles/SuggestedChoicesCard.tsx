import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ActorRow, RoleCatalogue, RoleChangePress, RoleChoice, RoleRow } from '../../data/wiring';
import { Button } from '../../design/ui';
import { store, stored } from '../../lib/stored';
import { type Answered, Refused } from '../../work/InlineConfirm';

/** What this browser keeps of a declined suggestion: the day of the table it declined, so a changed table asks again. */
export const DECLINED_KEY = 'daoris.rolesSuggestionDeclined';

/** The roles a suggestion would change: those the catalogue holds as a record, with the choices of agents this machine has. */
function wouldChange(catalogue: RoleCatalogue, agents: readonly string[]) {
  const left = new Set<string>();
  const changes = (catalogue.suggestedChoices ?? []).flatMap((each) => {
    const row = catalogue.roles.find((role) => role.id === each.role);
    if (!row || (row.state !== 'seed' && row.state !== 'set')) return [];
    const choices = each.choices.filter((choice) => {
      if (agents.includes(choice.agent)) return true;
      left.add(choice.agent);
      return false;
    });
    return choices.length > 0 ? [{ row, choices }] : [];
  });
  return { changes, left: [...left] };
}

/** The role's order is the person's own once it holds a stored one; until then every start runs on today's settings. */
function answered(row: RoleRow, staffing: readonly ActorRow[]): boolean {
  const actor = staffing.find((each) => each.id === row.id);
  return actor ? actor.state !== 'before' : row.choices != null;
}

/**
 * **Daoris suggests a role's choices** (MODELROLE1g, D205 point 7, the actors design §6.2): the head of Working roles at first run, while
 * none of the roles it would change holds an order of the person's own. One press sets every role's suggested choices as one change through
 * `SET_ROLES` (AGENTSPLACE1g2): the driver judges them whole, so a refusal says its own sentence and no role is written. Nothing is
 * applied by itself, and the words say it can spend more. *Not now* keeps the table's day in this browser, so the card stays away until
 * the table changes. A molecule: its press goes out as the route takes it.
 */
export function SuggestedChoicesCard({ catalogue, staffing, agents, busy, name, entry, onSetAll }: {
  catalogue: RoleCatalogue;
  staffing: readonly ActorRow[];
  /** The agents this machine has set up. */
  agents: readonly string[];
  busy: boolean;
  /** A role's name in the page's language, and a choice in its words. */
  name: (row: RoleRow) => string;
  entry: (choice: RoleChoice) => string;
  onSetAll: (presses: RoleChangePress[], answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const dated = catalogue.suggestedChoicesDated ?? '';
  const [declined, setDeclined] = useState(() => stored(DECLINED_KEY) === dated);
  const [running, setRunning] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  const { changes, left } = wouldChange(catalogue, agents);
  const first = changes.length > 0 && changes.every(({ row }) => !answered(row, staffing));
  if (!catalogue.kept || declined || changes.length === 0 || !(first || running)) return null;

  const apply = () => onSetAll(
    changes.map(({ row, choices }): RoleChangePress => ({
      change: 'set', id: row.id, revision: row.revision,
      role: {
        name: { en: row.nameEn ?? '', zh: row.nameZh ?? '' }, takes: row.takes,
        scope: { kind: row.scopeKind ?? 'machine', id: row.scopeId ?? null },
      },
      choices: choices.map((choice) => ({ agent: choice.agent, model: choice.model ?? null, effort: choice.effort ?? null })),
    })),
    { done: () => setRunning(false), refused: (sentence) => { setRunning(false); setRefused(sentence); } },
  );

  return (
    <section aria-label={t('settings.ai.roles.suggestedChoices.title')} className="mb-3 rounded-control border border-line p-2">
      <p className="m-0 text-body font-medium text-ink">{t('settings.ai.roles.suggestedChoices.title')}</p>
      <p className="m-0 text-small text-ink-soft wrap-anywhere">{t('settings.ai.roles.suggestedChoices.body', { date: dated })}</p>
      <ul className="m-0 mt-1 flex list-none flex-col gap-1 p-0 text-small text-ink">
        {changes.map(({ row, choices }) => (
          <li key={row.id} className="wrap-anywhere">
            {t('settings.ai.roles.suggestedChoices.line', { role: name(row), choices: choices.map(entry).join(t('settings.ai.roles.suggestedChoices.then')) })}
          </li>
        ))}
      </ul>
      {left.length > 0 && (
        <p className="m-0 mt-1 text-small text-ink-soft wrap-anywhere">{t('settings.ai.roles.suggestedChoices.left', { agents: left.join(', ') })}</p>
      )}
      <div className="mt-2 flex gap-2">
        <Button disabled={busy || running} onClick={() => { setRefused(null); setRunning(true); apply(); }}>
          {t('settings.ai.roles.suggestedChoices.use')}
        </Button>
        <Button variant="ghost" disabled={busy || running} onClick={() => { store(DECLINED_KEY, dated); setDeclined(true); }}>
          {t('settings.ai.roles.suggestedChoices.notNow')}
        </Button>
      </div>
      {refused !== null && <Refused sentence={refused} />}
    </section>
  );
}
