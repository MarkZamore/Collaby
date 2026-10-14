import React from 'react';
import { useTranslation } from 'react-i18next';
import { Flame, Snowflake } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';

export default function StreakIndicator() {
  const { t } = useTranslation('platform');
  const { streak } = useGamification();
  if (!streak) return null;

  const days = streak.streakDays || 0;
  const freezes = streak.freezeCount || 0;
  const multiplier = (1 + Math.min(Math.floor(days / 7), 5) * 0.1).toFixed(1);

  return (
    <div className="glass-card rounded-xl p-4">
      <h3
        className="mb-3 flex items-center gap-2 text-sm font-semibold"
        style={{ color: 'var(--app-text-main)' }}
      >
        <Flame size={16} className="text-orange-400" />
        {t('gamificationWidgets.streakTitle')}
      </h3>
      <div className="flex items-center gap-4">
        <div className="text-center">
          <div className="text-2xl font-bold" style={{ color: 'var(--app-accent)' }}>
            {days}
          </div>
          <div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
            {t('gamificationWidgets.daysInRow')}
          </div>
        </div>
        <div className="flex-1">
          <div className="flex gap-1">
            {Array.from({ length: 7 }, (_, index) => (
              <div
                key={index}
                className="h-2 w-full rounded-full"
                style={{
                  background:
                    index < (days % 7 || (days > 0 ? 7 : 0))
                      ? 'var(--app-accent)'
                      : 'var(--app-bg-tertiary)',
                }}
              />
            ))}
          </div>
          <div className="mt-1 text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
            {t('gamificationWidgets.multiplier', { value: multiplier })}
          </div>
        </div>
        {freezes > 0 && (
          <div className="flex items-center gap-1">
            <Snowflake size={14} className="text-blue-400" />
            <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {freezes}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
