import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import i18n from '../../locales/i18n';
import { code } from '../../test/code';
import { LIST, PALETTE } from '../edit/editFixtures';
import { FULL, GATES_CHANGED, INTEGRATION_CHANGED, START } from '../fixtures';
import { PlaceWorkflowView, type PlaceWorkflowProps } from './PlaceWorkflowView';

// AGENTSPLACE1h (the Agents-place design §3, §5): a repository place's Workflow draws the route and the chart with its start node, Gates
// with its two choices, Landing, and Change… and Edit steps…, which mount the FABRIC2 step editor. A molecule: every state is reached by its props.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const WAITING = { ...FULL, start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED } };
const SETTLED = { ...FULL, start: START };

const editor = (onOpen = vi.fn()) => ({
  list: LIST, palette: PALETTE, error: null, roles: [], shown: null, onShow: vi.fn(), judged: null, judging: false, onJudge: vi.fn(),
  saving: false, onSave: vi.fn(), using: false, onUse: vi.fn(async () => ({ done: true, refused: null, message: null, changed: true, reach: [] })), onOpen,
});

const draw = (props: Partial<PlaceWorkflowProps> = {}) => render(<PlaceWorkflowView name="engine" current={WAITING} {...props} />);

// The language goes back inside `act`, with the page still drawn (FRAMEACT1).
afterEach(async () => { await act(async () => { await i18n.changeLanguage('en'); }); });

describe("a repository place's Workflow", () => {
  it('draws the head, the route with its start node, the chart and Gates under one Workflow heading', () => {
    draw();

    expect(screen.getByRole('heading', { level: 2, name: 'Workflow' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Current workflow' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Where work here goes' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Gates' })).toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Before it lands' })).toBeInTheDocument();
  });

  it('draws the declaration once, in Gates, and not again on the landed route', () => {
    draw({ onAcceptGates: vi.fn(), onAcceptIntegration: vi.fn() });

    // The start node says where the route stands; the declaration and its press are Gates'.
    expect(document.querySelectorAll('pre')).toHaveLength(2);
    expect(within(screen.getByRole('heading', { name: 'Gates' }).closest('section')!).getAllByRole('checkbox')).toHaveLength(2);
    expect(screen.queryByRole('button', { name: 'Accept the gates' })).toBeNull();
  });

  it('presses the landed acceptance and the integration acceptance from the one confirmation', async () => {
    const onAcceptGates = vi.fn((answered: { done: () => void }) => answered.done());
    const onAcceptIntegration = vi.fn((_request: unknown, answered: { done: () => void }) => answered.done());
    draw({ onAcceptGates, onAcceptIntegration });

    await userEvent.click(screen.getByRole('button', { name: 'Accept the chosen…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));

    expect(onAcceptGates).toHaveBeenCalledOnce();
    expect(onAcceptIntegration).toHaveBeenCalledWith({ gates: '0e5751c026e543b2e8ab2eb06099daa1d1e5df47' }, expect.anything());
  });

  it("keeps *Verify…* on the route, beside the landed route's standing", () => {
    draw({ current: SETTLED, onVerify: vi.fn() });
    expect(screen.getByRole('button', { name: 'Verify…' })).toBeInTheDocument();
  });

  it('draws the line and landing rows it is handed, and opens them from a chart step that names its setting', async () => {
    const landing = vi.fn((asked: 'work' | null) => <p data-testid="landing">{asked ?? 'folded'}</p>);
    draw({ landing });

    expect(screen.getByTestId('landing')).toHaveTextContent('folded');
    const step = screen.getAllByRole('listitem').find((item) => item.getAttribute('data-step') === 'look')!;
    await userEvent.click(within(step).getByRole('button', { name: 'Change in Setup' }));

    expect(screen.getByTestId('landing')).toHaveTextContent('work');
  });

  it('opens Change… and Edit steps… in place, each told so the editor reads wait for them', async () => {
    const onOpen = vi.fn();
    draw({ editor: editor(onOpen) });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    expect(onOpen).toHaveBeenLastCalledWith(true);
    expect(screen.getByRole('region', { name: 'Change what its work follows' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));
    expect(onOpen).toHaveBeenLastCalledWith(false);

    await userEvent.click(screen.getByRole('button', { name: 'Edit steps…' }));
    expect(onOpen).toHaveBeenLastCalledWith(true);
    expect(screen.getByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(onOpen).toHaveBeenLastCalledWith(false);
  });

  it("chooses a saved version for this repository through Change…, the editor's own press", async () => {
    const bridge = editor();
    draw({ editor: bridge });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Use Current here' }));

    expect(bridge.onUse).toHaveBeenCalledWith({ repository: 'engine', workflow: 'current' });
  });

  it("starts the step editor from a preset's steps, from Change…", async () => {
    draw({ editor: editor() });

    await userEvent.click(screen.getByRole('button', { name: 'Change…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Edit steps from Documents workflow' }));

    expect(screen.getByRole('heading', { name: 'Compose a workflow' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Change what its work follows' })).toBeNull();
  });

  it("says the terminal's way where no editor is handed", () => {
    draw();
    expect(screen.queryByRole('button', { name: 'Change…' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit steps…' })).toBeNull();
    expect(screen.getByText(code('daoris driver workflow new <id> --from <default>'))).toBeInTheDocument();
  });

  it('says it is reading, and a refusal in the driver\'s words, in place of the body', () => {
    const { unmount } = draw({ current: null, reading: true });
    expect(screen.getByRole('heading', { level: 2, name: 'Workflow' })).toBeInTheDocument();
    expect(screen.getByText('Reading how work moves here…')).toBeInTheDocument();
    unmount();

    draw({ current: null, refusal: 'the driver is still coming up — try again in a moment.' });
    expect(screen.getByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();
  });

  it('says its heading in Chinese', async () => {
    await act(async () => { await i18n.changeLanguage('zh'); });
    draw();
    expect(screen.getByRole('heading', { level: 2, name: '工作流' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '检查' })).toBeInTheDocument();
  });
});
