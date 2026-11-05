import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { FAILED_GATE, lineOf, NOT_NOW, RUNNING, UNKNOWN_HELD, VERIFIED, WAITING } from './lineFixtures';
import { VerifyRuns } from './VerifyRuns';

// A repository's verifications on its Workflow tab (FABRIC-PIPELINE1d, the pipeline design §4.4): each verdict a person meets, newest
// first; an unknown held for them with Run again and Not now; none yet, on its way and refused; 中文 and dark.

const held = () => Promise.resolve({ done: true });
const width = (rem: number): Decorator => (Story) => <div className="border border-line bg-page p-3" style={{ width: `${rem}rem` }}><Story /></div>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof VerifyRuns> = {
  title: 'Workflow/VerifyRuns',
  component: VerifyRuns,
  args: { answer: lineOf(UNKNOWN_HELD, VERIFIED, FAILED_GATE, RUNNING, WAITING, NOT_NOW), onHeld: held },
  decorators: [width(48)],
};
export default meta;

type Story = StoryObj<typeof VerifyRuns>;

export const EachVerdict: Story = {};
export const EachVerdictChinese: Story = { decorators: [chinese] };
export const EachVerdictChineseDark: Story = { decorators: [chinese, dark] };
export const AtANarrowColumn: Story = { decorators: [width(22)] };
export const AtANarrowColumnChinese: Story = { decorators: [width(22), chinese] };

export const Verified: Story = { args: { answer: lineOf(VERIFIED) } };
export const AGateFailed: Story = { args: { answer: lineOf(FAILED_GATE) } };
export const AGateFailedChinese: Story = { ...AGateFailed, decorators: [chinese] };
export const HeldForYou: Story = { args: { answer: lineOf(UNKNOWN_HELD) } };
export const HeldForYouChinese: Story = { ...HeldForYou, decorators: [chinese] };

export const NoneYet: Story = { args: { answer: lineOf() } };
export const NoneYetChinese: Story = { ...NoneYet, decorators: [chinese] };
export const Reading: Story = { args: { answer: null, reading: true } };
export const Refused: Story = { args: { answer: null, refusal: "Daoris's driver is still coming up." } };
