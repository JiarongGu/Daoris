import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { OpinionGate } from './OpinionGate';
import { DETAIL, GATES, NOTHING } from './opinionFixtures';
import { FileOpener } from './preview';

const gate = () => screen.getByRole('region', { name: 'Second opinion' });

/**
 * XAGENT1g (D155 points 8–10; the second-agent design §8.5, §9): *Second opinion* in each state of §8.5's table, its findings
 * beside the working session's answers, and each press: one ask for *Ask now*, *Try again* and *Ask again*, *Stop…* asking once,
 * and *Go on anyway…*, *I looked myself…* and *Send back…* taking the person's words.
 */
describe('the second opinion’s gate', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says a later step of the chain still works here, and offers to ask now or go on anyway', () => {
    render(<OpinionGate gate={GATES.waitsChain} acts={{ ask: () => {}, anyway: () => {} }} />);

    expect(gate()).toHaveTextContent('awaits the chain');
    expect(gate()).toHaveTextContent("quest #q7 still works here, so one opinion reads the chain's whole work");
    expect(screen.getByRole('button', { name: 'Ask now' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Go on anyway…' })).toBeInTheDocument();
  });

  it('asks now with one press, and holds its presses while it is on its way', async () => {
    const ask = vi.fn();
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.notAsked} acts={{ ask }} />);

    await user.click(screen.getByRole('button', { name: 'Ask now' }));

    expect(ask).toHaveBeenCalledWith(expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
    expect(screen.getByRole('button', { name: 'Asking…' })).toBeDisabled();
  });

  it('says who reads it and for how long, offers its session and a stop that asks once', async () => {
    const stop = vi.fn();
    const openSession = vi.fn();
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.reading} detail={{ tier: 'agent', first: { ...DETAIL.first!, findings: null } }} acts={{ stop, openSession }} />);

    expect(gate()).toHaveTextContent('Being read by Codex (OpenAI), another maker\'s agent, for at most 20 minutes.');
    await user.click(screen.getByRole('button', { name: 'Open its session' }));
    expect(openSession).toHaveBeenCalledWith('r7c1');

    await user.click(screen.getByRole('button', { name: 'Stop…' }));
    expect(stop).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Stop reading' }));
    expect(stop).toHaveBeenCalled();
  });

  it('shows a dispute’s findings open, each beside its answer, and says Accept answers them where it is coming', () => {
    const opened = vi.fn();
    render(
      <FileOpener.Provider value={opened}>
        <OpinionGate gate={GATES.disputed} detail={DETAIL} pressComing working="s42" acts={{ anyway: () => {}, sendBack: () => {}, ask: () => {} }} />
      </FileOpener.Provider>,
    );

    expect(gate()).toHaveTextContent('1 finding by Codex (OpenAI) is disputed');
    expect(gate()).toHaveTextContent('Your Accept, or your Reviewed where a look is required, answers them.');
    expect(gate()).toHaveTextContent('The last row of each page is dropped');
    expect(gate()).toHaveTextContent('Rejected by the working session: The bound is exclusive on purpose');
    expect(gate()).toHaveTextContent('The recheck says it stands');
    expect(gate()).toHaveTextContent('Fixed at 9e8d7c6b, by the working session\'s account');
    expect(gate()).toHaveTextContent('What it read');
    expect(gate()).toHaveTextContent('Read by another agent: its findings are claims, not facts.');
    // A press is coming, so Go on anyway… is not offered here.
    expect(screen.queryByRole('button', { name: 'Go on anyway…' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Send back…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ask again' })).toBeInTheDocument();

    screen.getByRole('button', { name: 'src/catalog/page.ts:42' }).click();
    expect(opened).toHaveBeenCalledWith({ path: 'src/catalog/page.ts', lines: { from: 42, to: 42 } });
  });

  it('answers a dispute here with the person’s words where no press is coming', async () => {
    const anyway = vi.fn();
    const user = userEvent.setup();
    render(<OpinionGate gate={{ ...GATES.disputed, answers: null }} detail={DETAIL} acts={{ anyway }} />);

    expect(gate()).toHaveTextContent('No press of yours is coming, since the work lands by itself');
    await user.click(screen.getByRole('button', { name: 'Go on anyway…' }));
    expect(screen.getByText(/1 disputed finding stays so/)).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', { name: 'why, if you want to say' }), 'I read the bound myself');
    await user.click(screen.getByRole('button', { name: 'Go on anyway' }));

    expect(anyway).toHaveBeenCalledWith('I read the bound myself', expect.objectContaining({ done: expect.any(Function) }));
  });

  it('sends back only the person’s words, to the working session', async () => {
    const sendBack = vi.fn();
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.disputed} detail={DETAIL} pressComing working="s42" acts={{ sendBack }} />);

    await user.click(screen.getByRole('button', { name: 'Send back…' }));
    expect(screen.getByText(/Your words go to session s42 as its next turn/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send back' })).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: 'what to put right' }), 'Add the test for a full page.');
    await user.click(screen.getByRole('button', { name: 'Send back' }));

    expect(sendBack).toHaveBeenCalledWith('Add the test for a full page.', expect.anything());
  });

  it('says none could be had and until when, and offers the four presses where the rule requires one', async () => {
    const myself = vi.fn();
    const sameAgent = vi.fn();
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.unavailableRequired} acts={{ ask: () => {}, sameAgent, myself, anyway: () => {} }} />);

    expect(gate()).toHaveTextContent('No second opinion: every listed reviewer of another maker is cooling until');
    expect(gate()).toHaveTextContent('The rule requires one, so the work waits for you.');
    expect(screen.getAllByRole('button').map((button) => button.textContent))
      .toEqual(['Try again', 'Ask the same agent, fresh', 'I looked myself…', 'Go on anyway…']);

    await user.click(screen.getByRole('button', { name: 'Ask the same agent, fresh' }));
    expect(sameAgent).toHaveBeenCalled();
  });

  it('says none could be had beside Accept where the rule does not require one, and offers nothing', () => {
    render(<OpinionGate gate={GATES.unavailable} detail={{ tier: 'none' }} acts={{ ask: () => {}, anyway: () => {} }} />);

    expect(gate()).toHaveTextContent('No second opinion: no listed reviewer of another maker is installed.');
    expect(gate()).toHaveTextContent('The rule does not require one, so nothing waits for it.');
    expect(gate()).toHaveTextContent('No agent could read this.');
    expect(screen.queryByRole('button', { name: /Try again|Go on anyway/ })).toBeNull();
  });

  it('says a pass that raised nothing raised nothing in what it read, never no issues', async () => {
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.settledNothing} detail={NOTHING} />);

    expect(gate()).toHaveTextContent('Settled: Codex (OpenAI) raised nothing in what it read.');
    expect(gate()).not.toHaveTextContent(/no issues/i);
    await user.click(screen.getByRole('button', { name: 'Show the findings' }));
    expect(gate()).toHaveTextContent('src/catalog/ and its tests');
  });

  it('says what the driver said where it could not read the gate', () => {
    render(<OpinionGate gate={GATES.unread} />);

    expect(gate()).toHaveTextContent('not read');
    expect(gate()).toHaveTextContent('The driver says: Whether this work waits for a second opinion could not be read: the service did not answer.');
  });

  it('says a refusal inside the gate, in the driver’s words', async () => {
    const user = userEvent.setup();
    render(<OpinionGate gate={GATES.notAsked} acts={{ ask: (answered) => answered.refused('no second opinion is asked for `web-app`.') }} />);

    await user.click(screen.getByRole('button', { name: 'Ask now' }));

    expect(screen.getByRole('alert')).toHaveTextContent('no second opinion is asked for web-app.');
  });

  it('words every state in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<OpinionGate gate={GATES.disputed} detail={DETAIL} pressComing working="s42" acts={{ sendBack: () => {}, ask: () => {} }} />);

    const region = screen.getByRole('region', { name: '第二意见' });
    expect(region).toHaveTextContent('有争议');
    expect(region).toHaveTextContent('Codex (OpenAI) 的 1 条发现有争议');
    expect(screen.getByRole('button', { name: '退回…' })).toBeInTheDocument();
  });
});
