import React, { useEffect, useRef, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  CheckSquare,
  Users,
  ChevronLeft,
  ChevronRight,
  Plus,
  Inbox,
  ShoppingBag,
  BarChart2,
  ChevronDown,
  ChevronRight as ChevronRightIcon,
  Hash,
  Trash2,
  Sparkles,
  MessageSquare,
  Gamepad2,
  FileText,
  Trophy,
} from 'lucide-react';
import {
  DndContext,
  closestCenter,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  SortableContext,
  verticalListSortingStrategy,
  useSortable,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { cn } from '../../shared/utils/cn';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import { getEquippedAvatarFrame } from '../../shared/utils/avatarFrames';
import AvatarWithFrame from '../../shared/components/AvatarWithFrame';
import { useGamification } from '../../shared/context/GamificationContext';
import { usePlatform } from '../context/PlatformContext';
import { useMessenger } from '../context/MessengerContext';
import CreateWorkspaceModal from './CreateWorkspaceModal';
import CreateProjectModal from './CreateProjectModal';
import { DeleteWorkspaceModal } from './DeleteWorkspaceModal';
import GlobalXpBar from './gamification/GlobalXpBar';

const WorkspaceRow = ({
  ws,
  expandedWorkspaces,
  toggleWorkspace,
  handleCreateProject,
  setDeleteModal,
  canManageWorkspace,
  selectProject,
  setActivePage,
  setActiveItem,
  activeProjectId,
  navigate,
  t,
}) => {
  const { attributes, listeners, setNodeRef, transform, transition } = useSortable({ id: ws.id });

  const adjustedTransform = transform ? { ...transform, x: 0 } : null;
  const style = {
    transform: CSS.Transform.toString(adjustedTransform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style} className="flex flex-col">
      <div
        className="group/ws flex cursor-pointer items-center justify-between rounded-lg px-0 py-1.5 transition-colors hover:opacity-80"
        onClick={() => toggleWorkspace(ws.id)}
        {...attributes}
        {...listeners}
      >
        <div className="-ml-3 flex items-center gap-1 overflow-hidden">
          <span className={`h-2 w-2 shrink-0 rounded-full ${ws.color} shadow-sm`} />
          <span className="truncate text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>
            {ws.name}
          </span>
        </div>
        <div className="flex items-center gap-1">
          {canManageWorkspace && (
            <button
              type="button"
              onClick={(event) => handleCreateProject(event, ws.id)}
              className="p-1 opacity-0 transition-all hover:opacity-80 group-hover/ws:opacity-100"
              style={{ color: 'var(--app-text-secondary)' }}
              title={t('platform:sidebar.createProject')}
            >
              <Plus size={12} />
            </button>
          )}
          {canManageWorkspace && (
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                setDeleteModal({ isOpen: true, workspace: ws });
              }}
              className="p-1 text-red-500 opacity-0 transition-all hover:opacity-80 group-hover/ws:opacity-100"
              title={t('platform:sidebar.deleteWorkspace')}
            >
              <Trash2 size={11} />
            </button>
          )}
          {expandedWorkspaces[ws.id] ? (
            <ChevronDown size={14} style={{ color: 'var(--app-text-secondary)' }} />
          ) : (
            <ChevronRightIcon size={14} style={{ color: 'var(--app-text-secondary)' }} />
          )}
        </div>
      </div>

      {expandedWorkspaces[ws.id] && (
        <div className="mt-1 space-y-0.5">
          {ws.projects.length === 0 && (
            <div className="px-2 py-1 text-xs italic" style={{ color: 'var(--app-text-secondary)' }}>
              {t('platform:sidebar.emptyProjects')}
            </div>
          )}
          {ws.projects.map((project) => (
            <button
              key={project.id}
              type="button"
              onClick={() => {
                selectProject(project.id);
                setActivePage('dashboard');
                setActiveItem(null);
                navigate('/dashboard');
              }}
              className={cn(
                'w-full rounded-lg px-2 py-1.5 text-left text-sm transition-colors',
                'flex items-center gap-2',
                activeProjectId === project.id ? 'font-medium' : 'hover:opacity-80'
              )}
              style={{
                backgroundColor:
                  activeProjectId === project.id ? 'var(--app-sidebar-item-active-bg)' : 'transparent',
                color: activeProjectId === project.id ? 'var(--app-text-main)' : 'var(--app-text-secondary)',
              }}
            >
              <Hash size={12} />
              <span className="truncate">{project.name}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
};

const Sidebar = ({ isOpen, isMobile, toggleSidebar, onNavClick }) => {
  const { t } = useTranslation(['platform']);
  const navigate = useNavigate();
  const location = useLocation();

  const getItemFromPath = (pathname) => {
    if (pathname.startsWith('/assistant')) return 'assistant';
    if (pathname.startsWith('/mentor')) return 'mentor';
    if (pathname.startsWith('/inbox')) return 'inbox';
    if (pathname.startsWith('/tasks')) return 'my-tasks';
    if (pathname.startsWith('/messenger')) return 'messenger';
    if (pathname.startsWith('/team')) return 'team';
    if (pathname.startsWith('/analytics')) return 'analytics';
    if (pathname.startsWith('/shop')) return 'shop';
    if (pathname.startsWith('/awards')) return 'awards';
    if (pathname.startsWith('/gamification')) return 'gamification';
    if (pathname.includes('/dashboard/inbox')) return 'inbox';
    if (pathname.includes('/dashboard/tasks')) return 'my-tasks';
    if (pathname.includes('/dashboard/messenger')) return 'messenger';
    if (pathname.includes('/dashboard/team')) return 'team';
    if (pathname.includes('/dashboard/analytics')) return 'analytics';
    if (pathname.includes('/dashboard/shop')) return 'shop';
    if (pathname.includes('/dashboard/gamification')) return 'gamification';
    return null;
  };

  const [activeItem, setActiveItem] = useState(() => getItemFromPath(location.pathname) || 'inbox');
  const [expandedWorkspaces, setExpandedWorkspaces] = useState({});
  const [isCreateWorkspaceOpen, setIsCreateWorkspaceOpen] = useState(false);
  const [createProjectModal, setCreateProjectModal] = useState({ isOpen: false, workspaceId: null });
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, workspace: null });

  const messengerCtx = useMessenger();
  const messengerUnread = messengerCtx?.unreadTotal || 0;
  const { inventory } = useGamification();

  const {
    workspaces,
    activeProjectId,
    currentUser,
    currentUserRole,
    selectProject,
    createWorkspace,
    deleteWorkspace,
    reorderWorkspaces,
    setActivePage,
    inboxCount,
    unreadCount,
  } = usePlatform();

  useEffect(() => {
    const item = getItemFromPath(location.pathname);
    // item is null for /dashboard (project board view) — still update to deselect nav items
    setActiveItem(item);
  }, [location.pathname]);

  const displayName = currentUser?.name || t('platform:sidebar.defaultUser');
  const equippedAvatarFrame = getEquippedAvatarFrame(inventory);
  const firstNavBtnRef = useRef(null);

  const workspaceSensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 5 },
    })
  );

  const navItems = [
    { id: 'inbox', icon: Inbox, label: t('platform:sidebar.nav.inbox'), count: unreadCount },
    { id: 'my-tasks', icon: CheckSquare, label: t('platform:sidebar.nav.tasks'), count: inboxCount },
    { id: 'messenger', icon: MessageSquare, label: t('platform:sidebar.nav.messenger'), count: messengerUnread },
    { id: 'team', icon: Users, label: t('platform:sidebar.nav.team') },
    { id: 'analytics', icon: BarChart2, label: t('platform:sidebar.nav.analytics') },
    { id: 'shop', icon: ShoppingBag, label: t('platform:sidebar.nav.shop') },
    { id: 'awards', icon: Trophy, label: t('platform:sidebar.nav.awards') },
    { id: 'gamification', icon: Gamepad2, label: t('platform:sidebar.nav.gamification') },
  ];
  const canUseMentor = Boolean(currentUser?.isSystemAdmin || currentUserRole === 'Owner' || currentUserRole === 'Admin');
  const canManageWorkspace = canUseMentor;
  const aiRouteItems = [
    {
      id: 'assistant',
      icon: Sparkles,
      label: t('platform:sidebar.aiAssistant'),
      route: '/assistant',
    },
    ...(canUseMentor
      ? [{
          id: 'mentor',
          icon: FileText,
          label: t('aiAssistant.mentor.title', { defaultValue: 'Контекст наставника' }),
          route: '/mentor',
        }]
      : []),
  ];

  const toggleWorkspace = (workspaceId) => {
    setExpandedWorkspaces((prev) => ({
      ...prev,
      [workspaceId]: !prev[workspaceId],
    }));
  };

  const handleCreateWorkspace = (data) => {
    createWorkspace(data.workspaceName, {
      usagePurpose: data.usagePurpose,
      managementTypes: data.managementTypes,
    });
  };

  const handleCreateProject = (event, workspaceId) => {
    event.stopPropagation();
    setCreateProjectModal({ isOpen: true, workspaceId });
    setExpandedWorkspaces((prev) => ({ ...prev, [workspaceId]: true }));
  };

  const handleWorkspaceDragEnd = (event) => {
    const { active, over } = event;
    if (!over || active.id === over.id) {
      return;
    }

    reorderWorkspaces(active.id, over.id);
  };

  return (
    <>
      <aside
        style={{
          backgroundColor: 'var(--app-sidebar-bg)',
          color: 'var(--app-sidebar-text)',
          borderColor: 'var(--app-border)',
        }}
        className={cn(
          'z-40 flex flex-col border transition-all duration-300 ease-in-out',
          isMobile
            ? 'h-screen w-72 rounded-none'
            : 'my-2 ml-4 h-[calc(100vh-1rem)] rounded-2xl',
          !isMobile && (isOpen ? 'w-64' : 'w-20')
        )}
      >
        <div
          className={cn(
            'group relative flex h-16 cursor-pointer items-center border-b rounded-t-2xl transition-colors hover:bg-white/5',
            isOpen ? 'px-4' : 'justify-center px-0'
          )}
          style={{ borderColor: 'var(--app-border)' }}
          onClick={() => navigate('/profile')}
          data-testid="nav-profile"
        >
          <div className={cn('flex min-w-0 flex-1 items-center', isOpen ? 'gap-3' : 'justify-center gap-0')}>
            <AvatarWithFrame
              src={currentUser?.avatar}
              alt={displayName}
              frame={equippedAvatarFrame}
              sizeClassName="h-10 w-10"
              noFrameShellClassName="bg-slate-200 ring-2 ring-white/20 transition-all group-hover:ring-orange-500/50 dark:bg-slate-700 dark:ring-white/10 flex items-center justify-center"
              imageTestId="sidebar-user-avatar"
              onError={(event) => {
                event.currentTarget.src = resolveUserAvatar({
                  userId: currentUser?.id,
                  displayName,
                  username: currentUser?.username,
                  email: currentUser?.email,
                });
              }}
            />
            <div className={cn('flex flex-col overflow-hidden transition-opacity duration-200', isOpen ? 'opacity-100' : 'min-w-0 w-0 opacity-0')}>
              <span
                className="truncate whitespace-nowrap text-sm font-bold"
                style={{ color: 'var(--app-sidebar-text)' }}
                data-testid="sidebar-user-name"
              >
                {displayName}
              </span>
              <span className="truncate whitespace-nowrap text-sm font-bold" style={{ color: 'var(--app-sidebar-text)' }}>
                {t('platform:sidebar.profile')}
              </span>
            </div>
          </div>

          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              toggleSidebar();
            }}
            className="absolute -right-3 top-1/2 z-50 flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded-full border shadow-md transition-all duration-200 hover:scale-110 hover:shadow-lg"
            style={{
              backgroundColor: 'var(--app-layer-surface)',
              borderColor: 'var(--app-border)',
              color: 'var(--app-text-secondary)',
            }}
          >
            {isOpen ? <ChevronLeft size={14} /> : <ChevronRight size={14} />}
          </button>
        </div>

        <nav className="sidebar-nav custom-scrollbar flex-1 space-y-1 overflow-x-hidden overflow-y-auto px-3 py-6">
          <div className="mb-6 space-y-1">
            {navItems.map((item) => {
              const rawCount = item.count;
              const numericCount =
                typeof rawCount === 'number'
                  ? rawCount
                  : Number.isNaN(parseInt(rawCount, 10))
                    ? 0
                    : parseInt(rawCount, 10);
              const hasCount = numericCount > 0;

              return (
                <button
                  key={item.id}
                  type="button"
                  ref={navItems[0]?.id === item.id ? firstNavBtnRef : undefined}
                  onClick={() => {
                    setActiveItem(item.id);
                    const routes = {
                      inbox: '/inbox',
                      'my-tasks': '/tasks',
                      messenger: '/messenger',
                      team: '/team',
                      analytics: '/analytics',
                      shop: '/shop',
                      awards: '/awards',
                      gamification: '/gamification',
                    };
                    navigate(routes[item.id] || '/dashboard');
                    onNavClick?.();
                  }}
                  className={cn(
                    'group relative flex w-full items-center gap-3 rounded-[14px] px-3 py-2.5 text-sm font-medium transition-all duration-200 pointer-events-auto',
                    !isOpen && 'justify-center px-0',
                    activeItem === item.id ? 'sidebar-item-active' : 'opacity-70 hover:opacity-100 hover:bg-white/5'
                  )}
                  style={
                    activeItem === item.id
                      ? {
                          backgroundColor: 'var(--app-sidebar-item-active-bg)',
                          color: 'var(--app-sidebar-text)',
                          opacity: 1,
                        }
                      : {}
                  }
                >
                  <div className="flex h-5 w-5 shrink-0 items-center justify-center">
                    <item.icon
                      size={20}
                      strokeWidth={2}
                      className={cn(
                        'transition-transform duration-200',
                        activeItem === item.id && 'scale-110 text-accent'
                      )}
                    />
                  </div>
                  <span className={cn('transition-opacity duration-200', isOpen ? 'opacity-100' : 'min-w-0 w-0 overflow-hidden opacity-0')}>
                    {item.label}
                  </span>
                  {hasCount && isOpen && (
                    <span
                      className="ml-2 rounded-full px-2 py-0.5 text-xs font-bold"
                      style={{
                        backgroundColor: 'var(--app-accent)',
                        color: 'var(--app-accent-text)',
                      }}
                    >
                      {numericCount}
                    </span>
                  )}
                  {hasCount && !isOpen && (
                    <span
                      className="absolute -right-1 -top-1 flex h-[18px] min-w-[18px] items-center justify-center rounded-full px-1 text-[10px] font-bold"
                      style={{
                        backgroundColor: 'var(--app-accent)',
                        color: 'var(--app-accent-text)',
                      }}
                    >
                      {numericCount}
                    </span>
                  )}
                  {!isOpen && (
                    <div
                      className="pointer-events-none absolute left-full ml-4 whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-medium opacity-0 shadow-xl group-hover:opacity-100 z-50"
                      style={{
                        backgroundColor: 'var(--app-layer-surface)',
                        color: 'var(--app-text-main)',
                        border: '1px solid var(--app-border)',
                      }}
                    >
                      {item.label}
                    </div>
                  )}
                </button>
              );
            })}
          </div>

          {isOpen && (
            <div className="mb-2 px-3">
              <div className="mb-3 flex items-center justify-between">
                <span className="text-[10px] font-bold uppercase tracking-wider opacity-50">
                  {t('platform:sidebar.workspaces')}
                </span>
                {canManageWorkspace && (
                  <button
                    type="button"
                    onClick={() => setIsCreateWorkspaceOpen(true)}
                    className="opacity-50 transition-opacity hover:opacity-100"
                    title={t('platform:sidebar.workspaces')}
                  >
                    <Plus size={14} />
                  </button>
                )}
              </div>
              <DndContext sensors={workspaceSensors} collisionDetection={closestCenter} onDragEnd={handleWorkspaceDragEnd}>
                <SortableContext items={workspaces.map((workspace) => workspace.id)} strategy={verticalListSortingStrategy}>
                  <div className="space-y-2">
                    {workspaces.map((workspace) => (
                      <WorkspaceRow
                        key={workspace.id}
                        ws={workspace}
                        expandedWorkspaces={expandedWorkspaces}
                        toggleWorkspace={toggleWorkspace}
                        handleCreateProject={handleCreateProject}
                        setDeleteModal={setDeleteModal}
                        canManageWorkspace={canManageWorkspace}
                        selectProject={selectProject}
                        setActivePage={setActivePage}
                        setActiveItem={setActiveItem}
                        activeProjectId={activeProjectId}
                        navigate={navigate}
                        t={t}
                      />
                    ))}
                  </div>
                </SortableContext>
              </DndContext>
            </div>
          )}
        </nav>

        <div className={cn('shrink-0 space-y-2 pb-4 pt-2', isOpen ? 'px-3' : 'px-0')} style={{ borderTop: '1px solid var(--app-border)' }}>
          {aiRouteItems.map((item) => {
            const Icon = item.icon;
            const isActive = activeItem === item.id;

            return (
              <button
                key={item.id}
                type="button"
                onClick={() => {
                  setActiveItem(item.id);
                  setActivePage(item.id);
                  navigate(item.route);
                  onNavClick?.();
                }}
                className={cn(
                  'group relative flex w-full items-center rounded-[14px] px-3 py-2.5 text-sm font-semibold transition-all',
                  'gap-3 shadow-sm hover:scale-[1.02] hover:shadow-md active:scale-[0.98]',
                  !isOpen && 'justify-center gap-0 px-0'
                )}
                style={{
                  background: isActive
                    ? 'linear-gradient(135deg, color-mix(in srgb, var(--app-accent) 28%, transparent), color-mix(in srgb, var(--app-accent) 12%, transparent))'
                    : 'linear-gradient(135deg, color-mix(in srgb, var(--app-accent) 18%, transparent), color-mix(in srgb, var(--app-accent) 6%, transparent))',
                  border: '1px solid color-mix(in srgb, var(--app-accent) 55%, var(--app-border))',
                  color: 'var(--app-text-main)',
                }}
              >
                <div
                  className="flex h-5 w-5 shrink-0 items-center justify-center rounded-md"
                  style={{
                    background: 'linear-gradient(135deg, var(--app-accent), color-mix(in srgb, var(--app-accent) 60%, #000))',
                  }}
                >
                  <Icon size={12} className="text-white" />
                </div>
                <span className={cn('transition-opacity duration-200', isOpen ? 'opacity-100' : 'min-w-0 w-0 overflow-hidden opacity-0')}>
                  {item.label}
                </span>
                {!isOpen && (
                  <div
                    className="pointer-events-none absolute left-full ml-4 whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-medium opacity-0 shadow-xl group-hover:opacity-100 z-50"
                    style={{
                      backgroundColor: 'var(--app-layer-surface)',
                      color: 'var(--app-text-main)',
                      border: '1px solid var(--app-border)',
                    }}
                  >
                    {item.label}
                  </div>
                )}
              </button>
            );
          })}
        </div>

        {isOpen && <GlobalXpBar />}
      </aside>

      <CreateWorkspaceModal
        isOpen={isCreateWorkspaceOpen}
        onClose={() => setIsCreateWorkspaceOpen(false)}
        onComplete={handleCreateWorkspace}
      />
      <CreateProjectModal
        isOpen={createProjectModal.isOpen}
        onClose={() => setCreateProjectModal({ isOpen: false, workspaceId: null })}
        workspaceId={createProjectModal.workspaceId}
      />
      <DeleteWorkspaceModal
        workspace={deleteModal.workspace}
        isOpen={deleteModal.isOpen}
        onConfirm={async (workspaceId) => {
          try {
            await deleteWorkspace(workspaceId);
            setDeleteModal({ isOpen: false, workspace: null });
          } catch (error) {
            console.error('Failed to delete workspace:', error);
          }
        }}
        onCancel={() => setDeleteModal({ isOpen: false, workspace: null })}
      />
    </>
  );
};

export default Sidebar;
