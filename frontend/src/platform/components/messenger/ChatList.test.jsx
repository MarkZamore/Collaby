import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import ChatList from './ChatList';
import i18n from '../../../shared/i18n/config';

const messengerState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../../context/MessengerContext', () => ({
  useMessenger: () => messengerState.value,
}));

vi.mock('../../context/PlatformContext', () => ({
  usePlatform: () => ({
    currentUser: { id: 'user-1' },
  }),
}));

vi.mock('../../context/PreferencesContext', () => ({
  usePreferences: () => ({
    language: 'en',
  }),
}));

describe('ChatList', () => {
  beforeEach(async () => {
    messengerState.value = {
      chats: [
        {
          id: 'chat-1',
          type: 'private',
          createdBy: 'user-1',
          members: [
            { userId: 'user-1', displayName: 'Current User' },
            { userId: 'user-2', displayName: 'Teammate' },
          ],
          lastMessage: { text: 'Hello', sentAt: '2026-03-14T10:00:00Z' },
          unreadCount: 0,
          createdAt: '2026-03-14T09:00:00Z',
          updatedAt: '2026-03-14T10:00:00Z',
        },
      ],
      activeChat: null,
      selectChat: vi.fn(),
      onlineUsers: {},
      unreadTotal: 0,
      leaveChat: vi.fn(),
      deleteChat: vi.fn(),
    };

    await i18n.changeLanguage('en');
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('triggers deleteChat when the user chooses delete for everyone', async () => {
    const user = userEvent.setup();

    render(<ChatList onNewChat={vi.fn()} />);

    await user.click(screen.getByTitle('Actions'));
    await user.click(screen.getByText('Delete for everyone'));

    expect(messengerState.value.deleteChat).toHaveBeenCalledWith('chat-1');
  });
});
