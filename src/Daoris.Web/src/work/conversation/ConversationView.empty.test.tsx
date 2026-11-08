import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { ConversationView } from './ConversationView';

const view = (props: Partial<Parameters<typeof ConversationView>[0]>) =>
  render(<Tooltip.Provider><ConversationView turns={[]} {...props} /></Tooltip.Provider>);

describe('a conversation with no turn', () => {
  it('says an ended chat on a door not known to carry only text had nothing said, and sends nobody to the console', () => {
    view({ chat: true, live: false });

    expect(screen.getByText('Nothing was said in this conversation.')).toBeTruthy();
    expect(screen.queryByText(/console/)).toBeNull();
  });

  it('says a running chat on a door not known to carry only text has nothing said yet', () => {
    view({ chat: true, live: true });

    expect(screen.getByText(/Nothing said yet/)).toBeTruthy();
  });

  it('keeps the console sentence for a chat whose door is known to carry only text', () => {
    view({ chat: true, structured: false, live: false });

    expect(screen.getByText(/carries only text/)).toBeTruthy();
  });

  it('has a Chinese sentence for each', () => {
    for (const key of ['work.conversation.nothingYet', 'work.conversation.nothingSaid']) {
      expect(i18n.t(key, { lng: 'zh' })).not.toBe(key);
    }
  });
});
