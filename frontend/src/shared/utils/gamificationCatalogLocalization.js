const SEEDED_QUEST_KEYS = {
  'b0000001-0000-0000-0000-000000000001': 'taskStarter',
  'b0000001-0000-0000-0000-000000000002': 'commentator',
  'b0000001-0000-0000-0000-000000000003': 'priorityHunter',
  'b0000001-0000-0000-0000-000000000004': 'workhorse',
  'b0000001-0000-0000-0000-000000000005': 'streakMaster',
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

const SEEDED_SKILL_NODE_KEYS = {
  'c0000001-0000-0000-0000-000000000001': 'executionFocus',
  'c0000001-0000-0000-0000-000000000002': 'teamSupport',
  'c0000001-0000-0000-0000-000000000003': 'momentumBuilder',
  'c0000001-0000-0000-0000-000000000004': 'mentorAura',
  'c0000001-0000-0000-0000-000000000005': 'projectVanguard',
  'c0000001-0000-0000-0000-000000000006': 'cultureCatalyst',
};

const getSeededKey = (id, dictionary) => dictionary[String(id || '').toLowerCase()] || null;

const normalizeShopCategory = (category) => String(category || '').trim().toLowerCase();

export const localizeShopCategory = (category, t) => {
  const normalizedCategory = normalizeShopCategory(category);

  if (!normalizedCategory) {
    return category;
  }

  const translationKey = `shop.categories.${normalizedCategory}`;
  const localizedCategory = t(translationKey, { defaultValue: String(category) });

  return localizedCategory || category;
};

export const localizeQuest = (quest, t) => {
  const key = getSeededKey(quest?.id, SEEDED_QUEST_KEYS);
  if (!quest || !key) {
    return quest;
  }

  return {
    ...quest,
    title: t(`seededCatalog.quests.${key}.title`),
    description: t(`seededCatalog.quests.${key}.description`),
  };
};

export const localizeShopItem = (item, t) => {
  const key = getSeededKey(item?.id, SEEDED_SHOP_ITEM_KEYS);
  if (!item) {
    return item;
  }

  const categoryLabel = localizeShopCategory(item.category, t);

  if (!key) {
    return {
      ...item,
      categoryLabel,
    };
  }

  return {
    ...item,
    name: t(`seededCatalog.shopItems.${key}.name`),
    description: t(`seededCatalog.shopItems.${key}.description`),
    categoryLabel,
  };
};

export const localizeSkillTreeNode = (node, t) => {
  const key = getSeededKey(node?.id, SEEDED_SKILL_NODE_KEYS);
  if (!node || !key) {
    return node;
  }

  return {
    ...node,
    title: t(`seededCatalog.skillTree.${key}.title`),
    description: t(`seededCatalog.skillTree.${key}.description`),
  };
};
