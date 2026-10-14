import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Trophy } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';
import { resolveUserAvatar } from '../../../shared/utils/avatar';

export default function EmployeeOfMonth({ projectId }) {
  const { t } = useTranslation('platform');
  const { api, accessToken } = useGamification();
  const [topUser, setTopUser] = useState(null);

  useEffect(() => {
    if (!accessToken || !projectId) return;
    api.getProjectLeaderboard(accessToken, projectId, 1)
      .then((data) => {
        if (data && data.length > 0) setTopUser(data[0]);
      })
      .catch(() => {});
  }, [accessToken, projectId, api]);

  if (!topUser) return null;

  const avatarUrl = resolveUserAvatar({
    avatarUrl: topUser.avatarUrl,
    userId: topUser.userId,
    username: topUser.username,
  });

  return (
    <div className="glass-card rounded-xl p-4">
      <h3
        className="mb-3 flex items-center gap-2 text-xs font-bold uppercase tracking-wider"
        style={{ color: 'var(--app-text-secondary)' }}
      >
        <Trophy size={14} className="text-yellow-400" />
        {t('gamificationWidgets.projectLeader')}
      </h3>
      <div className="flex items-center gap-3">
        <div className="relative">
          <img
            src={avatarUrl}
            alt={topUser.username || t('gamificationWidgets.participantFallback')}
            className="h-10 w-10 rounded-full"
            style={{ border: '2px solid #F59E0B' }}
          />
          <div className="absolute -right-1 -top-1 flex h-5 w-5 items-center justify-center rounded-full bg-yellow-500">
            <Trophy size={10} className="text-white" />
          </div>
        </div>
        <div>
          <p className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
            {topUser.username || t('gamificationWidgets.participantFallback')}
          </p>
          <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
            {topUser.xp} {t('analytics.table.xp')} &middot; {t('gamificationWidgets.levelShort', { level: topUser.level })}
          </p>
        </div>
      </div>
    </div>
  );
}
