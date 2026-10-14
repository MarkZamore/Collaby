import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Search, Bell, Plus, MessageSquare, MoreVertical, X, Trash2 } from 'lucide-react';
import { usePlatform } from '../context/PlatformContext';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import NotificationPanel from './NotificationPanel';
import InviteMemberModal from './InviteMemberModal';
import { DeleteProjectModal } from './DeleteProjectModal';

const TopBar = ({ isMobile, onMenuClick }) => {
  const { t } = useTranslation(['platform']);
  const {
    users,
    activePage,
    setActivePage,
    activeProject,
    activeWorkspace,
    currentUser,
    currentUserRole,
    selectProject,
    createProject,
    deleteProject,
    unreadCount,
  } = usePlatform();

  const [isNotificationsOpen, setNotificationsOpen] = useState(false);
  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [isCreatingProject, setIsCreatingProject] = useState(false);
  const [projectMenuOpen, setProjectMenuOpen] = useState(null);
  const [projectMenuAnchor, setProjectMenuAnchor] = useState(null);
  const [deleteModal, setDeleteModal] = useState({ isOpen: false, project: null });
  const [newProjectName, setNewProjectName] = useState('');

  const notificationRef = useRef(null);
  const projectMenuRef = useRef(null);
  const newProjectInputRef = useRef(null);

  const workspaceMembers = useMemo(() => {
    if (!activeWorkspace?.members) {
      return [];
    }

    return activeWorkspace.members.map((member) => {
      const user = users.find((candidate) => candidate.id === member.userId);
      return {
        id: member.userId,
        name: user?.name || member.displayName || member.username || '?',
        avatar: resolveUserAvatar({
          avatarUrl: user?.avatar || member.avatarUrl,
          userId: member.userId,
          displayName: user?.name || member.displayName,
          username: member.username,
          email: member.email,
        }),
      };
    });
  }, [activeWorkspace, users]);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (notificationRef.current && !notificationRef.current.contains(event.target)) {
        setNotificationsOpen(false);
      }
    };

    if (isNotificationsOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }

    return undefined;
  }, [isNotificationsOpen]);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (projectMenuRef.current && !projectMenuRef.current.contains(event.target)) {
        setProjectMenuOpen(null);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useEffect(() => {
    if (isCreatingProject && newProjectInputRef.current) {
      newProjectInputRef.current.focus();
    }
  }, [isCreatingProject]);

  const handleCreateProject = () => {
    if (!newProjectName.trim() || !activeWorkspace) {
      return;
    }

    createProject(activeWorkspace.id, newProjectName.trim());
    setNewProjectName('');
    setIsCreatingProject(false);
  };

  const handleCreateProjectKeyDown = (event) => {
    if (event.key === 'Enter') {
      handleCreateProject();
    } else if (event.key === 'Escape') {
      setIsCreatingProject(false);
      setNewProjectName('');
    }
  };

  const projects = activeWorkspace?.projects || [];
  const canManageWorkspace = Boolean(
    currentUser?.isSystemAdmin || currentUserRole === 'Owner' || currentUserRole === 'Admin',
  );

  return (
    <div className="flex flex-col gap-1">
      <header className="mx-4 mt-2 flex h-12 items-center gap-4 px-2" style={{ color: 'var(--app-text-secondary)' }}>
        <div className="flex shrink-0 items-center gap-3">
          {isMobile && (
            <button
              type="button"
              onClick={onMenuClick}
              className="flex h-8 w-8 items-center justify-center rounded-lg -ml-1"
              style={{ color: 'var(--app-text-main)' }}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="3" y1="6" x2="21" y2="6" /><line x1="3" y1="12" x2="21" y2="12" /><line x1="3" y1="18" x2="21" y2="18" />
              </svg>
            </button>
          )}
          <h1 className="text-[15px] font-bold tracking-wide" style={{ color: 'var(--app-text-main)' }}>
            {activeWorkspace?.name || t('platform:topbar.workspaceFallback')}
          </h1>

          <div className="relative" ref={notificationRef}>
            <button
              type="button"
              onClick={() => setNotificationsOpen((prev) => !prev)}
              className="relative transition-colors hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              <Bell size={15} />
              {unreadCount > 0 && (
                <span className="absolute -right-1.5 -top-1.5 flex h-4 min-w-[16px] items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold leading-none text-white">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </button>
            <NotificationPanel isOpen={isNotificationsOpen} onClose={() => setNotificationsOpen(false)} />
          </div>

          <div className="flex -space-x-1.5">
            {workspaceMembers.slice(0, 4).map((member, index) => (
              <div
                key={member.id}
                className="relative h-7 w-7 shrink-0 overflow-hidden rounded-full text-[10px] font-bold"
                style={{
                  zIndex: 10 - index,
                  border: '2px solid var(--app-bg)',
                  backgroundColor: 'var(--app-layer-surface)',
                }}
                title={member.name}
              >
                {member.avatar ? (
                  <img
                    src={member.avatar}
                    alt={member.name}
                    className="h-full w-full rounded-full object-cover"
                    onError={(event) => {
                      event.target.style.display = 'none';
                      if (event.target.nextSibling) {
                        event.target.nextSibling.style.display = 'flex';
                      }
                    }}
                  />
                ) : null}
                <span
                  className="h-full w-full items-center justify-center text-[11px] font-bold"
                  style={{
                    display: member.avatar ? 'none' : 'flex',
                    color: 'var(--app-text-main)',
                  }}
                >
                  {member.name.charAt(0).toUpperCase()}
                </span>
              </div>
            ))}
            {workspaceMembers.length > 4 && (
              <div
                className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[10px] font-bold"
                style={{
                  zIndex: 5,
                  border: '2px solid var(--app-bg)',
                  backgroundColor: 'var(--app-layer-surface)',
                  color: 'var(--app-text-secondary)',
                }}
              >
                +{workspaceMembers.length - 4}
              </div>
            )}
          </div>

          {canManageWorkspace && (
            <button
              type="button"
              onClick={() => setIsInviteOpen(true)}
              className="transition-colors hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
              title={t('platform:topbar.invite')}
            >
              <Plus size={16} />
            </button>
          )}
        </div>

        <div className="mx-4 max-w-md flex-1">
          <div className="group relative">
            <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
              <Search size={14} style={{ color: 'var(--app-text-secondary)' }} />
            </div>
            <input
              type="text"
              placeholder={t('platform:topbar.searchPlaceholder')}
              className="w-full rounded-[14px] py-2 pl-9 pr-4 text-sm outline-none transition-all duration-200 placeholder:opacity-50"
              style={{
                backgroundColor: 'var(--app-layer-base)',
                border: '1px solid var(--app-border)',
                color: 'var(--app-text-main)',
              }}
            />
          </div>
        </div>

        <div className="ml-auto flex items-center gap-2">
          <button
            type="button"
            onClick={() => setActivePage('messenger')}
            className="relative rounded-[14px] p-2 transition-all duration-200 hover:opacity-80 hover:shadow-md"
            style={{
              backgroundColor:
                activePage === 'messenger' ? 'var(--app-accent)' : 'var(--app-layer-base)',
              color:
                activePage === 'messenger'
                  ? 'var(--app-accent-text, #fff)'
                  : 'var(--app-text-secondary)',
            }}
          >
            <MessageSquare size={16} />
          </button>
        </div>
      </header>

      <div className="toolbar-scroll-hidden mx-4 flex items-center gap-1 overflow-x-auto p-2">
        {projects.map((project) => (
          <div key={project.id} className="group relative flex items-center">
            <button
              type="button"
              onClick={() => selectProject(project.id)}
              className="relative shrink-0 rounded-[10px] px-3 py-1 pr-7 text-[14px] font-medium transition-all duration-200"
              style={{
                color:
                  activeProject?.id === project.id
                    ? 'var(--app-text-main)'
                    : 'var(--app-text-secondary)',
                backgroundColor:
                  activeProject?.id === project.id ? 'var(--app-layer-surface)' : 'transparent',
              }}
            >
              {project.name}
            </button>
            {canManageWorkspace && (
              <button
                type="button"
                onClick={(event) => {
                  event.stopPropagation();
                  const rect = event.currentTarget.getBoundingClientRect();
                  if (projectMenuOpen === project.id) {
                    setProjectMenuOpen(null);
                    setProjectMenuAnchor(null);
                  } else {
                    setProjectMenuOpen(project.id);
                    setProjectMenuAnchor(rect);
                  }
                }}
                className={`absolute right-1 top-1/2 -translate-y-1/2 rounded-sm p-0.5 transition-opacity hover:bg-black/10 dark:hover:bg-white/10 ${
                  activeProject?.id === project.id ? 'opacity-50 group-hover:opacity-100' : 'opacity-0 group-hover:opacity-100'
                }`}
                style={{ color: 'var(--app-text-secondary)' }}
              >
                <MoreVertical size={12} />
              </button>
            )}
          </div>
        ))}

        {canManageWorkspace && isCreatingProject ? (
          <div className="flex shrink-0 items-center gap-1">
            <input
              ref={newProjectInputRef}
              value={newProjectName}
              onChange={(event) => setNewProjectName(event.target.value)}
              onKeyDown={handleCreateProjectKeyDown}
              onBlur={() => {
                if (!newProjectName.trim()) {
                  setIsCreatingProject(false);
                }
              }}
              className="w-40 rounded-[10px] px-2 py-0.5 text-[13px] outline-none transition-all duration-200"
              style={{
                backgroundColor: 'var(--app-layer-base)',
                border: '1px solid var(--app-accent)',
                color: 'var(--app-text-main)',
              }}
              placeholder={t('platform:topbar.createProjectPlaceholder')}
            />
            <button
              type="button"
              onClick={handleCreateProject}
              className="rounded p-0.5 transition-colors hover:opacity-80"
              style={{ color: 'var(--app-accent)' }}
            >
              <Plus size={14} />
            </button>
            <button
              type="button"
              onClick={() => {
                setIsCreatingProject(false);
                setNewProjectName('');
              }}
              className="rounded p-0.5 transition-colors hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
            >
              <X size={14} />
            </button>
          </div>
        ) : canManageWorkspace ? (
          <button
            type="button"
            onClick={() => setIsCreatingProject(true)}
            className="shrink-0 transition-colors hover:opacity-80"
            style={{ color: 'var(--app-text-secondary)' }}
            title={t('platform:topbar.createProject')}
          >
            <Plus size={16} />
          </button>
        ) : null}
      </div>

      {canManageWorkspace && projectMenuOpen && projectMenuAnchor && (
        <div
          ref={projectMenuRef}
          className="fixed z-[999] w-48 overflow-hidden rounded-[14px] border shadow-xl"
          style={{
            top: projectMenuAnchor.bottom + 4,
            left: projectMenuAnchor.left,
            backgroundColor: 'var(--app-layer-surface)',
            borderColor: 'var(--app-border)',
          }}
        >
          <button
            type="button"
            className="flex w-full items-center gap-2 px-4 py-2 text-left text-sm text-red-500 hover:bg-white/5"
            onClick={(event) => {
              event.stopPropagation();
              const project = activeWorkspace?.projects?.find((item) => item.id === projectMenuOpen);
              setDeleteModal({ isOpen: true, project });
              setProjectMenuOpen(null);
              setProjectMenuAnchor(null);
            }}
          >
            <Trash2 size={14} /> {t('platform:topbar.deleteProject')}
          </button>
        </div>
      )}

      <InviteMemberModal isOpen={isInviteOpen} onClose={() => setIsInviteOpen(false)} />

      <DeleteProjectModal
        project={deleteModal.project}
        isOpen={deleteModal.isOpen}
        onConfirm={async (projectId) => {
          try {
            await deleteProject(activeWorkspace.id, projectId);
            setDeleteModal({ isOpen: false, project: null });
          } catch (error) {
            console.error('Failed to delete project:', error);
            alert(`${t('platform:topbar.deleteProjectError')}: ${error.message}`);
          }
        }}
        onCancel={() => setDeleteModal({ isOpen: false, project: null })}
      />
    </div>
  );
};

export default TopBar;
