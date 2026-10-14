import React from 'react';
import { createPortal } from 'react-dom';
import { X, Check, User, Tag, AlertCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { cn } from '../../shared/utils/cn';

const FilterMenu = ({ activeFilters, onFilterChange, onClose, style }) => {
  const { t } = useTranslation('platform');
  const { users, availableTags } = usePlatform();

  const priorities = [
    { id: 'urgent', label: t('filterMenu.priorities.urgent'), color: 'text-red-500' },
    { id: 'high', label: t('filterMenu.priorities.high'), color: 'text-orange-500' },
    { id: 'medium', label: t('filterMenu.priorities.medium'), color: 'text-blue-500' },
    { id: 'low', label: t('filterMenu.priorities.low'), color: 'text-slate-500' },
  ];

  const toggleTag = (tagId) => {
    const currentTags = activeFilters.tags || [];
    const newTags = currentTags.includes(tagId)
      ? currentTags.filter((id) => id !== tagId)
      : [...currentTags, tagId];
    onFilterChange('tags', newTags);
  };

  const clearFilters = () => {
    onFilterChange('reset', null);
  };

  const hasActiveFilters =
    activeFilters.assignee ||
    activeFilters.priority ||
    (activeFilters.tags && activeFilters.tags.length > 0);

  return createPortal(
    <>
      <div className="fixed inset-0 z-[9998]" onClick={onClose} />
      <div
        style={{
          ...style,
          zIndex: 9999,
          backgroundColor: 'var(--app-layer-overlay)',
          borderColor: 'var(--app-layer-border)',
          borderWidth: '1px',
          borderStyle: 'solid',
          color: 'var(--app-text-main)',
        }}
        className="w-72 rounded-xl shadow-xl overflow-hidden"
        onClick={(event) => event.stopPropagation()}
      >
        <div
          className="p-4 border-b flex items-center justify-between"
          style={{ borderColor: 'var(--app-layer-border)' }}
        >
          <h3 className="font-bold">{t('filterMenu.title')}</h3>
          <button onClick={onClose} style={{ color: 'var(--app-text-secondary)' }} className="hover:opacity-80 transition-opacity">
            <X size={16} />
          </button>
        </div>

        <div className="p-4 space-y-6 max-h-[400px] overflow-y-auto">
          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-2">
              <User size={12} />
              {t('filterMenu.assignee')}
            </label>
            <div className="space-y-1">
              <button
                onClick={() => onFilterChange('assignee', null)}
                className={cn(
                  'w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-sm transition-colors',
                  !activeFilters.assignee
                    ? 'bg-orange-50 text-orange-600 dark:bg-orange-900/20 dark:text-orange-400 font-medium'
                    : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800',
                )}
              >
                <span>{t('filterMenu.all')}</span>
                {!activeFilters.assignee && <Check size={14} className="ml-auto" />}
              </button>
              {users.map((user) => (
                <button
                  key={user.id}
                  onClick={() => onFilterChange('assignee', activeFilters.assignee === user.id ? null : user.id)}
                  className={cn(
                    'w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-sm transition-colors',
                    activeFilters.assignee === user.id
                      ? 'bg-orange-50 text-orange-600 dark:bg-orange-900/20 dark:text-orange-400 font-medium'
                      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800',
                  )}
                >
                  <img src={user.avatar} alt={user.name} className="w-5 h-5 rounded-full" />
                  <span className="truncate">{user.name}</span>
                  {activeFilters.assignee === user.id && <Check size={14} className="ml-auto" />}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-2">
              <AlertCircle size={12} />
              {t('filterMenu.priority')}
            </label>
            <div className="flex flex-wrap gap-2">
              {priorities.map((priority) => (
                <button
                  key={priority.id}
                  onClick={() => onFilterChange('priority', activeFilters.priority === priority.id ? null : priority.id)}
                  className={cn(
                    'px-3 py-1.5 rounded-lg text-xs font-medium border transition-all',
                    activeFilters.priority === priority.id
                      ? 'bg-orange-50 border-orange-200 text-orange-600 dark:bg-orange-900/20 dark:border-orange-900/50 dark:text-orange-400'
                      : 'bg-white dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-slate-600',
                  )}
                >
                  {priority.label}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <label className="text-xs font-bold text-slate-500 uppercase tracking-wider flex items-center gap-2">
              <Tag size={12} />
              {t('filterMenu.tags')}
            </label>
            <div className="flex flex-wrap gap-2">
              {availableTags.map((tag) => (
                <button
                  key={tag.id}
                  onClick={() => toggleTag(tag.id)}
                  className={cn(
                    'px-3 py-1.5 rounded-lg text-xs font-medium border transition-all',
                    (activeFilters.tags || []).includes(tag.id)
                      ? 'bg-orange-50 border-orange-200 text-orange-600 dark:bg-orange-900/20 dark:border-orange-900/50 dark:text-orange-400'
                      : 'bg-white dark:bg-slate-800 border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300 dark:hover:border-slate-600',
                  )}
                >
                  {tag.displayName || tag.name}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="p-4 border-t border-slate-100 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800/50 flex justify-between items-center">
          <span className="text-xs text-slate-400">
            {hasActiveFilters ? t('filterMenu.active') : t('filterMenu.inactive')}
          </span>
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="text-xs font-bold text-red-500 hover:text-red-600 transition-colors"
            >
              {t('filterMenu.resetAll')}
            </button>
          )}
        </div>
      </div>
    </>,
    document.body,
  );
};

export default FilterMenu;
