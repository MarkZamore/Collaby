import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Zap } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';

export default function ProjectXpBar({ projectId }) {
  const { t } = useTranslation('platform');
  const { api, accessToken } = useGamification();
  const [data, setData] = useState(null);

  useEffect(() => {
    if (!accessToken || !projectId) return;
    api.getProjectXp(accessToken, projectId).then(setData).catch(() => {});
  }, [accessToken, projectId, api]);

  if (!data) return null;

  const currentLevelFloor = data.level <= 1 ? 0 : data.level * 100;
  const nextLevelThreshold = (data.level + 1) * 100;
  const levelSpan = Math.max(nextLevelThreshold - currentLevelFloor, 1);
  const progress = Math.min(((data.xp - currentLevelFloor) / levelSpan) * 100, 100);

  return (
    <div className="glass-card flex items-center gap-3 rounded-lg px-3 py-1.5">
      <Zap size={14} className="flex-shrink-0 text-yellow-400" fill="currentColor" />
      <span className="whitespace-nowrap text-xs font-semibold" style={{ color: 'var(--app-text-main)' }}>
        {t('gamificationWidgets.levelShort', { level: data.level })}
      </span>
      <div
        className="h-1.5 min-w-[60px] flex-1 overflow-hidden rounded-full"
        style={{ background: 'var(--app-bg-tertiary, rgba(255,255,255,0.1))' }}
      >
        <div
          className="h-full rounded-full bg-gradient-to-r from-yellow-400 to-orange-500 transition-all"
          style={{ width: `${progress}%` }}
        />
      </div>
      <span className="whitespace-nowrap text-[10px] font-medium" style={{ color: 'var(--app-text-secondary)' }}>
        {data.xp} {t('analytics.table.xp')}
      </span>
    </div>
  );
}
