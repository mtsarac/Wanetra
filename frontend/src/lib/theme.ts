export const themes = [
  { id: 'default', name: 'Default', mode: 'light', swatch: '#4f83f1' },
  { id: 'catppuccin-latte', name: 'Catppuccin Latte', mode: 'light', swatch: '#8839ef' },
  { id: 'rose-pine-dawn', name: 'Rosé Pine Dawn', mode: 'light', swatch: '#907aa9' },
  { id: 'nord-light', name: 'Nord Light', mode: 'light', swatch: '#5e81ac' },
  { id: 'solarized-light', name: 'Solarized Light', mode: 'light', swatch: '#268bd2' },
  { id: 'gruvbox-light', name: 'Gruvbox Light', mode: 'light', swatch: '#458588' },
  { id: 'catppuccin-mocha', name: 'Catppuccin Mocha', mode: 'dark', swatch: '#cba6f7' },
  { id: 'dracula', name: 'Dracula', mode: 'dark', swatch: '#bd93f9' },
  { id: 'rose-pine', name: 'Rosé Pine', mode: 'dark', swatch: '#c4a7e7' },
  { id: 'rose-pine-moon', name: 'Rosé Pine Moon', mode: 'dark', swatch: '#c4a7e7' },
  { id: 'tokyo-night', name: 'Tokyo Night', mode: 'dark', swatch: '#7aa2f7' },
  { id: 'nord', name: 'Nord', mode: 'dark', swatch: '#88c0d0' },
  { id: 'one-dark', name: 'One Dark', mode: 'dark', swatch: '#61afef' },
  { id: 'gruvbox-dark', name: 'Gruvbox Dark', mode: 'dark', swatch: '#b8bb26' },
  { id: 'solarized-dark', name: 'Solarized Dark', mode: 'dark', swatch: '#268bd2' },
  { id: 'everforest', name: 'Everforest', mode: 'dark', swatch: '#a7c080' },
  { id: 'kanagawa', name: 'Kanagawa', mode: 'dark', swatch: '#7e9cd8' },
  { id: 'monokai', name: 'Monokai', mode: 'dark', swatch: '#f92672' },
  { id: 'monokai-pro', name: 'Monokai Pro', mode: 'dark', swatch: '#ff6188' },
  { id: 'material-dark', name: 'Material Dark', mode: 'dark', swatch: '#80cbc4' },
  { id: 'palenight', name: 'Palenight', mode: 'dark', swatch: '#c792ea' },
] as const

export type ThemeId = (typeof themes)[number]['id']
export const themeStorageKey = 'wanetra-theme'
export const defaultTheme: ThemeId = 'default'

export function readTheme(): ThemeId {
  try {
    const saved = localStorage.getItem(themeStorageKey)
    return themes.some((theme) => theme.id === saved) ? saved as ThemeId : defaultTheme
  } catch {
    return defaultTheme
  }
}

export function applyTheme(id: ThemeId) {
  const theme = themes.find((item) => item.id === id) ?? themes[0]
  document.documentElement.dataset.theme = theme.id
  document.documentElement.dataset.themeMode = theme.mode
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', getComputedStyle(document.documentElement).getPropertyValue('--canvas').trim())
  try {
    localStorage.setItem(themeStorageKey, theme.id)
  } catch {
    // Keep the active-page selection when browser storage is unavailable.
  }
}
