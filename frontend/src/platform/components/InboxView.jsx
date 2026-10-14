import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { Inbox, FolderKanban, Calendar, Search, RotateCw } from 'lucide-react';
import { cn } from '../../shared/utils/cn';
import { formatDate } from '../../shared/utils/locale';
import { usePreferences } from '../context/PreferencesContext';
import Skeleton from '../../shared/components/Skeleton';

const PRIORITY_MAP = { 0: 'low', 1: 'medium', 2: 'high', 3: 'critical' };
const mapPriority = (value) => PRIORITY_MAP[typeof value === 'string' ? parseInt(value, 10) : value] ?? 'medium';

const InboxView = ({ setSelectedTaskId }) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const { inboxTasksAll, inboxLoading, inboxError, refreshInbox, selectProject, setActivePage, currentUser } = usePlatform();
  const [sortBy, setSortBy] = useState('dueDate');
  const [searchQuery, setSearchQuery] = useState('');
  const [filterWorkspaceId, setFilterWorkspaceId] = useState('');
  const [filterPriority, setFilterPriority] = useState('');

  const sortOptions = [
    { id: 'dueDate', label: t('inbox.sort.dueDate') },
    { id: 'priority', label: t('inbox.sort.priority') },
    { id: 'project', label: t('inbox.sort.project') },
    { id: 'createdAt', label: t('inbox.sort.createdAt') },
  ];

  const filteredAndSorted = useMemo(() => {
    let list = inboxTasksAll || [];

    if (searchQuery.trim()) {
      const query = searchQuery.trim().toLowerCase();
      list = list.filter((task) => (task.title || task.content || '').toLowerCase().includes(query));
    }

    if (filterWorkspaceId) {
      list = list.filter((task) => (task.workspaceId || task.workspace) === filterWorkspaceId);
    }

    if (filterPriority) {
      list = list.filter((task) => String(mapPriority(task.priority)) === filterPriority);
    }

    const priorityOrder = (priority) => ({ critical: 0, high: 1, medium: 2, low: 3 }[mapPriority(priority)] ?? 4);

    if (sortBy === 'dueDate') {
      list = [...list].sort((a, b) => {
        const dueA = a.dueDate ? new Date(a.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        const dueB = b.dueDate ? new Date(b.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
        return dueA - dueB;
      });
    } else if (sortBy === 'priority') {
      list = [...list].sort((a, b) => priorityOrder(a.priority) - priorityOrder(b.priority));
    } else if (sortBy === 'project') {
      list = [...list].sort((a, b) => (a.projectName || '').localeCompare(b.projectName || ''));
    } else if (sortBy === 'createdAt') {
      list = [...list].sort((a, b) => new Date(b.createdAt || 0).getTime() - new Date(a.createdAt || 0).getTime());
    }

    return list;
  }, [filterPriority, filterWorkspaceId, inboxTasksAll, searchQuery, sortBy]);

  const groupedByWorkspace = useMemo(() => {
    const groups = new Map();
    filteredAndSorted.forEach((task) => {
      const workspaceId = task.workspaceId || task.workspace;
      if (!groups.has(workspaceId)) {
        groups.set(workspaceId, { workspaceName: task.workspaceName || '', items: [] });
      }
      groups.get(workspaceId).items.push(task);
    });

    return Array.from(groups.entries()).map(([workspaceId, { workspaceName, items }]) => ({
      workspaceId,
      workspaceName,
      items,
    }));
  }, [filteredAndSorted]);

  const uniqueWorkspaces = useMemo(() => {
    const seen = new Map();
    (inboxTasksAll || []).forEach((task) => {
      const id = task.workspaceId || task.workspace;
      if (id && !seen.has(id)) {
        seen.set(id, task.workspaceName || '');
      }
    });
    return Array.from(seen.entries()).map(([id, name]) => ({ id, name }));
  }, [inboxTasksAll]);

  const handleOpenTask = (task) => {
    selectProject(task.projectId);
    setActivePage('dashboard');
    setSelectedTaskId?.(task.id);
  };

  const formatDueDate = (dueDate) => {
    if (!dueDate) {
      return null;
    }

    const normalized = typeof dueDate === 'string' ? dueDate.split('T')[0] : dueDate;
    return formatDate(normalized, language, { day: 'numeric', month: 'short' });
  };

  const isOverdue = (dueDate) => dueDate && new Date(dueDate) < new Date();

  if (inboxLoading && !inboxTasksAll?.length) {
    return (
      <div className="flex flex-col flex-1 p-6 gap-4">
        <Skeleton width="30%" height={22} />
        <Skeleton.TaskRow rows={5} />
      </div>
    );
  }

  if (inboxError && !inboxTasksAll?.length) {
    return (
      <div className="flex flex-col items-center justify-center flex-1 p-8 text-center" style={{ color: 'var(--app-text-secondary)' }}>
        <Inbox size={48} className="opacity-50 mb-4" />
        <h2 className="text-xl font-semibold mb-2" style={{ color: 'var(--app-text-main)' }}>
          {t('inbox.title')}
        </h2>
        <p className="mb-4">{inboxError}</p>
        <button
          type="button"
          onClick={() => refreshInbox()}
          className="inline-flex items-center gap-2 px-4 py-2 rounded-lg border transition-colors"
          style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-main)' }}
        >
          <RotateCw size={16} /> {t('inbox.retry')}
        </button>
      </div>
    );
  }

  if (!inboxTasksAll?.length) {
    return (
      <div className="flex flex-col items-center justify-center flex-1 p-8 text-center" style={{ color: 'var(--app-text-secondary)' }}>
        <Inbox size={48} className="opacity-50 mb-4" />
        <h2 className="text-xl font-semibold mb-2" style={{ color: 'var(--app-text-main)' }}>
          {t('inbox.title')}
        </h2>
        <p>{t('inbox.empty')}</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col flex-1 overflow-hidden p-6">
      <div className="mb-4">
        <h1 className="text-2xl font-bold flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
          <Inbox size={22} style={{ color: 'var(--app-accent)' }} />
          {t('inbox.title')}
        </h1>
        <p className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
          {t('inbox.subtitle')}
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-3 mb-4">
        <div
          className="relative flex items-center rounded-lg border px-3 py-2 min-w-[200px]"
          style={{ borderColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
        >
          <Search size={16} style={{ color: 'var(--app-text-secondary)' }} className="shrink-0 mr-2" />
          <input
            type="text"
            placeholder={t('inbox.searchPlaceholder')}
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            className="bg-transparent border-none outline-none w-full text-sm"
            style={{ color: 'var(--app-text-main)' }}
          />
        </div>

        <select
          value={sortBy}
          onChange={(event) => setSortBy(event.target.value)}
          className="rounded-lg border px-3 py-2 text-sm font-medium cursor-pointer"
          style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-main)', backgroundColor: 'var(--app-layer-surface)' }}
        >
          {sortOptions.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>

        <select
          value={filterWorkspaceId}
          onChange={(event) => setFilterWorkspaceId(event.target.value)}
          className="rounded-lg border px-3 py-2 text-sm font-medium cursor-pointer"
          style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-main)', backgroundColor: 'var(--app-layer-surface)' }}
        >
          <option value="">{t('inbox.allWorkspaces')}</option>
          {uniqueWorkspaces.map((workspace) => (
            <option key={workspace.id} value={workspace.id}>
              {workspace.name}
            </option>
          ))}
        </select>

        <select
          value={filterPriority}
          onChange={(event) => setFilterPriority(event.target.value)}
          className="rounded-lg border px-3 py-2 text-sm font-medium cursor-pointer"
          style={{ borderColor: 'var(--app-border)', color: 'var(--app-text-main)', backgroundColor: 'var(--app-layer-surface)' }}
        >
          <option value="">{t('inbox.anyPriority')}</option>
          <option value="critical">{t('inbox.priority.critical')}</option>
          <option value="high">{t('inbox.priority.high')}</option>
          <option value="medium">{t('inbox.priority.medium')}</option>
          <option value="low">{t('inbox.priority.low')}</option>
        </select>

        <button
          type="button"
          onClick={() => refreshInbox()}
          className="p-2 rounded-lg border transition-colors hover:opacity-80"
          style={{ borderColor: 'var(--app-border)', backgroundColor: 'var(--app-layer-surface)' }}
          title={t('inbox.refresh')}
        >
          <RotateCw size={18} style={{ color: 'var(--app-text-secondary)' }} />
        </button>
      </div>

      {filteredAndSorted.length === 0 ? (
        <p className="text-sm py-4" style={{ color: 'var(--app-text-secondary)' }}>
          {t('inbox.noMatches')}
        </p>
      ) : (
        <div className="flex-1 overflow-y-auto space-y-6">
          {groupedByWorkspace.map(({ workspaceId, workspaceName, items }) => (
            <section key={workspaceId}>
              <h2 className="text-sm font-semibold mb-3 px-1" style={{ color: 'var(--app-text-secondary)' }}>
                {workspaceName}
              </h2>
              <div className="space-y-3">
                {items.map((task) => (
                  <button
                    key={task.id}
                    type="button"
                    onClick={() => handleOpenTask(task)}
                    className={cn(
                      'w-full text-left rounded-xl p-4 transition-all border shadow-sm',
                      'hover:shadow focus:outline-none focus:ring-2 focus:ring-offset-2'
                    )}
                    style={{
                      backgroundColor: 'var(--app-layer-surface)',
                      borderColor: 'var(--app-border)',
                      color: 'var(--app-text-main)',
                    }}
                  >
                    <div className="flex items-start gap-3">
                      {currentUser?.avatar && (
                        <div className="shrink-0 mt-0.5">
                          <img
                            src={currentUser.avatar}
                            alt=""
                            className="w-8 h-8 rounded-full object-cover ring-1 ring-white/10"
                          />
                        </div>
                      )}
                      <div className="min-w-0 flex-1">
                        <div className="font-medium truncate">{task.title || task.content}</div>
                        <div className="flex flex-wrap items-center gap-2 mt-2 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                          <span className="flex items-center gap-1">
                            <FolderKanban size={12} />
                            {task.workspaceName} · {task.projectName}
                          </span>
                          {task.priority != null && (
                            <span
                              className="px-1.5 py-0.5 rounded font-medium capitalize"
                              style={{
                                backgroundColor: 'var(--app-accent)',
                                color: 'var(--app-accent-text)',
                              }}
                            >
                              {t(`inbox.priority.${mapPriority(task.priority)}`)}
                            </span>
                          )}
                          {task.dueDate && (
                            <span
                              className={cn(
                                'flex items-center gap-1',
                                isOverdue(task.dueDate) && 'text-red-500 dark:text-red-400'
                              )}
                            >
                              <Calendar size={12} />
                              {formatDueDate(task.dueDate)}
                            </span>
                          )}
                        </div>
                        {task.labels?.length > 0 && (
                          <div className="flex flex-wrap gap-1 mt-2">
                            {task.labels.map((label) => (
                              <span
                                key={label.id}
                                className="text-xs px-1.5 py-0.5 rounded"
                                style={{
                                  backgroundColor: label.color ? `${label.color}20` : 'var(--app-border)',
                                  color: label.color || 'inherit',
                                }}
                              >
                                {label.displayName || label.name}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
};

export default InboxView;
