export const STATUS_KEYS = ['backlog', 'todo', 'inProgress', 'review', 'done'];

const STATUS_ALIASES = {
  backlog: ['бэклог', 'backlog'],
  todo: ['сделать', 'к выполнению', 'to do', 'todo', 'орындауға', 'жасау'],
  inProgress: ['в работе', 'в процессе', 'in progress', 'жұмыста', 'процесте'],
  review: ['ревью', 'review', 'тексеру'],
  done: ['готово', 'done', 'дайын'],
};

const STATUS_RAW_DEFAULTS = {
  backlog: 'Бэклог',
  todo: 'К выполнению',
  inProgress: 'В процессе',
  review: 'Ревью',
  done: 'Готово',
};

const STATUS_COLOR_MAP = {
  backlog: '#64748B',
  todo: '#3B82F6',
  inProgress: '#F59E0B',
  review: '#8B5CF6',
  done: '#22C55E',
};

const normalizeText = (value) => String(value || '').trim().toLowerCase();

export const getKnownStatusKey = (value) => {
  const normalized = normalizeText(value);
  return STATUS_KEYS.find((key) => STATUS_ALIASES[key].includes(normalized)) || null;
};

export const normalizeStatusKey = (value) => getKnownStatusKey(value) || 'backlog';

export const getStatusColor = (value) => {
  const statusKey = getKnownStatusKey(value);
  return STATUS_COLOR_MAP[statusKey || 'backlog'] || STATUS_COLOR_MAP.backlog;
};

export const isDoneStatus = (value) => normalizeStatusKey(value) === 'done';

export const isInProgressStatus = (value) => normalizeStatusKey(value) === 'inProgress';

export const getDefaultRawStatus = (statusKey) => STATUS_RAW_DEFAULTS[statusKey] || STATUS_RAW_DEFAULTS.todo;

export const getStatusDisplayName = (t, value) => {
  const statusKey = getKnownStatusKey(value);
  if (!statusKey) {
    return String(value || '').trim() || STATUS_RAW_DEFAULTS.todo;
  }

  return t(`analytics.statuses.${statusKey}`);
};
