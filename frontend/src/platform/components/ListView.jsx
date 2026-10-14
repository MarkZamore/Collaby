import React from 'react';
import {
  Calendar as CalendarIcon,
  User,
  AlertCircle,
  Circle,
  ChevronDown,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../shared/utils/cn';
import { formatDate } from '../../shared/utils/locale';
import { usePreferences } from '../context/PreferencesContext';

const STATUS_KEYS = ['backlog', 'todo', 'inProgress', 'review', 'done'];

const STATUS_VARIANTS = {
  backlog: ['бэклог', 'backlog'],
  todo: ['сделать', 'к выполнению', 'to do', 'todo', 'жасау', 'орындауға'],
  inProgress: ['в работе', 'в процессе', 'in progress', 'жұмыста', 'процесте'],
  review: ['ревью', 'review', 'тексеру'],
  done: ['готово', 'done', 'дайын'],
};

const normalizeStatus = (status) => {
  const value = String(status || '').trim().toLowerCase();
  return STATUS_KEYS.find((key) => STATUS_VARIANTS[key].includes(value)) || 'backlog';
};

const getStatusBadgeClass = (statusKey) => ({
  backlog: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
  todo: 'bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400',
  inProgress: 'bg-amber-100 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400',
  review: 'bg-purple-100 text-purple-600 dark:bg-purple-900/30 dark:text-purple-400',
  done: 'bg-green-100 text-green-600 dark:bg-green-900/30 dark:text-green-400',
}[statusKey] || 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400');

const StatusBadge = ({ statusKey, label }) => (
  <span className={cn('px-2 py-0.5 rounded text-xs font-medium whitespace-nowrap', getStatusBadgeClass(statusKey))}>
    {label}
  </span>
);

const PriorityIcon = ({ priority }) => {
  switch (priority) {
    case 'urgent':
      return <AlertCircle size={16} className="text-red-500" />;
    case 'high':
      return <div className="flex"><ChevronDown size={16} className="text-orange-500 rotate-180" /><ChevronDown size={16} className="text-orange-500 rotate-180 -ml-2" /></div>;
    case 'medium':
      return <ChevronDown size={16} className="text-blue-500 rotate-180" />;
    case 'low':
      return <ChevronDown size={16} className="text-slate-400" />;
    default:
      return <Circle size={14} className="text-slate-300" />;
  }
};

const ListView = ({
  tasks,
  onTaskClick,
  viewSettings = {
    showTaskId: true,
    showAssignee: true,
    showTags: true,
    showPriority: true,
    showDueDate: true,
  },
}) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();

  const groupedTasks = tasks.reduce((acc, task) => {
    const statusKey = normalizeStatus(task.status);
    if (!acc[statusKey]) acc[statusKey] = [];
    acc[statusKey].push(task);
    return acc;
  }, {});

  const getGridTemplate = () => {
    const template = [];
    if (viewSettings.showTaskId) template.push('80px');
    template.push('minmax(200px, 1fr)');
    if (viewSettings.showTags) template.push('150px');
    template.push('120px');
    if (viewSettings.showPriority) template.push('100px');
    if (viewSettings.showAssignee) template.push('150px');
    if (viewSettings.showDueDate) template.push('120px');
    return template.join(' ');
  };

  const gridStyle = { gridTemplateColumns: getGridTemplate() };

  return (
    <div className="h-full overflow-y-auto pb-20 px-6 scrollbar-hide">
      <div className="min-w-[800px]">
        <div
          className="grid gap-4 px-6 py-3 border-b text-xs font-bold uppercase tracking-wider sticky top-0 z-10 rounded-t-xl"
          style={{
            gridTemplateColumns: getGridTemplate(),
            backgroundColor: 'var(--app-layer-surface)',
            borderColor: 'var(--app-border)',
            color: 'var(--app-text-secondary)',
          }}
        >
          {viewSettings.showTaskId && <div>ID</div>}
          <div>{t('list.headers.title')}</div>
          {viewSettings.showTags && <div>{t('list.headers.tags')}</div>}
          <div>{t('list.headers.status')}</div>
          {viewSettings.showPriority && <div>{t('list.headers.priority')}</div>}
          {viewSettings.showAssignee && <div>{t('list.headers.assignee')}</div>}
          {viewSettings.showDueDate && <div>{t('list.headers.dueDate')}</div>}
        </div>

        <div className="space-y-6 mt-4">
          {STATUS_KEYS.map((statusKey) => {
            const groupTasks = groupedTasks[statusKey] || [];
            if (groupTasks.length === 0) return null;

            return (
              <div key={statusKey}>
                <div className="flex items-center gap-2 px-2 mb-2">
                  <div
                    className="p-1 rounded cursor-pointer transition-colors hover:opacity-80"
                    style={{ backgroundColor: 'var(--app-layer-base)' }}
                  >
                    <ChevronDown size={14} className="text-slate-400" />
                  </div>
                  <StatusBadge statusKey={statusKey} label={t(`analytics.statuses.${statusKey}`)} />
                  <span className="text-xs text-slate-400 font-bold">{groupTasks.length}</span>
                </div>

                <div className="glass-panel rounded-xl overflow-hidden">
                  {groupTasks.map((task) => {
                    const normalizedStatus = normalizeStatus(task.status);

                    return (
                      <div
                        key={task.id}
                        onClick={() => onTaskClick(task)}
                        className="grid gap-4 px-6 py-3 border-b last:border-0 transition-all cursor-pointer items-center group hover:opacity-90"
                        style={{
                          ...gridStyle,
                          borderColor: 'var(--app-border)',
                          backgroundColor: 'var(--app-layer-surface)',
                        }}
                      >
                        {viewSettings.showTaskId && <span className="text-xs font-mono font-bold text-slate-400">{task.id}</span>}
                        <div className={cn(
                          'font-bold text-sm text-slate-800 dark:text-slate-100 truncate pr-4',
                          normalizedStatus === 'done' && 'line-through text-slate-500 dark:text-slate-500',
                        )}>
                          {task.title}
                        </div>

                        {viewSettings.showTags && (
                          <div className="flex flex-wrap gap-1">
                            {task.tags && task.tags.map((tag) => (
                              <span key={tag.id || tag} className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
                                {typeof tag === 'string' ? tag : tag.displayName || tag.name}
                              </span>
                            ))}
                          </div>
                        )}

                        <div>
                          <StatusBadge statusKey={normalizedStatus} label={t(`analytics.statuses.${normalizedStatus}`)} />
                        </div>

                        {viewSettings.showPriority && (
                          <div className="pl-2">
                            <PriorityIcon priority={task.priority} />
                          </div>
                        )}

                        {viewSettings.showAssignee && (
                          <div className="flex items-center gap-2">
                            {task.assignee ? (
                              <>
                                {task.assignee.avatar ? (
                                  <img src={task.assignee.avatar} alt="" className="w-6 h-6 rounded-full object-cover shadow-sm" />
                                ) : (
                                  <div className="w-6 h-6 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-[10px] font-bold text-white shadow-sm">
                                    {task.assignee.initials || (typeof task.assignee === 'string' ? task.assignee.charAt(0) : '?')}
                                  </div>
                                )}
                                <span className="text-sm font-medium text-slate-600 dark:text-slate-300 truncate">
                                  {task.assignee.name || (typeof task.assignee === 'string' ? task.assignee : t('sidebar.defaultUser'))}
                                </span>
                              </>
                            ) : (
                              <span className="text-sm text-slate-400 flex items-center gap-1">
                                <User size={14} /> {t('list.unassigned')}
                              </span>
                            )}
                          </div>
                        )}

                        {viewSettings.showDueDate && (
                          <div className="text-sm font-medium text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
                            {task.dueDate ? (
                              <>
                                <CalendarIcon size={14} className={cn(new Date(task.dueDate) < new Date() ? 'text-red-500' : 'text-slate-400')} />
                                <span className={cn(new Date(task.dueDate) < new Date() ? 'text-red-600 dark:text-red-400' : '')}>
                                  {formatDate(task.dueDate, language, { day: 'numeric', month: 'short' })}
                                </span>
                              </>
                            ) : (
                              <span className="text-slate-300 dark:text-slate-600">-</span>
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

export default ListView;
