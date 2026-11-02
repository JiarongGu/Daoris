import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { type ReactNode, useEffect, useState } from 'react';
import type { PlaceView } from '../../agents/setup/placeShell';
import { registerAskFill } from '../../help/askFill';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { FoldRow } from '../../projects/setup/FoldRow';
import { RepositorySetup, FOLD_SECTIONS } from '../../projects/setup/RepositorySetup';
import { LIST, PALETTE } from '../edit/editFixtures';
import { WorkspaceLandingFolds } from '../../agents/setup/PlaceSessions';
import { EFFECTIVE_CURRENT, EFFECTIVE_LANDED_VERIFIES, EFFECTIVE_RELEASE } from './effectiveFixtures';
import { FULL, GATES_CHANGED, INTEGRATION_CHANGED, START, WORKSPACE } from '../fixtures';
import { answerFor, CURRENT_ANSWER, PLACE_SETUP, SAVED_ROUTES } from './placeWorkflowFixtures';
import { PlaceWorkflowView, type PlaceWorkflowProps } from './PlaceWorkflowView';

// A place's Workflow (AGENTSPLACE1h, SETUPUX1o; the Setup design's §2.3), every state drawn from props at the main area's 80, 39 and 25 rem, in
// 中文 and in dark: the routes as a selection, the chosen route's chart (in a column of its own at 80 rem, one press away at 39 and 25), a
// step's cell on selection, the folds with their owners, Gates waiting, landed work with no verification and with it, and a workspace's.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const editor = {
  list: LIST, palette: PALETTE, error: null, roles: [], shown: null, onShow: nothing, judged: null, judging: false, onJudge: nothing,
  saving: false, onSave: async () => { throw new Error('a story saves nothing'); }, using: false,
  onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
};

/** The section as the application holds it: the route and the step are the shell's, here a story's own. */
function Place({ first, route, landingOpen, ...props }: Partial<PlaceWorkflowProps> & {
  first?: Partial<PlaceView>; route?: (route: string) => PlaceWorkflowProps['current']; landingOpen?: boolean;
}) {
  const [view, setView] = useState<PlaceView>({ route: 'default', step: null, ...first });
  return (
    <PlaceWorkflowView
      name="engine"
      current={route ? route(view.route) : CURRENT_ANSWER}
      view={view}
      onView={(choice) => setView((was) => ({
        route: choice.route ?? was.route,
        step: choice.route && choice.route !== was.route ? choice.step ?? null : choice.step === undefined ? was.step : choice.step,
      }))}
      onAcceptGates={(answered) => answered.done()}
      onAcceptIntegration={(_request, answered) => answered.done()}
      onVerify={(_ask, answered) => answered.done()}
      editor={editor}
      landing={(hand) => (
        <RepositorySetup
          {...PLACE_SETUP}
          only={FOLD_SECTIONS}
          lead={hand.lead}
          lands={hand.lands}
          gatesApart
          open={hand.asked ?? (landingOpen ? 'landing' : undefined)}
          openAgain={hand.again}
        />
      )}
      {...props}
    />
  );
}

const meta: Meta<typeof Place> = {
  title: 'Workflow/PlaceWorkflowView',
  component: Place,
  args: { route: answerFor },
  // A place's page in the Agents place's main area, at the frame's wide width.
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Place>;

/**
 * **A saved default**: default work follows the saved release workflow, so its own graph is drawn and the label says which; the chart stands
 * in its own column at 80 rem and is one press away at 39 and 25.
 */
export const SavedDefault: Story = {};
export const SavedDefaultHalf: Story = { decorators: [storyWidths.half] };
export const SavedDefaultNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, at each width. */
export const SavedDefaultChinese: Story = { decorators: [chinese] };
export const SavedDefaultChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const SavedDefaultChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** The same in dark, at each width. */
export const SavedDefaultDark: Story = { decorators: [dark] };
export const SavedDefaultDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const SavedDefaultDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** **Current**: nothing names a workflow for default work, so it follows its old landing rule, with the conversion's door beside the routes. */
export const CurrentDefault: Story = { args: { route: undefined, current: CURRENT_ANSWER, onConvert: nothing } };
export const CurrentDefaultHalf: Story = { ...CurrentDefault, decorators: [storyWidths.half] };
export const CurrentDefaultNarrowChinese: Story = { ...CurrentDefault, decorators: [storyWidths.narrow, chinese] };
export const CurrentDefaultDark: Story = { ...CurrentDefault, decorators: [dark] };

/**
 * **The Landing row under a saved version** (WORKFLOWCLEAN1h-b): the version's own `land` options, saved as its next version once the places it
 * reaches are let follow it. And **under Current**: the rule as it stands, with the conversion and no editor.
 */
export const LandingByVersion: Story = { args: { landingOpen: true } };
export const LandingByVersionHalf: Story = { ...LandingByVersion, decorators: [storyWidths.half] };
export const LandingByVersionNarrow: Story = { ...LandingByVersion, decorators: [storyWidths.narrow] };
export const LandingByVersionChinese: Story = { ...LandingByVersion, decorators: [chinese] };
export const LandingByVersionChineseNarrow: Story = { ...LandingByVersion, decorators: [storyWidths.narrow, chinese] };
export const LandingByVersionDark: Story = { ...LandingByVersion, decorators: [dark] };
export const LandingUnderCurrent: Story = { args: { route: undefined, current: CURRENT_ANSWER, onConvert: nothing, landingOpen: true } };
export const LandingUnderCurrentHalf: Story = { ...LandingUnderCurrent, decorators: [storyWidths.half] };
export const LandingUnderCurrentNarrow: Story = { ...LandingUnderCurrent, decorators: [storyWidths.narrow] };
export const LandingUnderCurrentChinese: Story = { ...LandingUnderCurrent, decorators: [chinese] };
export const LandingUnderCurrentDark: Story = { ...LandingUnderCurrent, decorators: [dark] };

/** **A copy behind the default this build ships**: *Daoris ships a newer … · Update…*, a plan of one that appends the shipped graph. */
const behind = (route: string): PlaceWorkflowProps['current'] => {
  const answer = answerFor(route);
  return answer && answer.effective ? { ...answer, effective: { ...answer.effective, newer: { default: 'merge-after-accept', name: 'Merge workflow', graph: answer.effective.graph } } } : answer;
};
export const ShippedUpdate: Story = { args: { route: behind } };
export const ShippedUpdateHalf: Story = { ...ShippedUpdate, decorators: [storyWidths.half] };
export const ShippedUpdateNarrowChinese: Story = { ...ShippedUpdate, decorators: [storyWidths.narrow, chinese] };
export const ShippedUpdateDark: Story = { ...ShippedUpdate, decorators: [dark] };

/** **A kind**: Documentation work follows its own saved workflow, chosen by the workspace. */
export const DocumentsRoute: Story = { args: { first: { route: 'kind:docs' } } };
export const DocumentsRouteNarrowChinese: Story = { ...DocumentsRoute, decorators: [storyWidths.narrow, chinese] };

/** **A step selected**: its cell shows under the chart, with what it is set to, where that was set and its door to the setting. */
export const StepSelected: Story = { args: { route: undefined, current: CURRENT_ANSWER, first: { step: 'landing' } } };
export const StepSelectedHalf: Story = { ...StepSelected, decorators: [storyWidths.half] };

/** **Landed work with no verification**: *No verification here*, and no chart to draw. */
export const LandedNoVerification: Story = { args: { first: { route: 'landed' } } };
export const LandedNoVerificationNarrow: Story = { ...LandedNoVerification, decorators: [storyWidths.narrow] };

/** **Landed work verified**: the verification workflow's graph, its gates accepted as the tip holds them, and *Verify…* once they are. */
export const LandedVerified: Story = {
  args: {
    first: { route: 'landed' },
    route: () => ({ ...FULL, start: START, effective: EFFECTIVE_LANDED_VERIFIES }),
  },
};
export const LandedVerifiedHalf: Story = { ...LandedVerified, decorators: [storyWidths.half] };

/** **Both acceptances waiting**: the landed gates and the integration declaration changed since they were accepted, so Gates opens on both choices. */
export const GatesWaiting: Story = {
  args: { route: undefined, current: { ...FULL, start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED }, effective: EFFECTIVE_CURRENT } },
};
export const GatesWaitingHalf: Story = { ...GatesWaiting, decorators: [storyWidths.half] };
export const GatesWaitingNarrowChineseDark: Story = { ...GatesWaiting, decorators: [storyWidths.narrow, chinese, dark] };

/** **A workspace's own Workflow**: its routes and chart, and the repositories that follow it or set their own; Gates stay each repository's. */
export const WorkspaceWorkflow: Story = {
  args: {
    name: 'aurora',
    page: 'workspace',
    route: () => ({ ...WORKSPACE, start: { ...SAVED_ROUTES, integration: null }, effective: { ...EFFECTIVE_CURRENT } }),
    landing: undefined,
    doors: { repository: nothing },
  },
};
export const WorkspaceWorkflowHalf: Story = { ...WorkspaceWorkflow, decorators: [storyWidths.half] };

/**
 * **A workspace's Landing row** (WORKFLOWCLEAN1h-c): the same row a repository has, open here. Under a saved version, the version's `land` options;
 * under Current, the old rule as it stands, with the conversion and no editor.
 */
const workspaceLanding: Pick<PlaceWorkflowProps, 'name' | 'page' | 'doors' | 'landing' | 'onConvert'> = {
  name: 'aurora',
  page: 'workspace',
  doors: { repository: nothing },
  onConvert: nothing,
  landing: (hand) => (
    <WorkspaceLandingFolds
      workspace="aurora"
      hand={{ ...hand, asked: 'landing' }}
      landing={{ repository: '', workspace: 'aurora', source: 'workspace', form: 'branch', pattern: 'feature/{quest}-{slug}' }}
      unread={null}
    />
  ),
};
export const WorkspaceLandingByVersion: Story = {
  args: { ...workspaceLanding, route: () => ({ ...WORKSPACE, start: { ...SAVED_ROUTES, integration: null }, effective: EFFECTIVE_RELEASE }) },
};
export const WorkspaceLandingByVersionHalf: Story = { ...WorkspaceLandingByVersion, decorators: [storyWidths.half] };
export const WorkspaceLandingByVersionNarrow: Story = { ...WorkspaceLandingByVersion, decorators: [storyWidths.narrow] };
export const WorkspaceLandingByVersionChinese: Story = { ...WorkspaceLandingByVersion, decorators: [chinese] };
export const WorkspaceLandingByVersionChineseNarrow: Story = { ...WorkspaceLandingByVersion, decorators: [storyWidths.narrow, chinese] };
export const WorkspaceLandingByVersionDark: Story = { ...WorkspaceLandingByVersion, decorators: [dark] };
export const WorkspaceLandingUnderCurrent: Story = {
  args: { ...workspaceLanding, route: () => ({ ...WORKSPACE, start: { ...SAVED_ROUTES, integration: null }, effective: { ...EFFECTIVE_CURRENT } }) },
};
export const WorkspaceLandingUnderCurrentHalf: Story = { ...WorkspaceLandingUnderCurrent, decorators: [storyWidths.half] };
export const WorkspaceLandingUnderCurrentNarrow: Story = { ...WorkspaceLandingUnderCurrent, decorators: [storyWidths.narrow] };
export const WorkspaceLandingUnderCurrentChinese: Story = { ...WorkspaceLandingUnderCurrent, decorators: [chinese] };
export const WorkspaceLandingUnderCurrentChineseNarrow: Story = { ...WorkspaceLandingUnderCurrent, decorators: [storyWidths.narrow, chinese] };
export const WorkspaceLandingUnderCurrentDark: Story = { ...WorkspaceLandingUnderCurrent, decorators: [dark] };

/** The answer on its way, and refused in the driver's sentence. */
export const Reading: Story = { args: { route: undefined, current: null, reading: true } };
export const Refused: Story = {
  args: { route: undefined, current: null, refusal: 'the driver is still coming up — its service is not answering yet. A moment.' },
};

/** A window with Ask Daoris in it, so the section offers *Ask Daoris* beside *Edit steps…* (FLOWCHART2g). */
function WithAsk({ children }: { children: ReactNode }) {
  useEffect(() => registerAskFill(nothing), []);
  return <>{children}</>;
}
const asking: Decorator = (Story) => <WithAsk><Story /></WithAsk>;

/** **With Ask Daoris in the window**: *Ask Daoris* opens *Edit steps…* on the chart's graph and puts the start of a request in its box. */
export const WithAskDaoris: Story = { decorators: [storyWidths.wide, asking] };
export const WithAskDaorisHalf: Story = { decorators: [storyWidths.half, asking] };
export const WithAskDaorisNarrow: Story = { decorators: [storyWidths.narrow, asking] };
export const WithAskDaorisChinese: Story = { decorators: [storyWidths.wide, chinese, asking] };
export const WithAskDaorisChineseNarrowDark: Story = { decorators: [storyWidths.narrow, chinese, dark, asking] };

const askAndWait: Story['play'] = async ({ canvasElement }) => {
  const press = [...canvasElement.querySelectorAll<HTMLElement>('button')].find((each) => /^(Ask Daoris|问道衍)$/.test(each.textContent ?? ''));
  press?.click();
  await new Promise((resolve) => setTimeout(resolve, 120));
};

/** **Ask Daoris pressed**: the editor open on the chart's graph, which the conversation is told with the next words sent. */
export const AskedDaoris: Story = { decorators: [storyWidths.wide, asking], play: askAndWait };
export const AskedDaorisHalf: Story = { decorators: [storyWidths.half, asking], play: askAndWait };
export const AskedDaorisNarrow: Story = { decorators: [storyWidths.narrow, asking], play: askAndWait };

/** No editor handed (a shell before the step editor): the section says the terminal's way alone. */
export const WithoutTheEditor: Story = { args: { editor: undefined, landing: (hand) => <FoldRow folds={hand.lead} /> } };
