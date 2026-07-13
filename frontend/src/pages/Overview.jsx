import { useMemo } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useFetch } from '../hooks'
import { trendArrow, rootCauseLabel } from '../utils'

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:7071/api'

function Skeleton({ className }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className}`} />
}

function KpiTile({ label, value, sub, accent }) {
  const styles = {
    blue:  { card: 'border-blue-100 bg-blue-50',   val: 'text-blue-700' },
    red:   { card: 'border-red-100 bg-red-50',     val: 'text-red-700'  },
    amber: { card: 'border-amber-100 bg-amber-50', val: 'text-amber-700'},
    green: { card: 'border-green-100 bg-green-50', val: 'text-green-700'},
  }
  const s = styles[accent] ?? styles.blue
  return (
    <div className={`flex-1 min-w-0 border-2 rounded-xl p-5 ${s.card}`}>
      <p className="text-[11px] font-semibold text-gray-500 uppercase tracking-wider mb-2">{label}</p>
      <p className={`text-3xl font-bold tabular-nums leading-none ${s.val}`}>{value}</p>
      {sub && <p className="text-xs text-gray-500 mt-2">{sub}</p>}
    </div>
  )
}

const SEV_ORDER = { Critical: 0, Warning: 1, None: 2 }

function rowClass(sev) {
  return sev === 'Critical'
    ? 'bg-red-50 hover:bg-red-100 border-red-100'
    : sev === 'Warning'
    ? 'bg-amber-50 hover:bg-amber-100 border-amber-100'
    : 'hover:bg-gray-50 border-gray-100'
}

function StatusBadge({ sev }) {
  if (sev === 'Critical')
    return <span className="inline-flex items-center gap-1 text-red-700 font-semibold text-xs px-2 py-0.5 bg-red-100 rounded-full">Critical</span>
  if (sev === 'Warning')
    return <span className="inline-flex items-center gap-1 text-amber-700 font-semibold text-xs px-2 py-0.5 bg-amber-100 rounded-full">Warning</span>
  return <span className="inline-flex items-center gap-1 text-green-700 font-semibold text-xs px-2 py-0.5 bg-green-100 rounded-full">OK</span>
}

export default function Overview() {
  const navigate = useNavigate()
  const queuesR  = useFetch(`${API_BASE_URL}/queues`, 30_000)
  const queues   = queuesR.data ?? []

  const critCount   = queues.filter(q => q.alertSeverity === 'Critical').length
  const warnCount   = queues.filter(q => q.alertSeverity === 'Warning').length
  const totalActive = queues.reduce((s, q) => s + (q.activeCount ?? 0), 0)
  const totalDlq    = queues.reduce((s, q) => s + (q.dlqCount    ?? 0), 0)

  const sorted = useMemo(() =>
    [...queues].sort((a, b) =>
      (SEV_ORDER[a.alertSeverity] ?? 3) - (SEV_ORDER[b.alertSeverity] ?? 3)
    ), [queues])

  const alertQueue = sorted.find(q => q.alertSeverity === 'Critical' || q.alertSeverity === 'Warning')
  const alertCount = critCount + warnCount

  if (queuesR.loading && queues.length === 0) {
    return (
      <div className="space-y-5">
        <Skeleton className="h-14 w-full rounded-xl" />
        <div className="flex gap-4">
          {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-28 flex-1 rounded-xl" />)}
        </div>
        <Skeleton className="h-72 w-full rounded-xl" />
      </div>
    )
  }

  return (
    <div className="space-y-5">

      {/* Global alert banner */}
      {alertQueue && (
        <div className={`rounded-xl px-5 py-3.5 flex items-center justify-between gap-4 ${
          alertQueue.alertSeverity === 'Critical'
            ? 'bg-red-600 text-white'
            : 'bg-amber-400 text-amber-950'
        }`}>
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm font-medium">
            <span className="font-bold">
              {alertQueue.alertSeverity === 'Critical' ? '🔴 CRITICAL' : '🟡 WARNING'}
            </span>
            <span>— {alertQueue.queueName}</span>
            <span>· {rootCauseLabel(alertQueue.rootCause)}</span>
            {alertCount > 1 && (
              <span className="text-xs opacity-75">+{alertCount - 1} more queue{alertCount > 2 ? 's' : ''}</span>
            )}
          </div>
          <Link
            to={`/queues/${alertQueue.queueName}`}
            className={`shrink-0 text-xs px-3 py-1.5 rounded-lg border font-medium transition-colors ${
              alertQueue.alertSeverity === 'Critical'
                ? 'border-red-300 hover:bg-red-500'
                : 'border-amber-600 hover:bg-amber-300'
            }`}
          >
            View Queue →
          </Link>
        </div>
      )}

      {/* KPI row */}
      <div className="flex gap-4">
        <KpiTile
          label="Total Queues"
          value={queues.length}
          sub="Monitored by QBIS"
          accent="blue"
        />
        <KpiTile
          label="Queues in Alert"
          value={alertCount}
          sub={
            critCount > 0
              ? `${critCount} critical${warnCount > 0 ? ` · ${warnCount} warning` : ''}`
              : warnCount > 0
              ? `${warnCount} warning`
              : 'All healthy'
          }
          accent={critCount > 0 ? 'red' : warnCount > 0 ? 'amber' : 'green'}
        />
        <KpiTile
          label="Active Messages"
          value={totalActive.toLocaleString()}
          sub="Across all queues"
          accent="blue"
        />
        <KpiTile
          label="Dead Letter Queue"
          value={totalDlq}
          sub={totalDlq > 0 ? 'Requires investigation' : 'No dead letters'}
          accent={totalDlq > 0 ? 'red' : 'green'}
        />
      </div>

      {/* Queue health grid */}
      <div className="border border-gray-200 rounded-xl bg-white shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h2 className="text-sm font-semibold text-gray-800">Queue Health</h2>
            <p className="text-xs text-gray-400 mt-0.5">
              {queues.length === 0
                ? 'No queues configured'
                : `${queues.length} queue${queues.length !== 1 ? 's' : ''} · auto-refreshes every 30s`}
            </p>
          </div>
          <div className="flex items-center gap-3">
            {queuesR.error && (
              <span className="text-xs text-red-600">⚠ {queuesR.error}</span>
            )}
            <Link
              to="/settings"
              className="text-xs px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors font-medium"
            >
              + Manage Queues
            </Link>
          </div>
        </div>

        {queues.length === 0 ? (
          <div className="py-20 text-center">
            <p className="text-sm text-gray-400 mb-3">No queues configured yet.</p>
            <Link
              to="/settings"
              className="text-sm text-blue-600 hover:underline font-medium"
            >
              Add your first queue →
            </Link>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                {['Queue', 'Status', 'Backlog', 'Trend', 'Wait Time', 'Root Cause', ''].map(h => (
                  <th
                    key={h}
                    className="py-3 px-4 text-left text-[11px] font-semibold text-gray-500 uppercase tracking-wider"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {sorted.map(q => (
                <tr
                  key={q.queueName}
                  className={`border-b transition-colors cursor-pointer ${rowClass(q.alertSeverity)}`}
                  onClick={() => navigate(`/queues/${q.queueName}`)}
                >
                  <td className="py-3.5 px-4">
                    <div className="flex items-center gap-2">
                      <span className="font-mono text-xs font-semibold text-gray-800">
                        {q.queueName}
                      </span>
                      {!q.isEnabled && (
                        <span className="text-[10px] text-gray-400 bg-gray-100 border border-gray-200 px-1.5 py-0.5 rounded">
                          paused
                        </span>
                      )}
                    </div>
                  </td>
                  <td className="py-3.5 px-4">
                    <StatusBadge sev={q.alertSeverity} />
                  </td>
                  <td className="py-3.5 px-4 tabular-nums font-semibold text-gray-800">
                    {(q.activeCount ?? 0).toLocaleString()}
                  </td>
                  <td className="py-3.5 px-4 text-gray-600 tabular-nums">
                    {q.trendLabel ?? '—'} {trendArrow(q.trendLabel)}
                  </td>
                  <td className="py-3.5 px-4 text-gray-600 tabular-nums">
                    {q.waitTimeMinutes != null ? `${q.waitTimeMinutes.toFixed(1)} min` : '—'}
                  </td>
                  <td className="py-3.5 px-4 text-gray-600">
                    {rootCauseLabel(q.rootCause)}
                  </td>
                  <td className="py-3.5 px-4 text-right">
                    <span className="text-xs text-blue-600 font-medium">Details →</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}
