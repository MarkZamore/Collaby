import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { cn } from '../../shared/utils/cn';
import { getIntlLocale } from '../../shared/utils/locale';
import { usePreferences } from '../context/PreferencesContext';

const DONE_STATUS_VALUES = ['готово', 'done', 'дайын'];

const CalendarView = ({ tasks, onTaskClick }) => {
  const { t } = useTranslation('platform');
  const { language } = usePreferences();
  const [currentDate, setCurrentDate] = useState(new Date());

  const intlLocale = getIntlLocale(language);

  const getDaysInMonth = (date) => {
    const year = date.getFullYear();
    const month = date.getMonth();
    const days = new Date(year, month + 1, 0).getDate();
    const firstDay = new Date(year, month, 1).getDay();
    const adjustedFirstDay = firstDay === 0 ? 6 : firstDay - 1;
    return { days, firstDay: adjustedFirstDay };
  };

  const { days, firstDay } = getDaysInMonth(currentDate);

  const monthLabel = useMemo(
    () =>
      new Intl.DateTimeFormat(intlLocale, {
        month: 'long',
        year: 'numeric',
      }).format(currentDate),
    [currentDate, intlLocale],
  );

  const weekDays = useMemo(() => {
    const mondayStart = new Date(Date.UTC(2024, 0, 1));
    return Array.from({ length: 7 }, (_, index) =>
      new Intl.DateTimeFormat(intlLocale, { weekday: 'short' }).format(
        new Date(mondayStart.getTime() + index * 24 * 60 * 60 * 1000),
      ),
    );
  }, [intlLocale]);

  const prevMonth = () => {
    setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() - 1, 1));
  };

  const nextMonth = () => {
    setCurrentDate(new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 1));
  };

  const getTasksForDay = (day) =>
    tasks.filter((task) => {
      if (!task.dueDate) return false;
      const taskDate = new Date(task.dueDate);
      return (
        taskDate.getDate() === day &&
        taskDate.getMonth() === currentDate.getMonth() &&
        taskDate.getFullYear() === currentDate.getFullYear()
      );
    });

  const isDoneStatus = (status) =>
    DONE_STATUS_VALUES.includes(String(status || '').trim().toLowerCase());

  const renderCalendarDays = () => {
    const daysArray = [];

    for (let i = 0; i < firstDay; i += 1) {
      daysArray.push(
        <div
          key={`empty-${i}`}
          className="min-h-[120px] border-b border-r"
          style={{
            backgroundColor: 'var(--app-layer-base)',
            borderColor: 'var(--app-border)',
          }}
        />,
      );
    }

    for (let day = 1; day <= days; day += 1) {
      const dayTasks = getTasksForDay(day);
      const isToday =
        day === new Date().getDate() &&
        currentDate.getMonth() === new Date().getMonth() &&
        currentDate.getFullYear() === new Date().getFullYear();

      daysArray.push(
        <div
          key={day}
          className={cn(
            'min-h-[120px] border-b border-r p-2 transition-colors group relative hover:opacity-90',
          )}
          style={{
            backgroundColor: isToday ? 'var(--app-accent)' : 'var(--app-layer-surface)',
            borderColor: 'var(--app-border)',
            opacity: isToday ? 0.15 : 1,
          }}
        >
          <div className="mb-2 flex items-start justify-between">
            <span
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-full text-sm font-bold',
                isToday ? 'bg-gradient-to-br from-[#FF6B35] to-[#FF8C42] text-white shadow-md' : '',
              )}
              style={{ color: isToday ? undefined : 'var(--app-text-main)' }}
            >
              {day}
            </span>
            <button
              className="rounded-lg p-1 opacity-0 transition-all group-hover:opacity-100 hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
              type="button"
            >
              <Plus size={16} />
            </button>
          </div>

          <div className="space-y-1.5">
            {dayTasks.slice(0, 3).map((task) => {
              const done = isDoneStatus(task.status);
              return (
                <div
                  key={task.id}
                  onClick={(event) => {
                    event.stopPropagation();
                    onTaskClick(task);
                  }}
                  className={cn(
                    'cursor-pointer truncate rounded-lg border px-2 py-1.5 text-xs font-medium transition-all hover:scale-[1.02]',
                    done && 'line-through opacity-70',
                  )}
                  style={{
                    backgroundColor: done ? '#22C55E' : 'var(--app-layer-surface)',
                    color: done ? 'white' : 'var(--app-text-main)',
                    borderColor: 'var(--app-border)',
                  }}
                >
                  {task.title}
                </div>
              );
            })}

            {dayTasks.length > 3 && (
              <div className="pl-1 text-[10px] font-bold text-slate-400">
                + {t('calendarView.more', { count: dayTasks.length - 3 })}
              </div>
            )}
          </div>
        </div>,
      );
    }

    return daysArray;
  };

  return (
    <div
      className="mx-6 mb-6 flex h-full flex-col overflow-hidden rounded-xl border"
      style={{
        backgroundColor: 'var(--app-layer-surface)',
        borderColor: 'var(--app-border)',
      }}
    >
      <div
        className="flex items-center justify-between border-b px-6 py-4"
        style={{
          borderColor: 'var(--app-border)',
          backgroundColor: 'var(--app-layer-surface)',
        }}
      >
        <div className="flex items-center gap-4">
          <h2
            className="text-xl font-bold capitalize drop-shadow-sm"
            style={{ color: 'var(--app-text-main)' }}
          >
            {monthLabel}
          </h2>
          <div
            className="flex items-center rounded-xl p-1"
            style={{ backgroundColor: 'var(--app-layer-base)' }}
          >
            <button
              onClick={prevMonth}
              className="rounded-lg p-1.5 shadow-sm transition-all hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
              type="button"
            >
              <ChevronLeft size={18} />
            </button>
            <button
              onClick={nextMonth}
              className="rounded-lg p-1.5 shadow-sm transition-all hover:opacity-80"
              style={{ color: 'var(--app-text-secondary)' }}
              type="button"
            >
              <ChevronRight size={18} />
            </button>
          </div>
        </div>
        <button
          className="rounded-xl border px-4 py-2 text-sm font-bold transition-all shadow-sm"
          style={{
            color: 'var(--app-text-secondary)',
            borderColor: 'var(--app-border)',
            backgroundColor: 'var(--app-layer-base)',
          }}
          onClick={() => setCurrentDate(new Date())}
          type="button"
        >
          {t('calendarView.today')}
        </button>
      </div>

      <div
        className="grid grid-cols-7 border-b"
        style={{
          borderColor: 'var(--app-border)',
          backgroundColor: 'var(--app-layer-base)',
        }}
      >
        {weekDays.map((day) => (
          <div
            key={day}
            className="border-r py-3 text-center text-xs font-bold uppercase tracking-wider last:border-r-0"
            style={{
              color: 'var(--app-text-secondary)',
              borderColor: 'var(--app-border)',
            }}
          >
            {day}
          </div>
        ))}
      </div>

      <div
        className="grid flex-1 auto-rows-fr grid-cols-7 overflow-y-auto"
        style={{ backgroundColor: 'var(--app-layer-surface)' }}
      >
        {renderCalendarDays()}
      </div>
    </div>
  );
};

export default CalendarView;
