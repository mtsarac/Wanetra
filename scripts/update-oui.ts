// Rebuilds backend/Wanetra.Infrastructure/Lan/oui.txt from the IEEE MA-L registry.
//   bun scripts/update-oui.ts            download the registry
//   bun scripts/update-oui.ts oui.csv    use a file you already downloaded
// Output: one "AABBCC<TAB>Organization" line per assignment, sorted by prefix.
import { writeFileSync, readFileSync } from 'node:fs'

const SOURCE = 'https://standards-oui.ieee.org/oui/oui.csv'
const OUTPUT = new URL('../backend/Wanetra.Infrastructure/Lan/oui.txt', import.meta.url)

function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let quoted = false
  for (let i = 0; i < text.length; i++) {
    const c = text[i]
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') { field += '"'; i++ }
      else if (c === '"') quoted = false
      else field += c
    } else if (c === '"') quoted = true
    else if (c === ',') { row.push(field); field = '' }
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++
      row.push(field); field = ''
      if (row.length > 1 || row[0] !== '') rows.push(row)
      row = []
    } else field += c
  }
  if (field !== '' || row.length) { row.push(field); rows.push(row) }
  return rows
}

const path = process.argv[2]
const csv = path ? readFileSync(path, 'utf8') : await (await fetch(SOURCE)).text()

const entries = new Map<string, string>()
for (const [registry, assignment, organization] of parseCsv(csv).slice(1)) {
  if (registry !== 'MA-L' || !/^[0-9A-F]{6}$/i.test(assignment ?? '')) continue
  const name = (organization ?? '').replace(/\s+/g, ' ').trim()
  if (name) entries.set(assignment.toUpperCase(), name)
}

const lines = [...entries].sort(([a], [b]) => (a < b ? -1 : 1)).map(([prefix, name]) => `${prefix}\t${name}`)
writeFileSync(OUTPUT, lines.join('\n') + '\n')
console.log(`Wrote ${lines.length} vendors`)
