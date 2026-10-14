import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import TopBar from './TopBar';
import i18n from '../../shared/i18n/config';

const platformState = vi.hoisted(() => ({
  value: null,
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('./NotificationPanel', () => ({
  default: () => null,
}));

vi.mock('./InviteMemberModal', () => ({
  default: () => null,
}));

vi.mock('./DeleteProjectModal', () => ({
  DeleteProjectModal: () => null,
}));

const createPlatformState = (currentUserRole) => ({
  users: [
    {
      id: 'user-1',
      name: 'Owner User',
      avatar: 'https://example.test/avatar.png',
    },
  ],
  activePage: 'dashboard',
  setActivePage: vi.fn(),
  activeProject: { id: 'project-1', name: 'Project Alpha' },
  activeWorkspace: {
    id: 'workspace-1',
    name: 'Workspace Alpha',
    members: [{ userId: 'user-1' }],
    projects: [{ id: 'project-1', name: 'Project Alpha' }],
  },
  currentUser: {
    id: 'user-1',
    name: 'Owner User',
    avatar: 'https://example.test/avatar.png',
    isSystemAdmin: false,
  },
  currentUserRole,
  selectProject: vi.fn(),
  createProject: vi.fn(),
  deleteProject: vi.fn(),
  unreadCount: 0,
});

describe('TopBar workspace permissions', () => {
  it('hides owner actions for members', () => {
    platformState.value = createPlatformState('Member');

    render(<TopBar isMobile={false} onMenuClick={vi.fn()} />);

    expect(screen.queryByTitle(i18n.t('platform:topbar.invite'))).not.toBeInTheDocument();
    expect(screen.queryByTitle(i18n.t('platform:topbar.createProject'))).not.toBeInTheDocument();
  });

  it('shows owner actions for admins', () => {
    platformState.value = createPlatformState('Admin');

    render(<TopBar isMobile={false} onMenuClick={vi.fn()} />);

    expect(screen.getByTitle(i18n.t('platform:topbar.invite'))).toBeInTheDocument();
    expect(screen.getByTitle(i18n.t('platform:topbar.createProject'))).toBeInTheDocument();
  });
});
