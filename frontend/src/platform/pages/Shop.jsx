import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Check, Coins, Lock, Package, ShoppingBag, Crown, Flame, Sparkles, TreePine, Award, Shield, Snowflake, Zap } from 'lucide-react';
import { cn } from '../../shared/utils/cn';
import { useGamification } from '../../shared/context/GamificationContext';
import { localizeShopItem } from '../../shared/utils/gamificationCatalogLocalization';
import Skeleton from '../../shared/components/Skeleton';

const ITEM_VISUALS = {
  goldFrame: { icon: Crown, gradient: 'linear-gradient(135deg, #FFD700, #FFA000)', glow: 'rgba(255, 215, 0, 0.3)' },
  neonFrame: { icon: Zap, gradient: 'linear-gradient(135deg, #00E5FF, #7C4DFF)', glow: 'rgba(0, 229, 255, 0.3)' },
  fireFrame: { icon: Flame, gradient: 'linear-gradient(135deg, #FF6B35, #FF1744)', glow: 'rgba(255, 107, 53, 0.3)' },
  forestTheme: { icon: TreePine, gradient: 'linear-gradient(135deg, #66BB6A, #2E7D32)', glow: 'rgba(102, 187, 106, 0.3)' },
  earlyAccessBadge: { icon: Award, gradient: 'linear-gradient(135deg, #FF6B35, #FF8C42)', glow: 'rgba(255, 107, 53, 0.3)' },
  proBadge: { icon: Shield, gradient: 'linear-gradient(135deg, #7C4DFF, #448AFF)', glow: 'rgba(124, 77, 255, 0.3)' },
  seriesFreeze: { icon: Snowflake, gradient: 'linear-gradient(135deg, #4FC3F7, #0288D1)', glow: 'rgba(79, 195, 247, 0.3)' },
  goldCrown: { icon: Crown, gradient: 'linear-gradient(135deg, #FFD700, #FF8F00)', glow: 'rgba(255, 215, 0, 0.35)' },
  silverCrown: { icon: Crown, gradient: 'linear-gradient(135deg, #B0BEC5, #78909C)', glow: 'rgba(176, 190, 197, 0.3)' },
  bronzeCrown: { icon: Crown, gradient: 'linear-gradient(135deg, #D4A373, #8D6E63)', glow: 'rgba(212, 163, 115, 0.3)' },
};

const SEEDED_SHOP_ITEM_KEYS = {
  'a0000001-0000-0000-0000-000000000001': 'goldFrame',
  'a0000001-0000-0000-0000-000000000002': 'neonFrame',
  'a0000001-0000-0000-0000-000000000003': 'fireFrame',
  'a0000001-0000-0000-0000-000000000004': 'forestTheme',
  'a0000001-0000-0000-0000-000000000005': 'earlyAccessBadge',
  'a0000001-0000-0000-0000-000000000006': 'proBadge',
  'a0000001-0000-0000-0000-000000000007': 'seriesFreeze',
  'a0000001-0000-0000-0000-000000000008': 'goldCrown',
  'a0000001-0000-0000-0000-000000000009': 'silverCrown',
  'a0000001-0000-0000-0000-000000000010': 'bronzeCrown',
};

const getItemVisual = (item) => {
  const key = SEEDED_SHOP_ITEM_KEYS[String(item?.id || '').toLowerCase()];
  return key ? ITEM_VISUALS[key] : null;
};

const ItemCard = ({ item, isOwned, canAfford, isPurchasable, onBuy, t }) => {
  const visual = getItemVisual(item);
  const Icon = visual?.icon || Sparkles;
  const isActionDisabled = isOwned || !isPurchasable || !canAfford;

  return (
    <div className="group relative flex flex-col rounded-[20px] overflow-hidden transition-all duration-300 hover:translate-y-[-4px] hover:shadow-2xl"
      style={{
        backgroundColor: 'var(--app-layer-surface)',
        border: '1px solid var(--app-border)',
        boxShadow: '0 2px 12px rgba(0,0,0,0.06)',
      }}
    >
      {/* Preview area */}
      <div
        className="relative h-40 flex items-center justify-center overflow-hidden"
        style={{ background: visual?.gradient || 'linear-gradient(135deg, #64748b, #334155)' }}
      >
        {/* Decorative circles */}
        <div className="absolute -top-8 -right-8 w-32 h-32 rounded-full opacity-20" style={{ background: 'white' }} />
        <div className="absolute -bottom-6 -left-6 w-24 h-24 rounded-full opacity-10" style={{ background: 'white' }} />

        {/* Icon */}
        <div
          className="relative z-10 w-20 h-20 rounded-[18px] flex items-center justify-center transition-transform duration-300 group-hover:scale-110 group-hover:rotate-3"
          style={{
            background: 'rgba(255,255,255,0.2)',
            backdropFilter: 'blur(12px)',
            boxShadow: `0 8px 24px ${visual?.glow || 'rgba(0,0,0,0.2)'}`,
          }}
        >
          <Icon size={36} className="text-white drop-shadow-lg" strokeWidth={1.8} />
        </div>

        {/* Owned badge */}
        {isOwned && (
          <div className="absolute top-3 right-3 flex items-center gap-1.5 bg-green-500 text-white px-2.5 py-1 rounded-full text-xs font-bold shadow-lg">
            <Check size={12} strokeWidth={3} />
            {t('shop.owned')}
          </div>
        )}

        {/* Category pill */}
        <div className="absolute bottom-3 left-3 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider text-white/90"
          style={{ background: 'rgba(0,0,0,0.25)', backdropFilter: 'blur(8px)' }}
        >
          {item.categoryLabel || item.category}
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 flex flex-col p-5">
        <div className="flex justify-between items-start gap-2 mb-3">
          <div className="flex-1 min-w-0">
            <h3 className="font-bold text-[15px] leading-tight" style={{ color: 'var(--app-text-main)' }}>
              {item.name}
            </h3>
            <p className="text-xs mt-1 leading-relaxed" style={{ color: 'var(--app-text-secondary)' }}>
              {item.description || item.categoryLabel || item.category}
            </p>
          </div>
          <div
            className="flex items-center gap-1 font-bold text-sm px-2.5 py-1 rounded-[10px] shrink-0"
            style={{
              color: 'var(--app-accent)',
              backgroundColor: 'color-mix(in srgb, var(--app-accent) 12%, transparent)',
            }}
          >
            <Coins size={14} />
            {item.price}
          </div>
        </div>

        <button
          onClick={() => onBuy(item)}
          disabled={isActionDisabled}
          className={cn(
            'w-full py-3 rounded-[14px] text-sm font-bold transition-all duration-200 flex items-center justify-center gap-2 mt-auto',
            isOwned
              ? 'cursor-default'
              : isPurchasable && canAfford
                ? 'text-white hover:scale-[1.02] active:scale-[0.98]'
                : 'cursor-not-allowed',
          )}
          style={
            isOwned
              ? {
                  backgroundColor: 'color-mix(in srgb, var(--app-accent) 10%, var(--app-layer-base))',
                  color: 'var(--app-accent)',
                  border: '1px solid color-mix(in srgb, var(--app-accent) 20%, var(--app-border))',
                }
              : isPurchasable && canAfford
                ? {
                    background: 'var(--gradient-orange)',
                    boxShadow: '0 4px 16px color-mix(in srgb, var(--primary-orange) 28%, transparent), 0 1px 3px color-mix(in srgb, var(--primary-orange) 12%, transparent)',
                  }
                : {
                    backgroundColor: 'var(--app-layer-base)',
                    color: 'var(--app-text-secondary)',
                    border: '1px solid var(--app-border)',
                  }
          }
        >
          {isOwned ? (
            <>
              <Check size={16} strokeWidth={2.5} />
              {t('shop.owned')}
            </>
          ) : !isPurchasable ? (
            <>
              <Lock size={14} />
              {t('shop.awardOnly')}
            </>
          ) : !canAfford ? (
            <>
              <Lock size={14} />
              {t('shop.insufficientFunds')}
            </>
          ) : (
            <>
              <Sparkles size={16} />
              {t('shop.buy')}
            </>
          )}
        </button>
      </div>
    </div>
  );
};

const Shop = () => {
  const { t } = useTranslation('platform');
  const { coins, inventory, purchaseItem, api, accessToken } = useGamification();
  const [activeCategory, setActiveCategory] = useState(null);
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  const categories = useMemo(
    () => [
      { id: null, label: t('shop.categories.all'), icon: '✦' },
      { id: 'frames', label: t('shop.categories.frames'), icon: '◎' },
      { id: 'themes', label: t('shop.categories.themes'), icon: '◆' },
      { id: 'badges', label: t('shop.categories.badges'), icon: '★' },
      { id: 'consumables', label: t('shop.categories.consumables'), icon: '✺' },
    ],
    [t],
  );

  useEffect(() => {
    if (!accessToken) {
      setItems([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    api
      .getShopItems(accessToken, activeCategory)
      .then((data) => setItems(data || []))
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  }, [accessToken, activeCategory, api]);

  const ownedIds = new Set((inventory || []).map((item) => item.itemId || item.id));

  const handleBuy = async (item) => {
    if (item?.isPurchasable === false) {
      return;
    }

    const purchased = await purchaseItem(item.id);

    if (purchased) {
      api
        .getShopItems(accessToken, activeCategory)
        .then((data) => setItems(data || []))
        .catch(() => {});
    }
  };

  return (
    <main className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8 scrollbar-hide">
      <div className="w-full max-w-6xl mx-auto">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 flex-wrap mb-10">
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-3" style={{ color: 'var(--app-text-main)' }}>
              <div
                className="w-10 h-10 rounded-[14px] flex items-center justify-center"
                style={{
                  background: 'linear-gradient(135deg, var(--primary-orange, #FF6B35), var(--secondary-orange, #FF8C42))',
                  boxShadow: '0 4px 12px rgba(255, 107, 53, 0.24)',
                }}
              >
                <ShoppingBag size={20} className="text-white" />
              </div>
              {t('shop.title')}
            </h1>
            <p className="text-sm mt-2 ml-[52px]" style={{ color: 'var(--app-text-secondary)' }}>
              {t('shop.subtitle')}
            </p>
          </div>

          {/* Balance card */}
          <div
            className="flex items-center gap-4 px-6 py-4 rounded-[18px] transition-all duration-200 hover:translate-y-[-2px]"
            style={{
              backgroundColor: 'var(--app-layer-surface)',
              border: '1px solid var(--app-border)',
              boxShadow: 'var(--shadow-md, 0 4px 16px rgba(0,0,0,0.06))',
            }}
          >
            <div
              className="w-12 h-12 rounded-full flex items-center justify-center text-white"
              style={{
                background: 'var(--gradient-gold)',
                boxShadow: '0 4px 12px rgba(255, 215, 0, 0.3)',
              }}
            >
              <Coins size={22} />
            </div>
            <div>
              <div className="text-[10px] uppercase font-bold tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>
                {t('shop.balance')}
              </div>
              <div className="text-2xl font-bold tabular-nums" style={{ color: 'var(--app-text-main)' }}>
                {coins}
              </div>
            </div>
          </div>
        </div>

        {/* Categories */}
        <div className="flex gap-2 mb-8 overflow-x-auto pb-2 scrollbar-hide">
          {categories.map((category) => (
            <button
              key={category.id ?? 'all'}
              onClick={() => {
                setActiveCategory(category.id);
                setLoading(true);
              }}
              className="px-5 py-2.5 rounded-[14px] text-sm font-bold transition-all duration-200 whitespace-nowrap flex items-center gap-2"
              style={{
                backgroundColor: activeCategory === category.id ? 'var(--app-accent)' : 'var(--app-layer-base)',
                color: activeCategory === category.id ? 'white' : 'var(--app-text-secondary)',
                border: `1px solid ${activeCategory === category.id ? 'var(--app-accent)' : 'var(--app-border)'}`,
                boxShadow: activeCategory === category.id
                  ? '0 4px 12px rgba(255, 107, 53, 0.24)'
                  : '0 1px 3px rgba(0,0,0,0.04)',
                transform: activeCategory === category.id ? 'translateY(-1px)' : 'translateY(0)',
              }}
            >
              <span className="text-xs">{category.icon}</span>
              {category.label}
            </button>
          ))}
        </div>

        {/* Content */}
        {loading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="rounded-[20px] overflow-hidden"
                style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)' }}
              >
                <Skeleton width="100%" height={160} style={{ borderRadius: 0, animationDelay: `${i * 0.1}s` }} />
                <div className="p-5 space-y-3">
                  <Skeleton width="70%" height={16} style={{ animationDelay: `${i * 0.1}s` }} />
                  <Skeleton width="50%" height={12} style={{ animationDelay: `${i * 0.15}s` }} />
                  <Skeleton width="100%" height={44} style={{ borderRadius: 14, animationDelay: `${i * 0.2}s` }} />
                </div>
              </div>
            ))}
          </div>
        ) : items.length === 0 ? (
          <div className="text-center py-20 flex flex-col items-center gap-3">
            <div
              className="w-16 h-16 rounded-[18px] flex items-center justify-center"
              style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}
            >
              <Package size={28} style={{ color: 'var(--app-text-secondary)' }} />
            </div>
            <p className="text-sm font-medium" style={{ color: 'var(--app-text-secondary)' }}>{t('shop.empty')}</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {items.map((item) => {
              const localizedItem = localizeShopItem(item, t);
              const isOwned = ownedIds.has(localizedItem.id);
              const canAfford = coins >= localizedItem.price;

              return (
                <ItemCard
                  key={localizedItem.id}
                  item={localizedItem}
                  isOwned={isOwned}
                  canAfford={canAfford}
                  isPurchasable={localizedItem.isPurchasable !== false}
                  onBuy={handleBuy}
                  t={t}
                />
              );
            })}
          </div>
        )}
      </div>
    </main>
  );
};

export default Shop;
