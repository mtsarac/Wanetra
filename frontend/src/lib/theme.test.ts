import { afterEach, describe, expect, it } from 'vitest'
import { applyTheme, defaultTheme, readTheme, themes, themeStorageKey } from './theme'

afterEach(() => {
  localStorage.clear()
  delete document.documentElement.dataset.theme
  delete document.documentElement.dataset.themeMode
})

describe('theme preference', () => {
  it('persists the selected theme and applies palette mode', () => {
    applyTheme('catppuccin-mocha')

    expect(localStorage.getItem(themeStorageKey)).toBe('catppuccin-mocha')
    expect(document.documentElement.dataset.theme).toBe('catppuccin-mocha')
    expect(document.documentElement.dataset.themeMode).toBe('dark')
    expect(readTheme()).toBe('catppuccin-mocha')
  })

  it('falls back to default for unknown stored theme IDs', () => {
    localStorage.setItem(themeStorageKey, 'not-a-theme')

    expect(readTheme()).toBe(defaultTheme)
  })

  it('defines 21 distinct themes and applies each correctly', () => {
    expect(themes).toHaveLength(21)
    for (const theme of themes) {
      applyTheme(theme.id)
      expect(document.documentElement.dataset.theme).toBe(theme.id)
      expect(document.documentElement.dataset.themeMode).toBe(theme.mode)
      expect(readTheme()).toBe(theme.id)
    }
  })
})
