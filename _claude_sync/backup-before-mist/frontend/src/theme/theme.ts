// Meal Management — theme tokens for use in TypeScript/React
// (charts, inline styles, or to feed a UI library such as MUI/AntD).
// Keep in sync with tokens.css.
// Palette: Mist #dce2e5 · Cream #f4ebce · Butter #f9d689 · Orange #f09a32 · Sage #8bab6a

export const palette = {
  mist: '#dce2e5',
  cream: '#f4ebce',
  butter: '#f9d689',
  orange: '#f09a32',
  sage: '#8bab6a',
  white: '#ffffff',
} as const;

export const theme = {
  color: {
    primary: '#4f7038',          // sage, deepened for text/icons
    primaryHover: '#44612f',
    primarySubtle: '#eef3e7',
    primarySoft: '#e3edd7',      // light primary buttons
    primarySoftHover: '#d6e5c5',
    primarySoftFg: '#3d5a28',

    accent: palette.orange,      // fills, icons, chips
    accentStrong: '#a3590b',     // orange text on white
    accentSubtle: '#fdf0dc',
    accentSoftFg: '#8f4e0a',

    bg: '#faf7ee',
    surface: palette.white,
    surfaceMuted: '#f8f6f0',
    navBg: '#fbf6e6',
    navHover: palette.cream,
    navActive: '#e3edd7',
    border: '#e3e3d9',
    borderStrong: '#cdd3d1',

    text: '#2f3a2c',
    textMuted: '#6b7365',
    textSoft: '#46503f',

    success: '#4a6a33',
    warning: '#a66a0c',
    danger: '#b9412f',
    info: '#4f6773',
  },
  meal: {
    on:     { bg: '#e3edd7', fg: '#4a6a33', dot: palette.sage },
    off:    { bg: '#eef0ef', fg: '#6b7365', dot: '#a9b2ad' },
    locked: { bg: '#f6f4ec' },
  },
  mealWarm: { base: palette.orange, strong: '#9a560c', subtle: '#fdf3dc', border: '#f6dda0' },
  chip: {
    mint: { bg: '#e6efdc', fg: '#4c7034' },    // sage
    sky: { bg: '#e8edf0', fg: '#4f6773' },     // mist
    warm: { bg: '#fdeedb', fg: '#a65c0b' },    // orange
    rose: { bg: '#fae6df', fg: '#a44c35' },    // clay
    violet: { bg: '#fcf1d2', fg: '#82620c' },  // butter
  },
  cost: {
    employee: palette.orange,
    company: palette.sage,
  },
  radius: { sm: 6, md: 10, lg: 16, pill: 999 },
  font: {
    sans: '"Inter", "Segoe UI", system-ui, -apple-system, Roboto, "Noto Sans Bengali", sans-serif',
  },
} as const;

export type AppTheme = typeof theme;
export default theme;
