import { useTranslation } from 'react-i18next';
import type { QuestStep } from '../api';
import { type Carry, CarryFields, NO_CARRY, useCarry } from '../compose/carry';
import { Button, Drawer, EmptyState, Icon, Inline, SelectField } from '../ui';

/**
 * A quest being written. `links` is the text as typed — read into addresses at publish — and `files` are the
 * browser's own handles, read whole only when the quest is sent (D65 §2).
 */
export type QuestDraft = Carry & {
  from: string; to: string; title: string; body: string;
  /**
   * Its short title (SESSUX1j), optional: the person's few words that tell it apart in a list. Empty sends none, and the
   * service names the quest from its words.
   */
  short?: string;
  /** One next step (D65 §4), or none. The service takes a longer chain; the composer offers one. */
  step: QuestStep | null;
};

export const EMPTY_QUEST: QuestDraft = { from: '', to: '', title: '', body: '', short: '', step: null, ...NO_CARRY };

/** How long a short title may be (SESSUX1j), as the service judges it: characters, a Chinese one counted once. */
export const SHORT_TITLE_MAX = 40;

/** A short title's length as the service counts it: by character, never by UTF-16 unit. */
const characters = (text: string) => [...text.trim()].length;

/**
 * Whether a draft is whole enough to publish: a next step started is a next step owed, and a short title longer than
 * the service keeps is one it would refuse.
 */
export function draftReady(draft: QuestDraft): boolean {
  return Boolean(draft.from && draft.to && draft.title.trim() && draft.body.trim())
    && characters(draft.short ?? '') <= SHORT_TITLE_MAX
    && (draft.step === null || Boolean(draft.step.to && draft.step.title.trim() && draft.step.body.trim()));
}

/**
 * **The quest composer** (D41 §4): a form, so a drawer still (D118 §3d) — the screen twin of `quest_publish`.
 *
 * @remarks
 * **A molecule**: the draft is the caller's, and the press goes out; the carry's drop, paste and chooser are shared
 * with the ask composer (`compose/carry`), so the two cannot drift. Drawn only while composing, so what a drop left
 * off is forgotten with the drawer.
 *
 * - 🔴 **Nobody to ask is said, not offered**: with nothing registered, `from` and `to` offered nobody and a quest
 *   written in full could never be published (seen on the installed window, 2026-09-24).
 * - **Only what the host says can be asked is offered** (D70): the service still holds the judgement, and this only
 *   keeps the form from lying. A receiver that declared nothing earns the service's own caution, before the person
 *   relies on it.
 * - **A chain is behind a press** (D65 §4): most asks are one quest, and the form should not say otherwise.
 */
export function QuestComposer({ draft, onChange, receivers, nobody = false, caution = null, busy = false, onPublish, onCancel }: {
  draft: QuestDraft;
  onChange: (draft: QuestDraft) => void;
  /** Who can be named, on either side: the repositories the host says can be asked (D70). */
  receivers: string[];
  /** Known to be nobody, not merely not loaded yet: a composer that flashed "nobody" would be a lie. */
  nobody?: boolean;
  /** The receiver chosen has declared nothing, so this may not be its problem: its name, or null. */
  caution?: string | null;
  busy?: boolean;
  onPublish: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const carry = useCarry(draft, (next) => onChange({ ...draft, ...next }), true);
  const options = receivers.map((name) => ({ value: name, label: name }));
  const meta = <span className="font-mono text-meta text-ink-faint">{t('quests.compose.meta')}</span>;

  if (nobody) {
    return (
      <Drawer
        title={t('quests.compose.title')}
        onClose={onCancel}
        meta={meta}
        footer={<Button variant="ghost" onClick={onCancel}>{t('common.close')}</Button>}
      >
        <EmptyState icon="projects" headline={t('quests.compose.nobody.headline')} body={t('quests.compose.nobody.body')} />
      </Drawer>
    );
  }

  const ready = !busy && draftReady(draft);
  const step = draft.step;
  return (
    <Drawer
      title={t('quests.compose.title')}
      onClose={onCancel}
      meta={meta}
      footer={
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="primary" disabled={!ready} onClick={onPublish}>{t('quests.compose.publish')}</Button>
          <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
        </div>
      }
    >
      <form
        className="grid gap-3"
        onSubmit={(e) => { e.preventDefault(); if (ready) onPublish(); }}
        // The whole composer takes a drop and a paste (D65 §2) — aiming at a box inside a drawer is a chore, and
        // the box below lights up to say where the file went.
        {...carry.handlers}
      >
        <p className="m-0 text-body text-ink-soft">{t('quests.compose.hint')}</p>
        <div className="grid grid-cols-2 gap-2.5 max-md:grid-cols-1">
          <label className="grid gap-1 text-small text-ink-soft">
            {t('quests.compose.from')}
            <SelectField
              value={draft.from} required
              onChange={(from) => onChange({ ...draft, from })}
              placeholder={t('quests.compose.fromPlaceholder')}
              ariaLabel={t('quests.compose.from')}
              options={options}
            />
          </label>
          <label className="grid gap-1 text-small text-ink-soft">
            {t('quests.compose.to')}
            <SelectField
              value={draft.to} required
              onChange={(to) => onChange({ ...draft, to })}
              placeholder={t('quests.compose.toPlaceholder')}
              ariaLabel={t('quests.compose.to')}
              options={options}
            />
          </label>
        </div>
        <label className="grid gap-1 text-small text-ink-soft">
          {t('quests.compose.titleLabel')}
          <input
            required value={draft.title}
            onChange={(e) => onChange({ ...draft, title: e.target.value })}
            className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </label>
        {/* Its short title (SESSUX1j), optional and the person's own: what a list shows for it. Past the service's limit it
            says so here and holds the publish back, rather than sending a quest the service would refuse. */}
        <label className="grid gap-1 text-small text-ink-soft">
          {t('quests.field.short')}
          <input
            value={draft.short ?? ''}
            placeholder={t('quests.field.shortPlaceholder')}
            aria-invalid={characters(draft.short ?? '') > SHORT_TITLE_MAX || undefined}
            onChange={(e) => onChange({ ...draft, short: e.target.value })}
            className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink aria-[invalid=true]:border-st-declined"
          />
          {characters(draft.short ?? '') > SHORT_TITLE_MAX && (
            <span className="text-small text-ink-danger">{t('quests.field.shortTooLong', { count: SHORT_TITLE_MAX })}</span>
          )}
        </label>
        {caution && (
          /* The same caution the service gives an agent, before the person relies on it. */
          <p className="m-0 border-l-[3px] border-warn bg-raised px-3.5 py-2 text-body text-ink-soft">
            <Inline text={t('quests.compose.caution', { repository: caution })} />
          </p>
        )}
        <label className="grid gap-1 text-small text-ink-soft">
          {t('quests.compose.bodyLabel')}
          <textarea
            required value={draft.body}
            onChange={(e) => onChange({ ...draft, body: e.target.value })}
            className="min-h-28 resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
          />
        </label>
        <CarryFields
          carry={draft}
          filesLabel={t('quests.compose.filesLabel')}
          leftOff={carry.leftOff}
          dragging={carry.dragging}
          busy={busy}
          onLinks={(links) => onChange({ ...draft, links })}
          onAttach={carry.attach}
          onRemove={carry.remove}
        />
        {/* The chain (D65 §4): one next step, published by the service when this closes done. */}
        {step === null ? (
          <div>
            <Button type="button" variant="ghost" disabled={busy} onClick={() => onChange({ ...draft, step: { to: '', title: '', body: '' } })}>
              <Icon name="plus" size={12} />{t('quests.compose.addStep')}
            </Button>
          </div>
        ) : (
          <fieldset className="m-0 grid gap-2.5 rounded-control border border-line-strong px-3 pb-3 pt-2">
            <legend className="px-1 text-small text-ink-soft">{t('quests.compose.stepLegend')}</legend>
            <p className="m-0 text-small text-ink-faint">{t('quests.compose.stepHint')}</p>
            <label className="grid gap-1 text-small text-ink-soft">
              {t('quests.compose.stepTo')}
              <SelectField
                value={step.to}
                onChange={(to) => onChange({ ...draft, step: { ...step, to } })}
                placeholder={t('quests.compose.toPlaceholder')}
                ariaLabel={t('quests.compose.stepTo')}
                options={options}
              />
            </label>
            <label className="grid gap-1 text-small text-ink-soft">
              {t('quests.compose.stepTitle')}
              <input
                value={step.title}
                onChange={(e) => onChange({ ...draft, step: { ...step, title: e.target.value } })}
                className="min-h-[1.9rem] rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </label>
            <label className="grid gap-1 text-small text-ink-soft">
              {t('quests.compose.stepBody')}
              <textarea
                rows={3} value={step.body}
                onChange={(e) => onChange({ ...draft, step: { ...step, body: e.target.value } })}
                className="resize-y rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink"
              />
            </label>
            <div>
              <Button type="button" variant="ghost" disabled={busy} onClick={() => onChange({ ...draft, step: null })}>
                <Icon name="x" size={12} />{t('quests.compose.removeStep')}
              </Button>
            </div>
          </fieldset>
        )}
      </form>
    </Drawer>
  );
}
