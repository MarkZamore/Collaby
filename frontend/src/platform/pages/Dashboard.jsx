import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom';
import { useLocation, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Calendar,
  LayoutGrid,
  Plus,
  Clock,
  Layers,
  MoreHorizontal,
  Search,
  Settings,
  ChevronDown,
  X,
  Users,
  BarChart3,
  Code2,
  MessageCircle,
  Monitor,
  Timer,
  RefreshCw,
  Hourglass,
  Tag,
  Hash,
  Type,
  Zap,
  ChevronUp,
} from 'lucide-react';
import DashboardLayout from '../components/DashboardLayout';
import BoardView from '../components/BoardView';
import ListView from '../components/ListView';
import CalendarView from '../components/CalendarView';
import GanttView from '../components/GanttView';
import TaskDetailModal from '../components/TaskDetailModal';
import FirstWorkspaceModal from '../components/FirstWorkspaceModal';
import WelcomeTour from '../components/WelcomeTour';
import ViewSettingsMenu from '../components/ViewSettingsMenu';
import BoardSettingsModal from '../components/BoardSettingsModal';
import Shop from './Shop';
import Analytics from './Analytics';
import Profile from './Profile';
import HelpCenter from './HelpCenter';
import TeamPage from './TeamPage';
import GamificationInfo from './GamificationInfo';
import Awards from './Awards';
import InboxView from '../components/InboxView';
import NotificationsView from '../components/NotificationsView';
import MessengerPage from '../components/messenger/MessengerPage';
import ProjectProgressBar from '../components/gamification/ProjectProgressBar';
import ProjectXpBar from '../components/gamification/ProjectXpBar';
import QuestPanel from '../components/gamification/QuestPanel';
import StreakIndicator from '../components/gamification/StreakIndicator';
import SkillTreeView from '../components/gamification/SkillTreeView';
import EmployeeOfMonth from '../components/gamification/EmployeeOfMonth';
import LeaderboardPanel from '../components/gamification/LeaderboardPanel';
import { usePlatform } from '../context/PlatformContext';
import { usePreferences } from '../context/PreferencesContext';
import { useFirstWorkspaceModal } from '../context/FirstWorkspaceModalContext';

const STICKER_ICON_MAP = {
  Users,
  Calendar,
  BarChart3,
  Clock,
  Code2,
  MessageCircle,
  Monitor,
  Timer,
  RefreshCw,
  Hourglass,
  Tag,
  Hash,
  Type,
  Zap,
  Layers,
};

const StickerDropdown = ({ sticker, filterValues, onFilterChange, onClose, onRemove, anchorRect, isFixed }) => {
  const { t } = useTranslation('platform');
  const Icon = STICKER_ICON_MAP[sticker.iconName] || Tag;
  const dropdownRef = React.useRef(null);

  React.useEffect(() => {
    const handleClick = (event) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [onClose]);

  const style = isFixed && anchorRect
    ? {
        position: 'fixed',
        top: anchorRect.bottom + 4,
        left: Math.min(anchorRect.left, window.innerWidth - 256 - 16),
        zIndex: 150,
        backgroundColor: 'var(--app-sidebar-bg)',
        border: '1px solid var(--app-border)',
      }
    : {
        backgroundColor: 'var(--app-sidebar-bg)',
        border: '1px solid var(--app-border)',
      };

  return (
    <div
      ref={dropdownRef}
      className={`${isFixed ? '' : 'absolute top-full left-0 mt-1'} w-64 rounded-xl shadow-2xl overflow-hidden z-[150]`}
      style={style}
      onClick={(event) => event.stopPropagation()}
    >
      <div className="flex items-center justify-between px-4 py-3" style={{ borderBottom: '1px solid var(--app-border)' }}>
        <div className="flex items-center gap-2">
          <span className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {t('dashboard.stickers.title', { label: sticker.label })}
          </span>
          <button className="p-0.5 rounded hover:opacity-70" style={{ color: 'var(--app-text-secondary)' }}>
            <Settings size={13} />
          </button>
        </div>
        <span className="text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>
          {t('dashboard.stickers.filter')}
        </span>
      </div>

      {sticker.type === 'state-set' && sticker.values && sticker.values.length > 0 ? (
        <div className="py-2">
          {sticker.values.map((value) => {
            const isChecked = filterValues.has(value.id);

            return (
              <label
                key={value.id}
                className="flex items-center gap-3 px-4 py-2 cursor-pointer transition-colors hover:bg-white/5"
              >
                <Icon size={14} style={{ color: value.color }} />
                <span
                  className="px-2 py-0.5 rounded text-xs font-semibold"
                  style={{ backgroundColor: `${value.color}25`, color: value.color }}
                >
                  {value.label}
                </span>
                <div className="ml-auto">
                  <div
                    onClick={(event) => {
                      event.preventDefault();
                      onFilterChange(value.id);
                    }}
                    className="w-4 h-4 rounded border flex items-center justify-center cursor-pointer transition-colors"
                    style={{
                      backgroundColor: isChecked ? 'var(--app-accent)' : 'transparent',
                      borderColor: isChecked ? 'var(--app-accent)' : 'var(--app-border)',
                    }}
                  >
                    {isChecked && (
                      <svg width="10" height="10" viewBox="0 0 12 12" fill="none">
                        <path d="M2.5 6L5 8.5L9.5 3.5" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    )}
                  </div>
                </div>
              </label>
            );
          })}
          <button
            className="flex items-center gap-2 px-4 py-2 text-sm font-medium w-full hover:opacity-80 transition-all"
            style={{ color: 'var(--app-accent)' }}
          >
            <Plus size={13} />
            {t('dashboard.stickers.add')}
          </button>
        </div>
      ) : (
        <div className="py-4 px-4 text-center">
          <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
            {sticker.type === 'system' ? t('dashboard.stickers.systemSticker') : t('dashboard.stickers.noValues')}
          </p>
        </div>
      )}

      {sticker.removable && (
        <div style={{ borderTop: '1px solid var(--app-border)' }}>
          <button
            onClick={onRemove}
            className="flex items-center gap-2 px-4 py-2.5 text-xs font-medium w-full hover:bg-red-500/10 transition-colors"
            style={{ color: '#EF4444' }}
          >
            <X size={12} />
            {t('dashboard.stickers.remove')}
          </button>
        </div>
      )}
    </div>
  );
};

const AddStickerMenu = ({ currentIds, onAdd, onClose, isFixed, anchorRect, stickerCatalog }) => {
  const { t } = useTranslation('platform');
  const menuRef = React.useRef(null);
  const [search, setSearch] = React.useState('');

  React.useEffect(() => {
    const handleClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [onClose]);

  const available = stickerCatalog.filter(
    (sticker) => !currentIds.includes(sticker.id) && sticker.label.toLowerCase().includes(search.toLowerCase()),
  );
  const alreadyAdded = stickerCatalog.filter(
    (sticker) => currentIds.includes(sticker.id) && sticker.label.toLowerCase().includes(search.toLowerCase()),
  );

  const style = isFixed && anchorRect
    ? {
        position: 'fixed',
        top: anchorRect.bottom + 4,
        left: Math.min(anchorRect.left, window.innerWidth - 288 - 16),
        zIndex: 150,
        backgroundColor: 'var(--app-sidebar-bg)',
        border: '1px solid var(--app-border)',
      }
    : {
        backgroundColor: 'var(--app-sidebar-bg)',
        border: '1px solid var(--app-border)',
      };

  return (
    <div
      ref={menuRef}
      className={`${isFixed ? '' : 'absolute top-full right-0 mt-1'} w-72 rounded-xl shadow-2xl overflow-hidden z-[150]`}
      style={style}
      onClick={(event) => event.stopPropagation()}
    >
      <div className="px-3 pt-3 pb-2">
        <div className="text-xs font-bold uppercase tracking-wide mb-2" style={{ color: 'var(--app-text-secondary)' }}>
          {t('dashboard.stickers.addToBoard')}
        </div>
        <div className="relative">
          <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2" style={{ color: 'var(--app-text-secondary)' }} />
          <input
            autoFocus
            className="w-full pl-8 pr-3 py-1.5 rounded-lg text-xs outline-none"
            style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}
            placeholder={t('dashboard.stickers.search')}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
      </div>

      <div className="max-h-64 overflow-y-auto py-1">
        {available.length > 0 && (
          <>
            <div className="px-3 py-1 text-[10px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>
              {t('dashboard.stickers.available')}
            </div>
            {available.map((sticker) => {
              const Icon = STICKER_ICON_MAP[sticker.iconName] || Tag;

              return (
                <button
                  key={sticker.id}
                  onClick={() => onAdd(sticker.id)}
                  className="flex items-center gap-2.5 w-full px-3 py-2 text-left hover:bg-white/5 transition-colors"
                >
                  <div
                    className="w-7 h-7 rounded-lg flex items-center justify-center shrink-0"
                    style={{ backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)' }}
                  >
                    <Icon size={14} style={{ color: 'var(--app-accent)' }} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[13px] font-medium truncate" style={{ color: 'var(--app-text-main)' }}>{sticker.label}</div>
                    <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>{sticker.subtitle}</div>
                  </div>
                  <Plus size={14} style={{ color: 'var(--app-accent)' }} />
                </button>
              );
            })}
          </>
        )}

        {alreadyAdded.length > 0 && (
          <>
            <div className="px-3 py-1 mt-1 text-[10px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>
              {t('dashboard.stickers.onBoard')}
            </div>
            {alreadyAdded.map((sticker) => {
              const Icon = STICKER_ICON_MAP[sticker.iconName] || Tag;

              return (
                <div key={sticker.id} className="flex items-center gap-2.5 w-full px-3 py-2 opacity-50">
                  <div
                    className="w-7 h-7 rounded-lg flex items-center justify-center shrink-0"
                    style={{ backgroundColor: 'var(--app-layer-base)' }}
                  >
                    <Icon size={14} style={{ color: 'var(--app-text-secondary)' }} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[13px] font-medium truncate" style={{ color: 'var(--app-text-main)' }}>{sticker.label}</div>
                    <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>{sticker.subtitle}</div>
                  </div>
                  <span className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>{t('dashboard.stickers.added')}</span>
                </div>
              );
            })}
          </>
        )}

        {available.length === 0 && alreadyAdded.length === 0 && (
          <div className="py-4 text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>
            {t('dashboard.stickers.nothingFound')}
          </div>
        )}
      </div>
    </div>
  );
};

const GanttScaleDropdown = ({ currentScale, onSelect, onClose, anchorRect }) => {
  const { t } = useTranslation('platform');
  const menuRef = React.useRef(null);

  React.useEffect(() => {
    const handleClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [onClose]);

  if (!anchorRect) {
    return null;
  }

  return (
    <div
      ref={menuRef}
      className="fixed z-[200] w-32 rounded-[14px] shadow-xl overflow-hidden py-1"
      style={{ top: anchorRect.bottom + 4, left: anchorRect.left, backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
      onClick={(event) => event.stopPropagation()}
    >
      {['days', 'weeks', 'months', 'years'].map((scale) => (
        <button
          key={scale}
          onClick={() => onSelect(scale)}
          className={`w-full text-left px-3 py-2 text-xs transition-colors duration-150 ${currentScale === scale ? 'font-medium' : ''}`}
          style={{
            color: currentScale === scale ? 'var(--app-text-main)' : 'var(--app-text-secondary)',
            backgroundColor: currentScale === scale ? 'color-mix(in srgb, var(--app-accent) 12%, transparent)' : 'transparent',
          }}
          onMouseEnter={(e) => { if (currentScale !== scale) e.currentTarget.style.backgroundColor = 'color-mix(in srgb, var(--app-accent) 8%, transparent)'; }}
          onMouseLeave={(e) => { if (currentScale !== scale) e.currentTarget.style.backgroundColor = 'transparent'; }}
        >
          {t(`gantt.scales.${scale}`)}
        </button>
      ))}
    </div>
  );
};

const PageWrapper = ({ children }) => (
  <div
    className="flex-1 flex flex-col overflow-y-auto mx-4 mt-2 mb-2 rounded-2xl"
    style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
  >
    {children}
  </div>
);

const normalizeRouteToken = (value) => String(value || '').trim().toLowerCase();

const slugifyRouteSegment = (value, fallback = 'workspace') => {
  const normalized = String(value || '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9а-яё\s-]/gi, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');

  return normalized || fallback;
};

const getTaskIdFromRouteState = (searchParams, hash) => {
  const queryTaskId = searchParams.get('taskId');
  if (queryTaskId) {
    return queryTaskId;
  }

  const normalizedHash = String(hash || '').trim();
  if (!normalizedHash.startsWith('#task-')) {
    return null;
  }

  const hashTaskId = decodeURIComponent(normalizedHash.slice('#task-'.length)).trim();
  return hashTaskId || null;
};

const DashboardContent = () => {
  const { t } = useTranslation('platform');
  const [currentView, setCurrentView] = useState('board');
  const [selectedTaskId, setSelectedTaskId] = useState(null);
  const { resolvedTheme } = usePreferences();
  const isDark = resolvedTheme === 'dark';
  const [showWelcomeTour] = useState(false);
  const [firstWorkspaceProjectName, setFirstWorkspaceProjectName] = useState(null);
  const { isOpen: isFirstWorkspaceModalOpen, setIsOpen: setFirstWorkspaceModalOpen } = useFirstWorkspaceModal();
  const [ganttScale, setGanttScale] = useState('weeks');
  const [ganttAutoplan, setGanttAutoplan] = useState(true);
  const [ganttScaleMenuAnchor, setGanttScaleMenuAnchor] = useState(null);
  const [activeFilters, setActiveFilters] = useState({ assignee: null, priority: null, tags: [] });
  const [viewSettings, setViewSettings] = useState({
    showTaskId: false,
    showAssignee: true,
    showTags: true,
    showPriority: true,
    showDueDate: true,
  });
  const [isViewSettingsOpen, setIsViewSettingsOpen] = useState(false);
  const [isBoardSettingsOpen, setIsBoardSettingsOpen] = useState(false);
  const [menuPosition, setMenuPosition] = useState({ top: 0, left: 0 });
  const [openStickerDropdown, setOpenStickerDropdown] = useState(null);
  const [stickerDropdownAnchor, setStickerDropdownAnchor] = useState(null);
  const [showAddStickerMenu, setShowAddStickerMenu] = useState(false);
  const [addStickerMenuAnchor, setAddStickerMenuAnchor] = useState(null);
  const [stickerFilterValues, setStickerFilterValues] = useState({});
  const [isGamificationOpen, setIsGamificationOpen] = useState(() => {
    if (typeof window === 'undefined') return true;
    const stored = window.localStorage.getItem('collaby_gamification_open');
    return stored === null ? true : stored === 'true';
  });

  const {
    activeProject,
    moveTask,
    activePage,
    setActivePage,
    availableTags,
    activeWorkspace,
    workspaces,
    createProject,
    createWorkspace,
    addTask,
    boardStickers,
    addBoardSticker,
    removeBoardSticker,
    boardStickerIds,
    stickerCatalog,
    boardBackground,
    bootstrapped,
    currentUserRole,
    selectProject,
    selectWorkspace,
    users,
  } = usePlatform();

  const { section, slug, projectKey } = useParams();
  const location = useLocation();
  const [searchParams] = useSearchParams();

  useEffect(() => {
    const sectionMap = {
      inbox: 'inbox',
      tasks: 'my-tasks',
      messenger: 'messenger',
      team: 'team',
      analytics: 'analytics',
      shop: 'shop',
      awards: 'awards',
      gamification: 'gamification',
      profile: 'profile',
    };
    const directPath = Object.keys(sectionMap).find((key) => location.pathname === `/${key}` || location.pathname === `/${key}/`);
    setActivePage(sectionMap[directPath || section] || 'dashboard');
  }, [location.pathname, section, setActivePage]);

  useEffect(() => {
    if (!slug || !projectKey || !workspaces?.length) {
      return;
    }

    const normalizedWorkspaceToken = normalizeRouteToken(slug);
    const normalizedProjectToken = normalizeRouteToken(projectKey);
    const matchingWorkspace = workspaces.find((workspace) => {
      const workspaceId = normalizeRouteToken(workspace.id);
      const workspaceSlug = slugifyRouteSegment(workspace.name, workspaceId || 'workspace');
      return workspaceId === normalizedWorkspaceToken || workspaceSlug === normalizedWorkspaceToken;
    });

    if (!matchingWorkspace) {
      return;
    }

    const matchingProject = (matchingWorkspace.projects || []).find((project) => {
      const currentProjectId = normalizeRouteToken(project.id);
      const currentProjectSlug = slugifyRouteSegment(project.name, currentProjectId || 'project');
      return currentProjectId === normalizedProjectToken || currentProjectSlug === normalizedProjectToken;
    });

    if (!matchingProject) {
      return;
    }

    if (activeProject?.id !== matchingProject.id) {
      selectProject(matchingProject.id);
      return;
    }

    if (activeWorkspace?.id !== matchingWorkspace.id) {
      selectWorkspace(matchingWorkspace.id);
    }
  }, [
    activeProject?.id,
    activeWorkspace?.id,
    projectKey,
    selectProject,
    selectWorkspace,
    slug,
    workspaces,
  ]);

  const isViewer = currentUserRole === 'Viewer';
  const canManageBoardStructure = currentUserRole === 'Owner' || currentUserRole === 'Admin';
  const shouldShowFirstWorkspaceModal = bootstrapped && (!workspaces || workspaces.length === 0);

  useEffect(() => {
    setFirstWorkspaceModalOpen(shouldShowFirstWorkspaceModal);
  }, [setFirstWorkspaceModalOpen, shouldShowFirstWorkspaceModal]);

  useEffect(() => {
    if (activeWorkspace && activeWorkspace.projects.length === 0 && firstWorkspaceProjectName) {
      createProject(activeWorkspace.id, firstWorkspaceProjectName);
      setFirstWorkspaceProjectName(null);
    }
  }, [activeWorkspace, createProject, firstWorkspaceProjectName]);

  const handleCreateFirstWorkspace = async (workspaceName, projectName) => {
    await createWorkspace(workspaceName);
    setFirstWorkspaceProjectName(projectName);
  };

  const handleCloseFirstWorkspaceModal = () => {
    setFirstWorkspaceModalOpen(false);
  };

  useEffect(() => {
    const requestedTaskId = getTaskIdFromRouteState(searchParams, location.hash);
    if (!requestedTaskId || !activeProject?.boardData?.tasks?.[requestedTaskId]) {
      return;
    }

    setSelectedTaskId((current) => (current === requestedTaskId ? current : requestedTaskId));
  }, [activeProject, location.hash, searchParams]);

  // Listen for task mentions clicked in messenger
  useEffect(() => {
    const handleOpenTask = (event) => {
      const { taskId } = event.detail || {};
      if (taskId) {
        setSelectedTaskId(taskId);
      }
    };

    window.addEventListener('collaby:open-task', handleOpenTask);
    return () => window.removeEventListener('collaby:open-task', handleOpenTask);
  }, []);

  if (activePage === 'messenger') return <MessengerPage />;
  if (activePage === 'shop') return <PageWrapper><Shop /></PageWrapper>;
  if (activePage === 'awards') return <PageWrapper><Awards /></PageWrapper>;
  if (activePage === 'gamification') return <PageWrapper><GamificationInfo /></PageWrapper>;
  if (activePage === 'analytics') return <PageWrapper><Analytics /></PageWrapper>;
  if (activePage === 'profile') return <PageWrapper><Profile /></PageWrapper>;
  if (activePage === 'help') return <PageWrapper><HelpCenter /></PageWrapper>;
  if (activePage === 'team') return <PageWrapper><TeamPage /></PageWrapper>;
  if (activePage === 'inbox') return <PageWrapper><NotificationsView /></PageWrapper>;
  if (activePage === 'my-tasks') return <PageWrapper><InboxView setSelectedTaskId={setSelectedTaskId} /></PageWrapper>;

  const data = activeProject ? activeProject.boardData : { tasks: {}, columns: {}, columnOrder: [] };
  const priorityMap = { critical: 'critical', major: 'high', normal: 'medium', low: 'low' };

  const filteredTasks = Object.values(data.tasks || {}).filter((task) => {
    if (activeFilters.assignee) {
      const assigneeId = task.assigneeId || (typeof task.assignee === 'object' ? task.assignee?.id : task.assignee);
      if (assigneeId !== activeFilters.assignee) return false;
    }
    if (activeFilters.priority && task.priority !== activeFilters.priority) return false;
    if (activeFilters.tags && activeFilters.tags.length > 0) {
      const taskTags = task.tags || [];
      const selectedTagIds = new Set(activeFilters.tags);
      const selectedTagNames = new Set(
        activeFilters.tags
          .map((id) => availableTags.find((tag) => tag.id === id)?.name)
          .filter(Boolean)
          .map((name) => String(name).toLowerCase()),
      );
      if (
        !taskTags.some((tag) => {
          if (typeof tag === 'object') {
            return selectedTagIds.has(tag.id) || selectedTagNames.has(String(tag.name || '').toLowerCase());
          }

          return selectedTagNames.has(String(tag).toLowerCase());
        })
      ) {
        return false;
      }
    }

    for (const [stickerId, selectedValues] of Object.entries(stickerFilterValues)) {
      if (!selectedValues || selectedValues.size === 0) continue;

      if (stickerId === 'priority') {
        const allowedPriorities = new Set([...selectedValues].map((value) => priorityMap[value]).filter(Boolean));
        if (!allowedPriorities.has(task.priority)) return false;
      } else if (stickerId === 'assignee') {
        const taskAssigneeId = task.assigneeId || (typeof task.assignee === 'object' ? task.assignee?.id : task.assignee);
        if (!selectedValues.has(taskAssigneeId)) return false;
      } else {
        const taskValue = (task.stickerValues || {})[stickerId];
        if (!selectedValues.has(taskValue)) return false;
      }
    }

    return true;
  });

  const handleTaskClick = (task) => {
    setSelectedTaskId(task.id);
  };

  const handleAddTask = (status, title) => {
    if (!title) return;

    addTask({
      title,
      status,
      assignee: 'Unassigned',
    });
  };

  const handleDragEnd = (event) => {
    const { active, over } = event;
    if (!over) return;
    moveTask(active.id, over.id);
  };

  const handleViewSettingChange = (key) => {
    setViewSettings((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const firstWorkspaceModal = shouldShowFirstWorkspaceModal && isFirstWorkspaceModalOpen
    ? ReactDOM.createPortal(
        <FirstWorkspaceModal
          onCreateWorkspace={handleCreateFirstWorkspace}
          isDark={isDark}
          onClose={handleCloseFirstWorkspaceModal}
          isOpen
        />,
        document.body,
      )
    : null;

  if (!activeProject) {
    return (
      <>
        {firstWorkspaceModal}
        {!shouldShowFirstWorkspaceModal && (
          <div className="flex flex-col items-center justify-center h-full gap-3">
            <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
              {t('dashboard.noProject')}
            </p>
          </div>
        )}
      </>
    );
  }

  const bgStyle = boardBackground
    ? {
        ...(boardBackground.type === 'color' ? { backgroundColor: boardBackground.value } : {}),
        ...(boardBackground.type === 'image' || boardBackground.type === 'upload'
          ? {
              backgroundImage: `url(${boardBackground.value})`,
              backgroundSize: 'cover',
              backgroundPosition: 'center',
              backgroundRepeat: 'no-repeat',
            }
          : {}),
      }
    : {};

  const viewTabs = [
    { id: 'board', label: t('dashboard.views.board'), icon: LayoutGrid },
    { id: 'calendar', label: t('dashboard.views.calendar'), icon: Calendar },
    { id: 'gantt', label: t('dashboard.views.gantt'), icon: Clock },
  ];

  return (
    <>
      {showWelcomeTour && <WelcomeTour />}
      <div
        className="flex-1 flex flex-col mx-4 mt-2 mb-2 rounded-2xl overflow-hidden relative"
        style={{ ...bgStyle, border: '1px solid var(--app-border)' }}
      >
        <div className="flex items-center gap-2 mx-2 mt-2 mb-1 shrink-0">
          <div
            className="flex items-center rounded-[14px] shrink-0"
            style={{ border: '1px solid var(--app-border)', backgroundColor: 'var(--app-layer-base)' }}
          >
            <div className="flex items-center shrink-0 px-1 py-1">
              {viewTabs.map((view) => (
                <button
                  key={view.id}
                  onClick={() => setCurrentView(view.id)}
                  className="relative px-3 py-1.5 rounded-[10px] flex items-center gap-1.5 text-[13px] font-medium transition-all duration-200 focus:outline-none"
                  style={{
                    backgroundColor: currentView === view.id ? 'var(--app-layer-surface)' : 'transparent',
                    color: currentView === view.id ? 'var(--app-text-main)' : 'var(--app-text-secondary)',
                  }}
                >
                  <view.icon size={15} />
                  {view.label}
                </button>
              ))}
            </div>
          </div>

          {currentView !== 'gantt' && (
            <div
              className="flex items-center rounded-[14px] flex-1 min-w-0"
              style={{ border: '1px solid var(--app-border)', backgroundColor: 'var(--app-layer-base)', overflow: 'visible' }}
            >
              <div
                className="flex items-center gap-0.5 px-3 py-1 relative flex-1 min-w-0 overflow-x-auto toolbar-scroll-hidden"
                style={{ scrollbarGutter: 'stable' }}
              >
                {boardStickers.map((sticker) => {
                  const Icon = STICKER_ICON_MAP[sticker.iconName] || Tag;
                  const isOpen = openStickerDropdown === sticker.id;
                  const hasFilter = stickerFilterValues[sticker.id] && stickerFilterValues[sticker.id].size > 0;

                  return (
                    <div key={sticker.id} className="relative shrink-0">
                      <button
                        onClick={(event) => {
                          event.stopPropagation();
                          const rect = event.currentTarget.getBoundingClientRect();

                          if (isOpen) {
                            setOpenStickerDropdown(null);
                            setStickerDropdownAnchor(null);
                          } else {
                            setOpenStickerDropdown(sticker.id);
                            setStickerDropdownAnchor(rect);
                          }

                          setShowAddStickerMenu(false);
                        }}
                        className="h-7 px-2.5 rounded-md flex items-center gap-1.5 text-[13px] font-medium transition-all hover:opacity-80"
                        style={{
                          backgroundColor: hasFilter ? 'color-mix(in srgb, var(--app-accent) 15%, transparent)' : 'transparent',
                          color: hasFilter ? 'var(--app-accent)' : 'var(--app-text-secondary)',
                        }}
                      >
                        <Icon size={13} className="opacity-60" />
                        {sticker.label}
                        <ChevronDown size={12} className="opacity-40" />
                      </button>
                    </div>
                  );
                })}

                <div className="relative shrink-0">
                  <button
                    onClick={(event) => {
                      event.stopPropagation();
                      const rect = event.currentTarget.getBoundingClientRect();

                      if (showAddStickerMenu) {
                        setShowAddStickerMenu(false);
                        setAddStickerMenuAnchor(null);
                      } else {
                        setShowAddStickerMenu(true);
                        setAddStickerMenuAnchor(rect);
                      }

                      setOpenStickerDropdown(null);
                    }}
                    className="h-7 w-7 rounded-md flex items-center justify-center hover:opacity-80 transition-all"
                    style={{ color: 'var(--app-accent)' }}
                    title={t('dashboard.addSticker')}
                  >
                    <Plus size={15} />
                  </button>
                </div>

                <div className="h-4 w-px mx-0.5 shrink-0" style={{ backgroundColor: 'var(--app-border)' }} />

                {Object.values(stickerFilterValues).some((set) => set && set.size > 0) && (
                  <button
                    onClick={() => setStickerFilterValues({})}
                    className="h-7 px-2.5 rounded-md flex items-center gap-1.5 text-[12px] font-medium transition-all hover:opacity-80 shrink-0"
                    style={{ backgroundColor: 'rgba(239,68,68,0.12)', color: '#EF4444' }}
                    title={t('dashboard.clearFilters')}
                  >
                    <X size={12} />
                    {t('dashboard.clearFilters')}
                  </button>
                )}

                <button
                  className="h-7 w-7 rounded-md flex items-center justify-center hover:opacity-80 transition-all shrink-0"
                  style={{ color: 'var(--app-text-secondary)' }}
                >
                  <MoreHorizontal size={16} />
                </button>

                <div className="h-4 w-px mx-0.5 shrink-0" style={{ backgroundColor: 'var(--app-border)' }} />

                <button
                  className="h-7 w-7 rounded-md flex items-center justify-center hover:opacity-80 transition-all shrink-0"
                  style={{ color: 'var(--app-text-secondary)' }}
                >
                  <Search size={14} />
                </button>

                {canManageBoardStructure && (
                  <button
                    className="h-7 w-7 rounded-md flex items-center justify-center hover:opacity-80 transition-all shrink-0"
                    style={{ color: 'var(--app-text-secondary)' }}
                    onClick={() => setIsBoardSettingsOpen(true)}
                    title={t('dashboard.openBoardSettings')}
                  >
                    <Settings size={14} />
                  </button>
                )}

                {isViewSettingsOpen && (
                  <div style={{ position: 'fixed', top: menuPosition.top, left: menuPosition.left, zIndex: 100 }}>
                    <ViewSettingsMenu
                      viewSettings={viewSettings}
                      onViewSettingChange={handleViewSettingChange}
                      onClose={() => setIsViewSettingsOpen(false)}
                    />
                  </div>
                )}
              </div>
            </div>
          )}

          {currentView === 'gantt' && (
            <div
              className="flex items-center rounded-lg flex-1 min-w-0 overflow-hidden"
              style={{ border: '1px solid var(--app-border)', backgroundColor: 'var(--app-layer-base)' }}
            >
              <div className="flex items-center gap-2 px-4 py-1.5 ml-auto">
                <div className="relative">
                  <button
                    onClick={(event) => {
                      event.stopPropagation();
                      setGanttScaleMenuAnchor(ganttScaleMenuAnchor ? null : event.currentTarget.getBoundingClientRect());
                    }}
                    className="flex items-center gap-2 text-xs font-medium text-gray-300 hover:text-white bg-black/20 hover:bg-white/10 px-3 py-1.5 rounded transition-colors min-w-[100px] justify-between"
                  >
                    {t(`gantt.scales.${ganttScale}`)}
                    <ChevronDown size={12} className={`transition-transform ${ganttScaleMenuAnchor ? 'rotate-180' : ''}`} />
                  </button>
                </div>

                <div className="flex items-center bg-black/20 rounded h-7">
                  <button
                    className="h-full px-2.5 hover:bg-white/10 text-gray-400 hover:text-white transition-colors rounded-l"
                    onClick={() => {
                      const scales = ['days', 'weeks', 'months', 'years'];
                      const index = scales.indexOf(ganttScale);
                      if (index < scales.length - 1) {
                        setGanttScale(scales[index + 1]);
                      }
                    }}
                    title={t('gantt.zoomOut')}
                  >
                    -
                  </button>
                  <button className="h-full px-3 text-xs font-medium text-gray-300 hover:text-white hover:bg-white/10 transition-colors border-l border-r border-white/10">
                    {t('gantt.today')}
                  </button>
                  <button
                    className="h-full px-2.5 hover:bg-white/10 text-gray-400 hover:text-white transition-colors rounded-r"
                    onClick={() => {
                      const scales = ['days', 'weeks', 'months', 'years'];
                      const index = scales.indexOf(ganttScale);
                      if (index > 0) {
                        setGanttScale(scales[index - 1]);
                      }
                    }}
                    title={t('gantt.zoomIn')}
                  >
                    +
                  </button>
                </div>

                <div className="h-5 w-px bg-black/20 mx-2" />

                <div className="flex items-center gap-2 cursor-pointer" onClick={() => setGanttAutoplan(!ganttAutoplan)}>
                  <div className={`w-8 h-4 rounded-full relative transition-colors ${ganttAutoplan ? 'bg-blue-600' : 'bg-black/20'}`}>
                    <div className={`absolute top-0.5 w-3 h-3 bg-white rounded-full shadow-sm transition-all ${ganttAutoplan ? 'right-0.5' : 'left-0.5'}`} />
                  </div>
                  <span className="text-xs text-gray-400 hover:text-gray-200">{t('gantt.autoplan')}</span>
                </div>
              </div>
            </div>
          )}
        </div>

        <main className="flex-1 overflow-y-auto relative">
          <div className="h-full w-full pt-2 px-2">
            <div className="mb-2 space-y-2">
              <button
                onClick={() => {
                  setIsGamificationOpen((prev) => {
                    const next = !prev;
                    window.localStorage.setItem('collaby_gamification_open', String(next));
                    return next;
                  });
                }}
                className="flex items-center gap-1.5 px-2 py-1 rounded-lg text-xs transition-colors hover:bg-white/10"
                style={{ color: 'var(--app-text-secondary)' }}
              >
                <ChevronUp
                  size={14}
                  className="transition-transform"
                  style={{ transform: isGamificationOpen ? undefined : 'rotate(180deg)' }}
                />
                {isGamificationOpen ? t('dashboard.gamification.hide', { defaultValue: 'Скрыть' }) : t('dashboard.gamification.show', { defaultValue: 'Показать' })}
              </button>
              {isGamificationOpen && (
                <>
                  <div className="grid grid-cols-1 gap-2 xl:grid-cols-2">
                    <ProjectProgressBar projectId={activeProject.id} />
                    <ProjectXpBar projectId={activeProject.id} />
                  </div>
                  <div className="grid grid-cols-1 gap-2 lg:grid-cols-2 2xl:grid-cols-4">
                    <QuestPanel />
                    <StreakIndicator />
                    <SkillTreeView projectId={activeProject.id} />
                    <EmployeeOfMonth projectId={activeProject.id} />
                  </div>
                  <LeaderboardPanel projectId={activeProject.id} users={users} />
                </>
              )}
            </div>
            {currentView === 'board' && (
              <BoardView
                data={data}
                filteredTasks={filteredTasks}
                onDragEnd={handleDragEnd}
                onTaskClick={handleTaskClick}
                onAddTask={handleAddTask}
                viewSettings={viewSettings}
                boardStickers={boardStickers}
                readOnly={isViewer}
                canManageStructure={canManageBoardStructure}
              />
            )}
            {currentView === 'list' && (
              <ListView tasks={filteredTasks} onTaskClick={handleTaskClick} viewSettings={viewSettings} />
            )}
            {currentView === 'calendar' && (
              <CalendarView tasks={filteredTasks} onTaskClick={handleTaskClick} />
            )}
            {currentView === 'gantt' && (
              <div className="h-full overflow-hidden" style={{ backgroundColor: 'var(--app-layer-surface)' }}>
                <GanttView data={data} filteredTasks={filteredTasks} scale={ganttScale} autoplan={ganttAutoplan} />
              </div>
            )}
          </div>
        </main>
      </div>

      {selectedTaskId && activeProject?.boardData?.tasks?.[selectedTaskId] && (
        <TaskDetailModal
          task={activeProject.boardData.tasks[selectedTaskId]}
          onClose={() => setSelectedTaskId(null)}
          readOnly={isViewer}
        />
      )}

      {canManageBoardStructure && (
        <BoardSettingsModal
          isOpen={isBoardSettingsOpen}
          onClose={() => setIsBoardSettingsOpen(false)}
          projectName={activeProject?.name || t('dashboard.projectFallback')}
        />
      )}

      {openStickerDropdown && stickerDropdownAnchor && (() => {
        const sticker = boardStickers.find((item) => item.id === openStickerDropdown);
        if (!sticker) return null;

        return (
          <StickerDropdown
            isFixed
            anchorRect={stickerDropdownAnchor}
            sticker={sticker}
            filterValues={stickerFilterValues[sticker.id] || new Set()}
            onFilterChange={(valueId) => {
              setStickerFilterValues((prev) => {
                const current = new Set(prev[sticker.id] || []);
                if (current.has(valueId)) current.delete(valueId);
                else current.add(valueId);
                return { ...prev, [sticker.id]: current };
              });
            }}
            onClose={() => setOpenStickerDropdown(null)}
            onRemove={() => {
              removeBoardSticker(sticker.id);
              setOpenStickerDropdown(null);
            }}
          />
        );
      })()}

      {showAddStickerMenu && addStickerMenuAnchor && (
        <AddStickerMenu
          isFixed
          anchorRect={addStickerMenuAnchor}
          currentIds={boardStickerIds}
          stickerCatalog={stickerCatalog}
          onAdd={(id) => {
            addBoardSticker(id);
          }}
          onClose={() => setShowAddStickerMenu(false)}
        />
      )}

      {ganttScaleMenuAnchor && (
        <GanttScaleDropdown
          currentScale={ganttScale}
          onSelect={(scale) => {
            setGanttScale(scale);
            setGanttScaleMenuAnchor(null);
          }}
          onClose={() => setGanttScaleMenuAnchor(null)}
          anchorRect={ganttScaleMenuAnchor}
        />
      )}

      {firstWorkspaceModal}
    </>
  );
};

const Dashboard = () => (
  <DashboardLayout>
    <DashboardContent />
  </DashboardLayout>
);

export default Dashboard;
