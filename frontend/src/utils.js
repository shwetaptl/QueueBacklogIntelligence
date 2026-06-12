export function cardBg(sev) {
  return {
    Critical: 'bg-red-50 border-red-200',
    Warning:  'bg-yellow-50 border-yellow-200',
    None:     'bg-green-50 border-green-200',
  }[sev] ?? 'bg-gray-50 border-gray-200'
}

export function lineColor(sev) {
  return { Critical: '#EF4444', Warning: '#F59E0B', None: '#3B82F6' }[sev] ?? '#3B82F6'
}

export function trendArrow(t) {
  return {
    GrowingFast: '↑↑', Growing: '↑', Stable: '→',
    Idle: '—', Draining: '↓', DrainingFast: '↓↓',
  }[t] ?? '?'
}

export function signed(n) {
  const v = n ?? 0
  return (v > 0 ? '+' : '') + v.toFixed(1)
}

export function parseUTC(iso) {
  if (!iso) return null
  return new Date(iso.endsWith('Z') ? iso : iso + 'Z')
}

export function fmtHHMM(iso) {
  const d = parseUTC(iso)
  if (!d) return ''
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })
}

export function fmtDateTime(iso) {
  if (!iso) return '—'
  return parseUTC(iso)?.toISOString().replace('T', ' ').slice(0, 16) + ' UTC'
}

export function rootCauseLabel(cause) {
  return {
    Healthy:                         'Healthy',
    ConsumerStopped:                 'Consumer Stopped',
    ConsumerSlowdown:                'Consumer Slowdown',
    ProducerSpike:                   'Producer Spike',
    ProducerSpikeAndConsumerSlowdown:'Spike + Slowdown',
    DLQGrowth:                       'DLQ Growth',
    Recovering:                      'Recovering',
    Unknown:                         'Unknown',
  }[cause] ?? (cause ?? 'Unknown')
}

export function rootCauseColor(cause) {
  return {
    Healthy:                         '#22C55E',
    ConsumerStopped:                 '#EF4444',
    ConsumerSlowdown:                '#F97316',
    ProducerSpike:                   '#A855F7',
    ProducerSpikeAndConsumerSlowdown:'#DC2626',
    DLQGrowth:                       '#F59E0B',
    Recovering:                      '#3B82F6',
    Unknown:                         '#9CA3AF',
  }[cause] ?? '#9CA3AF'
}
