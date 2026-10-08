import type { Setting, SettingUpdates } from '../lib/api'

export const SPEEDTEST_ENGINE_KEY = 'speedtest.engine'
export const OOKLA_ACCEPT_LICENSE_KEY = 'speedtest.ookla.acceptLicense'
export const RETENTION_DAYS_KEY = 'retention.days'

export type EngineId = 'librespeed' | 'cloudflare' | 'ookla'

export type EngineOption = {
  id: EngineId
  name: string
  description: string
}

export const ENGINE_OPTIONS: EngineOption[] = [
  {
    id: 'librespeed',
    name: 'LibreSpeed',
    description: 'Runs librespeed-cli. Measures download, upload, ping, and jitter against LibreSpeed servers.',
  },
  {
    id: 'cloudflare',
    name: 'Cloudflare',
    description: 'Runs cfspeedtest against speed.cloudflare.com. Packet loss is not reported by this CLI.',
  },
  {
    id: 'ookla',
    name: 'Ookla',
    description: 'Runs the official Ookla Speedtest CLI. Measures download, upload, latency, jitter, and packet loss.',
  },
]

export const ENGINE_FIELD_KEYS: Record<EngineId, string[]> = {
  librespeed: [
    'speedtest.librespeed.timeoutSeconds',
    'speedtest.librespeed.serverId',
    'speedtest.librespeed.executablePath',
  ],
  cloudflare: [
    'speedtest.cloudflare.timeoutSeconds',
    'speedtest.cloudflare.executablePath',
  ],
  ookla: [
    'speedtest.ookla.timeoutSeconds',
    'speedtest.ookla.serverId',
    'speedtest.ookla.executablePath',
  ],
}

export type FieldMeta = {
  label: string
  help: string
}

export const FIELD_METAS: Record<string, FieldMeta> = {
  'speedtest.librespeed.timeoutSeconds': {
    label: 'Timeout (seconds)',
    help: 'Maximum duration before Wanetra stops the test process.',
  },
  'speedtest.librespeed.serverId': {
    label: 'Server ID',
    help: 'Specific LibreSpeed server ID. Leave empty for automatic server selection.',
  },
  'speedtest.librespeed.executablePath': {
    label: 'Executable path',
    help: 'Binary path or command name for librespeed-cli.',
  },
  'speedtest.cloudflare.timeoutSeconds': {
    label: 'Timeout (seconds)',
    help: 'Maximum duration before Wanetra stops the test process.',
  },
  'speedtest.cloudflare.executablePath': {
    label: 'Executable path',
    help: 'Binary path or command name for cfspeedtest.',
  },
  'speedtest.ookla.timeoutSeconds': {
    label: 'Timeout (seconds)',
    help: 'Maximum duration before Wanetra stops the test process.',
  },
  'speedtest.ookla.serverId': {
    label: 'Server ID',
    help: 'Specific Ookla server ID. Leave empty for automatic server selection.',
  },
  'speedtest.ookla.executablePath': {
    label: 'Executable path (fallback)',
    help: 'Optional fallback binary if Wanetra cannot download the official CLI directly.',
  },
  [RETENTION_DAYS_KEY]: {
    label: 'Keep results for (days)',
    help: 'Measurements older than this are deleted automatically. Degradation events are kept.',
  },
}

export type DraftValues = Record<string, string | boolean>

export function settingToDisplay(setting: Setting): string {
  if (setting.value === null || setting.value === undefined) return ''
  return String(setting.value)
}

export function validateInteger(raw: string, setting: Setting): string | null {
  const trimmed = raw.trim()
  if (trimmed === '') {
    if (setting.nullable) return null
    return `${setting.key} is required.`
  }

  if (!/^-?\d+$/.test(trimmed)) {
    return 'Enter a whole number.'
  }

  const num = Number(trimmed)
  if (!Number.isSafeInteger(num)) {
    return 'Value is out of range.'
  }

  if (setting.min !== null && setting.max !== null) {
    if (num < setting.min || num > setting.max) {
      return `Must be between ${setting.min} and ${setting.max}.`
    }
  } else if (setting.min !== null && num < setting.min) {
    return `Must be at least ${setting.min}.`
  } else if (setting.max !== null && num > setting.max) {
    return `Must be at most ${setting.max}.`
  }

  return null
}

export function buildChanges(
  settingsMap: Map<string, Setting>,
  draft: DraftValues,
  keys: string[],
): { updates: SettingUpdates; errors: Record<string, string>; isDirty: boolean } {
  const updates: SettingUpdates = {}
  const errors: Record<string, string> = {}
  let isDirty = false

  for (const key of keys) {
    const setting = settingsMap.get(key)
    if (!setting || setting.locked) continue

    const draftValue = draft[key]
    if (draftValue === undefined) continue

    if (setting.type === 'integer') {
      const text = String(draftValue).trim()
      const serverText = setting.value === null || setting.value === undefined ? '' : String(setting.value)
      if (text === serverText) continue

      const err = validateInteger(text, setting)
      if (err) {
        errors[key] = err
        isDirty = true
      } else {
        const parsed = text === '' ? null : Number(text)
        updates[key] = parsed
        isDirty = true
      }
    } else if (setting.type === 'choice') {
      const choice = String(draftValue)
      if (choice !== setting.value) {
        updates[key] = choice
        isDirty = true
      }
    } else if (setting.type === 'boolean') {
      const bool = Boolean(draftValue)
      if (bool !== setting.value) {
        updates[key] = bool
        isDirty = true
      }
    } else if (setting.type === 'text') {
      const text = String(draftValue)
      const current = setting.value === null ? '' : String(setting.value)
      if (text !== current) {
        updates[key] = text === '' && setting.nullable ? null : text
        isDirty = true
      }
    }
  }

  return { updates, errors, isDirty }
}

export function sourceText(setting: Setting): string {
  if (setting.source === 'environment') {
    return `Set by ${setting.environmentVariable}`
  }
  if (setting.source === 'stored') {
    return 'Saved'
  }
  return 'Default'
}

export function lockReasonText(setting: Setting): string | null {
  if (!setting.locked) return null
  if (setting.lockReason === 'restricted') {
    return `Executable paths can only be set with environment variables (${setting.environmentVariable}) until authentication is available.`
  }
  return `Set by environment variable ${setting.environmentVariable}; remove it to change this here.`
}

export function formatTimestamp(iso: string | null): string | null {
  if (!iso) return null
  try {
    const d = new Date(iso)
    if (Number.isNaN(d.getTime())) return null
    return d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })
  } catch {
    return null
  }
}
