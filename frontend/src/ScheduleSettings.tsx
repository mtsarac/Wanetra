import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Schedule } from './lib/api'
import { Button } from './components/ui/button'

type ScheduleDraft = Pick<Schedule, 'enabled' | 'cronExpression' | 'timezone'>

function displayTime(value: string) {
  return new Date(value).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })
}

export default function ScheduleSettings() {
  const client = useQueryClient()
  const schedule = useQuery({ queryKey: ['schedule'], queryFn: api.getSchedule })
  const [draft, setDraft] = useState<ScheduleDraft | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const values = draft ?? {
    enabled: schedule.data?.enabled ?? false,
    cronExpression: schedule.data?.cronExpression ?? '*/30 * * * *',
    timezone: schedule.data?.timezone ?? 'Europe/Istanbul',
  }

  const save = useMutation({
    mutationFn: api.updateSchedule,
    onSuccess: async (saved) => {
      setDraft({ enabled: saved.enabled, cronExpression: saved.cronExpression, timezone: saved.timezone })
      setMessage('Schedule saved.')
      await client.invalidateQueries({ queryKey: ['schedule'] })
    },
    onError: (error: Error) => setMessage(error.message),
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessage(null)
    save.mutate(values)
  }

  return (
    <section className="panel settings-panel">
      <div className="panel-head"><div><p className="kicker">Schedule</p><h2>Automatic speed tests</h2></div><span className="muted">Cron uses the selected IANA timezone</span></div>
      {schedule.isLoading ? <div className="empty">Loading schedule…</div> : (
        <form onSubmit={submit}>
          <label className="setting-check"><input type="checkbox" checked={values.enabled} onChange={(event) => setDraft({ ...values, enabled: event.target.checked })} /> Enable scheduled tests</label>
          <div className="settings-grid">
            <label><span>Cron expression</span><input value={values.cronExpression} onChange={(event) => setDraft({ ...values, cronExpression: event.target.value })} placeholder="*/30 * * * *" required /></label>
            <label><span>Timezone</span><input value={values.timezone} onChange={(event) => setDraft({ ...values, timezone: event.target.value })} placeholder="Europe/Istanbul" required /></label>
          </div>
          <div className="form-actions"><Button type="submit" disabled={save.isPending || schedule.isLoading}>{save.isPending ? 'Saving…' : 'Save schedule'}</Button>{message && <span className={save.isError ? 'failure' : 'success'} role="status">{message}</span>}</div>
        </form>
      )}
      {schedule.data?.enabled && <div className="next-runs"><strong>Upcoming runs</strong>{schedule.data.nextRuns.length ? <ol>{schedule.data.nextRuns.map((run) => <li key={run}>{displayTime(run)}</li>)}</ol> : <p className="muted">No upcoming run is currently available.</p>}</div>}
      {(schedule.error || save.error) && <div className="error" role="alert">{(schedule.error ?? save.error)?.message}</div>}
    </section>
  )
}
