import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RoleChangePress } from '../../data/wiring';
import { Button } from '../../design/ui';
import { holdDraftOf, holdPress } from '../roles/appointedHold';
import type { PlaceRoleEditor } from './PlaceRoles';

/** The seeded role that reads Ask Daoris's acts. */
export const SECOND_CHECK = 'second-check';

/**
 * *Turn on…* for the second check, while the person's rules hold it: a card saying whom each turn's acts are sent to, in the order its
 * choices are tried, and what the readings spend, in the role door's card's words; *Turn on* presses `SET_ROLE` with the seeded role held
 * by an agent, its choices and policy as they are. Nothing where an agent or the terminal reads already, or the catalogue is not this home's.
 */
export function SecondCheckTurnOn({ editor }: { editor: Pick<PlaceRoleEditor, 'catalogue' | 'agents' | 'busy' | 'onSet'> }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);
  const { catalogue } = editor;
  const row = catalogue?.roles.find((each) => each.id === SECOND_CHECK);
  if (!catalogue?.kept || !row?.hold || row.hold.holder !== 'rules') return null;
  const hold = row.hold;

  const policy = catalogue.policies?.find((each) => each.role === SECOND_CHECK)?.staffPolicy ?? null;
  const order = row.choices && row.choices.length > 0 ? row.choices : policy?.choices ?? [];
  const label = (agent: string) => editor.agents.find((each) => each.value === agent)?.label ?? agent;
  const readers = [...new Set(order.map((choice) => label(choice.agent)))].join(', ');
  const starts = [...new Set((policy?.choices ?? []).map((choice) => choice.budget?.starts ?? null))];
  const spend = starts.length === 1 && starts[0] !== null
    ? t('person.view.second-check-spend', { starts: starts[0] })
    : t('person.view.second-check-spend-open');

  const turnOn = () => {
    setRefused(null);
    const press: RoleChangePress = {
      change: 'set', id: row.id, revision: row.revision, choices: null,
      role: { name: row.name ?? '', takes: [], scope: { kind: 'machine', id: null }, ...holdPress({ ...holdDraftOf(hold, true), holder: 'agent' }) },
    };
    editor.onSet(press, { done: () => setOpen(false), refused: (sentence) => setRefused(sentence) });
  };

  if (!open) {
    return (
      <Button variant="ghost" disabled={editor.busy} onClick={() => { setRefused(null); setOpen(true); }}>
        {t('agents.setup.roles.decides.turnOn')}
      </Button>
    );
  }

  return (
    <div className="grid gap-1 rounded border border-line p-2">
      {order.length > 0 && <p className="m-0 text-small text-ink">{t('person.view.second-check-on', { readers })}</p>}
      <p className="m-0 text-small text-ink-soft">{spend}</p>
      {refused && <p role="alert" className="m-0 text-small text-ink-soft">{refused}</p>}
      <div className="flex gap-1">
        <Button disabled={editor.busy} onClick={turnOn}>{t('agents.setup.roles.decides.turnOnConfirm')}</Button>
        <Button variant="ghost" onClick={() => setOpen(false)}>{t('agents.setup.roles.decides.turnOnCancel')}</Button>
      </div>
    </div>
  );
}
