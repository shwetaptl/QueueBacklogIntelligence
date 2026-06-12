import { useState } from 'react'
import { useFetch }           from '../hooks'
import IncidentBanner         from '../components/IncidentBanner'
import StatusRow              from '../components/StatusRow'
import ActiveCountChart       from '../components/ActiveCountChart'
import RootCauseTimeline      from '../components/RootCauseTimeline'
import IncidentTable          from '../components/IncidentTable'

const API_BASE_URL = '/api'
const QUEUE_NAME   = 'qbi-queue'

const PRESET_MINUTES = { '15m': 15, '30m': 30, '1h': 60, '6h': 360, '24h': 1440, '7d': 10080 }

function buildTimeQuery(range) {
  if (range.preset === 'custom' && range.from && range.to)
    return `?from=${encodeURIComponent(range.from + 'T00:00:00Z')}&to=${encodeURIComponent(range.to + 'T23:59:59Z')}`
  return `?minutes=${PRESET_MINUTES[range.preset] ?? 30}`
}

export default function Dashboard() {
  const [timeRange,    setTimeRange]    = useState({ preset: '30m' })
  const [dismissedSev, setDismissedSev] = useState(null)

  const tq = buildTimeQuery(timeRange)

  const statusR  = useFetch(`${API_BASE_URL}/queues/${QUEUE_NAME}/status`,         30_000)
  const historyR = useFetch(`${API_BASE_URL}/queues/${QUEUE_NAME}/history${tq}`,   30_000)
  const alertsR  = useFetch(`${API_BASE_URL}/queues/${QUEUE_NAME}/alerts`,         60_000)

  function handleRefresh() {
    statusR.refetch()
    historyR.refetch()
    alertsR.refetch()
  }

  const openAlert     = (alertsR.data ?? []).find(a => a.status === 'Open')
  const breachStartMs = openAlert ? new Date(openAlert.openedAtUtc).getTime() : null

  // Reset dismissed severity when it changes (escalation or recovery)
  const currentSev = statusR.data?.alertSeverity
  if (dismissedSev && currentSev !== dismissedSev) {
    setDismissedSev(null)
  }

  return (
    <div className="space-y-4">
      <IncidentBanner
        status={statusR.data}
        breachStartMs={breachStartMs}
        dismissedSev={dismissedSev}
        onDismiss={() => setDismissedSev(currentSev)}
      />
      <StatusRow
        status={statusR.data}
        loading={statusR.loading}
        error={statusR.error}
        fetchedAt={statusR.fetchedAt}
        breachStartMs={breachStartMs}
        onRefresh={handleRefresh}
      />
      <ActiveCountChart
        history={historyR.data}
        slaMinutes={statusR.data?.slaMinutes}
        alertSeverity={statusR.data?.alertSeverity}
        loading={historyR.loading}
        error={historyR.error}
        timeRange={timeRange}
        onTimeRangeChange={setTimeRange}
      />
      <RootCauseTimeline history={historyR.data} />
      <IncidentTable
        alerts={alertsR.data}
        loading={alertsR.loading}
        error={alertsR.error}
      />
    </div>
  )
}
