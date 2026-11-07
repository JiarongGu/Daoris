import { type KeyboardEvent, useId, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { NODE_KINDS } from '../../data/generated/service';
import { QuickPanel } from '../../design/ui';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import type { WorkflowPaletteAnswer } from './answers';
import { stepAbout, stepTitle } from './editWords';
import { type StepChoice, stepChoices } from './graphDraft';

// The step picker (FLOWCHART2b; the flowchart design §2, D221 §2.3): every step the palette offers, searchable and grouped, the place the
// new step goes said above it, so an insertion names the way it replaces. A kind added to the driver or a plugin installed is offered with
// no change here; a plugin's steps stand under the plugin, titled by their manifest's words.

/** The kit's order (each built-in's place in it, which its `group` heads); a plugin's steps follow, and what the kit does not know is *Other*. */
const OTHER = 'others';
const KIT_RANK = new Map(NODE_KINDS.map((row, at) => [row.kind, at]));

type Offered = { choice: StepChoice; title: string; about: string; group: string };

export function StepPicker({ open, palette, place, onPick, onClose }: {
  open: boolean;
  palette: WorkflowPaletteAnswer;
  /** Where the new step goes, in words: between two steps, at a row's end, after a step, or unconnected. */
  place: string;
  onPick: (choice: StepChoice) => void;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const listId = useId();
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);

  const offered = useMemo((): Offered[] => {
    // The palette says how each kind is offered: a landing's tail comes with its method.
    const all = stepChoices(palette).filter((choice) => choice.offered === 'alone').map((choice) => {
      const known = NODE_KINDS.find((row) => row.kind === choice.kind);
      const group = choice.plugin ? t('workflow.edit.add.plugin', { plugin: choice.plugin })
        : t(`workflow.edit.picker.group.${known?.group ?? OTHER}`);
      return { choice, title: stepTitle(t, i18n, palette, choice), about: stepAbout(i18n, palette, choice.kind), group };
    });
    const rank = (each: Offered) => KIT_RANK.get(each.choice.kind) ?? (each.choice.plugin ? NODE_KINDS.length : NODE_KINDS.length + 1);
    return all.map((each, index) => ({ each, index })).sort((a, b) => rank(a.each) - rank(b.each) || a.index - b.index).map(({ each }) => each);
  }, [palette, t, i18n, i18n.language]);

  const words = query.trim().toLowerCase();
  const shown = words === '' ? offered
    : offered.filter((each) => [each.title, each.about, each.choice.kind, each.choice.plugin ?? ''].some((text) => text.toLowerCase().includes(words)));
  const at = Math.min(active, Math.max(0, shown.length - 1));
  const optionId = (index: number) => `${listId}-${index}`;

  const close = () => { setQuery(''); setActive(0); onClose(); };
  const pick = (each: Offered | undefined) => {
    if (!each) return;
    setQuery('');
    setActive(0);
    onPick(each.choice);
  };
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (isComposing(event)) return;
    if (event.key === 'ArrowDown') { event.preventDefault(); setActive(Math.min(at + 1, shown.length - 1)); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); setActive(Math.max(at - 1, 0)); }
    else if (event.key === 'Home') { event.preventDefault(); setActive(0); }
    else if (event.key === 'End') { event.preventDefault(); setActive(shown.length - 1); }
    else if (event.key === 'Enter') { event.preventDefault(); pick(shown[at]); }
  };

  return (
    <QuickPanel open={open} onClose={close} title={t('workflow.edit.picker.title')} initialFocus="[data-picker-search]">
      <div className="grid min-h-0 gap-2 p-3">
        <p className="m-0 text-small text-ink-soft">{place}</p>
        <input
          data-picker-search=""
          role="combobox"
          aria-expanded
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={shown.length > 0 ? optionId(at) : undefined}
          aria-label={t('workflow.edit.picker.search')}
          placeholder={t('workflow.edit.picker.search')}
          value={query}
          onChange={(event) => { setQuery(event.target.value); setActive(0); }}
          onKeyDown={onKeyDown}
          className="min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
        />
      </div>
      <div id={listId} role="listbox" aria-label={t('workflow.edit.picker.title')} className="min-h-0 overflow-y-auto px-1.5 pb-2">
        {shown.length === 0 && <p className="m-0 px-2 py-1.5 text-small text-ink-soft">{t('workflow.edit.picker.none')}</p>}
        {shown.map((each, index) => (
          <div key={each.choice.kind}>
            {(index === 0 || shown[index - 1]!.group !== each.group) && (
              <p aria-hidden className="m-0 px-2 pb-0.5 pt-2 text-meta font-semibold text-ink-faint">{each.group}</p>
            )}
            <div
              id={optionId(index)}
              role="option"
              aria-selected={index === at}
              onMouseDown={(event) => event.preventDefault()}
              onClick={() => pick(each)}
              onMouseEnter={() => setActive(index)}
              className={cn('cursor-pointer rounded-control px-2 py-1', index === at ? 'bg-raised text-ink' : 'text-ink')}
            >
              <span className="block text-body [overflow-wrap:anywhere]">{each.title}</span>
              {each.about && <span className="block text-meta text-ink-soft [overflow-wrap:anywhere]">{each.about}</span>}
            </div>
          </div>
        ))}
      </div>
    </QuickPanel>
  );
}
