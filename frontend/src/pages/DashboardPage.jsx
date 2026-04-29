import { useEffect } from 'react'
import useMetricsStore from '../stores/useMetricsStore'
import MetricsCards from '../components/MetricsCards'
import { RequestsChart, ErrorRateChart } from '../components/MetricsCharts'

export default function DashboardPage() {
  const { summary, history, startPolling, stopPolling } = useMetricsStore()

  // AC1 + AC2: start 30s poll on mount, stop on unmount
  useEffect(() => {
    startPolling()
    return () => stopPolling()
  }, [startPolling, stopPolling])

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold">Dashboard</h1>
        <p className="text-sm text-muted-foreground">Live gateway metrics — refreshes every 30 s</p>
      </div>

      {/* AC1: MetricsCards with real values */}
      <MetricsCards summary={summary} />

      {/* AC2: Charts update on 30s poll */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <RequestsChart history={history} />
        <ErrorRateChart history={history} />
      </div>
    </div>
  )
}
