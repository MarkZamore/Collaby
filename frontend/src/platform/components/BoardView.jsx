import React, { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import {
  DndContext,
  closestCorners,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragOverlay,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
  horizontalListSortingStrategy,
} from '@dnd-kit/sortable';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Plus, MoreHorizontal, Edit3, Archive, Trash2, Copy, ArrowRight, CheckCircle, Check, ClipboardList, Monitor } from 'lucide-react';
import TaskCard, { TaskCard as TaskCardOverlay } from './TaskCard';
import { usePlatform } from '../context/PlatformContext';
import { getStatusColor } from '../utils/statuses';

const DEFAULT_STATUS_COLORS = {
  'К выполнению': '#3B82F6',
  'В процессе': '#F59E0B',
  'Готово': '#22C55E',
  'Бэклог': '#64748B',
  'Ревью': '#8B5CF6',
  'Сделать': '#3B82F6',
  'To do': '#3B82F6',
  'In progress': '#F59E0B',
  Review: '#8B5CF6',
  Done: '#22C55E',
  Backlog: '#64748B',
  'Орындауға': '#3B82F6',
  'Жұмыста': '#F59E0B',
  'Дайын': '#22C55E',
};

const FALLBACK_HEADER_COLORS = ['#3B82F6', '#F59E0B', '#22C55E', '#8B5CF6'];

const COLOR_PALETTE = [
  '#EF4444',
  '#F97316',
  '#F59E0B',
  '#EAB308',
  '#84CC16',
  '#22C55E',
  '#14B8A6',
  '#06B6D4',
  '#3B82F6',
  '#6366F1',
  '#8B5CF6',
  '#A855F7',
  '#D946EF',
  '#EC4899',
  '#94A3B8',
  '#6B7280',
];

const ColumnMenu = ({ isOpen, onClose, onAction, currentColor, onColorChange, buttonRef }) => {
  const { t } = useTranslation('platform');
  const menuRef = useRef(null);
  const colorInputRef = useRef(null);
  const [menuPosition, setMenuPosition] = useState({ top: 0, left: 0 });

  useEffect(() => {
    if (isOpen && buttonRef?.current) {
      const buttonRect = buttonRef.current.getBoundingClientRect();
      const menuWidth = 224;
      const rightOverflow = buttonRect.right + menuWidth > window.innerWidth;

      setMenuPosition({
        top: buttonRect.bottom + 4,
        left: rightOverflow ? buttonRect.right - menuWidth : buttonRect.left,
      });
    }
  }, [buttonRef, isOpen]);

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        isOpen
        && menuRef.current
        && !menuRef.current.contains(event.target)
        && buttonRef?.current
        && !buttonRef.current.contains(event.target)
      ) {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [buttonRef, isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  return createPortal(
    <div
      ref={menuRef}
      className="fixed w-56 rounded-lg shadow-xl overflow-hidden"
      style={{
        backgroundColor: 'var(--app-layer-overlay, var(--app-layer-surface))',
        border: '1px solid var(--app-border)',
        zIndex: 10000,
        top: menuPosition.top,
        left: menuPosition.left,
      }}
    >
      <div className="py-2">
        <button
          onClick={() => { onAction('rename'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-black/5 dark:hover:bg-white/10 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: 'var(--app-text-main)', lineHeight: '1.4' }}
        >
          <Edit3 size={16} style={{ color: 'var(--app-text-tertiary)', flexShrink: 0 }} />
          {t('board.columnMenu.rename')}
        </button>
        <button
          onClick={() => { onAction('duplicate'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-black/5 dark:hover:bg-white/10 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: 'var(--app-text-main)', lineHeight: '1.4' }}
        >
          <Copy size={16} style={{ color: 'var(--app-text-tertiary)', flexShrink: 0 }} />
          {t('board.columnMenu.duplicate')}
        </button>
        <button
          onClick={() => { onAction('move'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-black/5 dark:hover:bg-white/10 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: 'var(--app-text-main)', lineHeight: '1.4' }}
        >
          <ArrowRight size={16} style={{ color: 'var(--app-text-tertiary)', flexShrink: 0 }} />
          {t('board.columnMenu.moveAllTasks')}
        </button>
        <button
          onClick={() => { onAction('complete'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-black/5 dark:hover:bg-white/10 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: 'var(--app-text-main)', lineHeight: '1.4' }}
        >
          <CheckCircle size={16} style={{ color: 'var(--app-text-tertiary)', flexShrink: 0 }} />
          {t('board.columnMenu.completeAllTasks')}
        </button>
        <button
          onClick={() => { onAction('archive'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-black/5 dark:hover:bg-white/10 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: 'var(--app-text-main)', lineHeight: '1.4' }}
        >
          <Archive size={16} style={{ color: 'var(--app-text-tertiary)', flexShrink: 0 }} />
          {t('board.columnMenu.archive')}
        </button>
      </div>

      <div className="h-px mx-3" style={{ backgroundColor: 'var(--app-border)' }} />

      <div className="px-4 py-3">
        <div className="text-xs font-medium mb-3" style={{ color: 'var(--app-text-tertiary)' }}>
          {t('board.columnMenu.color')}
        </div>
        <div className="flex flex-wrap gap-2">
          {COLOR_PALETTE.map((color) => (
            <button
              key={color}
              onClick={() => { onColorChange(color); }}
              className="w-5 h-5 rounded-full transition-transform hover:scale-110 focus:outline-none"
              style={{
                backgroundColor: color,
                boxShadow: currentColor === color ? `0 0 0 2px var(--app-layer-overlay, var(--app-layer-surface)), 0 0 0 3px ${color}` : 'none',
              }}
              title={color}
            />
          ))}
          <button
            onClick={() => colorInputRef.current?.click()}
            className="w-5 h-5 rounded-full transition-transform hover:scale-110 focus:outline-none flex items-center justify-center"
            style={{
              background: 'conic-gradient(red, yellow, lime, aqua, blue, magenta, red)',
              border: '1px solid var(--app-border)',
            }}
            title={t('board.columnMenu.customColor')}
          >
            <Plus size={10} style={{ color: 'white', filter: 'drop-shadow(0 0 1px rgba(0,0,0,0.5))' }} />
          </button>
          <input
            ref={colorInputRef}
            type="color"
            value={currentColor}
            onChange={(event) => onColorChange(event.target.value)}
            className="sr-only"
          />
        </div>
      </div>

      <div className="h-px mx-3" style={{ backgroundColor: 'var(--app-border)' }} />

      <div className="py-2">
        <button
          onClick={() => { onAction('delete'); onClose(); }}
          className="flex items-center w-full px-4 py-2.5 hover:bg-red-500/20 transition-colors"
          style={{ gap: '10px', fontSize: '14px', fontWeight: 400, color: '#EF4444', lineHeight: '1.4' }}
        >
          <Trash2 size={16} style={{ flexShrink: 0 }} />
          {t('board.columnMenu.delete')}
        </button>
      </div>
    </div>,
    document.body,
  );
};

const Column = ({
  column,
  tasks,
  onTaskClick,
  onAddTask,
  viewSettings,
  columnColors,
  onColumnColorChange,
  boardStickers,
  readOnly,
  canManageStructure,
}) => {
  const { t } = useTranslation('platform');
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: column.id,
    data: { type: 'column' },
  });

  const columnStyle = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  const [menuOpen, setMenuOpen] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [newTaskTitle, setNewTaskTitle] = useState('');
  const buttonRef = useRef(null);
  const inputRef = useRef(null);

  const dragAttributes = canManageStructure ? attributes : {};
  const dragListeners = canManageStructure ? listeners : {};

  const columnTitle = (column.title || '').trim();
  const statusColor = columnColors?.[column.id]
    || column.color
    || DEFAULT_STATUS_COLORS[columnTitle]
    || DEFAULT_STATUS_COLORS[columnTitle.charAt(0).toUpperCase() + columnTitle.slice(1).toLowerCase()]
    || getStatusColor(column.statusKey || column.rawTitle || columnTitle)
    || FALLBACK_HEADER_COLORS[(column.order ?? 0) % FALLBACK_HEADER_COLORS.length];

  const submitTask = () => {
    if (newTaskTitle.trim()) {
      onAddTask(column.rawTitle || column.title, newTaskTitle);
      setNewTaskTitle('');
      setIsCreating(true);
      setTimeout(() => inputRef.current?.focus(), 0);
    } else {
      setIsCreating(false);
    }
  };

  const handleKeyDown = (event) => {
    if (event.key === 'Enter') {
      submitTask();
    } else if (event.key === 'Escape') {
      setIsCreating(false);
      setNewTaskTitle('');
    }
  };

  return (
    <div
      ref={setNodeRef}
      className="flex flex-col w-72 shrink-0"
      style={{
        ...columnStyle,
        backgroundColor: `color-mix(in srgb, ${statusColor} 15%, var(--app-bg))`,
        borderRadius: '12px',
        boxShadow: '0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.06)',
        height: 'fit-content',
        maxHeight: 'calc(100vh - 140px)',
        marginBottom: '16px',
        overflowY: 'auto',
        overflowX: 'hidden',
      }}
    >
      <div
        className="shrink-0"
        style={{
          height: '12px',
          backgroundColor: statusColor,
          minHeight: '12px',
        }}
      />

      <div
        className={`flex flex-col sticky top-0 z-10 ${canManageStructure ? 'cursor-grab active:cursor-grabbing' : 'cursor-default'}`}
        style={{ backgroundColor: 'transparent' }}
        {...dragAttributes}
        {...dragListeners}
      >
        <div className="flex items-center justify-between px-3 pt-3 pb-1">
          <div className="flex items-center gap-2">
            <h3 className="text-xs font-bold uppercase tracking-wide" style={{ color: 'var(--app-text-main)' }}>{column.title}</h3>
            <span
              className="text-[10px] font-bold px-1.5 py-0.5 rounded"
              style={{
                backgroundColor: 'var(--app-layer-base)',
                color: 'var(--app-text-secondary)',
              }}
            >
              {tasks.length}
            </span>
          </div>

          {canManageStructure && (
            <div className="relative">
              <button
                ref={buttonRef}
                onClick={() => setMenuOpen(!menuOpen)}
                className="p-1 rounded transition-colors hover:opacity-80"
                style={{ color: menuOpen ? 'var(--app-text-main)' : 'var(--app-text-secondary)' }}
                title={t('board.columnMenuTitle')}
              >
                <MoreHorizontal size={14} />
              </button>

              <ColumnMenu
                isOpen={menuOpen}
                onClose={() => setMenuOpen(false)}
                onAction={() => {}}
                currentColor={statusColor}
                onColorChange={(color) => onColumnColorChange?.(column.id, color)}
                buttonRef={buttonRef}
              />
            </div>
          )}
        </div>

        {!isCreating && !readOnly && (
          <div className="px-3 pb-2">
            <button
              onClick={() => {
                setIsCreating(true);
                setTimeout(() => inputRef.current?.focus(), 0);
              }}
              className="flex items-center gap-1 text-sm font-medium hover:opacity-80 transition-opacity"
              style={{ color: '#3B82F6' }}
              title={t('board.addTask')}
            >
              <Plus size={14} />
              {t('board.addTask')}
            </button>
          </div>
        )}
      </div>

      <div
        className="flex-1 px-2 pb-2 scrollbar-hide"
        style={{ overflowY: 'auto', minHeight: '50px' }}
      >
        {isCreating && (
          <div
            className="mb-2 p-3 rounded-xl border shadow-sm"
            style={{
              backgroundColor: 'var(--app-layer-surface)',
              borderColor: 'var(--app-border)',
            }}
          >
            <div className="flex items-center gap-2">
              <div className="w-4 h-4 rounded-full border border-gray-500 flex items-center justify-center shrink-0">
                <Check size={10} className="text-gray-500" />
              </div>
              <input
                ref={inputRef}
                autoFocus
                placeholder={t('board.addTaskPlaceholder')}
                className="bg-transparent border-none outline-none text-sm w-full placeholder-gray-500"
                style={{ color: 'var(--app-text-main)' }}
                value={newTaskTitle}
                onChange={(event) => setNewTaskTitle(event.target.value)}
                onKeyDown={handleKeyDown}
                onBlur={() => {
                  if (!newTaskTitle) {
                    setIsCreating(false);
                  }
                }}
              />
            </div>
          </div>
        )}

        <SortableContext items={tasks.map((task) => task.id)} strategy={verticalListSortingStrategy}>
          <div className="flex flex-col gap-2">
            {tasks.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                onClick={() => onTaskClick(task)}
                viewSettings={viewSettings}
                boardStickers={boardStickers}
              />
            ))}
          </div>
        </SortableContext>
      </div>
    </div>
  );
};

const BoardView = ({
  data,
  filteredTasks,
  onDragEnd,
  onTaskClick,
  onAddTask,
  viewSettings,
  boardStickers,
  readOnly,
  canManageStructure = !readOnly,
}) => {
  const { t } = useTranslation('platform');
  const [activeId, setActiveId] = useState(null);
  const [activeDragType, setActiveDragType] = useState(null);
  const [columnColors, setColumnColors] = useState({});
  const { addColumn, updateColumnColor, reorderColumns } = usePlatform();
  const [isAddingColumn, setIsAddingColumn] = useState(false);
  const [newColumnName, setNewColumnName] = useState('');
  const newColumnInputRef = useRef(null);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 5 },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

  const handleDragStart = (event) => {
    const { active } = event;
    setActiveId(active.id);
    setActiveDragType(data.columns[active.id] ? 'column' : 'task');
  };

  const handleInternalDragEnd = (event) => {
    const { active, over } = event;
    setActiveId(null);
    setActiveDragType(null);
    if (!over) return;

    if (data.columns[active.id]) {
      if (!canManageStructure) {
        return;
      }

      if (active.id !== over.id && data.columns[over.id]) {
        reorderColumns(active.id, over.id);
      }
      return;
    }

    onDragEnd(event);
  };

  const renderColumnOverlay = () => {
    if (!activeId || activeDragType !== 'column') return null;
    const column = data.columns[activeId];
    if (!column) return null;

    let overlayTasks = column.taskIds.map((taskId) => data.tasks[taskId]);
    if (filteredTasks) {
      const filteredIds = new Set(filteredTasks.map((task) => task.id));
      overlayTasks = overlayTasks.filter((task) => filteredIds.has(task.id));
    }

    const statusColor = columnColors[activeId] || column.color || '#6B7280';

    return (
      <div
        className="flex flex-col w-72 shrink-0 rounded-2xl rotate-2 cursor-grabbing shadow-2xl pointer-events-none"
        style={{
          backgroundColor: `color-mix(in srgb, ${statusColor} 15%, var(--app-bg))`,
          maxHeight: 'calc(100vh - 140px)',
          marginBottom: '16px',
          overflowY: 'auto',
          overflowX: 'hidden',
        }}
      >
        <div className="shrink-0" style={{ height: '12px', backgroundColor: statusColor, minHeight: '12px' }} />
        <div className="flex items-center justify-between px-3 pt-3 pb-2">
          <div className="flex items-center gap-2">
            <h3 className="text-xs font-bold uppercase tracking-wide" style={{ color: 'var(--app-text-main)' }}>{column.title}</h3>
            <span
              className="text-[10px] font-bold px-1.5 py-0.5 rounded"
              style={{ backgroundColor: 'var(--app-layer-base)', color: 'var(--app-text-secondary)' }}
            >
              {overlayTasks.length}
            </span>
          </div>
        </div>

        <div className="flex-1 px-2 pb-2 scrollbar-hide" style={{ overflowY: 'auto', minHeight: '50px' }}>
          <div className="flex flex-col gap-2">
            {overlayTasks.map((task) => (
              <TaskCardOverlay
                key={task.id}
                task={task}
                isOverlay
                className="!mb-0 mx-1"
                style={{ transform: 'none' }}
                viewSettings={viewSettings}
                boardStickers={boardStickers}
              />
            ))}
          </div>
        </div>
      </div>
    );
  };

  const handleAddColumnSubmit = () => {
    if (newColumnName.trim()) {
      addColumn(newColumnName.trim());
      setNewColumnName('');
      setIsAddingColumn(false);
    } else {
      setIsAddingColumn(false);
    }
  };

  return (
    <DndContext
      sensors={readOnly ? [] : sensors}
      collisionDetection={closestCorners}
      onDragStart={readOnly ? undefined : handleDragStart}
      onDragEnd={readOnly ? undefined : handleInternalDragEnd}
    >
      <div className="flex h-full overflow-x-auto pb-4 gap-4 px-6 custom-scrollbar" style={{ scrollBehavior: 'smooth' }}>
        <SortableContext items={data.columnOrder} strategy={horizontalListSortingStrategy}>
          {data.columnOrder.map((columnId) => {
            const column = data.columns[columnId];
            let tasks = column.taskIds.map((taskId) => data.tasks[taskId]);

            if (filteredTasks) {
              const filteredIds = new Set(filteredTasks.map((task) => task.id));
              tasks = tasks.filter((task) => filteredIds.has(task.id));
            }

            const persistentColor = column.color ? { [column.id]: column.color } : {};
            const combinedColors = { ...columnColors, ...persistentColor };

            return (
              <Column
                key={column.id}
                column={column}
                tasks={tasks}
                onTaskClick={onTaskClick}
                onAddTask={readOnly ? undefined : onAddTask}
                viewSettings={viewSettings}
                columnColors={combinedColors}
                onColumnColorChange={(columnKey, color) => {
                  setColumnColors((prev) => ({ ...prev, [columnKey]: color }));
                  updateColumnColor(columnKey, color);
                }}
                boardStickers={boardStickers}
                readOnly={readOnly}
                canManageStructure={canManageStructure}
              />
            );
          })}
        </SortableContext>

        {!readOnly && canManageStructure && (
          <div className="flex flex-col gap-2 min-w-[200px] shrink-0 pt-0">
            {isAddingColumn ? (
              <div className="w-64 p-3 rounded-xl border border-[var(--app-border)] bg-[var(--app-layer-surface)] shadow-lg animate-in fade-in zoom-in-95 duration-200">
                <input
                  ref={newColumnInputRef}
                  autoFocus
                  value={newColumnName}
                  onChange={(event) => setNewColumnName(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') handleAddColumnSubmit();
                    if (event.key === 'Escape') setIsAddingColumn(false);
                  }}
                  onBlur={handleAddColumnSubmit}
                  placeholder={t('board.createColumnPlaceholder')}
                  className="w-full bg-transparent border-none outline-none text-sm font-medium placeholder-gray-500 text-[var(--app-text-main)] mb-1"
                />
              </div>
            ) : (
              <div className="flex flex-col gap-2 w-56">
                <button
                  onClick={() => {
                    setIsAddingColumn(true);
                    setTimeout(() => newColumnInputRef.current?.focus(), 0);
                  }}
                  className="flex items-center gap-2 w-full px-4 py-3 rounded-xl hover:bg-white/10 transition-all text-left group"
                  style={{
                    backgroundColor: 'var(--app-layer-surface)',
                    border: '1px dashed var(--app-text-secondary)',
                  }}
                >
                  <div
                    className="w-5 h-5 rounded-full flex items-center justify-center shrink-0 group-hover:scale-110 transition-transform"
                    style={{ border: '1.5px solid var(--app-text-secondary)' }}
                  >
                    <Plus size={12} style={{ color: 'var(--app-text-secondary)' }} className="group-hover:text-white" />
                  </div>
                  <span className="text-sm font-medium transition-colors" style={{ color: 'var(--app-text-secondary)' }}>{t('board.createColumn')}</span>
                </button>

                <button
                  className="flex items-center gap-2 w-full px-4 py-3 rounded-xl hover:bg-white/10 transition-all text-left group"
                  style={{
                    backgroundColor: 'var(--app-layer-surface)',
                    border: '1px dashed var(--app-text-secondary)',
                  }}
                >
                  <div className="w-5 h-5 flex items-center justify-center shrink-0">
                    <ClipboardList size={16} style={{ color: 'var(--app-text-secondary)' }} />
                  </div>
                  <span className="text-sm font-medium transition-colors" style={{ color: 'var(--app-text-secondary)' }}>{t('board.createSummary')}</span>
                </button>

                <button
                  className="flex items-center gap-2 w-full px-4 py-3 rounded-xl hover:bg-white/10 transition-all text-left group"
                  style={{
                    backgroundColor: 'var(--app-layer-surface)',
                    border: '1px dashed var(--app-text-secondary)',
                  }}
                >
                  <div className="w-5 h-5 flex items-center justify-center shrink-0">
                    <Monitor size={16} style={{ color: 'var(--app-text-secondary)' }} />
                  </div>
                  <span className="text-sm font-medium transition-colors" style={{ color: 'var(--app-text-secondary)' }}>{t('board.createMirror')}</span>
                </button>
              </div>
            )}
          </div>
        )}
      </div>

      {createPortal(
        <DragOverlay>
          {activeId && activeDragType === 'task' && data.tasks[activeId] ? (
            <TaskCardOverlay
              task={data.tasks[activeId]}
              isOverlay
              className="rotate-3 cursor-grabbing !mb-0 shadow-2xl scale-105"
              viewSettings={viewSettings}
              boardStickers={boardStickers}
            />
          ) : null}
          {renderColumnOverlay()}
        </DragOverlay>,
        document.body,
      )}
    </DndContext>
  );
};

export default BoardView;
