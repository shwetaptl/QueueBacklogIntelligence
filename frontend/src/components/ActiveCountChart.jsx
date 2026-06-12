import {
  ComposedChart, Area, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ResponsiveContainer, ReferenceLine, ReferenceArea,
} from 'recharts'
import { lineColor, fmtDateTime, rootCauseLabel } from '../utils'
import TimeRangeSelector from './TimeRangeSelector'

function Skeleton({ className }) {
  return <div className={`animate-pulse bg-gray-200 rounded ${className}`} />
}

// ── Time range helpers ────────────────────────────────────────────────────────

const PRESET_MINUTES_MAP = { '15m': 15, '30m': 30, '1h': 60, '6h': 360, '24h': 1440, '7d': 10080 }
const PRESET_LABELS = {
  '15m': 'last 15 min', '30m': 'last 30 min', '1h': 'last 1 h',
  '6h': 'last 6 h', '24h': 'last 24 h', '7d': 'last 7 days',
}

const BUCKET_MS = {
  '15m':  1 * 60_000,
  '30m':  1 * 60_000,
  '1h':   2 * 60_000,
  '6h':  10 * 60_000,
  '24h': 30 * 60_000,
  '7d':   4 * 60 * 60_000,
}

function customBucketMs(fromMs, toMs) {
  const days = (toMs - fromMs) / 86_400_000
  if (days <= 1)  return  2 * 60_000
  if (days <= 3)  return 10 * 60_000
  if (days <= 7)  return 30 * 60_000
  if (days <= 14) return  1 * 60 * 60_000
  if (days <= 30) return  4 * 60 * 60_000
  return 12 * 60 * 60_000
}

function customTickMs(fromMs, toMs) {
  const days = (toMs - fromMs) / 86_400_000
  if (days <= 1)  return  4 * 60 * 60_000
  if (days <= 7)  return 24 * 60 * 60_000
  if (days <= 14) return  2 * 24 * 60 * 60_000
  if (days <= 30) return  5 * 24 * 60 * 60_000
  return 7 * 24 * 60 * 60_000
}

function getFromToMs(timeRange) {
  if (timeRange?.preset === 'custom' && timeRange.from && timeRange.to) {
    return {
      fromMs: new Date(timeRange.from + 'T00:00:00Z').getTime(),
      toMs:   new Date(timeRange.to   + 'T23:59:59Z').getTime(),
    }
  }
  const mins = PRESET_MINUTES_MAP[timeRange?.preset] ?? 30
  const toMs = Date.now()
  return { fromMs: toMs - mins * 60_000, toMs }
}

function buildTitle(timeRange) {
  if (!timeRange || timeRange.preset !== 'custom')
    return `Queue Activity — ${PRESET_LABELS[timeRange?.preset] ?? 'last 30 min'}`
  if (timeRange.from && timeRange.to)
    return `Queue Activity — ${timeRange.from} to ${timeRange.to}`
  return 'Queue Activity'
}

// ── Time-bucketed series ──────────────────────────────────────────────────────
function buildTimeSeries(rawRows, fromMs, toMs, preset) {
  const interval = BUCKET_MS[preset] ?? customBucketMs(fromMs, toMs)
  const n        = Math.round((toMs - fromMs) / interval)

  const byBucket = new Map()
  for (const r of rawRows) {
    const idx = Math.round((r.timeMs - fromMs) / interval)
    if (idx >= 0 && idx <= n) byBucket.set(idx, r)
  }

  const buckets = []
  for (let i = 0; i <= n; i++) {
    const t = fromMs + i * interval
    const r = byBucket.get(i)
    buckets.push({
      timeMs:        t,
      activeCount:   r?.activeCount   ?? null,
      waitTime:      r?.waitTime      ?? null,
      severity:      r?.severity      ?? null,
      rootCause:     r?.rootCause     ?? null,
      incomingPerMin:r?.incomingPerMin ?? null,
      outgoingPerMin:r?.outgoingPerMin ?? null,
    })
  }
  return buckets
}

// ── Severity bands ────────────────────────────────────────────────────────────
function buildSeverityBands(buckets) {
  const bands = []
  let cur = null
  for (const b of buckets) {
    const sev = b.severity
    if (sev && sev !== 'None') {
      if (!cur || cur.sev !== sev) {
        if (cur) bands.push(cur)
        cur = { sev, x1: b.timeMs, x2: b.timeMs }
      } else {
        cur.x2 = b.timeMs
      }
    } else {
      if (cur) { bands.push(cur); cur = null }
    }
  }
  if (cur) bands.push(cur)
  return bands
}

const SEV_FILL   = { Critical: '#FEE2E2', Warning: '#FEF9C3' }
const SEV_STROKE = { Critical: '#FCA5A5', Warning: '#FDE68A' }

// ── Root cause change markers ─────────────────────────────────────────────────
const CAUSE_ABBR = {
  Healthy:'OK', ConsumerStopped:'CS', ConsumerSlowdown:'CS↓',
  ProducerSpike:'PS', ProducerSpikeAndConsumerSlowdown:'PS+CS',
  DLQGrowth:'DLQ', Recovering:'REC', Unknown:'?',
}

function buildCauseMarkers(rawRows, fromMs, preset) {
  const interval = BUCKET_MS[preset] ?? customBucketMs(fromMs, fromMs + 86_400_000)
  const markers  = []
  let lastCause  = null
  for (const r of rawRows) {
    if (r.rootCause && r.rootCause !== lastCause && lastCause !== null) {
      const idx       = Math.max(0, Math.round((r.timeMs - fromMs) / interval))
      const snappedMs = fromMs + idx * interval
      markers.push({ timeMs: snappedMs, cause: r.rootCause })
    }
    lastCause = r.rootCause
  }
  return markers
}

// ── Axis ticks ────────────────────────────────────────────────────────────────
const TICK_INTERVAL_MS = {
  '15m':  3 * 60_000, '30m': 5 * 60_000, '1h': 10 * 60_000,
  '6h':  60 * 60_000, '24h': 4 * 60 * 60_000, '7d': 24 * 60 * 60_000,
}

function getAxisTicks(fromMs, toMs, preset) {
  const tickInterval   = TICK_INTERVAL_MS[preset] ?? customTickMs(fromMs, toMs)
  const bucketInterval = BUCKET_MS[preset]        ?? customBucketMs(fromMs, toMs)
  const n              = Math.round((toMs - fromMs) / bucketInterval)

  const ticks   = []
  const firstMs = Math.ceil(fromMs / tickInterval) * tickInterval
  for (let t = firstMs; t <= toMs; t += tickInterval) {
    const idx = Math.max(0, Math.min(n, Math.round((t - fromMs) / bucketInterval)))
    ticks.push(fromMs + idx * bucketInterval)
  }
  return [...new Set(ticks)]
}

// ── Tooltip ───────────────────────────────────────────────────────────────────
function ChartTooltip({ active, payload, slaMinutes }) {
  if (!active || !payload?.length) return null
  const d   = payload[0]?.payload
  const t   = d?.timeMs
  const has = d?.activeCount != null

  const slaPct = has && d.waitTime != null && slaMinutes
    ? Math.round((d.waitTime / slaMinutes) * 100) : null

  const sevColor = {
    Critical: 'text-red-600', Warning: 'text-yellow-600', None: 'text-green-600',
  }[d?.severity] ?? 'text-gray-400'

  return (
    <div className="bg-white border border-gray-200 rounded-lg shadow-lg p-3 text-xs space-y-1.5 min-w-[200px]">
      <p className="font-semibold text-gray-700 border-b border-gray-100 pb-1.5">
        {t ? fmtDateTime(new Date(t).toISOString()) : '—'}
      </p>
      {has ? (
        <>
          <div className="flex justify-between gap-4">
            <span className="text-gray-500">Active messages</span>
            <strong className="text-gray-900">{d.activeCount}</strong>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-gray-500">Wait time</span>
            <strong className="text-gray-900">
              {d.waitTime != null ? `${d.waitTime.toFixed(1)} min` : '∞'}
              {slaPct != null && (
                <span className={`ml-1 font-normal text-[10px] ${slaPct > 100 ? 'text-red-500' : slaPct > 60 ? 'text-yellow-500' : 'text-green-500'}`}>
                  ({slaPct}% of SLA)
                </span>
              )}
            </strong>
          </div>
          <div className="flex justify-between gap-4">
            <span className="text-gray-500">Root cause</span>
            <strong className="text-gray-900">{rootCauseLabel(d.rootCause)}</strong>
          </div>
          {d.severity && (
            <div className="flex justify-between gap-4">
              <span className="text-gray-500">Severity</span>
              <strong className={sevColor}>{d.severity}</strong>
            </div>
          )}
          <div className="border-t border-gray-100 pt-1.5 space-y-1">
            <div className="flex justify-between gap-4">
              <span className="text-purple-500">Incoming</span>
              <strong className="text-purple-700">
                {d.incomingPerMin != null ? `${d.incomingPerMin.toFixed(1)}/min` : '—'}
              </strong>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-green-500">Outgoing</span>
              <strong className="text-green-700">
                {d.outgoingPerMin != null ? `${d.outgoingPerMin.toFixed(1)}/min` : '—'}
              </strong>
            </div>
          </div>
        </>
      ) : (
        <p className="text-gray-400 italic">No data collected</p>
      )}
    </div>
  )
}

// ── shared chart props ────────────────────────────────────────────────────────
const SYNC_ID = 'qbi-chart'
// Margins are identical so vertical grid lines align between the two panels.
const TOP_MARGIN    = { top: 8,  right: 52, left: 40, bottom: 0 }
const BOTTOM_MARGIN = { top: 0,  right: 52, left: 40, bottom: 4 }

const AXIS_STYLE = { fontSize: 10, fill: '#9CA3AF' }

// ── Component ─────────────────────────────────────────────────────────────────
export default function ActiveCountChart({
  history, slaMinutes, alertSeverity, loading, error, timeRange, onTimeRangeChange,
}) {
  if (loading) {
    return (
      <div className="border rounded-xl p-6 bg-white shadow-sm space-y-3">
        <Skeleton className="h-5 w-52" />
        <Skeleton className="h-6 w-72" />
        <Skeleton className="h-48 w-full" />
        <Skeleton className="h-24 w-full" />
      </div>
    )
  }

  const color   = lineColor(alertSeverity)
  const slaMins = slaMinutes ?? 15
  const { fromMs, toMs } = getFromToMs(timeRange)

  const isLongRange = timeRange?.preset === '7d' ||
    (timeRange?.preset === 'custom' && timeRange.from !== timeRange.to)

  function tickFmt(v) {
    const d = new Date(Number(v))
    if (isLongRange)
      return d.toLocaleDateString([], { month: 'short', day: 'numeric' })
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
  }

  const rawRows = (history ?? []).map(r => ({
    timeMs:        new Date(r.timestampUtc).getTime(),
    activeCount:   r.activeCount,
    waitTime:      r.waitTimeMinutes,
    severity:      r.alertSeverity,
    rootCause:     r.rootCause,
    incomingPerMin:r.incomingPerMin,
    outgoingPerMin:r.outgoingPerMin,
  }))

  const rows         = buildTimeSeries(rawRows, fromMs, toMs, timeRange?.preset)
  const ticks        = getAxisTicks(fromMs, toMs, timeRange?.preset)
  const sevBands     = buildSeverityBands(rows)
  const causeMarkers = buildCauseMarkers(rawRows, fromMs, timeRange?.preset)

  // Determine if this period has any real activity
  const maxActive = Math.max(0, ...rows.map(r => r.activeCount ?? 0))
  const hasRates  = rows.some(r => (r.incomingPerMin ?? 0) > 0 || (r.outgoingPerMin ?? 0) > 0)

  // Y-axis domain: always show at least a small range so flat-zero isn't ambiguous
  const countDomain  = [0, (max) => Math.max(max * 1.3, 5)]
  const waitDomain   = [0, (max) => Math.max(max * 1.3, slaMins * 1.5)]
  const rateDomain   = [0, (max) => Math.max(max * 1.3, 2)]

  return (
    <div className="border rounded-xl p-5 bg-white shadow-sm">
      {/* Header */}
      <div className="mb-3">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-sm font-semibold text-gray-700">{buildTitle(timeRange)}</h2>
          {error && <span className="text-xs text-red-600">⚠ {error}</span>}
        </div>
        {onTimeRangeChange && (
          <TimeRangeSelector value={timeRange} onChange={onTimeRangeChange} />
        )}
      </div>

      {/* Idle notice — shown but doesn't hide chart so x-axis range is still visible */}
      {maxActive === 0 && (
        <div className="mb-2 px-3 py-1.5 bg-green-50 border border-green-200 rounded-lg text-xs text-green-700">
          ✅ Queue idle — no messages in this period
        </div>
      )}

      {/* ── Panel 1 — Backlog + Wait Time ─────────────────────────────────── */}
      <div className="mb-0">
        <p className="text-[10px] text-gray-400 uppercase tracking-wide ml-10 mb-1">
          Backlog &amp; SLA
        </p>
        <ResponsiveContainer width="100%" height={190}>
          <ComposedChart data={rows} syncId={SYNC_ID} margin={TOP_MARGIN}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" vertical={false} />

            {/* Severity background bands */}
            {sevBands.map((b, i) => (
              <ReferenceArea
                key={i}
                yAxisId="count"
                x1={b.x1} x2={b.x2}
                fill={SEV_FILL[b.sev]}
                stroke={SEV_STROKE[b.sev]}
                strokeOpacity={0.4}
                fillOpacity={0.5}
              />
            ))}

            {/* Hidden x-axis (shares ticks with bottom panel for grid alignment) */}
            <XAxis
              dataKey="timeMs"
              ticks={ticks}
              tick={false}
              tickLine={false}
              axisLine={false}
            />

            {/* Left: message count */}
            <YAxis
              yAxisId="count"
              tick={AXIS_STYLE}
              tickLine={false}
              axisLine={false}
              width={38}
              domain={countDomain}
              label={{
                value: 'msgs',
                angle: -90,
                position: 'insideLeft',
                offset: 12,
                style: { fontSize: 9, fill: '#D1D5DB' },
              }}
            />

            {/* Right: wait time */}
            <YAxis
              yAxisId="wait"
              orientation="right"
              tick={AXIS_STYLE}
              tickLine={false}
              axisLine={false}
              width={42}
              domain={waitDomain}
              label={{
                value: 'wait (min)',
                angle: 90,
                position: 'insideRight',
                offset: 14,
                style: { fontSize: 9, fill: '#D1D5DB' },
              }}
            />

            <Tooltip
              content={<ChartTooltip slaMinutes={slaMins} />}
              cursor={{ stroke: '#94A3B8', strokeWidth: 1, strokeDasharray: '4 2' }}
            />

            <Legend
              wrapperStyle={{ fontSize: 11, paddingTop: 6 }}
              formatter={v => <span style={{ color: '#6B7280' }}>{v}</span>}
            />

            {/* SLA threshold */}
            <ReferenceLine
              yAxisId="wait"
              y={slaMins}
              stroke="#EF4444"
              strokeDasharray="5 3"
              strokeWidth={1.5}
              label={{
                value: `SLA ${slaMins}m`,
                position: 'insideTopRight',
                style: { fontSize: 9, fill: '#EF4444' },
              }}
            />

            {/* Root cause change markers */}
            {causeMarkers.map((m, i) => (
              <ReferenceLine
                key={i}
                yAxisId="count"
                x={m.timeMs}
                stroke="#94A3B8"
                strokeDasharray="3 3"
                strokeOpacity={0.7}
                label={{
                  value: CAUSE_ABBR[m.cause] ?? '?',
                  position: 'insideTopLeft',
                  style: { fontSize: 8, fill: '#64748B' },
                }}
              />
            ))}

            {/* Active count — filled area */}
            <Area
              yAxisId="count"
              type="monotone"
              dataKey="activeCount"
              name="Active Messages"
              stroke={color}
              strokeWidth={2}
              fill={color}
              fillOpacity={0.12}
              dot={false}
              connectNulls={false}
              activeDot={{ r: 4, strokeWidth: 0, fill: color }}
            />

            {/* Wait time — dashed line */}
            <Line
              yAxisId="wait"
              type="monotone"
              dataKey="waitTime"
              name="Wait Time (min)"
              stroke="#94A3B8"
              strokeWidth={1.5}
              strokeDasharray="5 3"
              dot={false}
              connectNulls={false}
              activeDot={{ r: 3, strokeWidth: 0 }}
            />
          </ComposedChart>
        </ResponsiveContainer>
      </div>

      {/* ── Panel 2 — Traffic Rates ────────────────────────────────────────── */}
      <div>
        <p className="text-[10px] text-gray-400 uppercase tracking-wide ml-10 mb-1">
          Message Rate (msgs/min)
        </p>
        <ResponsiveContainer width="100%" height={110}>
          <ComposedChart data={rows} syncId={SYNC_ID} margin={BOTTOM_MARGIN}>
            <CartesianGrid strokeDasharray="3 3" stroke="#F3F4F6" vertical={false} />

            <XAxis
              dataKey="timeMs"
              ticks={ticks}
              tick={AXIS_STYLE}
              tickLine={false}
              axisLine={{ stroke: '#E5E7EB' }}
              tickFormatter={tickFmt}
            />

            <YAxis
              yAxisId="rate"
              tick={AXIS_STYLE}
              tickLine={false}
              axisLine={false}
              width={38}
              domain={rateDomain}
            />

            {/* No popup tooltip on bottom panel — top panel's tooltip covers it */}
            <Tooltip content={() => null} cursor={{ stroke: '#94A3B8', strokeWidth: 1, strokeDasharray: '4 2' }} />

            <Legend
              wrapperStyle={{ fontSize: 11, paddingTop: 4 }}
              formatter={v => <span style={{ color: '#6B7280' }}>{v}</span>}
            />

            {/* Incoming rate */}
            <Area
              yAxisId="rate"
              type="monotone"
              dataKey="incomingPerMin"
              name="Incoming/min"
              stroke="#A855F7"
              strokeWidth={1.5}
              fill="#A855F7"
              fillOpacity={0.15}
              dot={false}
              connectNulls={false}
            />

            {/* Outgoing rate */}
            <Area
              yAxisId="rate"
              type="monotone"
              dataKey="outgoingPerMin"
              name="Outgoing/min"
              stroke="#22C55E"
              strokeWidth={1.5}
              fill="#22C55E"
              fillOpacity={0.15}
              dot={false}
              connectNulls={false}
            />

            {!hasRates && (
              <ReferenceLine
                yAxisId="rate"
                y={0}
                stroke="transparent"
                label={{
                  value: 'No rate data in this period',
                  position: 'center',
                  style: { fontSize: 10, fill: '#CBD5E1' },
                }}
              />
            )}
          </ComposedChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
