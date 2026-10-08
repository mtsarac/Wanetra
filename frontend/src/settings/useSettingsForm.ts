import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { api, type Setting, type SettingsResponse } from '../lib/api'
import { buildChanges, type DraftValues } from './types'

export type UseSettingsFormOptions = {
  settings: Setting[]
  activeKeys: (draft: DraftValues) => string[]
  successMessage: string
}

export function useSettingsForm({ settings, activeKeys, successMessage }: UseSettingsFormOptions) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<DraftValues>({})
  const [statusMessage, setStatusMessage] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)
  const [resettingKey, setResettingKey] = useState<string | null>(null)

  const settingsMap = new Map<string, Setting>()
  for (const s of settings) settingsMap.set(s.key, s)

  const currentActiveKeys = activeKeys(draft)
  const { updates, errors, isDirty } = buildChanges(settingsMap, draft, currentActiveKeys)

  const saveMutation = useMutation({
    mutationFn: (values: Record<string, string | number | boolean | null>) => api.updateSettings(values),
    onSuccess: (data: SettingsResponse) => {
      queryClient.setQueryData(['settings'], data)
      setStatusMessage(successMessage)
      setServerError(null)
      setDraft((prev) => {
        const next: DraftValues = { ...prev }
        for (const k of currentActiveKeys) delete next[k]
        return next
      })
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
    onError: (err: Error) => {
      setServerError(err.message)
      setStatusMessage(null)
    },
  })

  const resetMutation = useMutation({
    mutationFn: (key: string) => api.updateSettings({ [key]: null }),
    onSuccess: (data: SettingsResponse, key: string) => {
      queryClient.setQueryData(['settings'], data)
      setStatusMessage(`Reset to default.`)
      setServerError(null)
      setDraft((prev) => {
        const next = { ...prev }
        delete next[key]
        return next
      })
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
    onError: (err: Error) => {
      setServerError(err.message)
      setStatusMessage(null)
    },
    onSettled: () => setResettingKey(null),
  })

  function edit(key: string, value: string | boolean) {
    setStatusMessage(null)
    setServerError(null)
    setDraft((prev) => ({ ...prev, [key]: value }))
  }

  function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (!isDirty || Object.keys(errors).length > 0 || saveMutation.isPending) return
    setStatusMessage(null)
    setServerError(null)
    saveMutation.mutate(updates)
  }

  function reset(key: string) {
    if (resetMutation.isPending || saveMutation.isPending) return
    setStatusMessage(null)
    setServerError(null)
    setResettingKey(key)
    resetMutation.mutate(key)
  }

  return {
    draft,
    settingsMap,
    errors,
    isDirty,
    statusMessage,
    serverError,
    isSaving: saveMutation.isPending,
    resettingKey,
    edit,
    submit,
    reset,
  }
}
