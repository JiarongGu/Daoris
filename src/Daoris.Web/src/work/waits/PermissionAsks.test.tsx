import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { boundMinutes, heldFor, permissionSaid, PermissionAsks, type PermissionWait } from './PermissionAsks';

// AUTONOMY1b3c (D184 point 3 and its AUTONOMY1b3a note): a tool's permission request a session holds for the person under asking,
// answered where they talk to it, as the console asks its person. One press per choice the agent offered, by its own name: this
// call's allow and a refusal, never an allow nobody offered. A molecule: the frame holds the answer and its pending.

// Held three minutes before the tests run; a few seconds' run still reads three. Its bound is the driver's answer for it, `until`
// (PERMUNTIL1), so the card says the route's minutes: 15 from its start, as the driver's `HeldPermissions.Bound` is.
const HELD = Date.now() - 3 * 60_000 - 10_000;
const SINCE = new Date(HELD).toISOString();
const UNTIL = new Date(HELD + 15 * 60_000).toISOString();

const wait = (over: Partial<PermissionWait> = {}): PermissionWait => ({
  session: 's1a2b3c4', request: '900', call: 'tc-1', title: 'npm test --workspace engine', kind: 'execute',
  choices: [{ id: 'allow', name: 'Allow', kind: 'allow_once' }, { id: 'reject', name: 'Reject', kind: 'reject_once' }],
  since: SINCE, until: UNTIL, ...over,
});

const answered = { done: expect.any(Function), refused: expect.any(Function) };

afterEach(cleanup);

describe('a permission a session holds for the person', () => {
  it('shows the call as the agent named it, its kind, how long it waited and the bound, and a press per choice', () => {
    render(<PermissionAsks waits={[wait()]} onAnswer={vi.fn()} />);
    const item = screen.getByRole('listitem');
    expect(item.querySelector('code')).toHaveTextContent('npm test --workspace engine');
    expect(item).toHaveTextContent('A tool call of kind execute');
    expect(item).toHaveTextContent('waiting 3m');
    expect(item).toHaveTextContent('refused if nobody answers within 15 minutes');
    expect(within(item).getAllByRole('button').map((button) => button.textContent)).toEqual(['Allow once', 'Refuse']);
  });

  it('says the bound the driver answered for the request, and none where it said none (PERMUNTIL1)', () => {
    render(<PermissionAsks waits={[
      wait({ request: '1', until: new Date(HELD + 7 * 60_000).toISOString() }),
      wait({ request: '2', until: null }),
    ]} />);
    const [answered, unsaid] = screen.getAllByRole('listitem');
    expect(answered).toHaveTextContent('waiting 3m · refused if nobody answers within 7 minutes');
    expect(unsaid).toHaveTextContent('waiting 3m');
    expect(unsaid).not.toHaveTextContent('refused');
    expect(boundMinutes({ since: SINCE, until: UNTIL })).toBe(15);
    expect(boundMinutes({ since: SINCE, until: 'later' })).toBeNull();
    expect(boundMinutes({ since: UNTIL, until: SINCE })).toBeNull();
  });

  it('answers with the option the person pressed, by the agent’s id for it', async () => {
    const onAnswer = vi.fn((_wait: PermissionWait, _option: string, told: { done: () => void }) => told.done());
    render(<PermissionAsks waits={[wait()]} onAnswer={onAnswer} />);
    await userEvent.click(screen.getByRole('button', { name: 'Allow once' }));
    expect(onAnswer).toHaveBeenCalledWith(wait(), 'allow', answered);
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(onAnswer).toHaveBeenLastCalledWith(wait(), 'reject', answered);
  });

  it('offers no allow where the agent offered none, and says a choice of a kind it does not know by the agent’s name', () => {
    render(
      <PermissionAsks
        waits={[wait({ choices: [{ id: 'never', name: 'Reject always', kind: 'reject_always' }, { id: 'later', name: 'Ask me later', kind: 'snooze' }] })]}
        onAnswer={vi.fn()}
      />,
    );
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Always refuse', 'Ask me later']);
    expect(screen.queryByRole('button', { name: /allow/i })).toBeNull();
  });

  it('names an untitled call by its kind, and one with neither as a tool call', () => {
    render(<PermissionAsks waits={[wait({ request: '1', title: null }), wait({ request: '2', title: null, kind: null })]} onAnswer={vi.fn()} />);
    const [first, second] = screen.getAllByRole('listitem');
    expect(first).toHaveTextContent('An untitled execute tool call');
    expect(first).not.toHaveTextContent('of kind');
    expect(second).toHaveTextContent('An untitled tool call');
  });

  it('holds its presses while an answer is on its way, and says a refused answer beside them', async () => {
    const { rerender } = render(<PermissionAsks waits={[wait()]} onAnswer={(_wait, _option, told) => told.refused('It was already answered.')} />);
    await userEvent.click(screen.getByRole('button', { name: 'Allow once' }));
    expect(screen.getByRole('alert')).toHaveTextContent('It was already answered.');
    expect(screen.getByRole('button', { name: 'Allow once' })).toBeEnabled();

    rerender(<PermissionAsks waits={[wait()]} onAnswer={vi.fn()} busy />);
    expect(screen.getByRole('button', { name: 'Allow once' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Refuse' })).toBeDisabled();
  });

  it('offers no press where nothing can answer it, a browser or a teammate’s record', () => {
    render(<PermissionAsks waits={[wait()]} />);
    expect(screen.getByRole('listitem')).toHaveTextContent('npm test --workspace engine');
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('draws the session’s own, and says each refusal of an answer by its code, or in the driver’s words', () => {
    expect(heldFor([wait(), wait({ session: 'o7h3r000', request: '1' })], 's1a2b3c4').map((one) => one.request)).toEqual(['900']);
    const t = i18n.getFixedT('en');
    expect(permissionSaid(t, 'unheld', 'x')).toBe('It no longer waits: it was answered or released first. Nothing was answered.');
    expect(permissionSaid(t, 'unoffered', 'x')).toBe('The agent did not offer that answer. Nothing was answered.');
    expect(permissionSaid(t, 'later-code', 'The driver’s own sentence.')).toBe('The driver’s own sentence.');
  });

  it('speaks the active catalogue', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(
        <PermissionAsks
          waits={[wait({ choices: [...wait().choices, { id: 'never', name: 'Reject always', kind: 'reject_always' }] })]}
          onAnswer={vi.fn()}
        />,
      );
      const item = screen.getByRole('listitem');
      expect(within(item).getAllByRole('button').map((button) => button.textContent)).toEqual(['允许这一次', '拒绝', '始终拒绝']);
      expect(item).toHaveTextContent('execute 类工具调用');
      expect(item).toHaveTextContent('15 分钟内无人答复则拒绝');
      cleanup();
      render(<PermissionAsks waits={[wait({ until: null })]} />);
      expect(screen.getByRole('listitem')).toHaveTextContent('已等待 3 分钟');
      expect(permissionSaid(i18n.getFixedT('zh'), 'unheld', 'x')).toBe('它已不在等待：已先被答复或释放。未作答。');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
