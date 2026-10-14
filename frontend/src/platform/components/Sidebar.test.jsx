import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import Sidebar from './Sidebar';
import i18n from '../../shared/i18n/config';

const navigateMock = vi.hoisted(() => vi.fn());
const platformState = vi.hoisted(() => ({
  value: null,
}));
const messengerState = vi.hoisted(() => ({
  value: null,
}));
const gamificationState = vi.hoisted(() => ({
  value: { inventory: [] },
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigateMock,
    useLocation: () => ({ pathname: '/dashboard' }),
  };
});

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('../context/MessengerContext', () => ({
  useMessenger: () => messengerState.value,
}));

vi.mock('../../shared/context/GamificationContext', () => ({
  useGamification: () => gamificationState.value,
}));

vi.mock('./CreateWorkspaceModal', () => ({
  default: () => null,
}));

vi.mock('./CreateProjectModal', () => ({
  default: () => null,
}));

vi.mock('./AiAssistant', () => ({
  default: () => null,
}));

vi.mock('./DeleteWorkspaceModal', () => ({
  DeleteWorkspaceModal: () => null,
}));

vi.mock('./gamification/GlobalXpBar', () => ({
  default: () => null,
}));

const createPlatformState = ({ isSystemAdmin = false, currentUserRole = 'Member', workspaces = [] } = {}) => ({
  workspaces,
  activeProjectId: workspaces[0]?.projects?.[0]?.id ?? null,
  currentUser: {
    id: 'user-1',
    name: 'Collaby Admin',
    avatar: 'https://example.test/avatar.png',
    isSystemAdmin,
  },
  currentUserRole,
  selectProject: vi.fn(),
  createWorkspace: vi.fn(),
  deleteWorkspace: vi.fn(),
  reorderWorkspaces: vi.fn(),
  setActivePage: vi.fn(),
  inboxCount: 0,
  unreadCount: 0,
});

describe('Sidebar admin navigation', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    gamificationState.value = { inventory: [] };
  });

  it('does not render admin entry for system admins in restored shell', () => {
    platformState.value = createPlatformState(true);
    messengerState.value = { unreadTotal: 0 };

    render(<Sidebar isOpen toggleSidebar={vi.fn()} />);

    expect(screen.queryByText('Admin Panel')).not.toBeInTheDocument();
  });

  it('does not render admin entry for regular workspace users', () => {
    platformState.value = createPlatformState(false);
    messengerState.value = { unreadTotal: 0 };

    render(<Sidebar isOpen toggleSidebar={vi.fn()} />);

    expect(screen.queryByText('Admin Panel')).not.toBeInTheDocument();
  });

  it('hides workspace management controls for members', () => {
    platformState.value = createPlatformState({
      currentUserRole: 'Member',
      workspaces: [
        {
          id: 'workspace-1',
          name: 'Workspace',
          color: 'bg-blue-500',
          projects: [{ id: 'project-1', name: 'Project Alpha' }],
        },
      ],
    });
    messengerState.value = { unreadTotal: 0 };

    render(<Sidebar isOpen toggleSidebar={vi.fn()} />);

    expect(screen.queryByTitle(i18n.t('platform:sidebar.createWorkspace'))).not.toBeInTheDocument();
    expect(screen.queryByTitle(i18n.t('platform:sidebar.createProject'))).not.toBeInTheDocument();
    expect(screen.queryByTitle(i18n.t('platform:sidebar.deleteWorkspace'))).not.toBeInTheDocument();
  });

  it('shows workspace management controls for owners', () => {
    platformState.value = createPlatformState({
      currentUserRole: 'Owner',
      workspaces: [
        {
          id: 'workspace-1',
          name: 'Workspace',
          color: 'bg-blue-500',
          projects: [{ id: 'project-1', name: 'Project Alpha' }],
        },
      ],
    });
    messengerState.value = { unreadTotal: 0 };

    render(<Sidebar isOpen toggleSidebar={vi.fn()} />);

    expect(screen.getByTitle(i18n.t('platform:sidebar.createProject'))).toBeInTheDocument();
    expect(screen.getByTitle(i18n.t('platform:sidebar.deleteWorkspace'))).toBeInTheDocument();
  });

  it('renders the equipped fire avatar frame from inventory', () => {
    platformState.value = createPlatformState();
    messengerState.value = { unreadTotal: 0 };
    gamificationState.value = {
      inventory: [
        {
          itemId: 'a0000001-0000-0000-0000-000000000003',
          category: 'frames',
          isEquipped: true,
        },
      ],
    };

    render(<Sidebar isOpen toggleSidebar={vi.fn()} />);

    expect(screen.getByTestId('sidebar-user-avatar').closest('[data-avatar-frame]'))
      .toHaveAttribute('data-avatar-frame', 'fireFrame');
  });
});
