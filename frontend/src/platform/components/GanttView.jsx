import React, { useMemo, useState } from 'react';
import { ChevronRight, ChevronDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { usePreferences } from '../context/PreferencesContext';

const getAssigneeBadgeLabel = (assignee) => {
  if (!assignee) {
    return '';
  }

  if (typeof assignee === 'string') {
    const trimmed = assignee.trim();
    return trimmed ? trimmed.charAt(0).toUpperCase() : '';
  }

  if (typeof assignee === 'object') {
    const fallback = [assignee.initials, assignee.name, assignee.username, assignee.email]
      .find((value) => typeof value === 'string' && value.trim());

    if (fallback) {
      return fallback.trim().charAt(0).toUpperCase();
    }
  }

  return '';
};

const GanttRow = ({ item, level = 0, type = 'task', isExpanded, onToggle, columnsData, daysSuffix }) => {
  const paddingLeft = level * 20 + 12;
  const assigneeBadgeLabel = type === 'task' ? getAssigneeBadgeLabel(item.assignee) : '';

  return (
    <div className="flex border-b border-[#2f333c] group">
      <div className="w-[450px] shrink-0 flex border-r border-[#2f333c] text-[13px]">
        <div className="flex-1 flex items-center py-1.5 pr-2 overflow-hidden" style={{ paddingLeft }}>
          <button
            type="button"
            onClick={onToggle}
            className={`p-0.5 rounded mr-1 text-gray-400 hover:text-white transition-colors ${type === 'task' ? 'invisible' : ''}`}
          >
            {isExpanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>

          <div className="flex items-center gap-2 truncate text-[#d0d0d5]">
            {type === 'task' ? <div className={`w-2 h-2 rounded-full ${item.statusColor || 'bg-blue-500'}`} /> : null}
            <span className={`truncate ${type !== 'task' ? 'font-semibold text-white' : ''}`}>{item.title}</span>
          </div>

          {type === 'group' && (
            <span className="ml-2 text-[10px] text-gray-500 font-normal">{item.count}</span>
          )}
        </div>

        <div className="w-12 shrink-0 flex items-center justify-center border-l border-[#2f333c]/50">
          {assigneeBadgeLabel && (
            <div className="w-5 h-5 rounded-full bg-blue-600 flex items-center justify-center text-[9px] text-white font-bold">
              {assigneeBadgeLabel}
            </div>
          )}
        </div>

        <div className="w-32 shrink-0 flex items-center justify-center text-gray-500 text-[11px] border-l border-[#2f333c]/50">
          {type === 'task' && item.dateRange ? item.dateRange : ''}
        </div>

        <div className="w-16 shrink-0 flex items-center justify-center text-gray-500 text-[11px] border-l border-[#2f333c]/50">
          {type === 'task' && item.daysCount ? `${item.daysCount} ${daysSuffix}` : ''}
        </div>
      </div>

      <div className="flex-1 flex relative">
        {columnsData.map((column, index) => (
          <div key={index} className="flex-1 border-r border-[#2f333c]/30 h-full min-w-[40px] relative">
            {column.isToday && (
              <div className="h-full w-px bg-red-500/50 absolute top-0 bottom-0 pointer-events-none left-1/2 -ml-px z-10" />
            )}
          </div>
        ))}
      </div>
    </div>
  );
};

const GanttView = ({ data, scale = 'weeks' }) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const { activeProject } = usePlatform();
  const [expandedGroups, setExpandedGroups] = useState({});
  const [topProjectExpanded, setTopProjectExpanded] = useState(true);

  const columnsData = useMemo(() => {
    const today = new Date();
    const columns = [];

    if (scale === 'days') {
      const start = new Date(today);
      start.setDate(today.getDate() - 5);
      for (let index = 0; index < 30; index += 1) {
        const date = new Date(start);
        date.setDate(start.getDate() + index);
        columns.push({
          isToday: date.toDateString() === today.toDateString(),
          topLabel: date.getDate(),
          subLabel: new Intl.DateTimeFormat(language === 'kk' ? 'kk-KZ' : language === 'ru' ? 'ru-RU' : 'en-US', {
            weekday: 'short',
          }).format(date),
        });
      }
    } else if (scale === 'weeks') {
      const currentDay = today.getDay() || 7;
      const startOfWeek = new Date(today);
      if (currentDay !== 1) {
        startOfWeek.setDate(today.getDate() - (currentDay - 1));
      }

      const start = new Date(startOfWeek);
      start.setDate(startOfWeek.getDate() - 14);

      for (let index = 0; index < 12; index += 1) {
        const date = new Date(start);
        date.setDate(start.getDate() + index * 7);
        const end = new Date(date);
        end.setDate(date.getDate() + 6);

        columns.push({
          isToday: today >= date && today <= end,
          topLabel: date.getDate(),
          subLabel: new Intl.DateTimeFormat(language === 'kk' ? 'kk-KZ' : language === 'ru' ? 'ru-RU' : 'en-US', {
            month: 'short',
          }).format(date),
        });
      }
    } else if (scale === 'months') {
      const start = new Date(today);
      start.setMonth(today.getMonth() - 2);
      start.setDate(1);

      for (let index = 0; index < 12; index += 1) {
        const date = new Date(start);
        date.setMonth(start.getMonth() + index);
        columns.push({
          isToday: today.getMonth() === date.getMonth() && today.getFullYear() === date.getFullYear(),
          topLabel: new Intl.DateTimeFormat(language === 'kk' ? 'kk-KZ' : language === 'ru' ? 'ru-RU' : 'en-US', {
            month: 'long',
          }).format(date),
          subLabel: date.getFullYear(),
        });
      }
    } else if (scale === 'years') {
      const start = new Date(today);
      start.setFullYear(today.getFullYear() - 1);

      for (let index = 0; index < 5; index += 1) {
        const date = new Date(start);
        date.setFullYear(start.getFullYear() + index);
        columns.push({
          isToday: today.getFullYear() === date.getFullYear(),
          topLabel: date.getFullYear(),
          subLabel: '',
        });
      }
    }

    return columns;
  }, [language, scale]);

  const hierarchy = useMemo(() => {
    if (!data?.columnOrder) {
      return [];
    }

    return data.columnOrder.map((columnId) => {
      const column = data.columns[columnId];
      const tasks = column.taskIds.map((taskId) => data.tasks[taskId]).filter(Boolean);

      return {
        id: columnId,
        title: column.title,
        type: 'group',
        tasks: tasks.map((task) => ({
          ...task,
          title: task.content,
          dateRange: '12.12 - 15.12',
          daysCount: 4,
          statusColor:
            columnId === 'col-1' ? 'bg-blue-500' : columnId === 'col-2' ? 'bg-orange-500' : 'bg-green-500',
        })),
        count: tasks.length,
      };
    });
  }, [data]);

  const toggleGroup = (groupId) => {
    setExpandedGroups((current) => ({ ...current, [groupId]: !current[groupId] }));
  };

  const hasAnyTasks = hierarchy.some((group) => group.tasks.length > 0);

  if (!hasAnyTasks) {
    return (
      <div className="flex h-full items-center justify-center bg-[#1e2025] p-8 text-center text-[#d0d0d5]">
        <div className="max-w-md space-y-3 rounded-2xl border border-[#2f333c] bg-[#25272e] px-8 py-10">
          <div className="text-lg font-semibold text-white">{t('gantt.emptyTitle')}</div>
          <p className="text-sm text-gray-400">{t('gantt.emptyDescription')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-[#1e2025] text-[#d0d0d5] overflow-hidden">
      <div className="flex-1 flex flex-col overflow-hidden relative">
        <div className="flex border-b border-[#2f333c] bg-[#25272e] font-medium text-[10px] uppercase tracking-wider text-gray-500">
          <div className="w-[450px] shrink-0 flex border-r border-[#2f333c]">
            <div className="flex-1 py-2 px-3">{t('gantt.headers.name')}</div>
            <div className="w-12 py-2 text-center border-l border-[#2f333c]/50">{t('gantt.headers.assignee')}</div>
            <div className="w-32 py-2 text-center border-l border-[#2f333c]/50">{t('gantt.headers.dateRange')}</div>
            <div className="w-16 py-2 text-center border-l border-[#2f333c]/50">{t('gantt.headers.days')}</div>
          </div>
          <div className="flex-1 flex overflow-hidden">
            {columnsData.map((column, index) => (
              <div
                key={index}
                className="flex-1 min-w-[40px] border-r border-[#2f333c]/30 flex flex-col items-center justify-center py-1 bg-[#25272e]"
              >
                <span className="text-[9px] text-gray-300">{column.topLabel}</span>
                <span className="text-[8px] opacity-50">{column.subLabel}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="flex-1 overflow-auto custom-scrollbar">
          <div className="min-w-full">
            <GanttRow
              item={{ title: activeProject?.name || t('gantt.rootProject') }}
              level={0}
              type="project"
              isExpanded={topProjectExpanded}
              onToggle={() => setTopProjectExpanded((current) => !current)}
              columnsData={columnsData}
              daysSuffix={t('gantt.daysShort')}
            />

            {topProjectExpanded &&
              hierarchy.map((group) => (
                <React.Fragment key={group.id}>
                  <GanttRow
                    item={group}
                    level={1}
                    type="group"
                    isExpanded={!expandedGroups[group.id]}
                    onToggle={() => toggleGroup(group.id)}
                    columnsData={columnsData}
                    daysSuffix={t('gantt.daysShort')}
                  />
                  {!expandedGroups[group.id] &&
                    group.tasks.map((task) => (
                      <GanttRow
                        key={task.id}
                        item={task}
                        level={2}
                        type="task"
                        columnsData={columnsData}
                        daysSuffix={t('gantt.daysShort')}
                      />
                    ))}
                </React.Fragment>
              ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default GanttView;
