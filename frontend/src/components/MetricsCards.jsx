import { cn } from '../lib/utils'

function Card({ label, value, sub, accent }) {
  return (
    <div className={cn('rounded-lg border bg-card p-5 shadow-sm', accent && 'border-l-4 border-l-primary')}>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-1 text-3xl font-bold tracking-tight">{value}</p>
      {sub && <p className="mt-1 text-xs text-muted-foreground">{sub}</p>}
    </div>
  )
}

// AC1: shows real values from /metrics/summary
export default function MetricsCards({ summary }) {
  if (!summary) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {['Total Requests', 'Error Rate', 'Avg Latency'].map((l) => (
          <div key={l} className="rounded-lg border bg-card p-5 animate-pulse">
            <div className="h-4 bg-muted rounded w-1/2 mb-3" />
            <div className="h-8 bg-muted rounded w-2/3" />
          </div>
        ))}
      </div>
    )
  }

  const errorPct = ((summary.errorRate ?? 0) * 100).toFixed(1)

  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
      <Card
        label="Total Requests"
        value={summary.totalRequests.toLocaleString()}
        sub="since last restart"
        accent
      />
      <Card
        label="Error Rate"
        value={`${errorPct}%`}
        sub={`${summary.totalErrors} errors`}
      />
      <Card
        label="Avg Latency"
        value={`${summary.avgLatencyMs.toFixed(1)} ms`}
        sub="across all routes"
      />
    </div>
  )
}
