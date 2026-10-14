import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  ArrowRightLeft,
  Calendar,
  ChevronRight,
  ExternalLink,
  FileText,
  Loader2,
  Plus,
  RotateCcw,
  Save,
  Search,
  Tag,
  Trash2,
  Users,
  X,
  Zap,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../shared/context/AuthContext';
import {
  getSkillTree,
  getSkillTreeConfig,
  resetSkillTreeConfig,
  updateSkillTreeConfig,
} from '../../shared/utils/gamificationApi';
import { usePlatform } from '../context/PlatformContext';
import BackgroundPicker from './BackgroundPicker';

const Toggle = ({ checked, onChange, disabled = false }) => (
  <button type="button" disabled={disabled} onClick={() => onChange(!checked)} className="relative h-[22px] w-10 rounded-full disabled:cursor-default disabled:opacity-60" style={{ backgroundColor: checked ? '#3B82F6' : 'var(--app-layer-base)' }}>
    <div className="absolute top-0.5 h-[18px] w-[18px] rounded-full bg-white shadow-sm transition-transform" style={{ transform: checked ? 'translateX(20px)' : 'translateX(2px)' }} />
  </button>
);

const Checkbox = ({ checked, onChange, label, description }) => (
  <label className="flex items-start gap-3 py-1">
    <button type="button" onClick={onChange} className="mt-0.5 flex h-[18px] w-[18px] items-center justify-center rounded" style={{ backgroundColor: checked ? '#3B82F6' : 'transparent', border: checked ? 'none' : '2px solid var(--app-border)' }}>
      {checked ? <svg width="12" height="12" viewBox="0 0 12 12" fill="none"><path d="M2.5 6L5 8.5L9.5 3.5" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg> : null}
    </button>
    <div>
      <div className="text-sm" style={{ color: 'var(--app-text-main)' }}>{label}</div>
      {description ? <div className="mt-0.5 text-xs" style={{ color: 'var(--app-text-secondary)' }}>{description}</div> : null}
    </div>
  </label>
);

const EmptyCard = ({ icon: Icon, description, emptyText, createText }) => (
  <div className="space-y-4">
    <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>{description}</p>
    <div className="flex flex-col items-center gap-3 rounded-xl p-8" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px dashed var(--app-border)' }}>
      <Icon size={32} style={{ color: 'var(--app-text-secondary)' }} />
      <p className="text-center text-sm" style={{ color: 'var(--app-text-secondary)' }}>{emptyText}</p>
      <button type="button" className="mt-2 rounded-lg px-4 py-2 text-sm font-medium" style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}>{createText}</button>
    </div>
  </div>
);

const CreateAutomationForm = ({ onSubmit, onCancel, boardStickers, columns }) => {
  const { t } = useTranslation('platform');
  const triggerTypes = useMemo(() => [
    { value: 'task-moved', label: t('boardSettings.triggers.taskMoved') },
    { value: 'sticker-changed', label: t('boardSettings.triggers.stickerChanged') },
    { value: 'task-created', label: t('boardSettings.triggers.taskCreated') },
    { value: 'deadline-approaching', label: t('boardSettings.triggers.deadlineApproaching') },
  ], [t]);
  const actionTypes = useMemo(() => [
    { value: 'set-sticker', label: t('boardSettings.actions.setSticker') },
    { value: 'move-task', label: t('boardSettings.actions.moveTask') },
    { value: 'assign-user', label: t('boardSettings.actions.assignUser') },
    { value: 'set-priority', label: t('boardSettings.actions.setPriority') },
  ], [t]);
  const [form, setForm] = useState({ name: '', triggerType: 'task-moved', triggerValue: '', actionType: 'set-sticker', actionSticker: '', actionValue: '' });
  const columnList = columns ? Object.values(columns) : [];
  const selectedSticker = boardStickers.find((item) => item.id === form.actionSticker);
  const setField = (key, value) => setForm((prev) => ({ ...prev, [key]: value }));

  return (
    <div className="space-y-4 rounded-xl p-5" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-accent)' }}>
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-bold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.createAutomation.title')}</h4>
        <button type="button" onClick={onCancel} className="rounded p-1" style={{ color: 'var(--app-text-secondary)' }}><X size={14} /></button>
      </div>
      <input autoFocus value={form.name} onChange={(event) => setField('name', event.target.value)} placeholder={t('boardSettings.createAutomation.namePlaceholder')} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }} />
      <div className="space-y-2">
        <label className="flex items-center gap-1.5 text-[11px] font-semibold" style={{ color: 'var(--app-text-secondary)' }}><Zap size={11} />{t('boardSettings.createAutomation.when')}</label>
        <select value={form.triggerType} onChange={(event) => setField('triggerType', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}>
          {triggerTypes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
        {form.triggerType === 'task-moved' ? <select value={form.triggerValue} onChange={(event) => setField('triggerValue', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option value="">{t('boardSettings.createAutomation.selectColumn')}</option>{columnList.map((column) => <option key={column.id} value={column.id}>{column.title}</option>)}</select> : null}
        {form.triggerType === 'sticker-changed' ? <select value={form.triggerValue} onChange={(event) => setField('triggerValue', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option value="">{t('boardSettings.createAutomation.selectSticker')}</option>{boardStickers.map((sticker) => <option key={sticker.id} value={sticker.id}>{sticker.label}</option>)}</select> : null}
      </div>
      <div className="space-y-2">
        <label className="flex items-center gap-1.5 text-[11px] font-semibold" style={{ color: 'var(--app-text-secondary)' }}><ChevronRight size={11} />{t('boardSettings.createAutomation.then')}</label>
        <select value={form.actionType} onChange={(event) => setField('actionType', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}>
          {actionTypes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
        </select>
        {form.actionType === 'set-sticker' ? <><select value={form.actionSticker} onChange={(event) => setField('actionSticker', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option value="">{t('boardSettings.createAutomation.selectSticker')}</option>{boardStickers.map((sticker) => <option key={sticker.id} value={sticker.id}>{sticker.label}</option>)}</select>{selectedSticker?.type === 'state-set' && Array.isArray(selectedSticker.values) && selectedSticker.values.length > 0 ? <select value={form.actionValue} onChange={(event) => setField('actionValue', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option value="">{t('boardSettings.createAutomation.selectValue')}</option>{selectedSticker.values.map((item) => <option key={item.id} value={item.id}>{item.label}</option>)}</select> : selectedSticker ? <input value={form.actionValue} onChange={(event) => setField('actionValue', event.target.value)} placeholder={t('boardSettings.createAutomation.valuePlaceholder')} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }} /> : null}</> : null}
        {form.actionType === 'move-task' ? <select value={form.actionValue} onChange={(event) => setField('actionValue', event.target.value)} className="w-full rounded-lg px-3 py-2 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option value="">{t('boardSettings.createAutomation.selectColumn')}</option>{columnList.map((column) => <option key={column.id} value={column.id}>{column.title}</option>)}</select> : null}
      </div>
      <div className="flex justify-end gap-2">
        <button type="button" onClick={onCancel} className="rounded-lg px-4 py-2 text-sm font-medium" style={{ backgroundColor: 'var(--app-layer-surface)', color: 'var(--app-text-secondary)', border: '1px solid var(--app-border)' }}>{t('boardSettings.common.cancel')}</button>
        <button type="button" onClick={() => form.name.trim() && onSubmit({ name: form.name.trim(), trigger: { type: form.triggerType, value: form.triggerValue }, action: { type: form.actionType, stickerId: form.actionSticker, value: form.actionValue } })} className="rounded-lg px-4 py-2 text-sm font-medium disabled:opacity-40" disabled={!form.name.trim()} style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}>{t('boardSettings.createAutomation.create')}</button>
      </div>
    </div>
  );
};

const AutomationCard = ({ automation, boardStickers, columns, onToggle, onRemove }) => {
  const { t } = useTranslation('platform');
  const triggerLabels = { 'task-moved': t('boardSettings.triggers.taskMoved'), 'sticker-changed': t('boardSettings.triggers.stickerChanged'), 'task-created': t('boardSettings.triggers.taskCreated'), 'deadline-approaching': t('boardSettings.triggers.deadlineApproaching') };
  const actionLabels = { 'set-sticker': t('boardSettings.actions.setSticker'), 'move-task': t('boardSettings.actions.moveTask'), 'assign-user': t('boardSettings.actions.assignUser'), 'set-priority': t('boardSettings.actions.setPriority') };
  const triggerText = (() => {
    let value = triggerLabels[automation.trigger?.type] || automation.trigger?.type || '';
    if (automation.trigger?.type === 'task-moved' && columns?.[automation.trigger.value]) value += `: ${columns[automation.trigger.value].title}`;
    if (automation.trigger?.type === 'sticker-changed') {
      const sticker = boardStickers.find((item) => item.id === automation.trigger?.value);
      if (sticker) value += `: ${sticker.label}`;
    }
    return value;
  })();
  const actionText = (() => {
    let value = actionLabels[automation.action?.type] || automation.action?.type || '';
    if (automation.action?.stickerId) {
      const sticker = boardStickers.find((item) => item.id === automation.action.stickerId);
      if (sticker) value += ` → ${sticker.label}`;
    }
    if (automation.action?.type === 'move-task' && columns?.[automation.action.value]) value += ` → ${columns[automation.action.value].title}`;
    return value;
  })();
  return (
    <div className="space-y-3 rounded-xl p-4" style={{ backgroundColor: 'var(--app-layer-base)', border: `1px solid ${automation.enabled ? 'color-mix(in srgb, var(--app-accent) 30%, var(--app-border))' : 'var(--app-border)'}`, opacity: automation.enabled ? 1 : 0.6 }}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 flex-1 items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg" style={{ backgroundColor: automation.enabled ? 'color-mix(in srgb, #F59E0B 15%, transparent)' : 'var(--app-layer-surface)' }}><Zap size={16} style={{ color: automation.enabled ? '#F59E0B' : 'var(--app-text-secondary)' }} /></div>
          <div className="truncate text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{automation.name}</div>
        </div>
        <Toggle checked={automation.enabled} onChange={() => onToggle(automation.id)} />
      </div>
      <div className="space-y-1.5 pl-[42px]">
        <div className="flex items-center gap-2"><span className="rounded px-1.5 py-0.5 text-[10px] font-bold uppercase" style={{ backgroundColor: 'color-mix(in srgb, #22C55E 15%, transparent)', color: '#22C55E' }}>{t('boardSettings.automationCard.when')}</span><span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>{triggerText}</span></div>
        <div className="flex items-center gap-2"><span className="rounded px-1.5 py-0.5 text-[10px] font-bold uppercase" style={{ backgroundColor: 'color-mix(in srgb, #3B82F6 15%, transparent)', color: '#3B82F6' }}>{t('boardSettings.automationCard.then')}</span><span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>{actionText}</span></div>
      </div>
      <div className="flex justify-end"><button type="button" onClick={() => onRemove(automation.id)} className="flex items-center gap-1 rounded-lg px-2.5 py-1 text-[11px] font-medium hover:bg-red-500/10 hover:text-red-400" style={{ color: 'var(--app-text-secondary)' }}><Trash2 size={11} />{t('boardSettings.common.delete')}</button></div>
    </div>
  );
};

const GeneralTab = () => {
  const { t } = useTranslation('platform');
  const [settings, setSettings] = useState({
    moveOnComplete: false,
    completeOnMove: false,
    coverFromChat: true,
    archiveCompleted: false,
    showTimeInColumns: false,
    showTaskCount: true,
    showMessageCount: true,
    showIfNoMessages: false,
    showTaskId: false,
  });
  const toggle = (key) => setSettings((prev) => ({ ...prev, [key]: !prev[key] }));

  return (
    <div className="space-y-6">
      <div>
        <h3 className="mb-4 text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.general.automationTitle')}</h3>
        <div className="space-y-3">
          <Checkbox checked={settings.moveOnComplete} onChange={() => toggle('moveOnComplete')} label={t('boardSettings.general.moveOnCompleteLabel')} description={t('boardSettings.general.moveOnCompleteDescription')} />
          <Checkbox checked={settings.completeOnMove} onChange={() => toggle('completeOnMove')} label={t('boardSettings.general.completeOnMoveLabel')} description={t('boardSettings.general.completeOnMoveDescription')} />
          <Checkbox checked={settings.coverFromChat} onChange={() => toggle('coverFromChat')} label={t('boardSettings.general.coverFromChatLabel')} description={t('boardSettings.general.coverFromChatDescription')} />
          {settings.coverFromChat ? <div className="ml-8"><select className="w-48 rounded-lg px-3 py-2 text-sm" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}><option>{t('boardSettings.general.coverFromChatAll')}</option></select></div> : null}
          <Checkbox checked={settings.archiveCompleted} onChange={() => toggle('archiveCompleted')} label={t('boardSettings.general.archiveCompletedLabel')} description={t('boardSettings.general.archiveCompletedDescription')} />
        </div>
      </div>
      <div>
        <h3 className="mb-4 text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.general.displayTitle')}</h3>
        <div className="space-y-3">
          <Checkbox checked={settings.showTimeInColumns} onChange={() => toggle('showTimeInColumns')} label={t('boardSettings.general.showTimeInColumnsLabel')} description={t('boardSettings.general.showTimeInColumnsDescription')} />
          <Checkbox checked={settings.showTaskCount} onChange={() => toggle('showTaskCount')} label={t('boardSettings.general.showTaskCountLabel')} />
          <Checkbox checked={settings.showMessageCount} onChange={() => toggle('showMessageCount')} label={t('boardSettings.general.showMessageCountLabel')} />
          {settings.showMessageCount ? <div className="ml-8"><Checkbox checked={settings.showIfNoMessages} onChange={() => toggle('showIfNoMessages')} label={t('boardSettings.general.showIfNoMessagesLabel')} /></div> : null}
          <Checkbox checked={settings.showTaskId} onChange={() => toggle('showTaskId')} label={t('boardSettings.general.showTaskIdLabel')} />
        </div>
      </div>
    </div>
  );
};

const AppearanceTab = () => <BackgroundPicker showForAllCheckbox />;

const StickersTab = () => {
  const { t } = useTranslation('platform');
  const { boardStickers, boardStickerIds, stickerCatalog, addBoardSticker, removeBoardSticker } = usePlatform();
  const [searchQuery, setSearchQuery] = useState('');
  const matches = (value) => value.toLowerCase().includes(searchQuery.toLowerCase());
  const availableStickers = stickerCatalog.filter((item) => !boardStickerIds.includes(item.id) && matches(item.label));
  const activeStickers = boardStickers.filter((item) => matches(item.label));

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.stickers.title')}</h3>
        <p className="mt-0.5 text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.stickers.onBoardCount', { active: boardStickers.length, total: stickerCatalog.length })}</p>
      </div>
      <div className="relative">
        <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: 'var(--app-text-secondary)' }} />
        <input type="text" value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} placeholder={t('boardSettings.stickers.searchPlaceholder')} className="w-full rounded-lg py-2 pl-9 pr-4 text-sm outline-none" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }} />
      </div>
      {activeStickers.length > 0 ? <div><h4 className="mb-2 text-[11px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.stickers.onBoard')}</h4><div className="grid grid-cols-2 gap-3">{activeStickers.map((sticker) => <div key={sticker.id} className="space-y-3 rounded-xl p-4" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid color-mix(in srgb, var(--app-accent) 30%, var(--app-border))' }}><div className="flex items-center gap-2.5"><div className="flex h-8 w-8 items-center justify-center rounded-lg" style={{ backgroundColor: 'color-mix(in srgb, var(--app-accent) 15%, transparent)' }}><Tag size={16} style={{ color: 'var(--app-accent)' }} /></div><div><div className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{sticker.label}</div><div className="text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>{sticker.subtitle}</div></div></div>{sticker.removable ? <button type="button" onClick={() => removeBoardSticker(sticker.id)} className="flex items-center gap-1 rounded-lg px-2.5 py-1 text-[11px] font-medium" style={{ backgroundColor: 'var(--app-layer-surface)', color: 'var(--app-text-secondary)', border: '1px solid var(--app-border)' }}><Trash2 size={11} />{t('boardSettings.stickers.removeFromBoard')}</button> : null}</div>)}</div></div> : null}
      {availableStickers.length > 0 ? <div><h4 className="mb-2 text-[11px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.stickers.catalog')}</h4><div className="grid grid-cols-2 gap-3">{availableStickers.map((sticker) => <button key={sticker.id} type="button" onClick={() => addBoardSticker(sticker.id)} className="flex items-center gap-2.5 rounded-xl p-4 text-left" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px dashed var(--app-border)' }}><div className="flex h-8 w-8 items-center justify-center rounded-lg" style={{ backgroundColor: 'var(--app-layer-surface)' }}><Tag size={16} style={{ color: 'var(--app-text-secondary)' }} /></div><div className="min-w-0 flex-1"><div className="truncate text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>{sticker.label}</div><div className="text-[10px]" style={{ color: 'var(--app-text-secondary)' }}>{sticker.subtitle}</div></div><Plus size={14} style={{ color: 'var(--app-accent)' }} /></button>)}</div></div> : null}
      {activeStickers.length === 0 && availableStickers.length === 0 && searchQuery ? <div className="py-8 text-center"><p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.stickers.notFound')}</p></div> : null}
    </div>
  );
};

const AutomationsTab = () => {
  const { t } = useTranslation('platform');
  const { boardAutomations, boardStickers, activeProject, addBoardAutomation, removeBoardAutomation, toggleBoardAutomation } = usePlatform();
  const [showCreateForm, setShowCreateForm] = useState(false);
  const columns = activeProject?.boardData?.columns || {};
  const enabledCount = boardAutomations.filter((item) => item.enabled).length;
  const presets = [
    { name: t('boardSettings.automations.presetCompleteLow'), trigger: { type: 'task-moved', value: 'col-3' }, action: { type: 'set-sticker', stickerId: 'priority', value: 'low' } },
    { name: t('boardSettings.automations.presetNewMedium'), trigger: { type: 'task-created', value: '' }, action: { type: 'set-sticker', stickerId: 'priority', value: 'medium' } },
  ];
  const handleCreate = (data) => { addBoardAutomation(data); setShowCreateForm(false); };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div><h3 className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.automations.title')}</h3><p className="mt-0.5 text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>{boardAutomations.length ? t('boardSettings.automations.activeCount', { enabled: enabledCount, total: boardAutomations.length }) : t('boardSettings.automations.empty')}</p></div>
        <button type="button" onClick={() => setShowCreateForm(true)} className="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold" style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}><Plus size={13} />{t('boardSettings.automations.create')}</button>
      </div>
      {boardAutomations.length === 0 && !showCreateForm ? <><div className="flex flex-col items-center gap-3 rounded-xl p-6" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px dashed var(--app-border)' }}><div className="flex h-12 w-12 items-center justify-center rounded-xl" style={{ backgroundColor: 'color-mix(in srgb, #F59E0B 15%, transparent)' }}><Zap size={24} style={{ color: '#F59E0B' }} /></div><p className="text-center text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.automations.routineTitle')}</p><p className="max-w-xs text-center text-xs" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.automations.routineDescription')}</p></div><div><h4 className="mb-2 text-[11px] font-semibold uppercase tracking-wide" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.automations.popularTemplates')}</h4><div className="space-y-2">{presets.map((preset, index) => <button key={`${preset.name}-${index}`} type="button" onClick={() => addBoardAutomation(preset)} className="flex w-full items-center gap-3 rounded-lg p-3 text-left" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}><Zap size={14} style={{ color: '#F59E0B' }} /><span className="text-sm" style={{ color: 'var(--app-text-main)' }}>{preset.name}</span><Plus size={14} className="ml-auto" style={{ color: 'var(--app-text-secondary)' }} /></button>)}</div></div></> : null}
      {showCreateForm ? <CreateAutomationForm onSubmit={handleCreate} onCancel={() => setShowCreateForm(false)} boardStickers={boardStickers} columns={columns} /> : null}
      {boardAutomations.length ? <div className="space-y-3">{boardAutomations.map((automation) => <AutomationCard key={automation.id} automation={automation} boardStickers={boardStickers} columns={columns} onToggle={toggleBoardAutomation} onRemove={removeBoardAutomation} />)}</div> : null}
    </div>
  );
};

const SKILL_TREE_SLOTS = [
  { tier: 1, branch: 'A' },
  { tier: 1, branch: 'B' },
  { tier: 2, branch: 'A' },
  { tier: 2, branch: 'B' },
  { tier: 3, branch: 'A' },
  { tier: 3, branch: 'B' },
];

const normalizeRewardJson = (value) => {
  if (!value) {
    return '{}';
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
};

const createSkillTreeNodeDraft = (slot, node = null) => ({
  id: node?.id || null,
  projectId: node?.projectId || null,
  tier: slot.tier,
  branch: slot.branch,
  title: node?.title || '',
  description: node?.description || '',
  rewardJson: normalizeRewardJson(node?.rewardJson || '{}'),
  isActive: node?.isActive ?? true,
});

const buildSkillTreeDraft = (nodes) => SKILL_TREE_SLOTS.map((slot) => {
  const matchedNode = (nodes || []).find((node) =>
    Number(node?.tier) === slot.tier &&
    String(node?.branch || '').toUpperCase() === slot.branch);

  return createSkillTreeNodeDraft(slot, matchedNode);
});

export const SkillTreeTab = () => {
  const { t } = useTranslation('platform');
  const { accessToken } = useAuth();
  const { activeProjectId, currentUserRole } = usePlatform();
  const [nodes, setNodes] = useState(() => buildSkillTreeDraft([]));
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [readOnly, setReadOnly] = useState(false);
  const [feedback, setFeedback] = useState({ type: null, message: '' });
  const canEdit = currentUserRole === 'Owner' || currentUserRole === 'Admin';

  const loadConfig = useCallback(async () => {
    if (!accessToken || !activeProjectId) {
      setNodes(buildSkillTreeDraft([]));
      setReadOnly(false);
      return;
    }

    setLoading(true);
    setReadOnly(!canEdit);
    setFeedback({ type: null, message: '' });

    try {
      if (!canEdit) {
        const response = await getSkillTree(accessToken, activeProjectId);
        setNodes(buildSkillTreeDraft(response));
        setReadOnly(true);
        setFeedback({
          type: 'warning',
          message: t('boardSettings.skillTree.errors.forbidden'),
        });
        return;
      }

      const response = await getSkillTreeConfig(accessToken, activeProjectId);
      setNodes(buildSkillTreeDraft(response));
      setReadOnly(false);
    } catch (error) {
      if (error?.status === 403) {
        try {
          const response = await getSkillTree(accessToken, activeProjectId);
          setNodes(buildSkillTreeDraft(response));
        } catch {
          setNodes(buildSkillTreeDraft([]));
        }

        setReadOnly(true);
        setFeedback({
          type: 'warning',
          message: t('boardSettings.skillTree.errors.forbidden'),
        });
      } else {
        setFeedback({
          type: 'error',
          message: error?.message || t('boardSettings.skillTree.errors.load'),
        });
      }
    } finally {
      setLoading(false);
    }
  }, [accessToken, activeProjectId, canEdit, t]);

  useEffect(() => {
    loadConfig();
  }, [loadConfig]);

  const setNodeField = useCallback((tier, branch, key, value) => {
    setNodes((prev) => prev.map((node) =>
      node.tier === tier && node.branch === branch
        ? { ...node, [key]: value }
        : node));
  }, []);

  const handleSave = useCallback(async () => {
    if (!accessToken || !activeProjectId || readOnly) {
      return;
    }

    setSaving(true);
    setFeedback({ type: null, message: '' });

    try {
      const payload = {
        nodes: nodes.map((node) => ({
          tier: node.tier,
          branch: node.branch,
          title: node.title.trim(),
          description: node.description.trim(),
          rewardJson: node.rewardJson.trim() || '{}',
          isActive: Boolean(node.isActive),
        })),
      };

      const response = await updateSkillTreeConfig(accessToken, activeProjectId, payload);
      setNodes(buildSkillTreeDraft(response));
      setFeedback({ type: 'success', message: t('boardSettings.skillTree.feedback.saved') });
    } catch (error) {
      setFeedback({
        type: 'error',
        message: error?.message || t('boardSettings.skillTree.errors.save'),
      });
    } finally {
      setSaving(false);
    }
  }, [accessToken, activeProjectId, nodes, readOnly, t]);

  const handleReset = useCallback(async () => {
    if (!accessToken || !activeProjectId || readOnly) {
      return;
    }

    setResetting(true);
    setFeedback({ type: null, message: '' });

    try {
      const response = await resetSkillTreeConfig(accessToken, activeProjectId);
      setNodes(buildSkillTreeDraft(response));
      setFeedback({ type: 'success', message: t('boardSettings.skillTree.feedback.reset') });
    } catch (error) {
      setFeedback({
        type: 'error',
        message: error?.message || t('boardSettings.skillTree.errors.reset'),
      });
    } finally {
      setResetting(false);
    }
  }, [accessToken, activeProjectId, readOnly, t]);

  if (!activeProjectId) {
    return (
      <div className="rounded-xl border p-5 text-sm" style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}>
        {t('boardSettings.skillTree.emptyProject')}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h3 className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
          {t('boardSettings.skillTree.title')}
        </h3>
        <p className="text-sm" style={{ color: 'var(--app-text-secondary)' }}>
          {t('boardSettings.skillTree.description')}
        </p>
      </div>

      <div className="rounded-xl border p-4 text-xs" style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}>
        {t('boardSettings.skillTree.projectScope')}
      </div>

      {feedback.message ? (
        <div
          className="rounded-xl border px-4 py-3 text-sm"
          style={{
            backgroundColor:
              feedback.type === 'error'
                ? 'rgba(239,68,68,0.08)'
                : feedback.type === 'warning'
                  ? 'rgba(245,158,11,0.10)'
                  : 'rgba(34,197,94,0.08)',
            borderColor:
              feedback.type === 'error'
                ? 'rgba(239,68,68,0.2)'
                : feedback.type === 'warning'
                  ? 'rgba(245,158,11,0.25)'
                  : 'rgba(34,197,94,0.2)',
            color: 'var(--app-text-main)',
          }}
        >
          {feedback.message}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={handleSave}
          disabled={readOnly || loading || saving || resetting}
          className="inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium disabled:opacity-50"
          style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}
        >
          {saving ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
          {saving ? t('boardSettings.skillTree.actions.saving') : t('boardSettings.skillTree.actions.save')}
        </button>
        <button
          type="button"
          onClick={handleReset}
          disabled={readOnly || loading || saving || resetting}
          className="inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium disabled:opacity-50"
          style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-main)', border: '1px solid var(--app-border)' }}
        >
          {resetting ? <Loader2 size={15} className="animate-spin" /> : <RotateCcw size={15} />}
          {resetting ? t('boardSettings.skillTree.actions.resetting') : t('boardSettings.skillTree.actions.reset')}
        </button>
      </div>

      {loading ? (
        <div className="flex items-center gap-2 rounded-xl border px-4 py-3 text-sm" style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-border)', color: 'var(--app-text-secondary)' }}>
          <Loader2 size={16} className="animate-spin" />
          {t('boardSettings.skillTree.actions.loading')}
        </div>
      ) : (
        <div className="space-y-5">
          {[1, 2, 3].map((tier) => {
            const tierNodes = nodes.filter((node) => node.tier === tier);

            return (
              <section key={tier} className="space-y-3">
                <div className="text-xs font-semibold uppercase tracking-wide" style={{ color: 'var(--app-text-secondary)' }}>
                  {t('boardSettings.skillTree.tierLabel', { tier })}
                </div>
                <div className="grid gap-4 xl:grid-cols-2">
                  {tierNodes.map((node) => (
                    <div
                      key={`${node.tier}-${node.branch}`}
                      className="space-y-3 rounded-xl border p-4"
                      style={{ backgroundColor: 'var(--app-layer-base)', borderColor: 'var(--app-border)' }}
                    >
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <div className="text-sm font-semibold" style={{ color: 'var(--app-text-main)' }}>
                            {t('boardSettings.skillTree.branchLabel', { branch: node.branch })}
                          </div>
                          <div className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>
                            {t('boardSettings.skillTree.slotHint', { tier: node.tier, branch: node.branch })}
                          </div>
                        </div>
                        <label className="flex items-center gap-2 text-xs font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                          <Toggle checked={node.isActive} disabled={readOnly} onChange={(value) => setNodeField(node.tier, node.branch, 'isActive', value)} />
                          {t('boardSettings.skillTree.activeLabel')}
                        </label>
                      </div>

                      <input
                        type="text"
                        value={node.title}
                        onChange={(event) => setNodeField(node.tier, node.branch, 'title', event.target.value)}
                        disabled={readOnly}
                        placeholder={t('boardSettings.skillTree.placeholders.title')}
                        className="w-full rounded-lg px-3 py-2 text-sm outline-none disabled:cursor-default disabled:opacity-70"
                        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}
                      />

                      <textarea
                        value={node.description}
                        onChange={(event) => setNodeField(node.tier, node.branch, 'description', event.target.value)}
                        disabled={readOnly}
                        placeholder={t('boardSettings.skillTree.placeholders.description')}
                        rows={3}
                        className="w-full rounded-lg px-3 py-2 text-sm outline-none disabled:cursor-default disabled:opacity-70"
                        style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)' }}
                      />

                      <div className="space-y-1">
                        <div className="text-xs font-medium" style={{ color: 'var(--app-text-secondary)' }}>
                          {t('boardSettings.skillTree.rewardJsonLabel')}
                        </div>
                        <textarea
                          value={node.rewardJson}
                          onChange={(event) => setNodeField(node.tier, node.branch, 'rewardJson', event.target.value)}
                          disabled={readOnly}
                          placeholder='{"xp": 150}'
                          rows={5}
                          className="w-full rounded-lg px-3 py-2 text-xs outline-none disabled:cursor-default disabled:opacity-70"
                          style={{ backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', color: 'var(--app-text-main)', fontFamily: 'monospace' }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </section>
            );
          })}
        </div>
      )}
    </div>
  );
};

const LinkAccessTab = () => {
  const { t } = useTranslation('platform');
  const [enabled, setEnabled] = useState(false);
  return (
    <div className="space-y-5">
      <label className="flex items-center gap-3">
        <button type="button" onClick={() => setEnabled((prev) => !prev)} className="flex h-[18px] w-[18px] items-center justify-center rounded" style={{ backgroundColor: enabled ? '#3B82F6' : 'transparent', border: enabled ? 'none' : '2px solid var(--app-border)' }}>
          {enabled ? <svg width="12" height="12" viewBox="0 0 12 12" fill="none"><path d="M2.5 6L5 8.5L9.5 3.5" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg> : null}
        </button>
        <span className="text-sm font-medium" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.linkAccess.title')}</span>
      </label>
      <div className="flex items-start gap-2 rounded-lg p-3" style={{ backgroundColor: 'var(--app-layer-base)' }}>
        <div className="mt-0.5 flex h-5 w-5 items-center justify-center rounded-full" style={{ backgroundColor: 'var(--app-accent)' }}><span className="text-xs font-bold text-white">i</span></div>
        <p className="text-sm leading-relaxed" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.linkAccess.description')}</p>
      </div>
    </div>
  );
};

const TaskTransferTab = () => {
  const { t } = useTranslation('platform');
  return <EmptyCard icon={ArrowRightLeft} description={t('boardSettings.taskTransfer.description')} emptyText={t('boardSettings.taskTransfer.empty')} createText={t('boardSettings.taskTransfer.create')} />;
};

const TemplatesTab = () => {
  const { t } = useTranslation('platform');
  return <EmptyCard icon={FileText} description={t('boardSettings.templates.description')} emptyText={t('boardSettings.templates.empty')} createText={t('boardSettings.templates.create')} />;
};

const BoardSettingsModal = ({ isOpen, onClose, projectName }) => {
  const { t } = useTranslation('platform');
  const [activeTab, setActiveTab] = useState('general');
  const tabs = useMemo(() => [
    { id: 'general', label: t('boardSettings.tabs.general') },
    { id: 'appearance', label: t('boardSettings.tabs.appearance') },
    { id: 'skill-tree', label: t('boardSettings.tabs.skillTree') },
    { id: 'stickers', label: t('boardSettings.tabs.stickers') },
    { id: 'automations', label: t('boardSettings.tabs.automations') },
    { id: 'link-access', label: t('boardSettings.tabs.linkAccess') },
    { id: 'task-transfer', label: t('boardSettings.tabs.taskTransfer') },
    { id: 'templates', label: t('boardSettings.tabs.templates') },
  ], [t]);

  useEffect(() => {
    if (!isOpen) return undefined;
    const handleKeyDown = (event) => { if (event.key === 'Escape') onClose(); };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const renderContent = () => {
    switch (activeTab) {
      case 'appearance': return <AppearanceTab />;
      case 'skill-tree': return <SkillTreeTab />;
      case 'stickers': return <StickersTab />;
      case 'automations': return <AutomationsTab />;
      case 'link-access': return <LinkAccessTab />;
      case 'task-transfer': return <TaskTransferTab />;
      case 'templates': return <TemplatesTab />;
      case 'general':
      default: return <GeneralTab />;
    }
  };

  return createPortal(
    <div className="fixed inset-0 z-[200] flex items-center justify-center">
      <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={onClose} />
      <div className="relative flex max-h-[85vh] w-full max-w-[900px] flex-col overflow-hidden rounded-2xl shadow-2xl" style={{ backgroundColor: 'var(--app-sidebar-bg)', border: '1px solid var(--app-border)' }}>
        <div className="flex shrink-0 items-center justify-between px-8 pb-4 pt-6">
          <h2 className="text-xl font-bold" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.modal.title', { projectName })}</h2>
          <div className="flex items-center gap-3">
            <button type="button" onClick={onClose} className="flex h-8 w-8 items-center justify-center rounded-lg" style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}><X size={16} /></button>
            <span className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>esc</span>
          </div>
        </div>
        <div className="flex flex-1 gap-8 overflow-hidden px-8 pb-6">
          <nav className="flex w-48 shrink-0 flex-col gap-0.5">
            {tabs.map((tab) => <button key={tab.id} type="button" onClick={() => setActiveTab(tab.id)} className="flex items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium" style={{ backgroundColor: activeTab === tab.id ? 'var(--app-accent)' : 'transparent', color: activeTab === tab.id ? 'white' : 'var(--app-text-secondary)' }}>{tab.id === 'automations' || tab.id === 'skill-tree' ? <Zap size={14} /> : null}{tab.label}</button>)}
            <div className="mt-auto space-y-2 rounded-xl p-4" style={{ backgroundColor: 'var(--app-layer-base)', border: '1px solid var(--app-border)' }}>
              <p className="text-sm font-bold leading-tight" style={{ color: 'var(--app-text-main)' }}>{t('boardSettings.promo.title')}</p>
              <p className="text-xs" style={{ color: 'var(--app-text-secondary)' }}>{t('boardSettings.promo.description')}</p>
              <button type="button" className="flex w-full items-center justify-center gap-1 rounded-lg px-3 py-2 text-xs font-medium" style={{ backgroundColor: 'var(--app-accent)', color: 'white' }}>{t('boardSettings.promo.cta')} <ExternalLink size={12} /></button>
            </div>
          </nav>
          <div className="custom-scrollbar flex-1 overflow-y-auto pr-2">{renderContent()}</div>
        </div>
      </div>
    </div>,
    document.body
  );
};

export default BoardSettingsModal;
