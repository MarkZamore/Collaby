import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  Award,
  BarChart3,
  ChevronRight,
  Clock,
  Coins,
  Flame,
  Gamepad2,
  Gift,
  Shield,
  ShoppingBag,
  Star,
  Target,
  TreePine,
  Trophy,
  TrendingUp,
  Zap,
} from 'lucide-react';

const Section = ({ icon: Icon, iconColor, title, children }) => (
  <div className="glass-card rounded-2xl p-6 space-y-4">
    <h2 className="flex items-center gap-2.5 text-lg font-bold" style={{ color: 'var(--app-text-main)' }}>
      <div
        className="flex h-9 w-9 items-center justify-center rounded-xl"
        style={{ backgroundColor: `color-mix(in srgb, ${iconColor} 18%, transparent)` }}
      >
        <Icon size={18} style={{ color: iconColor }} />
      </div>
      {title}
    </h2>
    <div className="space-y-3 text-sm leading-relaxed" style={{ color: 'var(--app-text-secondary)' }}>
      {children}
    </div>
  </div>
);

const InfoRow = ({ icon: Icon, label, value, color }) => (
  <div
    className="flex items-center gap-3 rounded-xl p-3"
    style={{ backgroundColor: 'var(--app-layer-base)' }}
  >
    <div
      className="flex h-8 w-8 flex-shrink-0 items-center justify-center rounded-lg"
      style={{ backgroundColor: `color-mix(in srgb, ${color} 20%, transparent)` }}
    >
      <Icon size={14} style={{ color }} />
    </div>
    <span className="flex-1" style={{ color: 'var(--app-text-main)' }}>
      {label}
    </span>
    <span className="text-sm font-bold" style={{ color }}>
      {value}
    </span>
  </div>
);

const RewardCard = ({ icon: Icon, color, title, subtitle }) => (
  <div className="rounded-xl p-4 text-center" style={{ backgroundColor: 'var(--app-layer-base)' }}>
    <Icon size={24} className="mx-auto mb-2" style={{ color }} />
    <div className="text-xs font-bold" style={{ color }}>
      {title}
    </div>
    <div className="mt-1 text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>
      {subtitle}
    </div>
  </div>
);

const GamificationInfo = () => {
  const { t } = useTranslation('platform');
  const xpLabel = t('analytics.table.xp');

  const questsDaily = t('gamificationInfo.quests.dailyTasks', { returnObjects: true });
  const questsWeekly = t('gamificationInfo.quests.weeklyTasks', { returnObjects: true });

  return (
    <main className="flex-1 overflow-y-auto p-4 scrollbar-hide sm:p-6 lg:p-8">
      <div className="mx-auto w-full max-w-4xl">
        <div className="mb-8">
          <h1 className="flex items-center gap-2.5 text-2xl font-bold" style={{ color: 'var(--app-text-main)' }}>
            <Gamepad2 size={24} style={{ color: 'var(--app-accent)' }} />
            {t('gamificationInfo.title')}
          </h1>
          <p className="mt-1.5 text-sm" style={{ color: 'var(--app-text-secondary)' }}>
            {t('gamificationInfo.subtitle')}
          </p>
        </div>

        <div className="grid gap-6">
          <Section icon={Zap} iconColor="var(--color-warning, #FBBF24)" title={t('gamificationInfo.xp.title')}>
            <p>{t('gamificationInfo.xp.description')}</p>
            <div className="space-y-2">
              <InfoRow
                icon={Target}
                label={t('gamificationInfo.xp.taskCompletion')}
                value={`+5 ... +20 ${xpLabel}`}
                color="var(--color-success, #22C55E)"
              />
              <InfoRow
                icon={ChevronRight}
                label={t('gamificationInfo.xp.subtaskCompletion')}
                value={`+3 ${xpLabel}`}
                color="var(--color-info, #3B82F6)"
              />
              <InfoRow
                icon={ChevronRight}
                label={t('gamificationInfo.xp.commentAdded')}
                value={`+2 ${xpLabel}`}
                color="var(--role-admin, #8B5CF6)"
              />
            </div>
            <div
              className="rounded-xl p-3 text-xs"
              style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}
            >
              <strong style={{ color: 'var(--app-text-main)' }}>{t('gamificationInfo.xp.noteTitle')}:</strong>{' '}
              {t('gamificationInfo.xp.note')}
            </div>
          </Section>

          <Section icon={TrendingUp} iconColor="var(--color-info, #3B82F6)" title={t('gamificationInfo.levels.title')}>
            <p>{t('gamificationInfo.levels.description')}</p>
            <div className="space-y-2">
              <InfoRow icon={Star} label={t('gamificationInfo.levels.level2')} value={`200 ${xpLabel}`} color="var(--color-info, #3B82F6)" />
              <InfoRow icon={Star} label={t('gamificationInfo.levels.level5')} value={`500 ${xpLabel}`} color="var(--role-admin, #8B5CF6)" />
              <InfoRow icon={Star} label={t('gamificationInfo.levels.level10')} value={`1 000 ${xpLabel}`} color="var(--color-warning, #F59E0B)" />
            </div>
            <div
              className="rounded-xl p-3 text-xs"
              style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}
            >
              <strong style={{ color: 'var(--app-text-main)' }}>{t('gamificationInfo.levels.noteTitle')}:</strong>{' '}
              {t('gamificationInfo.levels.note')}
            </div>
          </Section>

          <Section icon={Coins} iconColor="var(--color-warning, #F59E0B)" title={t('gamificationInfo.coins.title')}>
            <p>{t('gamificationInfo.coins.description')}</p>
            <div className="space-y-2">
              <InfoRow icon={ShoppingBag} label={t('gamificationInfo.coins.avatarFrames')} value="500 - 2 000" color="var(--color-warning, #F59E0B)" />
              <InfoRow icon={ShoppingBag} label={t('gamificationInfo.coins.themes')} value="0 - 800" color="var(--color-success, #22C55E)" />
              <InfoRow icon={ShoppingBag} label={t('gamificationInfo.coins.badges')} value="100 - 5 000" color="var(--role-admin, #8B5CF6)" />
              <InfoRow icon={Shield} label={t('gamificationInfo.coins.streakFreeze')} value="500" color="var(--color-error, #EF4444)" />
            </div>
            <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {t('gamificationInfo.coins.note')}
            </p>
          </Section>

          <Section icon={Target} iconColor="var(--role-admin, #A855F7)" title={t('gamificationInfo.quests.title')}>
            <p>{t('gamificationInfo.quests.description')}</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="rounded-xl p-4" style={{ backgroundColor: 'var(--app-layer-base)' }}>
                <div className="mb-2 flex items-center gap-2">
                  <Clock size={14} style={{ color: 'var(--role-admin, #A855F7)' }} />
                  <span className="text-xs font-bold uppercase tracking-wider" style={{ color: 'var(--role-admin, #A855F7)' }}>
                    {t('gamificationInfo.quests.dailyTitle')}
                  </span>
                </div>
                <p className="text-xs" style={{ color: 'var(--app-text-main)' }}>
                  {t('gamificationInfo.quests.dailyDescription')}
                </p>
                <ul className="mt-2 space-y-1 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                  {questsDaily.map((item) => (
                    <li key={item}>&bull; {item}</li>
                  ))}
                </ul>
              </div>
              <div className="rounded-xl p-4" style={{ backgroundColor: 'var(--app-layer-base)' }}>
                <div className="mb-2 flex items-center gap-2">
                  <Gift size={14} style={{ color: 'var(--color-info, #3B82F6)' }} />
                  <span className="text-xs font-bold uppercase tracking-wider" style={{ color: 'var(--color-info, #3B82F6)' }}>
                    {t('gamificationInfo.quests.weeklyTitle')}
                  </span>
                </div>
                <p className="text-xs" style={{ color: 'var(--app-text-main)' }}>
                  {t('gamificationInfo.quests.weeklyDescription')}
                </p>
                <ul className="mt-2 space-y-1 text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                  {questsWeekly.map((item) => (
                    <li key={item}>&bull; {item}</li>
                  ))}
                </ul>
              </div>
            </div>
          </Section>

          <Section icon={Flame} iconColor="var(--color-error, #EF4444)" title={t('gamificationInfo.streak.title')}>
            <p>{t('gamificationInfo.streak.description')}</p>
            <div className="space-y-2">
              <InfoRow icon={Flame} label={t('gamificationInfo.streak.day7')} value={`x1.1 ${xpLabel}`} color="var(--primary-orange, #F97316)" />
              <InfoRow icon={Flame} label={t('gamificationInfo.streak.day14')} value={`x1.2 ${xpLabel}`} color="var(--color-error, #EF4444)" />
              <InfoRow icon={Flame} label={t('gamificationInfo.streak.day21')} value={`x1.3 ${xpLabel}`} color="var(--color-error, #DC2626)" />
              <InfoRow icon={Flame} label={t('gamificationInfo.streak.day28')} value={`x1.4 ${xpLabel}`} color="var(--color-error, #B91C1C)" />
              <InfoRow icon={Flame} label={t('gamificationInfo.streak.day35')} value={`x1.5 ${xpLabel}`} color="var(--color-error, #991B1B)" />
            </div>
            <div
              className="rounded-xl p-3 text-xs"
              style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}
            >
              <strong style={{ color: 'var(--app-text-main)' }}>{t('gamificationInfo.streak.noteTitle')}:</strong>{' '}
              {t('gamificationInfo.streak.note')}
            </div>
          </Section>

          <Section icon={Trophy} iconColor="var(--color-warning, #F59E0B)" title={t('gamificationInfo.leaderboard.title')}>
            <p>{t('gamificationInfo.leaderboard.description')}</p>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3">
              <RewardCard
                icon={Award}
                color="var(--color-warning, #F59E0B)"
                title={t('gamificationInfo.leaderboard.firstPlace')}
                subtitle={t('gamificationInfo.leaderboard.goldCrown')}
              />
              <RewardCard
                icon={Award}
                color="var(--app-text-secondary, #94A3B8)"
                title={t('gamificationInfo.leaderboard.secondPlace')}
                subtitle={t('gamificationInfo.leaderboard.silverCrown')}
              />
              <RewardCard
                icon={Award}
                color="var(--color-warning, #D97706)"
                title={t('gamificationInfo.leaderboard.thirdPlace')}
                subtitle={t('gamificationInfo.leaderboard.bronzeCrown')}
              />
            </div>
            <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {t('gamificationInfo.leaderboard.note')}
            </p>
          </Section>

          <Section icon={TreePine} iconColor="var(--color-success, #10B981)" title={t('gamificationInfo.skillTree.title')}>
            <p>{t('gamificationInfo.skillTree.description')}</p>
            <div className="space-y-2">
              <InfoRow
                icon={TreePine}
                label={t('gamificationInfo.skillTree.tier1')}
                value={t('gamificationInfo.skillTree.tier1Value')}
                color="var(--color-success, #10B981)"
              />
              <InfoRow
                icon={TreePine}
                label={t('gamificationInfo.skillTree.tier2')}
                value={t('gamificationInfo.skillTree.tier2Value')}
                color="var(--color-success, #059669)"
              />
              <InfoRow
                icon={TreePine}
                label={t('gamificationInfo.skillTree.tier3')}
                value={t('gamificationInfo.skillTree.tier3Value')}
                color="var(--color-success, #047857)"
              />
            </div>
            <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
              {t('gamificationInfo.skillTree.note')}
            </p>
          </Section>
        </div>
      </div>
    </main>
  );
};

export default GamificationInfo;
