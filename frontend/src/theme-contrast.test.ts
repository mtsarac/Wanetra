/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

function srgbLuminance(r: number, g: number, b: number): number {
  const [rs, gs, bs] = [r, g, b].map((c) => {
    const v = c / 255
    return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4)
  })
  return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs
}

function hexToRgb(hex?: string): [number, number, number] | null {
  if (!hex) return null
  let clean = hex.trim()
  if (clean.startsWith('#')) clean = clean.slice(1)
  if (clean.length === 3) clean = clean.split('').map((x) => x + x).join('')
  if (clean.length === 6) {
    const num = parseInt(clean, 16)
    return [(num >> 16) & 255, (num >> 8) & 255, num & 255]
  }
  return null
}

function contrastRatio(c1?: string, c2?: string): number {
  const rgb1 = hexToRgb(c1)
  const rgb2 = hexToRgb(c2)
  if (!rgb1 || !rgb2) return 1
  const l1 = srgbLuminance(...rgb1)
  const l2 = srgbLuminance(...rgb2)
  const [lighter, darker] = l1 > l2 ? [l1, l2] : [l2, l1]
  return (lighter + 0.05) / (darker + 0.05)
}

function parseBlocks(css: string): { sel: string; vars: Record<string, string> }[] {
  const blocks: { sel: string; vars: Record<string, string> }[] = []
  const re = /([^{]+)\{([^}]+)\}/g
  let match: RegExpExecArray | null
  while ((match = re.exec(css))) {
    const sel = (match[1].split(/[;}]/).pop() ?? '').trim()
    const body = match[2]
    const vars: Record<string, string> = {}
    const varRe = /(--[a-z0-9-]+)\s*:\s*([^;]+);/gi
    let vm: RegExpExecArray | null
    while ((vm = varRe.exec(body))) {
      vars[vm[1].trim()] = vm[2].trim()
    }
    if (Object.keys(vars).length > 0) blocks.push({ sel, vars })
  }
  return blocks
}

const themeDefs = [
  { id: 'default', mode: 'light' },
  { id: 'catppuccin-latte', mode: 'light' },
  { id: 'rose-pine-dawn', mode: 'light' },
  { id: 'nord-light', mode: 'light' },
  { id: 'solarized-light', mode: 'light' },
  { id: 'gruvbox-light', mode: 'light' },
  { id: 'catppuccin-mocha', mode: 'dark' },
  { id: 'dracula', mode: 'dark' },
  { id: 'rose-pine', mode: 'dark' },
  { id: 'rose-pine-moon', mode: 'dark' },
  { id: 'tokyo-night', mode: 'dark' },
  { id: 'nord', mode: 'dark' },
  { id: 'one-dark', mode: 'dark' },
  { id: 'gruvbox-dark', mode: 'dark' },
  { id: 'solarized-dark', mode: 'dark' },
  { id: 'everforest', mode: 'dark' },
  { id: 'kanagawa', mode: 'dark' },
  { id: 'monokai', mode: 'dark' },
  { id: 'monokai-pro', mode: 'dark' },
  { id: 'material-dark', mode: 'dark' },
  { id: 'palenight', mode: 'dark' },
] as const

function resolveThemeVars(themeId: string, cssText: string): Record<string, string> {
  const def = themeDefs.find((t) => t.id === themeId)
  if (!def) throw new Error(`Unknown theme ${themeId}`)
  const blocks = parseBlocks(cssText)
  const resolved: Record<string, string> = {}
  for (const b of blocks) {
    let match = false
    if (b.sel === ':root') match = true
    else if (b.sel.includes(`data-theme-mode="${def.mode}"`) && !b.sel.includes('data-theme=')) match = true
    else if (b.sel.split(',').some((part) => part.includes(`data-theme="${themeId}"`))) match = true
    if (match) {
      Object.assign(resolved, b.vars)
    }
  }
  let changed = true
  let passes = 0
  while (changed && passes < 10) {
    changed = false
    passes++
    for (const [k, v] of Object.entries(resolved)) {
      if (typeof v === 'string' && v.includes('var(')) {
        const nv = v.replace(/var\((--[a-z0-9-]+)\)/g, (_, name) => resolved[name] || _)
        if (nv !== v) {
          resolved[k] = nv
          changed = true
        }
      }
    }
  }
  return resolved
}

const css = readFileSync(`${process.cwd()}/src/index.css`, 'utf8')

describe('WCAG 2.2 AA theme contrast across all 21 themes', () => {
  themeDefs.forEach(({ id, mode }) => {
    describe(`theme: ${id} (${mode})`, () => {
      const vars = resolveThemeVars(id, css)
      const surfaces: [string, string][] = [
        ['canvas', vars['--canvas']],
        ['surface', vars['--surface']],
        ['surface-soft', vars['--surface-soft']],
      ]

      surfaces.forEach(([sName, sColor]) => {
        it(`has >=4.5:1 text on ${sName}`, () => {
          expect(contrastRatio(vars['--text'], sColor)).toBeGreaterThanOrEqual(4.5)
        })

        it(`has >=4.5:1 muted text on ${sName}`, () => {
          expect(contrastRatio(vars['--muted'], sColor)).toBeGreaterThanOrEqual(4.5)
        })

        it(`has >=4.5:1 ink on ${sName}`, () => {
          expect(contrastRatio(vars['--ink'], sColor)).toBeGreaterThanOrEqual(4.5)
        })

        it(`has >=4.5:1 blue link text on ${sName}`, () => {
          expect(contrastRatio(vars['--blue'], sColor)).toBeGreaterThanOrEqual(4.5)
        })

        it(`has >=3.0:1 focus ring against ${sName}`, () => {
          const focus = vars['--focus'] || vars['--ink']
          expect(contrastRatio(focus, sColor)).toBeGreaterThanOrEqual(3.0)
        })
      })

      it('has >=4.5:1 green status on green-bg', () => {
        expect(contrastRatio(vars['--green'], vars['--green-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 yellow status on yellow-bg', () => {
        expect(contrastRatio(vars['--yellow'], vars['--yellow-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 red status on red-bg', () => {
        expect(contrastRatio(vars['--red'], vars['--red-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 blue status on blue-bg', () => {
        expect(contrastRatio(vars['--blue'], vars['--blue-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 alert text on yellow-bg', () => {
        expect(contrastRatio(vars['--alert-text'], vars['--yellow-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 alert muted text on yellow-bg', () => {
        expect(contrastRatio(vars['--alert-muted'], vars['--yellow-bg'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=4.5:1 canvas on ink for buttons and selected tabs', () => {
        expect(contrastRatio(vars['--canvas'], vars['--ink'])).toBeGreaterThanOrEqual(4.5)
      })

      it('has >=3.0:1 field border against surfaces', () => {
        expect(contrastRatio(vars['--field'], vars['--surface'])).toBeGreaterThanOrEqual(3.0)
        expect(contrastRatio(vars['--field'], vars['--surface-soft'])).toBeGreaterThanOrEqual(3.0)
      })
    })
  })
})
