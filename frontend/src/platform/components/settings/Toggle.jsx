// Переключатель настроек Collaby.
import React from 'react';
import { cn } from '../../../shared/utils/cn';

const Toggle = ({ value, onChange }) => {
  return (
    <button 
        onClick={() => onChange(!value)}
        className={cn(
        "w-12 h-6 rounded-full transition-colors relative shadow-inner focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-accent",
        value ? "bg-accent" : "bg-slate-300 dark:bg-slate-600"
        )}
    >
        <div className={cn(
        "absolute top-1/2 -translate-y-1/2 w-4 h-4 rounded-full bg-white transition-all duration-200 shadow-sm",
        value ? "left-[calc(100%-1.25rem)]" : "left-1"
        )} />
    </button>
  );
};

export default Toggle;
