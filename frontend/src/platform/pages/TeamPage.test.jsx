import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import TeamPage from './TeamPage';
import i18n from '../../shared/i18n/config';

const platformState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('../components/InviteMemberModal', () => ({
  default: () => null,
}));

const defaultMembers = [
  { userId: 'user-1', role: 'Owner', displayName: 'Owner User', email: 'owner@example.invalid' },
  { userId: 'user-2', role: 'Member', displayName: 'Member User', email: 'member@example.invalid' },
];

const createPlatformState = (workspaceRole, options = {}) => {
  const members = options.members || defaultMembers;
  const tasks = options.tasks || {};

  return {
    workspaces: [
      {
        id: 'workspace-1',
        name: 'Workspace Alpha',
        role: workspaceRole,
        members,
      },
    ],
    activeWorkspace: {
      id: 'workspace-1',
      name: 'Workspace Alpha',
      role: workspaceRole,
      members,
    },
  activeWorkspaceId: 'workspace-1',
  currentUser: {
    id: options.currentUserId || (workspaceRole === 'Member' ? 'user-2' : 'user-1'),
    isSystemAdmin: false,
  },
  activeProject: { boardData: { tasks } },
  refreshWorkspaceMembers: vi.fn(),
  };
};

describe('TeamPage workspace permissions', () => {
  it('hides invite action for members', () => {
    platformState.value = createPlatformState('Member');

    render(<TeamPage />);

    expect(screen.queryByText(i18n.t('platform:team.invite'))).not.toBeInTheDocument();
  });

  it('shows invite action for owners', () => {
    platformState.value = createPlatformState('Owner');

    render(<TeamPage />);

    expect(screen.getByText(i18n.t('platform:team.invite'))).toBeInTheDocument();
  });

  it('does not count todo tasks as in-progress member work', () => {
    platformState.value = createPlatformState('Owner', {
      members: [{ userId: 'user-2', role: 'Member', displayName: 'Member User', email: 'member@example.invalid' }],
      currentUserId: 'user-1',
      tasks: {
        'task-todo': {
          id: 'task-todo',
          assigneeId: 'user-2',
          title: 'Todo task',
          status: 'К выполнению',
          statusRaw: 'К выполнению',
        },
        'task-done': {
          id: 'task-done',
          assigneeId: 'user-2',
          title: 'Done task',
          status: 'Готово',
          statusRaw: 'Готово',
        },
      },
    });

    render(<TeamPage />);

    const inProgressLabel = screen.getByText(i18n.t('platform:team.member.inProgress'));
    const doneLabel = screen.getByText(i18n.t('platform:team.member.done'));

    expect(inProgressLabel.previousElementSibling).toHaveTextContent('0');
    expect(doneLabel.previousElementSibling).toHaveTextContent('1');
  });
});
