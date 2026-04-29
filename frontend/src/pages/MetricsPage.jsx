import { useEffect } from 'react'
import useMetricsStore from '../stores/useMetricsStore'
import MetricsCards from '../components/MetricsCards'
import { RequestsChart, ErrorRateChart } from '../components/MetricsCharts'

export default function MetricsPage() {
  const { summary, history, startPolling, stopPolling } = useMetricsStore()

  useEffect(() => {
    startPolling()
    return () => stopPolling()
  }, [startPolling, stopPolling])

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Metrics</h1>
        <p className="text-sm text-muted-foreground">Full metrics view — refreshes every 30 s</p>
      </div>

      <MetricsCards summary={summary} />

      <div className="grid grid-cols-1 gap-4">
        <RequestsChart history={history} />
        <ErrorRateChart history={history} />
      </div>

      {/* Rate Limit Monitor */}
      {summary?.topRoutes?.length > 0 && (
        <div className="rounded-lg border bg-card shadow-sm overflow-hidden">
          <div className="px-5 py-3 border-b">
            <p className="text-sm font-medium">Rate Limit Monitor — Top Routes</p>
          </div>
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left px-4 py-2 font-medium">Route</th>
                <th className="text-right px-4 py-2 font-medium">Total Requests</th>
                <th className="text-right px-4 py-2 font-medium">Errors</th>
                <th className="text-right px-4 py-2 font-medium">Avg Latency</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {summary.topRoutes.map((r) => (
                <tr key={r.route} className="hover:bg-muted/30">
                  <td className="px-4 py-2 font-mono text-xs">{r.route}</td>
                  <td className="px-4 py-2 text-right">{r.totalRequests.toLocaleString()}</td>
                  <td className="px-4 py-2 text-right">{r.totalErrors}</td>
                  <td className="px-4 py-2 text-right">{r.avgLatencyMs?.toFixed(1)} ms</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
