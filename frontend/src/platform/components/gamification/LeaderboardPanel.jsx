import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Crown, Medal, Trophy } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';
import Skeleton from '../../../shared/components/Skeleton';

const RANK_ICONS = {
  1: <Crown size={16} className="text-yellow-400" />,
  2: <Medal size={16} className="text-gray-300" />,
  3: <Medal size={16} className="text-amber-600" />,
};

export default function LeaderboardPanel({ projectId = null, users = [] }) {
  const { t } = useTranslation('platform');
  const { api, accessToken } = useGamification();
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!accessToken) {
      setLoading(false);
      return;
    }

    const fetchEntries = async () => {
      try {
        const data = projectId
          ? await api.getProjectLeaderboard(accessToken, projectId)
          : await api.getGlobalLeaderboard(accessToken);
        setEntries(data || []);
      } catch {
        setEntries([]);
      }

      setLoading(false);
    };

    fetchEntries();
  }, [accessToken, projectId, api]);

  const getUserName = (userId) => {
    const user = users.find((candidate) => String(candidate.id).toLowerCase() === String(userId).toLowerCase());
    return user?.name || t('leaderboard.userFallback', { id: String(userId).slice(0, 6) });
  };

  return (
    <div className="glass-card rounded-xl p-4">
      <h3 className="text-sm font-semibold mb-3 flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
        <Trophy size={16} className="text-yellow-400" />
        {projectId ? t('leaderboard.project') : t('leaderboard.global')}
      </h3>
      {loading ? (
        <Skeleton.List rows={3} />
      ) : entries.length === 0 ? (
        <div className="text-xs text-center py-4" style={{ color: 'var(--app-text-secondary)' }}>
          {t('leaderboard.empty')}
        </div>
      ) : (
        <div className="space-y-1">
          {entries.map((entry) => (
            <div
              key={entry.userId}
              className="flex items-center gap-2 p-2 rounded-lg"
              style={{ background: entry.rank <= 3 ? 'var(--app-bg-secondary)' : 'transparent' }}
            >
              <div className="w-6 flex justify-center">
                {RANK_ICONS[entry.rank] || (
                  <span className="text-xs font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                    {entry.rank}
                  </span>
                )}
              </div>
              <span className="flex-1 text-xs font-medium truncate" style={{ color: 'var(--app-text-main)' }}>
                {getUserName(entry.userId)}
              </span>
              <span className="text-xs font-semibold" style={{ color: 'var(--app-accent)' }}>
                {t('leaderboard.levelShort', { level: entry.level })}
              </span>
              <span className="text-[10px] w-16 text-right" style={{ color: 'var(--app-text-secondary)' }}>
                {entry.xp} {t('analytics.table.xp')}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
