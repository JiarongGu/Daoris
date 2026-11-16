import type { ReactElement } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { answerOf, GRAPH_RUN_GO_AHEAD, GRAPH_RUN_REQUESTS } from './runFixtures';
import { WorkflowRunView } from './WorkflowRunView';

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const steps = () => screen.getAllByRole('listitem').filter((item) => item.hasAttribute('data-step'));
const row = (id: string) => steps().find((item) => item.getAttribute('data-step') === id)!;
/** Opens a step's record, unless it is open already. */
const open = async (id: string) => {
  const details = within(row(id)).queryByRole('button', { name: /^Details of |的详情$/ });
  if (details) await userEvent.click(details);
};

// The requests the work's sessions made are drawn inside the work node, and a decline holds
// the work's requests waiting on the person, whose *Not needed this run…* is the run's existing skip.
describe("the work's requests", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws each quest the sessions published inside the work, and a helper beside them as a request to an agent', async () => {
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_REQUESTS)} />);
    await open('claimed');
    const requests = within(within(row('claimed')).getByRole('list', { name: 'Requests the work made' })).getAllByRole('listitem');
    expect(requests).toHaveLength(4);
    expect(requests[0]).toHaveTextContent('Quest #q8 to tools');
    expect(requests[0]).toHaveTextContent('declined');
    expect(requests[0]).toHaveTextContent('“not something this repository takes”');
    expect(requests[1]).toHaveTextContent('Quest #q7 to docs');
    expect(requests[1]).toHaveTextContent('done');
    expect(requests[1]).toHaveTextContent('“the page is in”');
    expect(requests[2]).toHaveTextContent('Quest #q6 to tools');
    expect(requests[2]).toHaveTextContent('taken');
    expect(requests[2]).toHaveTextContent('its session waits on the answer');
    expect(requests[3]).toHaveTextContent('Request to an agent: codex-acp');
    expect(requests[3]).toHaveTextContent('working');
    expect(within(row('requests')).queryByRole('list', { name: 'Requests the work made' })).toBeNull();
  });

  it('says a declined hold waits on you, offers Not needed this run…, and sends the run, the node and the words', async () => {
    const onNodeWord = vi.fn().mockResolvedValue({ run: 'q1', node: 'requests', act: 'skip', done: true });
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_REQUESTS)} onNodeWord={onNodeWord} />);
    expect(row('requests')).toHaveTextContent('waiting on acceptor: you');
    expect(row('requests')).toHaveTextContent('A quest the work\'s sessions published was declined: it waits on you');

    await userEvent.click(within(row('requests')).getByRole('button', { name: 'Not needed this run…' }));
    const ask = screen.getByRole('group', { name: 'requests: not needed this run' });
    await userEvent.type(within(ask).getByRole('textbox', { name: 'why it is not needed' }), 'tools will not take it; I do it myself');
    await userEvent.click(within(ask).getByRole('button', { name: 'Not needed this run' }));

    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'requests', act: 'skip', words: 'tools will not take it; I do it myself' });
  });

  // A declined request of the run's own offers the driver's two words, asked again with nothing to say, gone on without with words.
  it('offers a declined request Ask again… and Go on without it…, each sent as the run’s node word', async () => {
    const asked = answerOf(GRAPH_RUN_REQUESTS);
    const [only] = asked.runs;
    const steps = only.steps.map((step) => (step.node === 'work.requests' ? { ...step, acts: ['ask-again', 'go-on'] } : step));
    const onNodeWord = vi.fn().mockResolvedValue({ run: 'q1', node: 'requests', act: 'ask-again', done: true });
    render(<WorkflowRunView answer={{ ...asked, runs: [{ ...only, steps }] }} onNodeWord={onNodeWord} />);
    expect(within(row('requests')).queryByRole('button', { name: 'Not needed this run…' })).toBeNull();

    await userEvent.click(within(row('requests')).getByRole('button', { name: 'Ask again…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'Ask again for requests' })).getByRole('button', { name: 'Ask again' }));
    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'requests', act: 'ask-again', words: null });

    await userEvent.click(within(row('requests')).getByRole('button', { name: 'Go on without it…' }));
    const ask = screen.getByRole('group', { name: 'Go on without requests' });
    expect(within(ask).getByRole('button', { name: 'Go on' })).toBeDisabled();
    await userEvent.type(within(ask).getByRole('textbox', { name: 'why you go on without it' }), 'tools has no time');
    await userEvent.click(within(ask).getByRole('button', { name: 'Go on' }));
    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'requests', act: 'go-on', words: 'tools has no time' });
  });

  it.each([
    ['en', 'Send…', 'Send requests', 'Send'],
    ['zh', '发出…', '发出 requests', '发出'],
  ])('offers a ready request its Send, sent as the run’s node word, in %s', async (language, opens, title, meanIt) => {
    await i18n.changeLanguage(language);
    const asked = answerOf(GRAPH_RUN_REQUESTS);
    const [only] = asked.runs;
    const steps = only.steps.map((step) => (step.node === 'work.requests' ? { ...step, acts: ['send'] } : step));
    const onNodeWord = vi.fn().mockResolvedValue({ run: 'q1', node: 'requests', act: 'send', done: true });
    render(<WorkflowRunView answer={{ ...asked, runs: [{ ...only, steps }] }} onNodeWord={onNodeWord} />);

    await userEvent.click(within(row('requests')).getByRole('button', { name: opens }));
    await userEvent.click(within(screen.getByRole('group', { name: title })).getByRole('button', { name: meanIt }));
    expect(onNodeWord).toHaveBeenCalledWith({ run: 'q1', node: 'requests', act: 'send', words: null });
  });

  it('opens a quest request where a door is handed in, and draws no list for a run with none', async () => {
    const quest = vi.fn();
    const { unmount } = render(<WorkflowRunView answer={answerOf(GRAPH_RUN_REQUESTS)} doors={{ quest }} />);
    await open('claimed');
    await userEvent.click(within(row('claimed')).getByRole('button', { name: 'Open quest #q8' }));
    expect(quest).toHaveBeenCalledWith('q8');
    unmount();

    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_GO_AHEAD)} />);
    expect(screen.queryByRole('list', { name: 'Requests the work made' })).toBeNull();
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkflowRunView answer={answerOf(GRAPH_RUN_REQUESTS)} onNodeWord={vi.fn()} />);
    expect(row('requests')).toHaveTextContent('工作的请求');
    expect(row('requests')).toHaveTextContent('工作的会话发布的一个委托被谢绝了');
    await open('claimed');
    const requests = within(within(row('claimed')).getByRole('list', { name: '工作发出的请求' })).getAllByRole('listitem');
    expect(requests[0]).toHaveTextContent('发给 tools 的委托 #q8');
    expect(requests[0]).toHaveTextContent('已谢绝');
    expect(requests[2]).toHaveTextContent('它的会话在等回答');
    expect(requests[3]).toHaveTextContent('对智能体 codex-acp 的请求');
  });
});
