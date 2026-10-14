import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useAuth } from './AuthContext';
import * as gApi from '../utils/gamificationApi';
import useGamificationSignalR from '../hooks/useGamificationSignalR';
import RewardToast from '../../platform/components/RewardToast';

const GamificationContext = createContext(null);

export const useGamification = () => {
  const ctx = useContext(GamificationContext);
  if (!ctx) throw new Error('useGamification must be used within GamificationProvider');
  return ctx;
};

const initialToast = { show: false, xp: 0, coins: 0 };

const normalizeReward = (payload) => ({
  xp: payload?.xpAwarded ?? payload?.xp ?? 0,
  coins: payload?.coinsAwarded ?? payload?.coins ?? 0,
  reason: payload?.reason ?? '',
});

export function GamificationProvider({ children }) {
  const { accessToken } = useAuth();
  const [globalXp, setGlobalXp] = useState(null);
  const [quests, setQuests] = useState([]);
  const [streak, setStreak] = useState(null);
  const [inventory, setInventory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [rewardToast, setRewardToast] = useState(initialToast);

  const markDegraded = useCallback((errorCode, error) => {
    console.error('[Gamification]', errorCode, error);
  }, []);

  const clearDegraded = useCallback(() => {}, []);

  const resetState = useCallback(() => {
    setGlobalXp(null);
    setQuests([]);
    setStreak(null);
    setInventory([]);
  }, []);

  const refreshXp = useCallback(async () => {
    if (!accessToken) return null;
    const data = await gApi.getGlobalXp(accessToken);
    setGlobalXp(data);
    clearDegraded();
    return data;
  }, [accessToken, clearDegraded]);

  const refreshQuests = useCallback(async () => {
    if (!accessToken) return [];
    const data = await gApi.getQuests(accessToken);
    setQuests(data || []);
    clearDegraded();
    return data || [];
  }, [accessToken, clearDegraded]);

  const refreshStreak = useCallback(async () => {
    if (!accessToken) return null;
    const data = await gApi.getStreak(accessToken);
    setStreak(data);
    clearDegraded();
    return data;
  }, [accessToken, clearDegraded]);

  const refreshInventory = useCallback(async () => {
    if (!accessToken) return [];
    const data = await gApi.getInventory(accessToken);
    setInventory(data || []);
    clearDegraded();
    return data || [];
  }, [accessToken, clearDegraded]);

  const fetchAll = useCallback(async () => {
    if (!accessToken) {
      resetState();
      clearDegraded();
      setLoading(false);
      return;
    }

    setLoading(true);

    const [xpData, questData, streakData, invData] = await Promise.allSettled([
      gApi.getGlobalXp(accessToken),
      gApi.getQuests(accessToken),
      gApi.getStreak(accessToken),
      gApi.getInventory(accessToken),
    ]);

    if (xpData.status === 'fulfilled') setGlobalXp(xpData.value);
    if (questData.status === 'fulfilled') setQuests(questData.value || []);
    if (streakData.status === 'fulfilled') setStreak(streakData.value);
    if (invData.status === 'fulfilled') setInventory(invData.value || []);

    const firstError = [xpData, questData, streakData, invData].find((result) => result.status === 'rejected');
    if (firstError) {
      markDegraded('gamification-bootstrap-partial', firstError.reason);
    } else {
      clearDegraded();
    }

    setLoading(false);
  }, [accessToken, clearDegraded, markDegraded, resetState]);

  useEffect(() => {
    void fetchAll();
  }, [fetchAll]);

  const handleReward = useCallback((payload) => {
    const reward = normalizeReward(payload);
    setRewardToast({ show: true, xp: reward.xp, coins: reward.coins });

    if (accessToken) {
      Promise.allSettled([refreshXp(), refreshQuests(), refreshStreak()])
        .then((results) => {
          const firstError = results.find((result) => result.status === 'rejected');
          if (firstError) {
            markDegraded('gamification-reward-refresh-failed', firstError.reason);
          } else {
            clearDegraded();
          }
        })
        .catch((error) => markDegraded('gamification-reward-refresh-failed', error));
    }
  }, [accessToken, clearDegraded, markDegraded, refreshQuests, refreshStreak, refreshXp]);

  useGamificationSignalR(accessToken, handleReward);

  const claimQuest = useCallback(async (questId) => {
    if (!accessToken) return null;

    try {
      const result = await gApi.claimQuest(accessToken, questId);
      const reward = normalizeReward(result);
      await Promise.allSettled([refreshXp(), refreshQuests()]);
      setRewardToast({ show: true, xp: reward.xp, coins: reward.coins });
      clearDegraded();
      return result;
    } catch (error) {
      markDegraded('gamification-claim-quest-failed', error);
      return null;
    }
  }, [accessToken, clearDegraded, markDegraded, refreshQuests, refreshXp]);

  const purchaseItem = useCallback(async (itemId) => {
    if (!accessToken) return false;

    try {
      await gApi.purchaseItem(accessToken, itemId);
      await Promise.allSettled([refreshXp(), refreshInventory()]);
      clearDegraded();
      return true;
    } catch (error) {
      markDegraded('gamification-purchase-failed', error);
      return false;
    }
  }, [accessToken, clearDegraded, markDegraded, refreshInventory, refreshXp]);

  const equipItem = useCallback(async (itemId) => {
    if (!accessToken) return false;

    try {
      await gApi.equipItem(accessToken, itemId);
      await refreshInventory();
      clearDegraded();
      return true;
    } catch (error) {
      markDegraded('gamification-equip-failed', error);
      return false;
    }
  }, [accessToken, clearDegraded, markDegraded, refreshInventory]);

  const unequipItem = useCallback(async (itemId) => {
    if (!accessToken) return false;

    try {
      await gApi.unequipItem(accessToken, itemId);
      await refreshInventory();
      clearDegraded();
      return true;
    } catch (error) {
      markDegraded('gamification-unequip-failed', error);
      return false;
    }
  }, [accessToken, clearDegraded, markDegraded, refreshInventory]);

  const useFreeze = useCallback(async () => {
    if (!accessToken) return false;

    try {
      await gApi.useStreakFreeze(accessToken);
      await Promise.allSettled([refreshStreak(), refreshXp(), refreshInventory()]);
      clearDegraded();
      return true;
    } catch (error) {
      markDegraded('gamification-freeze-failed', error);
      return false;
    }
  }, [accessToken, clearDegraded, markDegraded, refreshInventory, refreshStreak, refreshXp]);

  const value = useMemo(() => ({
    loading,
    xp: globalXp?.totalXp ?? 0,
    level: globalXp?.level ?? 1,
    coins: globalXp?.coins ?? 0,
    globalXp,
    quests,
    claimQuest,
    refreshQuests,
    streak,
    useFreeze,
    inventory,
    purchaseItem,
    equipItem,
    unequipItem,
    refreshXp,
    api: gApi,
    accessToken,
  }), [
    accessToken,
    claimQuest,
    equipItem,
    globalXp,
    inventory,
    loading,
    purchaseItem,
    quests,
    refreshQuests,
    refreshXp,
    streak,
    unequipItem,
    useFreeze,
  ]);

  return (
    <GamificationContext.Provider value={value}>
      {children}
      <RewardToast
        show={rewardToast.show}
        xp={rewardToast.xp}
        coins={rewardToast.coins}
        onClose={() => setRewardToast(initialToast)}
      />
    </GamificationContext.Provider>
  );
}

