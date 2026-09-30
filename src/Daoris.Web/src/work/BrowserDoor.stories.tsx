import type { Meta, StoryObj } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import { BrowserDoor } from './BrowserDoor';

// Daoris's browser's door on the strip (BRW7), and who is driving it (BRW8), in each state a person can
// meet: nobody, one session, two, a name too long for the chip, and a window with nowhere to open one.

const meta: Meta = { title: 'Work/Browser door' };
export default meta;

const ONE = [{ id: 's1a2b3c4', name: 'engine · Read the ticket' }];

export const States: StoryObj = {
  render: () => (
    <Tooltip.Provider>
      <div className="grid w-[36rem] gap-3">
        <BrowserDoor onOpen={() => {}} onAttend={() => {}} />
        <BrowserDoor onOpen={() => {}} drivers={ONE} onAttend={() => {}} />
        <BrowserDoor
          onOpen={() => {}}
          drivers={[...ONE, { id: 'c0ffee00', name: 'game · conversation' }]}
          onAttend={() => {}}
        />
        <BrowserDoor
          onOpen={() => {}}
          drivers={[{ id: 'i9n8t7k6', name: 'engine · 检查世界流式加载引擎的每一个分块预算，并报告超出预算的地方' }]}
          onAttend={() => {}}
        />
        <BrowserDoor onOpen={() => {}} drivers={ONE} />
      </div>
    </Tooltip.Provider>
  ),
};
