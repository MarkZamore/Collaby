import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Activity,
  AlertCircle,
  BarChart2,
  Briefcase,
  Check,
  ChevronDown,
  Clock,
  Crown,
  Eye,
  MoreVertical,
  Search,
  Shield,
  Star,
  Trash2,
  TrendingUp,
  User,
  UserPlus,
  Users,
  X,
} from 'lucide-react';
import { cn } from '../../shared/utils/cn';
import { buildFallbackAvatar, resolveUserAvatar } from '../../shared/utils/avatar';
import { isDoneStatus, normalizeStatusKey } from '../utils/statuses';
import { usePlatform } from '../context/PlatformContext';
import InviteMemberModal from '../components/InviteMemberModal';


const getRoleMeta = (t) => ({
  Owner: {
    label: t('team.roles.owner.label'),
    description: t('team.roles.owner.description'),
    icon: Crown,
    color: 'var(--role-owner, #F59E0B)',
    bg: 'color-mix(in srgb, var(--role-owner, #F59E0B) 12%, transparent)',
  },
  Admin: {
    label: t('team.roles.admin.label'),
    description: t('team.roles.admin.description'),
    icon: Shield,
    color: 'var(--role-admin, #8B5CF6)',
    bg: 'color-mix(in srgb, var(--role-admin, #8B5CF6) 12%, transparent)',
  },
  Member: {
    label: t('team.roles.member.label'),
    description: t('team.roles.member.description'),
    icon: User,
    color: 'var(--role-member, #3B82F6)',
    bg: 'color-mix(in srgb, var(--role-member, #3B82F6) 12%, transparent)',
  },
  Viewer: {
    label: t('team.roles.viewer.label'),
    description: t('team.roles.viewer.description'),
    icon: Eye,
    color: 'var(--role-viewer, #6B7280)',
    bg: 'color-mix(in srgb, var(--role-viewer, #6B7280) 12%, transparent)',
  },
});

const RoleBadge = ({ role }) => {
  const { t } = useTranslation('platform');
  const roleMeta = getRoleMeta(t);
  const cfg = roleMeta[role] || roleMeta.Member;
  const Icon = cfg.icon;

  return (
    <span
      className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold"
      style={{ backgroundColor: cfg.bg, color: cfg.color }}
    >
      <Icon size={11} />
      {cfg.label}
    </span>
  );
};

const RoleDropdown = ({ currentRole, memberId, workspaceId, onClose, anchorRect, currentUserRole }) => {
  const { t } = useTranslation('platform');
  const { changeMemberRole } = usePlatform();
  const ref = useRef(null);
  const canAssign = currentUserRole === 'Owner' || currentUserRole === 'Admin';
  const roleMeta = getRoleMeta(t);
  const roles = Object.entries(roleMeta).filter(([roleId]) => roleId !== 'Owner');

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (ref.current && !ref.current.contains(event.target)) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, [onClose]);

  const handleSelect = async (role) => {
    if (!canAssign) {
      return;
    }

    try {
      await changeMemberRole(workspaceId, memberId, role);
    } catch {
      // Ошибки обновления роли уже пробрасываются через platform store.
    }

    onClose();
  };

  if (!anchorRect) {
    return null;
  }

  return (
    <div
      ref={ref}
      className="fixed z-[900] w-52 rounded-xl overflow-hidden shadow-2xl py-1"
      style={{
        top: anchorRect.bottom + 4,
        left: anchorRect.left,
        backgroundColor: 'var(--app-layer-surface)',
        border: '1px solid var(--app-border)',
      }}
    >
      {roles.map(([roleId, cfg]) => {
        const Icon = cfg.icon;
        const isActive = currentRole === roleId;

        return (
          <button
            key={roleId}
            onClick={() => handleSelect(roleId)}
            disabled={!canAssign}
            className="w-full flex items-center gap-2.5 px-3 py-2 text-left hover:bg-white/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <Icon size={13} style={{ color: cfg.color }} />
            <div className="flex-1">
              <div className="text-sm" style={{ color: 'var(--app-text-main)' }}>
                {cfg.label}
              </div>
            </div>
            {isActive && <Check size={13} style={{ color: 'var(--app-accent)' }} />}
          </button>
        );
      })}
    </div>
  );
};

const MemberCard = ({ member, workspaceId, currentUserRole, currentUserId, tasks, onKickSuccess }) => {
  const { t } = useTranslation('platform');
  const { kickMember } = usePlatform();
  const [menuOpen, setMenuOpen] = useState(false);
  const [roleDropdown, setRoleDropdown] = useState(null);
  const [kickError, setKickError] = useState('');
  const [kicking, setKicking] = useState(false);
  const menuRef = useRef(null);

  const displayName = member.displayName || member.username || `user-${String(member.userId).slice(0, 6)}`;
  const avatar = resolveUserAvatar({
    avatarUrl: member.avatarUrl,
    userId: member.userId,
    displayName,
    username: member.username,
    email: member.email,
  });
  const isCurrentUser = member.userId === currentUserId;
  const isOwner = member.role === 'Owner';
  const canManage = (currentUserRole === 'Owner' || currentUserRole === 'Admin') && !isOwner && !isCurrentUser;

  const memberTasks = tasks.filter((task) => task.assigneeId === member.userId);
  const doneTasks = memberTasks.filter((task) => isDoneStatus(task.statusRaw || task.status)).length;
  const activeTasks = memberTasks.filter((task) => {
    const statusKey = normalizeStatusKey(task.statusRaw || task.status);
    return statusKey === 'inProgress' || statusKey === 'review';
  }).length;

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (menuRef.current && !menuRef.current.contains(event.target)) {
        setMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, []);

  const handleKick = async () => {
    if (!canManage) {
      return;
    }

    setMenuOpen(false);
    setKickError('');
    setKicking(true);

    try {
      await kickMember(workspaceId, member.userId);
      onKickSuccess?.();
    } catch (error) {
      setKickError(error?.message || t('team.member.removeError'));
    } finally {
      setKicking(false);
    }
  };

  return (
    <div className="flex flex-col gap-1">
      <div
        className="flex items-center gap-4 px-4 py-3.5 rounded-2xl transition-all hover:shadow-sm group"
        style={{
          backgroundColor: 'var(--app-layer-surface)',
          border: `1px solid ${kickError ? 'var(--color-error, #EF4444)' : 'var(--app-border)'}`,
        }}
      >
        <div className="relative shrink-0">
          <img
            src={avatar}
            alt={displayName}
            className="w-11 h-11 rounded-full object-cover"
            onError={(event) => {
              event.target.src = buildFallbackAvatar(member.userId);
            }}
          />
          <span
            className="absolute bottom-0 right-0 w-3 h-3 rounded-full border-2"
            style={{
              backgroundColor: 'var(--color-success, #22C55E)',
              borderColor: 'var(--app-layer-surface)',
            }}
          />
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold truncate" style={{ color: 'var(--app-text-main)' }}>
              {displayName}
              {isCurrentUser && (
                <span className="ml-1.5 text-xs font-normal opacity-50">{t('team.member.you')}</span>
              )}
            </span>
          </div>
          <div className="text-xs truncate mt-0.5" style={{ color: 'var(--app-text-secondary)' }}>
            {member.username ? `@${member.username}` : member.email || t('team.member.noData')}
          </div>
        </div>

        <div className="hidden sm:flex items-center gap-4 mr-2 shrink-0">
          <div className="text-center">
            <div className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
              {memberTasks.length}
            </div>
            <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
              {t('team.member.taskCount')}
            </div>
          </div>
          <div className="text-center">
            <div className="text-sm font-bold" style={{ color: 'var(--color-success, #22C55E)' }}>
              {doneTasks}
            </div>
            <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
              {t('team.member.done')}
            </div>
          </div>
          <div className="text-center">
            <div className="text-sm font-bold" style={{ color: 'var(--color-warning, #F59E0B)' }}>
              {activeTasks}
            </div>
            <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
              {t('team.member.inProgress')}
            </div>
          </div>
        </div>

        <div className="shrink-0 relative">
          <button
            onClick={(event) => {
              if (!canManage) {
                return;
              }

              const rect = event.currentTarget.getBoundingClientRect();
              setRoleDropdown((prev) => (prev ? null : rect));
            }}
            className={cn('transition-opacity', canManage ? 'hover:opacity-80 cursor-pointer' : 'cursor-default')}
            title={canManage ? t('team.member.changeRole') : undefined}
          >
            <RoleBadge role={member.role} />
            {canManage && <ChevronDown size={11} className="ml-0.5 inline opacity-50" />}
          </button>

          {roleDropdown && (
            <RoleDropdown
              currentRole={member.role}
              memberId={member.userId}
              workspaceId={workspaceId}
              currentUserRole={currentUserRole}
              anchorRect={roleDropdown}
              onClose={() => setRoleDropdown(null)}
            />
          )}
        </div>

        <div className="relative shrink-0 ml-1" ref={menuRef}>
          {canManage && (
            <button
              onClick={() => setMenuOpen((value) => !value)}
              className={cn('p-1.5 rounded-lg transition-all', 'opacity-0 group-hover:opacity-100', menuOpen && 'opacity-100')}
              style={{ color: 'var(--app-text-secondary)' }}
            >
              <MoreVertical size={15} />
            </button>
          )}

          {menuOpen && (
            <div
              className="absolute right-0 top-full mt-1 w-52 rounded-xl overflow-hidden shadow-2xl z-50"
              style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
            >
              <button
                onClick={handleKick}
                disabled={kicking}
                className="w-full flex items-center gap-2 px-3 py-2.5 text-sm hover:bg-red-500/10 transition-colors text-red-400 disabled:opacity-50"
              >
                <Trash2 size={13} />
                {kicking ? t('team.member.removing') : t('team.member.remove')}
              </button>
            </div>
          )}
        </div>
      </div>

      {kickError && (
        <div
          className="px-4 py-2 rounded-xl text-xs flex items-center gap-2"
          style={{ backgroundColor: 'rgba(239,68,68,0.1)', color: 'var(--color-error, #EF4444)' }}
        >
          <AlertCircle size={12} />
          {kickError}
          <button onClick={() => setKickError('')} className="ml-auto opacity-60 hover:opacity-100">
            <X size={11} />
          </button>
        </div>
      )}
    </div>
  );
};

const StatCard = ({ icon: Icon, value, label, color }) => (
  <div
    className="flex flex-col gap-2 px-5 py-4 rounded-2xl flex-1 min-w-[120px]"
    style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
  >
    <div className="w-9 h-9 rounded-xl flex items-center justify-center" style={{ backgroundColor: `${color}18` }}>
      <Icon size={18} style={{ color }} />
    </div>
    <div>
      <div className="text-xl font-bold" style={{ color: 'var(--app-text-main)' }}>
        {value}
      </div>
      <div className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
        {label}
      </div>
    </div>
  </div>
);

const TeamPage = () => {
  const { t } = useTranslation('platform');
  const { workspaces, activeWorkspace, currentUser, activeProject, refreshWorkspaceMembers, activeWorkspaceId } = usePlatform();

  const [selectedWsId, setSelectedWsId] = useState(activeWorkspaceId || null);
  const [wsDropdown, setWsDropdown] = useState(false);
  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [roleFilter, setRoleFilter] = useState('all');
  const wsRef = useRef(null);

  useEffect(() => {
    if (activeWorkspaceId && !selectedWsId) {
      setSelectedWsId(activeWorkspaceId);
    }
  }, [activeWorkspaceId, selectedWsId]);

  useEffect(() => {
    if (selectedWsId) {
      refreshWorkspaceMembers(selectedWsId);
    }
  }, [selectedWsId, refreshWorkspaceMembers]);

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (wsRef.current && !wsRef.current.contains(event.target)) {
        setWsDropdown(false);
      }
    };

    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, []);

  const selectedWorkspace = workspaces.find((workspace) => workspace.id === selectedWsId) || activeWorkspace;
  const currentUserRole = selectedWorkspace?.role || 'Member';
  const canManageWorkspace = Boolean(
    currentUser?.isSystemAdmin || currentUserRole === 'Owner' || currentUserRole === 'Admin',
  );
  const members = selectedWorkspace?.members || [];
  const tasks = activeProject ? Object.values(activeProject.boardData?.tasks || {}) : [];

  const filteredMembers = members.filter((member) => {
    const name = (member.displayName || member.username || '').toLowerCase();
    const email = (member.email || '').toLowerCase();
    const query = search.toLowerCase();

    if (query && !name.includes(query) && !email.includes(query)) {
      return false;
    }

    if (roleFilter !== 'all' && member.role !== roleFilter) {
      return false;
    }

    return true;
  });

  const completedTasks = tasks.filter((task) => isDoneStatus(task.statusRaw || task.status)).length;

  const roleOptions = useMemo(
    () => [
      { value: 'all', label: t('team.filters.all') },
      { value: 'Owner', label: t('team.filters.owner') },
      { value: 'Admin', label: t('team.filters.admin') },
      { value: 'Member', label: t('team.filters.member') },
      { value: 'Viewer', label: t('team.filters.viewer') },
    ],
    [t],
  );

  const roleMeta = useMemo(() => getRoleMeta(t), [t]);

  return (
    <div className="flex flex-col h-full overflow-y-auto px-6 py-5 gap-6" style={{ color: 'var(--app-text-main)' }}>
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Users size={22} style={{ color: 'var(--app-accent)' }} />
            {t('team.title')}
          </h1>
          <p className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
            {t('team.subtitle')}
          </p>
        </div>

        <div className="flex items-center gap-3 flex-wrap">
          {workspaces.length > 1 && (
            <div ref={wsRef} className="relative">
              <button
                onClick={() => setWsDropdown((value) => !value)}
                className="flex items-center gap-2 px-3 py-2 rounded-xl text-sm font-medium transition-all hover:opacity-80"
                style={{
                  backgroundColor: 'var(--app-layer-surface)',
                  border: '1px solid var(--app-border)',
                  color: 'var(--app-text-main)',
                }}
              >
                <Briefcase size={13} style={{ color: 'var(--app-accent)' }} />
                <span className="truncate max-w-[140px]">
                  {selectedWorkspace?.name || t('team.workspaceFallback')}
                </span>
                <ChevronDown
                  size={13}
                  className={wsDropdown ? 'rotate-180' : ''}
                  style={{ color: 'var(--app-text-secondary)', transition: 'transform 0.2s' }}
                />
              </button>

              {wsDropdown && (
                <div
                  className="absolute right-0 top-full mt-1 w-56 rounded-xl overflow-hidden shadow-2xl z-50"
                  style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
                >
                  {workspaces.map((workspace) => (
                    <button
                      key={workspace.id}
                      onClick={() => {
                        setSelectedWsId(workspace.id);
                        setWsDropdown(false);
                      }}
                      className="w-full flex items-center gap-2 px-3 py-2.5 text-left text-sm hover:bg-white/5 transition-colors"
                      style={{ color: 'var(--app-text-main)' }}
                    >
                      <span className={`w-2 h-2 rounded-full ${workspace.color || 'bg-blue-500'}`} />
                      <span className="flex-1 truncate">{workspace.name}</span>
                      {workspace.id === selectedWsId && <Check size={13} style={{ color: 'var(--app-accent)' }} />}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}

          {canManageWorkspace && (
            <button
              onClick={() => setIsInviteOpen(true)}
              className="flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-semibold transition-all hover:opacity-90 shadow-lg"
              style={{ backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }}
            >
              <UserPlus size={15} />
              {t('team.invite')}
            </button>
          )}
        </div>
      </div>

      <div className="flex gap-3 flex-wrap">
        <StatCard icon={Users} value={members.length} label={t('team.stats.members')} color="var(--color-info, #3B82F6)" />
        <StatCard icon={Activity} value={members.length} label={t('team.stats.online')} color="var(--color-success, #22C55E)" />
        <StatCard icon={BarChart2} value={tasks.length} label={t('team.stats.tasks')} color="var(--role-admin, #8B5CF6)" />
        <StatCard icon={TrendingUp} value={completedTasks} label={t('team.stats.completed')} color="var(--color-warning, #F59E0B)" />
      </div>

      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--app-text-secondary)' }} />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('team.searchPlaceholder')}
            className="w-full pl-9 pr-4 py-2 rounded-xl text-sm outline-none transition-all"
            style={{
              backgroundColor: 'var(--app-layer-surface)',
              border: '1px solid var(--app-border)',
              color: 'var(--app-text-main)',
            }}
          />
          {search && (
            <button onClick={() => setSearch('')} className="absolute right-3 top-1/2 -translate-y-1/2 opacity-50 hover:opacity-80">
              <X size={13} style={{ color: 'var(--app-text-secondary)' }} />
            </button>
          )}
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          {roleOptions.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => setRoleFilter(value)}
              className={cn(
                'px-3 py-1.5 rounded-full text-xs font-semibold transition-all',
                roleFilter === value ? 'shadow-sm' : 'hover:opacity-80',
              )}
              style={
                roleFilter === value
                  ? { backgroundColor: 'var(--app-accent)', color: 'var(--app-accent-text)' }
                  : {
                      backgroundColor: 'var(--app-layer-surface)',
                      color: 'var(--app-text-secondary)',
                      border: '1px solid var(--app-border)',
                    }
              }
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {filteredMembers.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-20 gap-4 opacity-50">
          <Users size={48} />
          <p className="text-sm">{members.length === 0 ? t('team.empty.members') : t('team.empty.search')}</p>
          {members.length === 0 && canManageWorkspace && (
            <button
              onClick={() => setIsInviteOpen(true)}
              className="px-4 py-2 rounded-xl text-sm font-medium hover:opacity-80"
              style={{ border: '1px dashed var(--app-border)', color: 'var(--app-accent)' }}
            >
              + {t('team.empty.inviteFirst')}
            </button>
          )}
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          {filteredMembers.map((member) => (
            <MemberCard
              key={member.userId}
              member={member}
              workspaceId={selectedWsId}
              currentUserRole={currentUserRole}
              currentUserId={currentUser?.id}
              tasks={tasks}
              onKickSuccess={() => refreshWorkspaceMembers(selectedWsId)}
            />
          ))}
        </div>
      )}

      <div
        className="rounded-2xl p-5 mt-2"
        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
      >
        <div className="flex items-center gap-2 mb-4">
          <Clock size={16} style={{ color: 'var(--app-accent)' }} />
          <h2 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
            {t('team.activity.title')}
          </h2>
        </div>

        {tasks.length === 0 ? (
          <p className="text-sm text-center py-4 opacity-40">{t('team.activity.empty')}</p>
        ) : (
          <div className="space-y-3">
            {tasks.slice(0, 5).map((task, index) => {
              const assigneeName = task.assignee?.name || t('team.activity.unassigned');
              const colors = ['var(--color-info, #3B82F6)', 'var(--color-success, #22C55E)', 'var(--role-admin, #8B5CF6)', 'var(--color-warning, #F59E0B)', 'var(--color-error, #EF4444)'];
              const initials = task.assignee?.initials || assigneeName.slice(0, 1).toUpperCase() || '?';

              return (
                <div key={task.id} className="flex items-center gap-3">
                  <div
                    className="w-7 h-7 rounded-full flex items-center justify-center text-[11px] font-bold shrink-0 text-white"
                    style={{ backgroundColor: colors[index % colors.length] }}
                  >
                    {initials}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-xs truncate" style={{ color: 'var(--app-text-main)' }}>
                      <span className="font-medium">{assigneeName}</span>
                      {' — '}
                      <span className="opacity-70 truncate">{task.content || task.title}</span>
                    </p>
                    <p className="text-[10px] mt-0.5" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('team.activity.status')}: {task.status}
                    </p>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <div
        className="rounded-2xl p-5"
        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
      >
        <div className="flex items-center gap-2 mb-4">
          <Star size={16} style={{ color: 'var(--app-accent)' }} />
          <h2 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>
            {t('team.rolesTitle')}
          </h2>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {Object.entries(roleMeta).map(([roleId, cfg]) => {
            const Icon = cfg.icon;
            return (
              <div key={roleId} className="flex items-start gap-3 px-3 py-2.5 rounded-xl" style={{ backgroundColor: cfg.bg }}>
                <Icon size={15} style={{ color: cfg.color, marginTop: 2 }} />
                <div>
                  <div className="text-xs font-semibold" style={{ color: cfg.color }}>
                    {cfg.label}
                  </div>
                  <div className="text-[11px] mt-0.5" style={{ color: 'var(--app-text-secondary)' }}>
                    {cfg.description}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {canManageWorkspace && (
        <InviteMemberModal isOpen={isInviteOpen} onClose={() => setIsInviteOpen(false)} workspaceId={selectedWsId} />
      )}
    </div>
  );
};

export default TeamPage;
