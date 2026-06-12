import { useState } from 'react'
import { rootCauseLabel, rootCauseColor, fmtDateTime } from '../utils'

const CAUSE_ABBR = {
  Healthy:                         'OK',
  ConsumerStopped:                 'CS',
  ConsumerSlowdown:                'CS↓',
  ProducerSpike:                   'PS',
  ProducerSpikeAndConsumerSlowdown:'PS+CS',
  DLQGrowth:                       'DLQ',
  Recovering:                      'REC',
  Unknown:                         '?',
}

function buildSegments(history) {
  if (!history?.length) return []
  const sorted = [...history].sort(
    (a, b) => new Date(a.timestampUtc) - new Date(b.timestampUtc)
  )

  const segments = []
  let cur = {
    cause: sorted[0].rootCause ?? 'Unknown',
    fromIso: sorted[0].timestampUtc,
    toIso:   sorted[0].timestampUtc,
    fromMs:  new Date(sorted[0].timestampUtc).getTime(),
    toMs:    new Date(sorted[0].timestampUtc).getTime(),
  }

  for (let i = 1; i < sorted.length; i++) {
    const r = sorted[i]
    const cause = r.rootCause ?? 'Unknown'
    if (cause === cur.cause) {
      cur.toIso = r.timestampUtc
      cur.toMs  = new Date(r.timestampUtc).getTime()
    } else {
      segments.push({ ...cur })
      cur = {
        cause,
        fromIso: r.timestampUtc,
        toIso:   r.timestampUtc,
        fromMs:  new Date(r.timestampUtc).getTime(),
        toMs:    new Date(r.timestampUtc).getTime(),
      }
    }
  }
  segments.push({ ...cur })
  return segments
}

export default function RootCauseTimeline({ history, onSegmentClick }) {
  const [hoveredIdx, setHoveredIdx] = useState(null)

  const segments = buildSegments(history)
  if (!segments.length) return null

  const totalMs    = segments[segments.length - 1].toMs - segments[0].fromMs
  const minWidthPx = 28

  return (
    <div className="border rounded-xl p-4 bg-white shadow-sm">
      <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-3">
        Root Cause Timeline
      </p>

      <div className="flex h-8 rounded-lg overflow-hidden gap-px">
        {segments.map((seg, i) => {
          const durMs = Math.max(seg.toMs - seg.fromMs, 1)
          const pct   = totalMs > 0 ? (durMs / totalMs) * 100 : (100 / segments.length)
          const color = rootCauseColor(seg.cause)
          const abbr  = CAUSE_ABBR[seg.cause] ?? '?'

          return (
            <div
              key={i}
              className="relative flex items-center justify-center text-white text-xs font-bold cursor-pointer transition-opacity hover:opacity-80 select-none"
              style={{ flexBasis: `${pct}%`, minWidth: minWidthPx, backgroundColor: color }}
              onMouseEnter={() => setHoveredIdx(i)}
              onMouseLeave={() => setHoveredIdx(null)}
              onClick={() => onSegmentClick?.(seg)}
              title={`${rootCauseLabel(seg.cause)}\n${fmtDateTime(seg.fromIso)} → ${fmtDateTime(seg.toIso)}`}
            >
              <span className="overflow-hidden whitespace-nowrap px-1">{abbr}</span>

              {hoveredIdx === i && (
                <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 z-10
                                bg-gray-900 text-white text-xs rounded-lg px-3 py-2 shadow-lg
                                whitespace-nowrap pointer-events-none">
                  <p className="font-semibold">{rootCauseLabel(seg.cause)}</p>
                  <p className="text-gray-300">{fmtDateTime(seg.fromIso)}</p>
                  <p className="text-gray-300">→ {fmtDateTime(seg.toIso)}</p>
                </div>
              )}
            </div>
          )
        })}
      </div>

      {/* Legend */}
      <div className="flex flex-wrap gap-x-4 gap-y-1 mt-2">
        {[...new Set(segments.map(s => s.cause))].map(cause => (
          <span key={cause} className="flex items-center gap-1 text-xs text-gray-500">
            <span
              className="inline-block w-2.5 h-2.5 rounded-sm flex-shrink-0"
              style={{ backgroundColor: rootCauseColor(cause) }}
            />
            {rootCauseLabel(cause)}
          </span>
        ))}
      </div>
    </div>
  )
}
