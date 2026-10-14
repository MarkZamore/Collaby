import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Award,
  CheckCircle2,
  Coins,
  Flame,
  Gift,
  Package,
  Sparkles,
  Star,
  Target,
  Trophy,
} from 'lucide-react';

import Skeleton from '../../shared/components/Skeleton';
import { useGamification } from '../../shared/context/GamificationContext';
import { localizeQuest, localizeShopItem } from '../../shared/utils/gamificationCatalogLocalization';
import '../styles/Awards.css';

const SummaryCard = ({ icon: Icon, label, value, accent = 'var(--app-accent)' }) => (
  <div className="awards-summary-card">
    <div className="awards-summary-icon" style={{ color: accent }}>
      <Icon size={18} />
    </div>
    <div className="awards-summary-copy">
      <span className="awards-summary-label">{label}</span>
      <span className="awards-summary-value">{value}</span>
    </div>
  </div>
);

const SectionHeader = ({ icon: Icon, title, subtitle }) => (
  <div className="awards-section-header">
    <div className="awards-section-title-wrap">
      <div className="awards-section-icon">
        <Icon size={16} />
      </div>
      <div>
        <h2 className="awards-section-title">{title}</h2>
        {subtitle && <p className="awards-section-subtitle">{subtitle}</p>}
      </div>
    </div>
  </div>
);

const EmptyState = ({ icon: Icon, text }) => (
  <div className="awards-empty-panel">
    <Icon size={20} />
    <span>{text}</span>
  </div>
);

const RewardCard = ({ item, fallbackName, t }) => {
  const title = item?.name || fallbackName || t('awards.rewardFallback');
  const description = item?.description || item?.categoryLabel || '';
  const equipped = Boolean(item?.isEquipped || item?.equipped);

  return (
    <article className="awards-card awards-card-earned">
      <div className="awards-card-icon">
        <Gift size={18} />
      </div>
      <div className="awards-card-body">
        <div className="awards-card-topline">
          <h3 className="awards-card-title">{title}</h3>
          {equipped && <span className="awards-badge awards-badge-equipped">{t('awards.equipped')}</span>}
        </div>
        {description && <p className="awards-card-description">{description}</p>}
      </div>
    </article>
  );
};

const QuestCard = ({ quest, t }) => {
  const progress = Math.min(
    100,
    Math.round(((quest?.progress || 0) / Math.max(quest?.requiredCount || 1, 1)) * 100),
  );

  return (
    <article className={`awards-card ${quest?.isClaimed ? 'awards-card-earned' : 'awards-card-progress'}`}>
      <div className="awards-card-icon">
        {quest?.isClaimed ? <CheckCircle2 size={18} /> : <Target size={18} />}
      </div>
      <div className="awards-card-body">
        <div className="awards-card-topline">
          <h3 className="awards-card-title">{quest?.title}</h3>
          {quest?.isClaimed && <span className="awards-badge awards-badge-earned">{t('awards.claimed')}</span>}
        </div>
        {quest?.description && <p className="awards-card-description">{quest.description}</p>}
        <div className="awards-progress-row">
          <div className="awards-progress-bar">
            <div className="awards-progress-fill" style={{ width: `${progress}%` }} />
          </div>
          <span className="awards-progress-text">{quest?.progress || 0}/{quest?.requiredCount || 0}</span>
        </div>
      </div>
    </article>
  );
};

const Awards = () => {
  const { t } = useTranslation('platform');
  const { accessToken, api, coins, globalXp, inventory, loading, quests, streak } = useGamification();
  const [shopCatalog, setShopCatalog] = useState([]);
  const [catalogLoading, setCatalogLoading] = useState(false);

  useEffect(() => {
    if (!accessToken) {
      setShopCatalog([]);
      return;
    }

    setCatalogLoading(true);
    api.getShopItems(accessToken)
      .then((items) => setShopCatalog(Array.isArray(items) ? items : []))
      .catch(() => setShopCatalog([]))
      .finally(() => setCatalogLoading(false));
  }, [accessToken, api]);

  const localizedCatalog = useMemo(
    () => shopCatalog.map((item) => localizeShopItem(item, t)),
    [shopCatalog, t],
  );

  const localizedInventory = useMemo(() => (
    (inventory || []).map((entry) => {
      const catalogItem = localizedCatalog.find((item) => item.id === (entry.itemId || entry.id));
      return {
        ...entry,
        ...(catalogItem || {}),
      };
    })
  ), [inventory, localizedCatalog]);

  const localizedQuests = useMemo(
    () => (quests || []).map((quest) => localizeQuest(quest, t)),
    [quests, t],
  );

  const claimedQuests = localizedQuests.filter((quest) => quest.isClaimed);
  const activeQuests = localizedQuests.filter((quest) => !quest.isClaimed);
  const streakDays = streak?.streakDays || 0;
  const freezeCount = streak?.freezeCount || 0;
  const earnedCount = localizedInventory.length + claimedQuests.length;
  const isLoading = loading || catalogLoading;

  return (
    <div className="awards-page">
      <div className="awards-header">
        <div>
          <h1 className="awards-title">{t('awards.title')}</h1>
          <p className="awards-subtitle">{t('awards.subtitle')}</p>
        </div>
        <div className="awards-pill">
          <Trophy size={18} />
          <span>{earnedCount}</span>
        </div>
      </div>

      <div className="awards-summary-grid">
        <SummaryCard
          icon={Star}
          label={t('awards.summary.level')}
          value={globalXp?.level ?? 1}
        />
        <SummaryCard
          icon={Sparkles}
          label={t('awards.summary.xp')}
          value={globalXp?.totalXp ?? 0}
        />
        <SummaryCard
          icon={Coins}
          label={t('awards.summary.coins')}
          value={coins ?? 0}
        />
        <SummaryCard
          icon={Flame}
          label={t('awards.summary.streak')}
          value={streakDays}
          accent="#f97316"
        />
      </div>

      {isLoading ? (
        <div className="awards-loading-grid">
          {Array.from({ length: 6 }).map((_, index) => (
            <div key={index} className="awards-card awards-card-loading">
              <Skeleton width="100%" height={16} style={{ animationDelay: `${index * 0.08}s` }} />
              <Skeleton width="85%" height={12} style={{ animationDelay: `${index * 0.12}s` }} />
              <Skeleton width="100%" height={8} style={{ animationDelay: `${index * 0.16}s` }} />
            </div>
          ))}
        </div>
      ) : (
        <div className="awards-sections">
          <section className="awards-section">
            <SectionHeader
              icon={Award}
              title={t('awards.sections.inventoryTitle')}
              subtitle={t('awards.sections.inventorySubtitle')}
            />
            {localizedInventory.length > 0 ? (
              <div className="awards-grid">
                {localizedInventory.map((item) => (
                  <RewardCard
                    key={item.itemId || item.id}
                    item={item}
                    fallbackName={String(item.itemId || item.id)}
                    t={t}
                  />
                ))}
              </div>
            ) : (
              <EmptyState icon={Package} text={t('awards.inventoryEmpty')} />
            )}
          </section>

          <section className="awards-section">
            <SectionHeader
              icon={CheckCircle2}
              title={t('awards.sections.completedQuestsTitle')}
              subtitle={t('awards.sections.completedQuestsSubtitle')}
            />
            {claimedQuests.length > 0 ? (
              <div className="awards-grid">
                {claimedQuests.map((quest) => (
                  <QuestCard key={quest.id} quest={quest} t={t} />
                ))}
              </div>
            ) : (
              <EmptyState icon={CheckCircle2} text={t('awards.completedEmpty')} />
            )}
          </section>

          <section className="awards-section">
            <SectionHeader
              icon={Target}
              title={t('awards.sections.activeQuestsTitle')}
              subtitle={t('awards.sections.activeQuestsSubtitle')}
            />
            {activeQuests.length > 0 ? (
              <div className="awards-grid">
                {activeQuests.map((quest) => (
                  <QuestCard key={quest.id} quest={quest} t={t} />
                ))}
              </div>
            ) : (
              <EmptyState icon={Target} text={t('awards.activeEmpty')} />
            )}
          </section>

          <section className="awards-section">
            <SectionHeader
              icon={Flame}
              title={t('awards.sections.streakTitle')}
              subtitle={t('awards.sections.streakSubtitle')}
            />
            {streakDays > 0 || freezeCount > 0 ? (
              <div className="awards-streak-card">
                <div className="awards-streak-value">
                  <Flame size={18} />
                  <span>{streakDays}</span>
                </div>
                <div className="awards-streak-copy">
                  <p className="awards-card-title">{t('awards.summary.streak')}</p>
                  <p className="awards-card-description">
                    {t('awards.streakDetails', { days: streakDays, freezes: freezeCount })}
                  </p>
                </div>
              </div>
            ) : (
              <EmptyState icon={Flame} text={t('awards.streakEmpty')} />
            )}
          </section>
        </div>
      )}
    </div>
  );
};

export default Awards;
