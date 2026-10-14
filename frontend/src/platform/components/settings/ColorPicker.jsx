// Color picker Collaby — минимальная обёртка над native color input и набором preset-цветов.
import React from 'react';
import { cn } from '../../../shared/utils/cn';

const ColorPicker = ({ label, value, onChange, presets = [], icon: Icon }) => {
  return (
    <div className="flex items-center justify-between py-1 group">
        <label className="flex items-center gap-3 text-sm font-medium text-slate-700 dark:text-slate-300">
            {Icon && <Icon size={16} className="text-slate-400" />}
            {label}
        </label>
        
        <div className="flex items-center gap-3">
             {/* Ряд preset-цветов. При желании можно сделать его hover-only или всегда видимым.
                 Linear shows only the hex value and a swab, but let's add quick presets if space permits
             */}
            <span className="text-xs font-mono text-slate-500 uppercase">{value}</span>
            
            <div className="relative overflow-hidden w-24 h-8 rounded-lg border border-white/10 shadow-sm ring-1 ring-white/5 transition-transform hover:scale-105">
                <input 
                    type="color" 
                    value={value}
                    onChange={(e) => onChange(e.target.value)}
                    className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[150%] h-[150%] p-0 border-0 cursor-pointer" 
                />
            </div>
        </div>
    </div>
  );
};

export default ColorPicker;
