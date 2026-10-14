import React, { useEffect, useMemo, useState } from 'react';
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  Filler,
} from 'chart.js';
import { Line, Doughnut } from 'react-chartjs-2';
import { BarChart2, Trophy, Medal, TrendingUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { usePlatform } from '../context/PlatformContext';
import { cn } from '../../shared/utils/cn';
import { usePreferences } from '../context/PreferencesContext';
import { useGamification } from '../../shared/context/GamificationContext';
import { resolveUserAvatar } from '../../shared/utils/avatar';
import { formatNumber } from '../../shared/utils/locale';
import { getProjectOverview } from '../../shared/utils/analyticsApi';
import Skeleton from '../../shared/components/Skeleton';

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  ArcElement,
  Filler
);

const Analytics = () => {
  const { t } = useTranslation('platform');
  const { users, currentUser: rawCurrentUser, activeProjectId } = usePlatform();
  const { resolvedTheme, language } = usePreferences();
  const { api: gApi, accessToken: gToken } = useGamification();
  const isDark = resolvedTheme === 'dark';
  const currentUser = rawCurrentUser;

  const [selectedPeriod, setSelectedPeriod] = useState('week');
  const [overview, setOverview] = useState(null);
  const [overviewStatus, setOverviewStatus] = useState('idle');
  const [leaderboard, setLeaderboard] = useState([]);

  const getCssVar = (name, fallback) =>
    typeof document !== 'undefined'
      ? getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
      : fallback;

  const legendColor = getCssVar('--app-text-secondary', isDark ? '#94a3b8' : '#475569');
  const accentOrange = getCssVar('--primary-orange', '#f97316');

  useEffect(() => {
    if (!gToken || !activeProjectId) {
      setOverview(null);
      setOverviewStatus('idle');
      return;
    }

    let cancelled = false;

    const loadOverview = async () => {
      setOverviewStatus('loading');

      try {
        const data = await getProjectOverview(gToken, activeProjectId, selectedPeriod);
        if (cancelled) {
          return;
        }

        setOverview(data || null);
        setOverviewStatus('success');
      } catch {
        if (cancelled) {
          return;
        }

        setOverview(null);
        setOverviewStatus('error');
      }
    };

    loadOverview();

    return () => {
      cancelled = true;
    };
  }, [activeProjectId, gToken, selectedPeriod]);

  useEffect(() => {
    if (!gToken || !activeProjectId) {
      setLeaderboard([]);
      return;
    }

    let cancelled = false;

    const loadLeaderboard = async () => {
      try {
        const data = await gApi.getProjectLeaderboard(gToken, activeProjectId, 10);
        if (cancelled) {
          return;
        }

        setLeaderboard(data || []);
      } catch {
        if (cancelled) {
          return;
        }

        setLeaderboard([]);
      }
    };

    loadLeaderboard();

    return () => {
      cancelled = true;
    };
  }, [activeProjectId, gApi, gToken]);

  const formatBucketLabel = (value) => {
    const date = new Date(value);
    const formatter = new Intl.DateTimeFormat(language || undefined, {
      ...(selectedPeriod === 'month'
        ? { day: 'numeric', month: 'short' }
        : { weekday: 'short' }),
      timeZone: 'UTC',
    });

    return formatter.format(date);
  };

  const productivitySeries = useMemo(
    () =>
      (overview?.productivity || []).map((bucket) => ({
        label: formatBucketLabel(bucket.bucketStartUtc),
        total:
          (bucket.tasksCompleted || 0) +
          (bucket.tasksReopened || 0) +
          (bucket.subtasksCompleted || 0) +
          (bucket.commentsAdded || 0),
      })),
    [language, overview?.productivity, selectedPeriod]
  );

  const hasProductivityData = productivitySeries.some((bucket) => bucket.total > 0);

  const sortedStatusDistribution = useMemo(
    () => [...(overview?.statusDistribution || [])].sort((left, right) => left.order - right.order),
    [overview?.statusDistribution]
  );

  const hasStatusDistributionData = sortedStatusDistribution.some((entry) => entry.taskCount > 0);

  const lineData = useMemo(
    () => ({
      labels: productivitySeries.map((entry) => entry.label),
      datasets: [
        {
          label: t('analytics.completedTasks'),
          data: productivitySeries.map((entry) => entry.total),
          borderColor: accentOrange,
          backgroundColor: (context) => {
            const gradient = context.chart.ctx.createLinearGradient(0, 0, 0, 300);
            gradient.addColorStop(0, `${accentOrange}66`);
            gradient.addColorStop(1, `${accentOrange}00`);
            return gradient;
          },
          tension: 0.4,
          fill: true,
          pointBackgroundColor: accentOrange,
          pointBorderColor: getCssVar('--app-layer-base', isDark ? '#1e293b' : '#fff'),
          pointBorderWidth: 2,
          pointRadius: 4,
          pointHoverRadius: 6,
        },
      ],
    }),
    [productivitySeries, t]
  );

  const lineOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: getCssVar('--app-layer-base', isDark ? '#1e293b' : '#fff'),
        titleColor: getCssVar('--app-text-main', isDark ? '#fff' : '#0f172a'),
        bodyColor: getCssVar('--app-text-secondary', isDark ? '#cbd5e1' : '#475569'),
        borderColor: getCssVar('--app-border', isDark ? '#334155' : '#e2e8f0'),
        borderWidth: 1,
        padding: 10,
        displayColors: false,
      },
    },
    scales: {
      x: {
        grid: { display: false },
        ticks: { color: getCssVar('--app-text-secondary', isDark ? '#94a3b8' : '#64748b') },
      },
      y: {
        grid: { color: getCssVar('--app-border', isDark ? '#334155' : '#e2e8f0'), borderDash: [4, 4] },
        ticks: { color: getCssVar('--app-text-secondary', isDark ? '#94a3b8' : '#64748b'), stepSize: 2 },
      },
    },
  };

  const doughnutData = useMemo(
    () => ({
      labels: sortedStatusDistribution.map((entry) => entry.name || t('analytics.table.rolePlaceholder')),
      datasets: [
        {
          data: sortedStatusDistribution.map((entry) => entry.taskCount),
          backgroundColor: sortedStatusDistribution.map((entry, index) => {
            const fallbackPalette = [getCssVar('--app-text-secondary', '#94a3b8'), getCssVar('--color-info', '#3b82f6'), getCssVar('--color-warning', '#f59e0b'), getCssVar('--role-admin', '#a855f7'), getCssVar('--color-success', '#22c55e')];
            return entry.color || fallbackPalette[index % fallbackPalette.length];
          }),
          borderWidth: 0,
        },
      ],
    }),
    [sortedStatusDistribution, t]
  );

  const doughnutOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'right',
        labels: {
          color: legendColor,
          font: { family: "'Inter', sans-serif", size: 12 },
        },
      },
    },
    cutout: '70%',
  };

  const memberDirectory = useMemo(() => {
    const directory = new Map();

    (overview?.memberDirectory || []).forEach((entry) => {
      directory.set(String(entry.userId).toLowerCase(), entry);
    });

    return directory;
  }, [overview?.memberDirectory]);

  const platformUsers = useMemo(() => {
    const directory = new Map();

    (users || []).forEach((user) => {
      directory.set(String(user.id).toLowerCase(), user);
    });

    return directory;
  }, [users]);

  const getRoleLabel = (role) => {
    const normalizedRole = String(role || '').trim().toLowerCase();

    if (!normalizedRole) {
      return t('analytics.table.rolePlaceholder');
    }

    if (['owner', 'admin', 'member', 'viewer'].includes(normalizedRole)) {
      return t(`team.roles.${normalizedRole}.label`);
    }

    return role;
  };

  const leaderboardRows = useMemo(
    () =>
      leaderboard.map((entry) => {
        const normalizedUserId = String(entry.userId).toLowerCase();
        const directoryEntry = memberDirectory.get(normalizedUserId);
        const platformUser = platformUsers.get(normalizedUserId);
        const name =
          directoryEntry?.displayName ||
          platformUser?.name ||
          directoryEntry?.username ||
          platformUser?.username ||
          `User ${String(entry.userId).slice(0, 6)}`;

        return {
          ...entry,
          avatar: resolveUserAvatar({
            avatarUrl: directoryEntry?.avatarUrl || platformUser?.avatar,
            userId: entry.userId,
            displayName: directoryEntry?.displayName || platformUser?.name,
            username: directoryEntry?.username || platformUser?.username,
          }),
          name,
          roleLabel: getRoleLabel(directoryEntry?.role),
        };
      }),
    [leaderboard, memberDirectory, platformUsers, t]
  );

  const renderCardState = (messageKey) => {
    if (messageKey === 'analytics.loading') {
      return <Skeleton.Chart />;
    }
    return (
      <div
        className="flex h-[250px] items-center justify-center text-center text-sm"
        style={{ color: 'var(--app-text-secondary)' }}
      >
        {t(messageKey)}
      </div>
    );
  };

  const getRankIcon = (index) => {
    switch (index) {
      case 0:
        return <Trophy className="text-yellow-500" size={20} />;
      case 1:
        return <Medal className="text-slate-400" size={20} />;
      case 2:
        return <Medal className="text-orange-500" size={20} />;
      default:
        return (
          <span className="text-slate-400 font-mono font-medium w-5 text-center">
            {index + 1}
          </span>
        );
    }
  };

  if (!activeProjectId) {
    return (
      <main className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8 scrollbar-hide">
        <div className="w-full space-y-6">
          <div className="flex items-start justify-between gap-4 flex-wrap">
            <div>
              <h1 className="text-2xl font-bold flex items-center gap-2">
                <BarChart2 size={22} style={{ color: 'var(--app-accent)' }} />
                {t('analytics.title')}
              </h1>
              <p className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
                {t('analytics.subtitle')}
              </p>
            </div>
          </div>

          <div className="glass-card rounded-2xl p-8">
            <div className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
              {t('analytics.noProject')}
            </div>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8 scrollbar-hide">
      <div className="w-full space-y-6">
        <div className="flex items-start justify-between gap-4 flex-wrap">
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <BarChart2 size={22} style={{ color: 'var(--app-accent)' }} />
              {t('analytics.title')}
            </h1>
            <p className="text-sm mt-1" style={{ color: 'var(--app-text-secondary)' }}>
              {t('analytics.subtitle')}
            </p>
          </div>
          <div
            className="text-sm font-medium px-3 py-1 rounded-full"
            style={{
              color: 'var(--app-text-secondary)',
              backgroundColor: 'var(--app-layer-base)',
            }}
          >
            {t('analytics.updatedJustNow')}
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="glass-card rounded-2xl p-6 relative overflow-hidden">
            <div className="flex items-center justify-between mb-6 relative z-10">
              <h3 className="font-bold flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
                <TrendingUp size={18} className="text-orange-500" />
                {t('analytics.productivity')}
              </h3>
              <select
                className="text-xs border-none rounded-lg px-3 py-1.5 outline-none cursor-pointer font-bold transition-colors"
                style={{
                  backgroundColor: 'var(--app-layer-base)',
                  color: 'var(--app-text-secondary)',
                }}
                value={selectedPeriod}
                onChange={(event) => setSelectedPeriod(event.target.value)}
              >
                <option value="week">{t('analytics.periods.week')}</option>
                <option value="month">{t('analytics.periods.month')}</option>
              </select>
            </div>
            <div className="h-[250px] w-full relative z-10">
              {overviewStatus === 'loading'
                ? renderCardState('analytics.loading')
                : overviewStatus === 'error'
                  ? renderCardState('analytics.loadError')
                  : hasProductivityData
                    ? <Line data={lineData} options={lineOptions} />
                    : renderCardState('analytics.noData')}
            </div>
          </div>

          <div className="glass-card rounded-2xl p-6 relative overflow-hidden">
            <h3 className="font-bold mb-6 relative z-10" style={{ color: 'var(--app-text-main)' }}>
              {t('analytics.taskDistribution')}
            </h3>
            <div className="h-[250px] w-full flex items-center justify-center relative z-10">
              {overviewStatus === 'loading'
                ? renderCardState('analytics.loading')
                : overviewStatus === 'error'
                  ? renderCardState('analytics.loadError')
                  : hasStatusDistributionData
                    ? <Doughnut data={doughnutData} options={doughnutOptions} />
                    : renderCardState('analytics.noData')}
            </div>
          </div>
        </div>

        <div className="glass-panel rounded-2xl overflow-hidden">
          <div
            className="p-6 border-b"
            style={{
              borderColor: 'var(--app-border)',
              backgroundColor: 'var(--app-layer-base)',
            }}
          >
            <h3 className="font-bold flex items-center gap-2" style={{ color: 'var(--app-text-main)' }}>
              <Trophy className="text-yellow-500" size={18} />
              {t('analytics.leaderboard')}
            </h3>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr
                  className="text-xs uppercase tracking-wider font-bold"
                  style={{
                    backgroundColor: 'var(--app-layer-base)',
                    color: 'var(--app-text-secondary)',
                  }}
                >
                  <th className="px-3 py-3 w-12 text-center sm:px-6 sm:py-4 sm:w-16">{t('analytics.table.rank')}</th>
                  <th className="px-3 py-3 sm:px-6 sm:py-4">{t('analytics.table.member')}</th>
                  <th className="hidden px-3 py-3 sm:table-cell sm:px-6 sm:py-4">{t('analytics.table.role')}</th>
                  <th className="px-3 py-3 text-center sm:px-6 sm:py-4">{t('analytics.table.level')}</th>
                  <th className="px-3 py-3 text-right sm:px-6 sm:py-4">{t('analytics.table.xp')}</th>
                </tr>
              </thead>
              <tbody className="divide-y" style={{ borderColor: 'var(--app-border)' }}>
                {leaderboardRows.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-6 py-8 text-center text-sm" style={{ color: 'var(--app-text-secondary)' }}>
                      {t('analytics.leaderboardEmpty')}
                    </td>
                  </tr>
                ) : (
                  leaderboardRows.map((entry, index) => {
                    const isCurrentUser =
                      String(entry.userId).toLowerCase() === String(currentUser?.id).toLowerCase();

                    return (
                      <tr
                        key={entry.userId}
                        className="transition-all hover:opacity-90"
                        style={{
                          backgroundColor: isCurrentUser
                            ? `color-mix(in srgb, var(--primary-orange) 15%, transparent)`
                            : 'var(--app-layer-surface)',
                        }}
                      >
                        <td className="px-3 py-3 text-center sm:px-6 sm:py-4">
                          <div className="flex justify-center">{getRankIcon(index)}</div>
                        </td>
                        <td className="px-3 py-3 sm:px-6 sm:py-4">
                          <div className="flex items-center gap-3">
                            <div
                              className={cn(
                                'w-10 h-10 rounded-full p-0.5 shadow-sm',
                                index === 0
                                  ? 'bg-gradient-to-br from-yellow-300 to-yellow-600'
                                  : index === 1
                                    ? 'bg-gradient-to-br from-slate-300 to-slate-500'
                                    : index === 2
                                      ? 'bg-gradient-to-br from-orange-300 to-orange-600'
                                      : 'bg-transparent'
                              )}
                            >
                              <img
                                src={entry.avatar}
                                alt={entry.name}
                                className="w-full h-full rounded-full"
                                style={{ backgroundColor: 'var(--app-layer-surface)' }}
                              />
                            </div>
                            <div>
                              <div
                                className="font-bold text-sm"
                                style={{
                                  color: isCurrentUser ? 'var(--app-accent)' : 'var(--app-text-main)',
                                }}
                              >
                                {entry.name} {isCurrentUser && t('analytics.you')}
                              </div>
                              {index === 0 && (
                                <span className="text-[10px] text-yellow-600 font-bold uppercase tracking-wide">
                                  {t('analytics.leader')}
                                </span>
                              )}
                            </div>
                          </div>
                        </td>
                        <td className="hidden px-3 py-3 text-sm font-medium sm:table-cell sm:px-6 sm:py-4" style={{ color: 'var(--app-text-secondary)' }}>
                          {entry.roleLabel}
                        </td>
                        <td className="px-3 py-3 text-center sm:px-6 sm:py-4">
                          <span
                            className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold border"
                            style={{
                              backgroundColor: 'var(--app-layer-base)',
                              color: 'var(--app-text-main)',
                              borderColor: 'var(--app-border)',
                            }}
                          >
                            {t('analytics.table.levelShort', { level: entry.level || 0 })}
                          </span>
                        </td>
                        <td className="px-3 py-3 text-right font-mono font-bold sm:px-6 sm:py-4" style={{ color: 'var(--app-text-secondary)' }}>
                          {formatNumber(entry.xp || 0, language)} {t('analytics.table.xp')}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </main>
  );
};

export default Analytics;
