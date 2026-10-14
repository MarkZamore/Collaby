import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import Dashboard from './Dashboard';
import i18n from '../../shared/i18n/config';

const platformState = vi.hoisted(() => ({
  value: null,
}));

const routerState = vi.hoisted(() => ({
  location: { pathname: '/dashboard', hash: '' },
  searchParams: new URLSearchParams(),
}));

vi.mock('react-router-dom', () => ({
  useLocation: () => routerState.location,
  useParams: () => ({}),
  useSearchParams: () => [routerState.searchParams],
}));

vi.mock('../context/PlatformContext', () => ({
  usePlatform: () => platformState.value,
}));

vi.mock('../context/PreferencesContext', () => ({
  usePreferences: () => ({ resolvedTheme: 'light' }),
}));

vi.mock('../context/FirstWorkspaceModalContext', () => ({
  useFirstWorkspaceModal: () => ({ isOpen: false, setIsOpen: vi.fn() }),
}));

vi.mock('../components/DashboardLayout', () => ({
  default: ({ children }) => <div>{children}</div>,
}));

vi.mock('../components/BoardView', () => ({
  default: () => <div>BoardView</div>,
}));

vi.mock('../components/ListView', () => ({
  default: () => null,
}));

vi.mock('../components/CalendarView', () => ({
  default: () => null,
}));

vi.mock('../components/GanttView', () => ({
  default: () => null,
}));

vi.mock('../components/TaskDetailModal', () => ({
  default: ({ task }) => (task ? <div>Task detail: {task.title}</div> : null),
}));

vi.mock('../components/FirstWorkspaceModal', () => ({
  default: () => null,
}));

vi.mock('../components/WelcomeTour', () => ({
  default: () => null,
}));

vi.mock('../components/ViewSettingsMenu', () => ({
  default: () => null,
}));

vi.mock('../components/BoardSettingsModal', () => ({
  default: ({ isOpen }) => (isOpen ? <div>Board settings modal</div> : null),
}));

vi.mock('./Shop', () => ({
  default: () => null,
}));

vi.mock('./Analytics', () => ({
  default: () => null,
}));

vi.mock('./Profile', () => ({
  default: () => null,
}));

vi.mock('./HelpCenter', () => ({
  default: () => null,
}));

vi.mock('./TeamPage', () => ({
  default: () => null,
}));

vi.mock('./GamificationInfo', () => ({
  default: () => null,
}));

vi.mock('./Awards', () => ({
  default: () => null,
}));

vi.mock('../components/InboxView', () => ({
  default: () => null,
}));

vi.mock('../components/NotificationsView', () => ({
  default: () => null,
}));

vi.mock('../components/messenger/MessengerPage', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/ProjectProgressBar', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/ProjectXpBar', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/QuestPanel', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/StreakIndicator', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/SkillTreeView', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/EmployeeOfMonth', () => ({
  default: () => null,
}));

vi.mock('../components/gamification/LeaderboardPanel', () => ({
  default: () => null,
}));

const createPlatformState = (currentUserRole) => ({
  activeProject: {
    id: 'project-1',
    name: 'Project Alpha',
    boardData: {
      tasks: {},
      columns: {},
      columnOrder: [],
    },
  },
  moveTask: vi.fn(),
  activePage: 'dashboard',
  setActivePage: vi.fn(),
  availableTags: [],
  activeWorkspace: {
    id: 'workspace-1',
    name: 'Workspace Alpha',
    projects: [{ id: 'project-1', name: 'Project Alpha' }],
  },
  workspaces: [
    {
      id: 'workspace-1',
      name: 'Workspace Alpha',
      projects: [{ id: 'project-1', name: 'Project Alpha' }],
    },
  ],
  createProject: vi.fn(),
  createWorkspace: vi.fn(),
  addTask: vi.fn(),
  boardStickers: [],
  addBoardSticker: vi.fn(),
  removeBoardSticker: vi.fn(),
  boardStickerIds: [],
  stickerCatalog: [],
  boardBackground: null,
  bootstrapped: true,
  currentUserRole,
  selectProject: vi.fn(),
  selectWorkspace: vi.fn(),
  users: [],
});

describe('Dashboard board settings permissions', () => {
  beforeEach(() => {
    routerState.location = { pathname: '/dashboard', hash: '' };
    routerState.searchParams = new URLSearchParams();
  });

  it('hides the board settings entry point for members', () => {
    platformState.value = createPlatformState('Member');

    render(<Dashboard />);

    expect(screen.queryByTitle(i18n.t('platform:dashboard.openBoardSettings'))).not.toBeInTheDocument();
  });

  it('shows the board settings entry point for owners', () => {
    platformState.value = createPlatformState('Owner');

    render(<Dashboard />);

    expect(screen.getByTitle(i18n.t('platform:dashboard.openBoardSettings'))).toBeInTheDocument();
  });

  it('opens task detail from hash-based deep links', () => {
    routerState.location = { pathname: '/dashboard', hash: '#task-task-1' };
    platformState.value = {
      ...createPlatformState('Member'),
      activeProject: {
        id: 'project-1',
        name: 'Project Alpha',
        boardData: {
          tasks: {
            'task-1': {
              id: 'task-1',
              title: 'Hash-linked task',
            },
          },
          columns: {},
          columnOrder: [],
        },
      },
    };

    render(<Dashboard />);

    expect(screen.getByText('Task detail: Hash-linked task')).toBeInTheDocument();
  });
});
