import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Icon, Pill, Prose, SelectField } from '../../design/ui';
import { CheckField } from '../../design/atoms/FormControls';
import { PageSection } from '../../work/frame/ViewMain';
import type { FilesLook, FilesPress } from './filesLook';

const LISTS = ['deny', 'ask', 'allow'] as const;
const TONE = { deny: 'declined', ask: 'open', allow: 'done' } as const;

/**
 * **What Daoris would do in a tree, and the person's permissions**: each install, copy and share the effective rules would make with
 * the person's answer to it, an `ask` allowed in one press (the allow is written with who gave it), and the permissions the
 * person's file holds, each with its remove and *Add a permission* one press away.
 */
export function FilesPermissions({ look, onPress }: { look: FilesLook; onPress?: (press: FilesPress) => void }) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const [list, setList] = useState<(typeof LISTS)[number]>('ask');
  const [rule, setRule] = useState('');
  const [machine, setMachine] = useState(false);
  const held = look.permissions.flatMap((scope) => [
    ...scope.deny.map((one) => ({ scope: scope.scope, list: 'deny' as const, rule: one, by: null as string | null | undefined })),
    ...scope.ask.map((one) => ({ scope: scope.scope, list: 'ask' as const, rule: one, by: null as string | null | undefined })),
    ...scope.allow.map((one) => ({ scope: scope.scope, list: 'allow' as const, rule: one.rule, by: one.by })),
  ]);
  const allowed = new Set(held.filter((one) => one.list === 'allow').map((one) => one.rule));

  return (
    <PageSection title={t('files.view.acts')}>
      <div className="grid gap-3">
        {look.acts.length === 0
          ? <Prose className="text-small">{t('files.view.actsNone')}</Prose>
          : (
            <ul aria-label={t('files.view.acts')} className="m-0 grid list-none gap-1 p-0 text-small">
              {look.acts.map((act) => (
                <li key={`${act.act}:${act.path}`} className="flex min-w-0 flex-wrap items-center gap-2">
                  <span className="font-mono [overflow-wrap:anywhere]">{act.act}</span>
                  <Pill tone={act.verdict === 'made' ? 'done' : act.verdict === 'held' ? 'open' : 'declined'}>{t(`files.view.verdict.${act.verdict}`)}</Pill>
                  {onPress && act.verdict === 'held' && (
                    <Button
                      variant="ghost" className="border border-line px-2 py-0 text-meta" aria-label={t('files.view.allowNamed', { act: act.act })}
                      onClick={() => onPress({ act: 'permit', change: 'add', list: 'allow', rule: act.act })}
                    >
                      {t('files.view.allow')}
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}

        {held.length === 0
          ? <Prose className="text-small text-ink-soft">{t('files.view.permissionsNone')}</Prose>
          : (
            <ul aria-label={t('files.view.permissions')} className="m-0 grid list-none gap-1 p-0 text-small">
              {held.map((one) => (
                <li key={`${one.scope}:${one.list}:${one.rule}`} className="flex min-w-0 flex-wrap items-center gap-2 border-t border-line pt-1 first:border-t-0 first:pt-0">
                  <Pill tone={TONE[one.list]}>{t(`files.view.list.${one.list}`)}</Pill>
                  <span className="min-w-0 flex-1 font-mono [overflow-wrap:anywhere]">{one.rule}</span>
                  <span className="text-meta text-ink-faint">
                    {one.scope === 'machine' ? t('files.view.scope.machine') : t('files.view.scope.repository')}
                    {one.list === 'allow' && one.by ? ` · ${t('files.view.givenBy', { by: one.by === 'person' ? t('files.view.maker.person') : t('files.view.maker.role', { role: one.by }) })}` : ''}
                  </span>
                  {onPress && (
                    <Button
                      variant="ghost" aria-label={t(one.list === 'allow' ? 'files.view.takeBackNamed' : 'files.view.removeNamed', { rule: one.rule })}
                      onClick={() => onPress({ act: 'permit', change: 'remove', list: one.list, rule: one.rule, machine: one.scope === 'machine' })}
                    >
                      <Icon name="x" size={12} />
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}

        {onPress && !adding && (
          <div><Button variant="ghost" onClick={() => setAdding(true)}><Icon name="plus" size={13} />{t('files.view.addPermission')}</Button></div>
        )}
        {onPress && adding && (
          <form
            className="flex min-w-0 flex-wrap items-center gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              const text = rule.trim();
              if (!text || (list === 'allow' && allowed.has(text))) return;
              onPress({ act: 'permit', change: 'add', list, rule: text, machine });
              setRule('');
              setAdding(false);
            }}
          >
            <SelectField
              value={list} onChange={(value) => setList(value as (typeof LISTS)[number])} ariaLabel={t('files.view.listLabel')}
              options={LISTS.map((name) => ({ value: name, label: t(`files.view.list.${name}`) }))}
            />
            <input
              value={rule} autoFocus aria-label={t('files.view.ruleLabel')} placeholder="run(npm *)" onChange={(event) => setRule(event.target.value)}
              className="min-w-0 flex-1 rounded-control border border-line bg-raised px-2 py-1 font-mono text-small"
            />
            <CheckField checked={machine} onChange={setMachine} label={t('files.view.machine')} />
            <Button type="submit">{t('files.view.add')}</Button>
          </form>
        )}
      </div>
    </PageSection>
  );
}
