// Meal Management — theme tokens for use in TypeScript/React
// (charts, inline styles, or to feed a UI library such as MUI/AntD).
// Keep in sync with tokens.css.

export const palette = {
  teal: '#2b6777',
  mist: '#c8d8e4',
  white: '#ffffff',
  smoke: '#f2f2f2',
  green: '#52ab98',
} as const;

export const theme = {
  color: {
    primary: palette.teal,
    primaryHover: '#235663',
    primaryActive: '#1c4550',
    primaryContrast: '#ffffff',
    primarySubtle: '#e6eff4',
    primarySoft: '#dcebf0',        // light primary buttons
    primarySoftHover: '#cfe3ea',
    primarySoftFg: '#1f5563',

    accent: palette.green,       // fills, icons, chips (not for small text on white)
    accentStrong: '#33806f',     // AA-safe green for text / white-on-green buttons
    accentSubtle: '#e3f2ee',
    accentSoftFg: '#24675a',

    bg: palette.smoke,
    surface: palette.white,
    surfaceMuted: '#f7f9fa',
    navBg: '#f8fbfc',
    navHover: '#eef4f7',
    navActive: '#e3f2ee',
    border: palette.mist,
    borderStrong: '#a9c0d0',
    selected: palette.mist,

    text: '#1e3a42',
    textMuted: '#5b7480',
    textSoft: '#3d5a64',

    success: '#33806f',
    warning: '#b7791f',
    danger: '#c0392b',
    info: palette.teal,
  },
  meal: {
    on:     { bg: '#e3f2ee', fg: '#33806f', dot: palette.green },
    off:    { bg: palette.smoke, fg: '#5b7480', dot: '#a9b6bd' },
    locked: { bg: '#eef3f6' },
  },
  mealWarm: { base: '#e9963e', strong: '#a2591a', subtle: '#fff3e4', border: '#f6d9b5' },
  chip: {
    mint: { bg: '#e3f2ee', fg: '#2f7f6d' },
    sky: { bg: '#e6eff4', fg: '#2b6777' },
    warm: { bg: '#fff3e4', fg: '#c06f22' },
    rose: { bg: '#fdecea', fg: '#b4473b' },
    violet: { bg: '#efedf8', fg: '#5d55a0' },
  },
  cost: {
    employee: palette.teal,
    company: palette.green,
  },
  radius: { sm: 6, md: 10, lg: 16, pill: 999 },
  font: {
    sans: '"Inter", "Segoe UI", system-ui, -apple-system, Roboto, "Noto Sans Bengali", sans-serif',
  },
} as const;

export type AppTheme = typeof theme;
export default theme;
