import React, { forwardRef, useState, useRef, useEffect, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import {
  MoreVertical, MessageSquare, ExternalLink, Trash2, ArrowRight,
  Tag, Clock, User, Users, Calendar, BarChart3, Code2, MessageCircle,
  Monitor, Timer, RefreshCw, Hourglass, Layers, Hash, Type, Zap,
  Pencil, Plus, Check, X, CheckSquare, ChevronDown, ChevronUp
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../shared/utils/cn';
import { formatDate } from '../../shared/utils/locale';
import { usePlatform } from '../context/PlatformContext';
import { usePreferences } from '../context/PreferencesContext';

// ─── Icon map for sticker rendering ────────────────────────────────────────
const STICKER_ICON_MAP = {
  Users, Calendar, BarChart3, Clock, Code2, MessageCircle,
  Monitor, Timer, RefreshCw, Hourglass, Tag, Hash, Type, Zap, Layers
};

// ─── Derive chip colors from hex ───────────────────────────────────────────
const colorFromHex = (hex) => ({
  bg: hex + '18', text: hex, iconBg: hex + '28', outline: hex + '30',
});

const DONE_STATUS_VALUES = ['готово', 'done', 'дайын'];
const isDoneStatus = (status) => DONE_STATUS_VALUES.includes(String(status || '').trim().toLowerCase());

// ─── Task Card Menu (Portal) ───────────────────────────────────────────────
const TaskCardMenu = ({ isOpen, onClose, onAction, buttonRef }) => {
  const { t } = useTranslation('platform');
  const menuRef = useRef(null);
  const [pos, setPos] = useState({ top: 0, left: 0 });

  useEffect(() => {
    if (isOpen && buttonRef?.current) {
      const r = buttonRef.current.getBoundingClientRect();
      const menuW = 200, menuH = 280;
      let top = r.bottom + 4;
      let left = r.right - menuW;
      if (left < 8) left = r.left;
      if (top + menuH > window.innerHeight) top = r.top - menuH - 4;
      setPos({ top, left });
    }
  }, [isOpen, buttonRef]);

  useEffect(() => {
    if (!isOpen) return;
    const h = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target) &&
          buttonRef?.current && !buttonRef.current.contains(e.target)) onClose();
    };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [isOpen, onClose, buttonRef]);

  if (!isOpen) return null;

  const items = [
    { key: 'open',     icon: ExternalLink, label: t('taskCard.menu.open') },
    { key: 'assignee', icon: User,         label: t('taskCard.menu.assignee') },
    { key: 'due-date', icon: Clock,        label: t('taskCard.menu.dueDate') },
    { key: 'tags',     icon: Tag,          label: t('taskCard.menu.tags') },
    { key: 'move',     icon: ArrowRight,   label: t('taskCard.menu.move') },
    { key: 'divider' },
    { key: 'delete',   icon: Trash2,       label: t('taskCard.menu.delete'), danger: true },
  ];

  return createPortal(
    <div
      ref={menuRef}
      className="fixed w-[200px] rounded-xl shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-150"
      style={{ top: pos.top, left: pos.left, backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', zIndex: 9999 }}
      onClick={(e) => e.stopPropagation()}
    >
      {items.map((item) =>
        item.key === 'divider'
          ? <div key="divider" className="h-px mx-3 my-1" style={{ backgroundColor: 'var(--app-border)' }} />
          : (
            <button
              key={item.key}
              onClick={(e) => { e.stopPropagation(); onAction(item.key); onClose(); }}
              className={cn(
                "flex items-center gap-2.5 w-full px-3.5 py-2 text-[13px] transition-colors",
                item.danger ? "text-red-400 hover:bg-red-500/10" : "hover:bg-white/[0.06]"
              )}
              style={item.danger ? {} : { color: 'var(--app-text-main)' }}
            >
              <item.icon size={15} style={{ opacity: 0.6, flexShrink: 0 }} />
              {item.label}
            </button>
          )
      )}
    </div>,
    document.body
  );
};

// ─── Inline Assignee Picker (Portal) ───────────────────────────────────────
const AssigneePickerPortal = ({ isOpen, onClose, onSelect, anchorRef }) => {
  const { t } = useTranslation('platform');
  const ref = useRef(null);
  const [pos, setPos] = useState({ top: 0, left: 0 });
  const { users } = usePlatform();

  useEffect(() => {
    if (isOpen && anchorRef?.current) {
      const r = anchorRef.current.getBoundingClientRect();
      setPos({ top: r.bottom + 4, left: Math.max(8, r.left) });
    }
  }, [isOpen, anchorRef]);

  useEffect(() => {
    if (!isOpen) return;
    const h = (e) => { if (ref.current && !ref.current.contains(e.target)) onClose(); };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return createPortal(
    <div
      ref={ref}
      className="fixed w-56 rounded-xl shadow-2xl overflow-hidden py-1 animate-in fade-in zoom-in-95 duration-150"
      style={{ top: pos.top, left: pos.left, backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', zIndex: 9999 }}
      onClick={(e) => e.stopPropagation()}
    >
      <button
        onClick={() => { onSelect(null); onClose(); }}
        className="flex items-center gap-2 w-full px-3 py-2 text-[13px] hover:bg-white/[0.06] transition-colors"
        style={{ color: 'var(--app-text-secondary)' }}
      >
        <X size={14} /> {t('taskCard.unassigned')}
      </button>
      {users.map(u => (
        <button
          key={u.id}
          onClick={() => { onSelect(u); onClose(); }}
          className="flex items-center gap-2.5 w-full px-3 py-2 text-[13px] hover:bg-white/[0.06] transition-colors"
          style={{ color: 'var(--app-text-main)' }}
        >
          {u.avatar ? (
            <img src={u.avatar} alt="" className="w-5 h-5 rounded-full object-cover" />
          ) : (
            <div className="w-5 h-5 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 flex items-center justify-center text-[8px] font-bold text-white">
              {u.initials || '?'}
            </div>
          )}
          {u.name}
        </button>
      ))}
    </div>,
    document.body
  );
};

// ─── Add Sticker Button on Card (Portal menu) ─────────────────────────────
const AddStickerButton = ({ taskId }) => {
  const { t } = useTranslation('platform');
  const [isOpen, setIsOpen] = useState(false);
  const buttonRef = useRef(null);
  const menuRef = useRef(null);
  const { stickerCatalog, boardStickerIds, addBoardSticker, updateTaskStickerValue } = usePlatform();
  const [pos, setPos] = useState({ top: 0, left: 0 });

  useEffect(() => {
    if (isOpen && buttonRef.current) {
      const r = buttonRef.current.getBoundingClientRect();
      const menuH = 320;
      let top = r.bottom + 4;
      if (top + menuH > window.innerHeight) top = r.top - menuH - 4;
      setPos({ top, left: Math.max(8, r.left) });
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    const h = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target) &&
          buttonRef.current && !buttonRef.current.contains(e.target)) setIsOpen(false);
    };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [isOpen]);

  const stickersOnBoard = (boardStickerIds || [])
    .map(id => stickerCatalog.find(s => s.id === id))
    .filter(Boolean)
    .filter(s => s.type !== 'system');

  const stickersNotOnBoard = stickerCatalog
    .filter(s => !(boardStickerIds || []).includes(s.id) && s.type !== 'system');

  return (
    <>
      <button
        ref={buttonRef}
        onClick={(e) => { e.stopPropagation(); setIsOpen(!isOpen); }}
        className="flex items-center justify-center w-6 h-6 rounded-md transition-all hover:scale-110"
        style={{ color: 'var(--app-text-secondary)', border: '1px dashed var(--app-border)' }}
        title={t('taskCard.addSticker')}
      >
        <Plus size={12} />
      </button>
      {isOpen && createPortal(
        <div
          ref={menuRef}
          className="fixed w-64 max-h-80 overflow-y-auto rounded-xl shadow-2xl py-2 animate-in fade-in zoom-in-95 duration-150 custom-scrollbar"
          style={{ top: pos.top, left: pos.left, backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', zIndex: 9999 }}
          onClick={(e) => e.stopPropagation()}
        >
          <div className="px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>
            {t('taskCard.addSticker')}
          </div>
          {stickersOnBoard.map(s => {
            const Icon = STICKER_ICON_MAP[s.iconName] || Tag;
            return (
              <div key={s.id}>
                <div className="px-3 py-1.5 flex items-center gap-2 text-[13px]" style={{ color: 'var(--app-text-main)' }}>
                  <Icon size={14} style={{ opacity: 0.5 }} />
                  <span className="font-medium">{s.label}</span>
                </div>
                {s.type === 'state-set' && s.values?.length > 0 && (
                  <div className="pl-8 pr-3 pb-1 flex flex-wrap gap-1">
                    {s.values.map(v => {
                      const c = colorFromHex(v.color);
                      return (
                        <button
                          key={v.id}
                          onClick={() => { updateTaskStickerValue(taskId, s.id, v.id); setIsOpen(false); }}
                          className="px-2 py-0.5 rounded-md text-[11px] font-semibold transition-colors hover:brightness-110"
                          style={{ backgroundColor: c.bg, color: c.text, border: `1px solid ${c.outline}` }}
                        >
                          {v.label}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
          {stickersNotOnBoard.length > 0 && (
            <>
              <div className="h-px mx-3 my-1" style={{ backgroundColor: 'var(--app-border)' }} />
              <div className="px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider" style={{ color: 'var(--app-text-secondary)' }}>
                {t('taskCard.moreStickers')}
              </div>
              {stickersNotOnBoard.map(s => {
                const Icon = STICKER_ICON_MAP[s.iconName] || Tag;
                return (
                  <button
                    key={s.id}
                    onClick={() => { addBoardSticker(s.id); }}
                    className="flex items-center gap-2 w-full px-3 py-1.5 text-[13px] hover:bg-white/[0.06] transition-colors"
                    style={{ color: 'var(--app-text-main)' }}
                  >
                    <Icon size={14} style={{ opacity: 0.5 }} />
                    {s.label}
                    <Plus size={11} className="ml-auto" style={{ opacity: 0.4 }} />
                  </button>
                );
              })}
            </>
          )}
        </div>,
        document.body
      )}
    </>
  );
};

// ─── Sticker Chip Component ────────────────────────────────────────────────
const StickerChip = ({ sticker, value, taskId }) => {
  const { t } = useTranslation('platform');
  const { updateTaskStickerValue } = usePlatform();
  const [pickerOpen, setPickerOpen] = useState(false);
  const chipRef = useRef(null);
  const pickerRef = useRef(null);
  const [pos, setPos] = useState({ top: 0, left: 0 });

  const Icon = STICKER_ICON_MAP[sticker.iconName] || Tag;

  let displayValue = value;
  let chipColor = colorFromHex(sticker.values?.[0]?.color || '#6366F1');

  if (sticker.type === 'state-set' && sticker.values) {
    const stateVal = sticker.values.find(v => v.id === value);
    if (stateVal) {
      displayValue = stateVal.label;
      chipColor = colorFromHex(stateVal.color);
    }
  } else if (sticker.type === 'number') {
    chipColor = colorFromHex('#6366F1');
  } else if (sticker.type === 'text') {
    chipColor = colorFromHex('#3B82F6');
    if (typeof displayValue === 'string' && displayValue.length > 16) {
      displayValue = displayValue.slice(0, 16) + '…';
    }
  }

  useEffect(() => {
    if (pickerOpen && chipRef.current) {
      const r = chipRef.current.getBoundingClientRect();
      setPos({ top: r.bottom + 4, left: Math.max(8, r.left) });
    }
  }, [pickerOpen]);

  useEffect(() => {
    if (!pickerOpen) return;
    const h = (e) => {
      if (pickerRef.current && !pickerRef.current.contains(e.target) &&
          chipRef.current && !chipRef.current.contains(e.target)) setPickerOpen(false);
    };
    document.addEventListener('mousedown', h);
    return () => document.removeEventListener('mousedown', h);
  }, [pickerOpen]);

  return (
    <>
      <div
        ref={chipRef}
        onClick={(e) => {
          e.stopPropagation();
          if (sticker.type === 'state-set') setPickerOpen(!pickerOpen);
        }}
        className="relative cursor-pointer flex items-center gap-1 py-0.5 pl-1 pr-2 rounded-md text-[11px] font-semibold leading-none transition-all hover:brightness-110"
        style={{
          backgroundColor: chipColor.bg,
          color: chipColor.text,
          outline: `1px solid ${chipColor.outline}`,
          outlineOffset: '-1px',
        }}
      >
        <span className="flex items-center justify-center w-4 h-4 rounded" style={{ backgroundColor: chipColor.iconBg }}>
          <Icon size={10} />
        </span>
        <span className="whitespace-nowrap max-w-[120px] truncate">{displayValue}</span>
      </div>

      {pickerOpen && sticker.type === 'state-set' && createPortal(
        <div
          ref={pickerRef}
          className="fixed w-48 rounded-xl shadow-2xl py-1.5 animate-in fade-in zoom-in-95 duration-150"
          style={{ top: pos.top, left: pos.left, backgroundColor: 'var(--app-layer-surface)', border: '1px solid var(--app-border)', zIndex: 9999 }}
          onClick={(e) => e.stopPropagation()}
        >
          {sticker.values.map(v => {
            const c = colorFromHex(v.color);
            const isSelected = v.id === value;
            return (
              <button
                key={v.id}
                onClick={() => { updateTaskStickerValue(taskId, sticker.id, v.id); setPickerOpen(false); }}
                className={cn("flex items-center gap-2 w-full px-3 py-1.5 text-[12px] font-medium transition-colors", isSelected ? "bg-white/[0.08]" : "hover:bg-white/[0.04]")}
                style={{ color: c.text }}
              >
                <span className="w-2 h-2 rounded-full" style={{ backgroundColor: v.color }} />
                {v.label}
                {isSelected && <Check size={12} className="ml-auto" />}
              </button>
            );
          })}
          <div className="h-px mx-2 my-1" style={{ backgroundColor: 'var(--app-border)' }} />
          <button
            onClick={(e) => { e.stopPropagation(); updateTaskStickerValue(taskId, sticker.id, null); setPickerOpen(false); }}
            className="flex items-center gap-2 w-full px-3 py-1.5 text-[12px] text-red-400 hover:bg-red-500/10 transition-colors"
          >
            <X size={12} /> {t('taskCard.removeValue')}
          </button>
        </div>,
        document.body
      )}
    </>
  );
};

// ─── Priority Chip ─────────────────────────────────────────────────────────
const PriorityChip = ({ priority }) => {
  const map = {
    critical: { label: 'critical', color: '#EF4444' },
    urgent:   { label: 'urgent',   color: '#EF4444' },
    high:     { label: 'major',    color: '#F97316' },
    medium:   { label: 'normal',   color: '#22C55E' },
    low:      { label: 'low',      color: '#94A3B8' },
  };
  const info = map[priority];
  if (!info) return null;
  const c = colorFromHex(info.color);
  return (
    <div
      className="flex items-center gap-1 py-0.5 pl-1 pr-2 rounded-md text-[11px] font-semibold leading-none"
      style={{ backgroundColor: c.bg, color: c.text, outline: `1px solid ${c.outline}`, outlineOffset: '-1px' }}
    >
      <span className="flex items-center justify-center w-4 h-4 rounded" style={{ backgroundColor: c.iconBg }}>
        <BarChart3 size={10} />
      </span>
      <span className="whitespace-nowrap">{info.label}</span>
    </div>
  );
};

// ─── Due Date Chip ─────────────────────────────────────────────────────────
const DueDateChip = ({ dueDate }) => {
  const { language } = usePreferences();
  if (!dueDate) return null;
  const d = new Date(dueDate);
  const now = new Date();
  const isOverdue = d < now && d.toDateString() !== now.toDateString();
  const formatted = formatDate(dueDate, language, { day: '2-digit', month: '2-digit', year: '2-digit' });
  const color = isOverdue ? '#EF4444' : '#3B82F6';
  const c = colorFromHex(color);
  return (
    <div
      className="flex items-center gap-1 py-0.5 pl-1 pr-2 rounded-md text-[11px] font-semibold leading-none"
      style={{ backgroundColor: c.bg, color: c.text, outline: `1px solid ${c.outline}`, outlineOffset: '-1px' }}
    >
      <span className="flex items-center justify-center w-4 h-4 rounded" style={{ backgroundColor: c.iconBg }}>
        <Calendar size={10} />
      </span>
      <span className="whitespace-nowrap">{formatted}</span>
    </div>
  );
};

// ─── Assignee Avatar ───────────────────────────────────────────────────────
const AssigneeAvatar = ({ assignee }) => {
  if (!assignee || assignee === 'Unassigned') return null;
  const name = assignee.name || (typeof assignee === 'string' ? assignee : '');
  const initials = assignee.initials || (name ? name.charAt(0).toUpperCase() : '?');
  return assignee.avatar ? (
    <img src={assignee.avatar} alt={name} title={name} className="w-5 h-5 rounded-full object-cover ring-1 ring-white/10" />
  ) : (
    <div
      className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white ring-1 ring-white/10"
      style={{ background: 'linear-gradient(135deg, #6366F1, #8B5CF6)' }}
      title={name}
    >
      {initials}
    </div>
  );
};

// ═══════════════════════════════════════════════════════════════════════════
// TaskCard оформлен как компактная карточка в духе Kaiten.
// ═══════════════════════════════════════════════════════════════════════════
export const TaskCard = forwardRef(({
  task, onClick, style, className, isOverlay,
  viewSettings = { showTaskId: false, showAssignee: true, showTags: true, showPriority: true, showDueDate: true },
  boardStickers = [],
  ...props
}, ref) => {
  const { t } = useTranslation('platform');
  const { updateTask, deleteTask, moveTask, activeProject, updateSubTask } = usePlatform();
  const [menuOpen, setMenuOpen] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editTitle, setEditTitle] = useState(task.title || task.content || '');
  const menuBtnRef = useRef(null);
  const cardRef = useRef(null);
  const editInputRef = useRef(null);
  const assigneePickerAnchor = useRef(null);
  const [assigneePickerOpen, setAssigneePickerOpen] = useState(false);

  useEffect(() => { setEditTitle(task.title || task.content || ''); }, [task.title, task.content]);
  useEffect(() => {
    if (isEditing && editInputRef.current) { editInputRef.current.focus(); editInputRef.current.select(); }
  }, [isEditing]);

  const handleMenuAction = useCallback((action) => {
    switch (action) {
      case 'open': onClick?.(); break;
      case 'assignee': setAssigneePickerOpen(true); break;
      case 'delete': deleteTask(task.id); break;
      default: onClick?.();
    }
  }, [onClick, deleteTask, task.id]);

  const handleAssigneeSelect = useCallback((user) => {
    updateTask(task.id, {
      assigneeId: user ? user.id : null,
      assignee: user ? { id: user.id, name: user.name, initials: user.initials, avatar: user.avatar } : null,
    });
  }, [updateTask, task.id]);

  const handleTitleSave = useCallback(() => {
    const trimmed = editTitle.trim();
    if (trimmed && trimmed !== (task.title || task.content)) {
      updateTask(task.id, { title: trimmed });
    }
    setIsEditing(false);
  }, [editTitle, task, updateTask]);

  const handleEditKeyDown = useCallback((e) => {
    if (e.key === 'Enter') { e.preventDefault(); handleTitleSave(); }
    if (e.key === 'Escape') { setEditTitle(task.title || task.content || ''); setIsEditing(false); }
  }, [handleTitleSave, task]);

  // Стикеры и компактные индикаторы задачи.
  const stickerValues = task.stickerValues || {};
  const enabledStickers = boardStickers.filter(s =>
    s.id !== 'assignee' && s.id !== 'deadline' && s.type !== 'system'
  );
  const stickersWithValues = enabledStickers.filter(s => stickerValues[s.id]);
  const subtasks = task.subtasks || [];
  const subtasksDone = subtasks.filter(s => s.isCompleted).length;
  const commentsCount = Array.isArray(task.comments) ? task.comments.length : (task.comments || 0);
  const hasBottomRow = true; // Второй ряд всегда показывается: там живут кнопки действий и avatar/assignee.
  const [subtasksExpanded, setSubtasksExpanded] = useState(false);

  return (
    <div
      ref={(el) => {
        cardRef.current = el;
        if (typeof ref === 'function') ref(el);
        else if (ref) ref.current = el;
      }}
      className={cn(
        "group relative p-2.5 pb-3 min-h-[40px] cursor-grab active:cursor-grabbing pointer-events-auto",
        "rounded-lg border transition-all duration-150",
        "hover:shadow-md",
        isOverlay && "shadow-xl rotate-[2deg] scale-[1.02]",
        className
      )}
      style={{
        ...style,
        backgroundColor: 'var(--app-layer-surface)',
        borderColor: menuOpen ? 'var(--app-accent)' : 'var(--app-border)',
        transition: isOverlay ? 'none' : undefined,
      }}
      {...props}
    >
      {/* Ряд 1: чекбокс, заголовок и меню. */}
      <div className="flex gap-1.5 items-start">
        {/* Чекбокс-радиус для быстрого завершения. */}
        <button
          onClick={(e) => {
            e.stopPropagation();
            if (activeProject) {
              const columns = activeProject.boardData?.columns || {};
              const doneCol = Object.values(columns).find(c => isDoneStatus(c.title));
              if (doneCol && task.statusId !== doneCol.id) {
                moveTask(task.id, doneCol.id);
              }
            }
          }}
          className="mt-[2px] w-[18px] h-[18px] min-w-[18px] rounded-full border-2 flex items-center justify-center shrink-0 transition-all hover:border-green-400 hover:bg-green-500/20"
          style={{
            borderColor: isDoneStatus(task.status) ? '#22C55E' : '#9CA3AF',
            backgroundColor: isDoneStatus(task.status) ? '#22C55E' : 'rgba(255,255,255,0.06)',
          }}
        >
          {isDoneStatus(task.status) && <Check size={11} className="text-white" strokeWidth={3} />}
        </button>

        {/* Заголовок карточки. */}
        <div className="flex-1 min-w-0 ml-0.5">
          {isEditing ? (
            <input
              ref={editInputRef}
              value={editTitle}
              onChange={(e) => setEditTitle(e.target.value)}
              onBlur={handleTitleSave}
              onKeyDown={handleEditKeyDown}
              onClick={(e) => e.stopPropagation()}
              className="w-full bg-transparent text-sm leading-snug outline-none border-b"
              style={{ color: 'var(--app-text-main)', borderColor: 'var(--app-accent)' }}
            />
          ) : (
            <div className="relative inline">
              <span
                onClick={onClick}
                className={cn(
                  "text-sm leading-snug break-words cursor-pointer hover:underline decoration-1 underline-offset-2",
                  isDoneStatus(task.status) && "line-through opacity-50"
                )}
                style={{ color: 'var(--app-text-main)', textDecorationColor: 'var(--app-text-secondary)' }}
              >
                {task.title || task.content}
              </span>
              <button
                onClick={(e) => { e.stopPropagation(); setIsEditing(true); }}
                className="invisible group-hover:visible ml-1 inline-flex items-center align-middle p-0.5 rounded transition-all hover:scale-110"
                style={{ color: 'var(--app-text-secondary)' }}
                title={t('taskCard.editTitle')}
              >
                <Pencil size={11} />
              </button>
            </div>
          )}
        </div>

        {/* Кнопка контекстного меню с тремя точками. */}
        <div className="relative" ref={assigneePickerAnchor}>
          <button
            ref={menuBtnRef}
            onClick={(e) => { e.stopPropagation(); setMenuOpen(!menuOpen); }}
            className="-m-1 w-6 h-6 flex items-center justify-center rounded transition-all shrink-0"
            style={{ color: 'var(--app-text-secondary)' }}
          >
            <MoreVertical size={15} />
          </button>
        </div>
      </div>

      {/* Ряд 2: chips, inline-plus и закреплённый справа avatar. */}
      <div className="flex items-end gap-1.5 mt-2 ml-[22px]">
        {/* Слева chips и inline-plus с переносом, справа заранее оставлено место под avatar. */}
        <div className="flex flex-wrap gap-1 items-center flex-1 min-w-0">
          {viewSettings.showPriority && task.priority != null && task.priority !== '' && (
            <PriorityChip priority={typeof task.priority === 'string' ? task.priority.toLowerCase() : task.priority} />
          )}
          {viewSettings.showDueDate && task.dueDate && <DueDateChip dueDate={task.dueDate} />}

          {stickersWithValues.map(sticker => (
            <StickerChip key={sticker.id} sticker={sticker} value={stickerValues[sticker.id]} taskId={task.id} />
          ))}

          {viewSettings.showTags && task.tags?.map(tag => {
            const isObj = typeof tag === 'object';
            const name = isObj ? tag.name : tag;
            const color = isObj ? tag.color : '#6366F1';
            const c = colorFromHex(color || '#6366F1');
            return (
              <div
                key={isObj ? tag.id : tag}
                className="flex items-center gap-1 py-0.5 pl-1 pr-2 rounded-md text-[11px] font-semibold leading-none"
                style={{ backgroundColor: c.bg, color: c.text, outline: `1px solid ${c.outline}`, outlineOffset: '-1px' }}
              >
                <span className="flex items-center justify-center w-4 h-4 rounded" style={{ backgroundColor: c.iconBg }}>
                  <Tag size={10} />
                </span>
                <span className="whitespace-nowrap max-w-[100px] truncate">{isObj ? tag.displayName || name : name}</span>
              </div>
            );
          })}

          {/* Счётчик подзадач. */}
          {subtasks.length > 0 && (
            <button
              onClick={(e) => { e.stopPropagation(); setSubtasksExpanded(v => !v); }}
              className="flex items-center gap-0.5 text-[11px] transition-colors hover:opacity-80"
              style={{ color: subtasksDone === subtasks.length ? '#22C55E' : 'var(--app-text-secondary)' }}
              title={t('taskCard.subtasks', { done: subtasksDone, total: subtasks.length })}
            >
              <CheckSquare size={11} />
              <span>{subtasksDone}/{subtasks.length}</span>
              {subtasksExpanded ? <ChevronUp size={10} /> : <ChevronDown size={10} />}
            </button>
          )}

          {/* Счётчик комментариев. */}
          {commentsCount > 0 && (
            <span className="flex items-center gap-0.5 text-[11px]" style={{ color: 'var(--app-text-secondary)' }}>
              <MessageSquare size={11} /> {commentsCount}
            </span>
          )}

          {/* Inline-plus после последнего chip, с плавным проявлением на hover. */}
          <div className="opacity-0 group-hover:opacity-100 transition-opacity duration-150">
            <AddStickerButton taskId={task.id} />
          </div>
        </div>

        {/* Справа avatar назначенного пользователя или кнопка назначения, с фиксированной шириной против наложений. */}
        {viewSettings.showAssignee && (
          <div className="shrink-0 w-5 flex items-center justify-center">
            {task.assignee && task.assignee !== 'Unassigned' ? (
              <AssigneeAvatar assignee={task.assignee} />
            ) : (
              <button
                onClick={(e) => { e.stopPropagation(); setAssigneePickerOpen(true); }}
                className="opacity-0 group-hover:opacity-100 transition-opacity duration-150 w-5 h-5 rounded-full flex items-center justify-center hover:scale-110"
                style={{ color: 'var(--app-text-secondary)', border: '1px dashed var(--app-border)' }}
                title={t('taskCard.assignAssignee')}
              >
                <User size={11} />
              </button>
            )}
          </div>
        )}
      </div>

      {/* Выпадающий список подзадач. */}
      {subtasksExpanded && subtasks.length > 0 && (
        <div className="ml-[22px] mt-1 space-y-0.5">
          {subtasks.map(st => (
            <label
              key={st.id}
              className="flex items-center gap-1.5 py-0.5 px-1 rounded cursor-pointer hover:bg-white/[0.04] text-[12px] transition-colors"
              onClick={(e) => e.stopPropagation()}
            >
              <input
                type="checkbox"
                checked={!!st.isCompleted}
                onChange={() => updateSubTask(st.id, { isCompleted: !st.isCompleted, title: st.title })}
                className="accent-green-500 w-3.5 h-3.5"
              />
              <span
                className={cn(st.isCompleted && 'line-through opacity-50')}
                style={{ color: 'var(--app-text-main)' }}
              >
                {st.title}
              </span>
            </label>
          ))}
        </div>
      )}

      {/* Portals и контекстные меню. */}
      <TaskCardMenu isOpen={menuOpen} onClose={() => setMenuOpen(false)} onAction={handleMenuAction} buttonRef={menuBtnRef} />
      <AssigneePickerPortal isOpen={assigneePickerOpen} onClose={() => setAssigneePickerOpen(false)} onSelect={handleAssigneeSelect} anchorRef={assigneePickerAnchor} />
    </div>
  );
});

// ═══════════════════════════════════════════════════════════════════════════
// SortableTaskCard — DnD-обёртка вокруг обычной карточки.
// ═══════════════════════════════════════════════════════════════════════════
const SortableTaskCard = ({ task, onClick, viewSettings, boardStickers }) => {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: task.id });
  const wasDraggingRef = useRef(false);

  // Track drag state so we can suppress the click that fires after drag ends
  useEffect(() => {
    if (isDragging) {
      wasDraggingRef.current = true;
    }
  }, [isDragging]);

  const handleClick = useCallback(() => {
    if (wasDraggingRef.current) {
      wasDraggingRef.current = false;
      return;
    }
    onClick?.();
  }, [onClick]);

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  };

  return (
    <TaskCard
      ref={setNodeRef}
      style={style}
      task={task}
      onClick={handleClick}
      viewSettings={viewSettings}
      boardStickers={boardStickers}
      {...attributes}
      {...listeners}
    />
  );
};

export default SortableTaskCard;
