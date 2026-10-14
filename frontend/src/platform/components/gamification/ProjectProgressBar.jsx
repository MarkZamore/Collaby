import React, { useState, useEffect } from 'react';
import { BarChart3 } from 'lucide-react';
import { useGamification } from '../../../shared/context/GamificationContext';

export default function ProjectProgressBar({ projectId }) {
  const { api, accessToken } = useGamification();
  const [progress, setProgress] = useState(null);

  useEffect(() => {
    if (!accessToken || !projectId) return;
    api.getProjectProgress(accessToken, projectId)
      .then(setProgress)
      .catch(() => {});
  }, [accessToken, projectId, api]);

  if (!progress) return null;

  const pct = progress.totalWeight > 0
    ? Math.round((progress.completedWeight / Math.max(progress.totalWeight, 1)) * 100)
    : Math.round(progress.progressPercent || 0);

  return (
    <div className="flex items-center gap-2 px-3 py-1.5">
      <BarChart3 size={14} style={{ color: 'var(--app-text-secondary)' }} />
      <div className="flex-1 h-1.5 rounded-full overflow-hidden" style={{ background: 'var(--app-bg-tertiary)' }}>
        <div
          className="h-full rounded-full transition-all duration-500"
          style={{ width: `${pct}%`, background: 'var(--app-accent)' }}
        />
      </div>
      <span className="text-[10px] font-medium" style={{ color: 'var(--app-text-secondary)' }}>
        {pct}%
      </span>
    </div>
  );
}
