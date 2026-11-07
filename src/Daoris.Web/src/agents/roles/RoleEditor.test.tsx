import * as Tooltip from '@radix-ui/react-tooltip';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactElement, useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { type Editing, RoleEditor } from './RoleEditor';

// One role editor serves a place, All roles and a step. Its choices come first, with the agent's offers or
// a typed model; its prose stands behind one ⓘ and one sentence of consequence before Save; a shared role names its works; a refusal is
// the driver's sentence under the form.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const EDITING: Editing = {
  id: 'engine-build', isNew: false, revision: 3, name: 'Engine build', nameIn: [['zh', '引擎开发']], takes: ['build'], work: 'build',
  place: 'repository', placeId: 'engine', drafts: [{ key: 1, agent: 'claude-code', model: 'sonnet', effort: 'high' }],
};

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];
const WORKS = [{ value: 'build', label: 'building' }, { value: 'design', label: 'design and decisions' }];

type Props = Partial<Parameters<typeof RoleEditor>[0]>;

function Held({ start = EDITING, ...props }: Props & { start?: Editing }) {
  const [editing, setEditing] = useState<Editing>(start);
  return (
    <RoleEditor
      editing={editing} onEditing={(change) => setEditing((now) => (typeof change === 'function' ? change(now) : change))}
      title="Engine build" agents={AGENTS} offers={{}} works={WORKS} busy={false} held={false}
      placed="Placed in repository engine; the place is fixed and the id is made for you."
      consequence="Saving puts this in force for new work; running work keeps its choices."
      conversing={false} mayOff={false} isOff={false} mayClear={false} refused={null}
      onSave={() => {}} onClear={() => {}} onCancel={() => {}} {...props}
    />
  );
}

describe('The role editor', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('puts its prose behind one ⓘ and says one sentence of consequence before Save', () => {
    render(<Held />);
    const form = screen.getByRole('form', { name: 'Engine build' });
    const notes = within(form).getAllByRole('note');
    expect(notes).toHaveLength(1);
    expect(notes[0]).toHaveAccessibleName(/Choice 2 runs only when no account of choice 1 can/);
    expect(notes[0]).toHaveAccessibleName(/Running work never moves to another choice\./);
    expect(within(form).queryByText(/Choice 2 runs only when/)).toBeNull();
    const consequence = within(form).getByText('Saving puts this in force for new work; running work keeps its choices.');
    const save = within(form).getByRole('button', { name: 'Save' });
    expect(consequence.compareDocumentPosition(save) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('keeps the choices in front of the folded Edit role', () => {
    render(<Held />);
    const form = screen.getByRole('form', { name: 'Engine build' });
    const choice = within(form).getByRole('combobox', { name: 'choice 1\'s agent' });
    const fold = within(form).getByText('Edit role');
    expect(choice.compareDocumentPosition(fold) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(fold.closest('details')).not.toHaveAttribute('open');
  });

  it('types a model the agent does not offer and hands it on', async () => {
    const onSave = vi.fn();
    render(<Held onSave={onSave} />);
    const form = screen.getByRole('form', { name: 'Engine build' });
    const model = within(form).getByRole('textbox', { name: "choice 1's model" });
    await userEvent.clear(model);
    await userEvent.type(model, 'gpt-x');
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenCalledWith(false);
    expect(model).toHaveValue('gpt-x');
  });

  it('names every work a shared role takes instead of offering one to change', () => {
    render(<Held start={{ ...EDITING, takes: ['build', 'design'] }} />);
    const form = screen.getByRole('form', { name: 'Engine build' });
    expect(within(form).getByText('It takes building and design and decisions; one edit covers them all.')).toBeTruthy();
    expect(within(form).queryByRole('combobox', { name: 'The work it takes' })).toBeNull();
  });

  it('says the driver\'s refusal under the form and holds Save while a press is out', () => {
    render(<Held refused="`engine-build` already takes `build` in that scope: at most one role takes a work in one scope." busy />);
    const form = screen.getByRole('form', { name: 'Engine build' });
    expect(within(form).getByText(/at most one role takes a work in one scope/)).toBeTruthy();
    expect(within(form).getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('is in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<Held title="引擎开发" consequence="保存后立即生效。" />);
    const form = screen.getByRole('form', { name: '引擎开发' });
    expect(within(form).getAllByRole('note')).toHaveLength(1);
    expect(within(form).getByText('保存后立即生效。')).toBeTruthy();
  });
});
