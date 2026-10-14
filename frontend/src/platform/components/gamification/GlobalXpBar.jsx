import React from 'react';
import { useTranslation } from 'react-i18next';
import { Coins, Flame, Zap } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';

export default function GlobalXpBar() {
  const { t } = useTranslation('platform');
  const { xp, level, coins, streak } = useGamification();

  const currentLevelFloor = level <= 1 ? 0 : level * 100;
  const nextLevelThreshold = (level + 1) * 100;
  const levelSpan = Math.max(nextLevelThreshold - currentLevelFloor, 1);
  const progress = Math.min(((xp - currentLevelFloor) / levelSpan) * 100, 100);

  return (
    <div className="mx-2 mb-2 rounded-lg px-3 py-2 glass-card">
      <div className="mb-1 flex items-center justify-between">
        <div className="flex items-center gap-1.5">
          <Zap size={14} className="text-yellow-400" fill="currentColor" />
          <span className="text-xs font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {t('gamificationWidgets.levelShort', { level })}
          </span>
        </div>
        <div className="flex items-center gap-2">
          {streak && streak.streakDays > 0 && (
            <div className="flex items-center gap-0.5">
              <Flame size={12} className="text-orange-400" />
              <span className="text-[10px] font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                {streak.streakDays}
              </span>
            </div>
          )}
          <div className="flex items-center gap-0.5">
            <Coins size={12} className="text-yellow-500" />
            <span className="text-[10px] font-medium" style={{ color: 'var(--app-text-secondary)' }}>
              {coins}
            </span>
          </div>
        </div>
      </div>
      <div className="h-1.5 w-full overflow-hidden rounded-full" style={{ background: 'var(--app-bg-tertiary)' }}>
        <div
          className="h-full rounded-full transition-all duration-500"
          style={{ width: `${progress}%`, background: 'var(--app-accent)' }}
        />
      </div>
      <div className="mt-0.5 flex justify-between">
        <span className="text-[9px]" style={{ color: 'var(--app-text-secondary)' }}>
          {xp} {t('analytics.table.xp')}
        </span>
        <span className="text-[9px]" style={{ color: 'var(--app-text-secondary)' }}>
          {nextLevelThreshold} {t('analytics.table.xp')}
        </span>
      </div>
    </div>
  );
}
