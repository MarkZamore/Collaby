
// Вспомогательное преобразование HEX -> RGB.
export const hexToRgb = (hex) => {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  return result ? {
    r: parseInt(result[1], 16),
    g: parseInt(result[2], 16),
    b: parseInt(result[3], 16)
  } : null;
};

// Вспомогательное преобразование RGB -> HSL для работы с однотонными цветами.
export const rgbToHsl = (r, g, b) => {
  r /= 255;
  g /= 255;
  b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  let h, s, l = (max + min) / 2;

  if (max === min) {
    h = s = 0; // ахроматический цвет
  } else {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    switch (max) {
      case r: h = (g - b) / d + (g < b ? 6 : 0); break;
      case g: h = (b - r) / d + 2; break;
      case b: h = (r - g) / d + 4; break;
    }
    h /= 6;
  }
  return { h, s, l };
};

export const hslToHex = (h, s, l) => {
  let r, g, b;
  if (s === 0) {
    r = g = b = l; // ахроматический цвет
  } else {
    const hue2rgb = (p, q, t) => {
      if (t < 0) t += 1;
      if (t > 1) t -= 1;
      if (t < 1 / 6) return p + (q - p) * 6 * t;
      if (t < 1 / 2) return q;
      if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
      return p;
    };
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
    const p = 2 * l - q;
    r = hue2rgb(p, q, h + 1 / 3);
    g = hue2rgb(p, q, h);
    b = hue2rgb(p, q, h - 1 / 3);
  }
  const toHex = x => {
    const hex = Math.round(x * 255).toString(16);
    return hex.length === 1 ? '0' + hex : hex;
  };
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
};

// Аккуратно корректируем однотонный цвет.
// Меняем только Lightness в HSL, сохраняя Hue и Saturation.
export const adjustSolidColor = (hex, amount) => {
    const rgb = hexToRgb(hex);
    if (!rgb) return hex;
    
    let { h, s, l } = rgbToHsl(rgb.r, rgb.g, rgb.b);
    
    // amount задаётся в долях: 0.05 = +5% к Lightness, -0.05 = -5%.
    // Ограничиваем диапазон, чтобы случайно не уехать в чисто белый или чёрный цвет.
    l = Math.max(0.02, Math.min(0.98, l + amount));
    
    return hslToHex(h, s, l);
};

// Считаем относительную яркость для проверки контраста по WCAG.
export const getLuminance = (r, g, b) => {
  const a = [r, g, b].map((v) => {
    v /= 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  });
  return a[0] * 0.2126 + a[1] * 0.7152 + a[2] * 0.0722;
};

// Подбираем лучший цвет текста на данном фоне: чёрный или белый.
export const getContrastTextColor = (hexColor) => {
  const rgb = hexToRgb(hexColor);
  if (!rgb) return '#000000';
  
  const luminance = getLuminance(rgb.r, rgb.g, rgb.b);
  return luminance > 0.4 ? '#000000' : '#FFFFFF';
};

// Простая корректировка яркости цвета.
export const adjustBrightness = (hex, percent) => {
    var num = parseInt(hex.replace("#",""),16),
    amt = Math.round(2.55 * percent),
    R = (num >> 16) + amt,
    B = (num >> 8 & 0x00FF) + amt,
    G = (num & 0x0000FF) + amt;
    return "#" + (0x1000000 + (R<255?R<1?0:R:255)*0x10000 + (B<255?B<1?0:B:255)*0x100 + (G<255?G<1?0:G:255)).toString(16).slice(1);
};
