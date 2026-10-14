const DEFAULT_PROJECT_LABELS = [
  { key: 'critical', aliases: ['critical', '\u043a\u0440\u0438\u0442\u0438\u0447\u0435\u0441\u043a\u0438\u0439'], translationKey: 'seededCatalog.projectLabels.critical' },
  { key: 'high', aliases: ['high', '\u0432\u044b\u0441\u043e\u043a\u0438\u0439'], translationKey: 'seededCatalog.projectLabels.high' },
  { key: 'medium', aliases: ['medium', '\u0441\u0440\u0435\u0434\u043d\u0438\u0439'], translationKey: 'seededCatalog.projectLabels.medium' },
  { key: 'low', aliases: ['low', '\u043d\u0438\u0437\u043a\u0438\u0439'], translationKey: 'seededCatalog.projectLabels.low' },
  { key: 'feature', aliases: ['feature', '\u0444\u0438\u0447\u0430'], translationKey: 'seededCatalog.projectLabels.feature' },
  { key: 'bug', aliases: ['bug', '\u0431\u0430\u0433'], translationKey: 'seededCatalog.projectLabels.bug' },
  { key: 'improvement', aliases: ['improvement', '\u0443\u043b\u0443\u0447\u0448\u0435\u043d\u0438\u0435'], translationKey: 'seededCatalog.projectLabels.improvement' },
  { key: 'frontend', aliases: ['frontend'], translationKey: 'seededCatalog.projectLabels.frontend' },
  { key: 'backend', aliases: ['backend'], translationKey: 'seededCatalog.projectLabels.backend' },
  { key: 'design', aliases: ['design', '\u0434\u0438\u0437\u0430\u0439\u043d'], translationKey: 'seededCatalog.projectLabels.design' },
  { key: 'sprint1', aliases: ['sprint 1', '\u0441\u043f\u0440\u0438\u043d\u0442 1'], translationKey: 'seededCatalog.projectLabels.sprint1' },
  { key: 'sprint2', aliases: ['sprint 2', '\u0441\u043f\u0440\u0438\u043d\u0442 2'], translationKey: 'seededCatalog.projectLabels.sprint2' },
];

const normalizeProjectLabelName = (value) => String(value || '').trim().toLowerCase();

const getDefaultProjectLabelConfig = (name) => {
  const normalized = normalizeProjectLabelName(name);
  return DEFAULT_PROJECT_LABELS.find((item) => item.aliases.includes(normalized)) || null;
};

export const localizeProjectLabel = (label, t) => {
  if (!label || typeof label !== 'object') {
    return label;
  }

  const config = getDefaultProjectLabelConfig(label.name);
  if (!config) {
    return { ...label, displayName: label.displayName || label.name };
  }

  return {
    ...label,
    defaultKey: config.key,
    displayName: t(config.translationKey),
  };
};

export const getProjectLabelDisplayName = (label, t) => {
  if (!label) {
    return '';
  }

  if (typeof label === 'object') {
    if (label.displayName) {
      return label.displayName;
    }

    const config = getDefaultProjectLabelConfig(label.name);
    return config ? t(config.translationKey) : label.name;
  }

  const config = getDefaultProjectLabelConfig(label);
  return config ? t(config.translationKey) : label;
};
