import {
  ResponsiveContainer,
  LineChart, Line,
  AreaChart, Area,
  XAxis, YAxis, CartesianGrid, Tooltip, Legend,
} from 'recharts'

// Shared chart wrapper with a title
function ChartCard({ title, children }) {
  return (
    <div className="rounded-lg border bg-card p-5 shadow-sm">
      <p className="text-sm font-medium mb-4">{title}</p>
      {children}
    </div>
  )
}

// AC2: history updates every 30s via useMetricsStore polling
export function RequestsChart({ history }) {
  if (!history?.length) {
    return (
      <ChartCard title="Requests Over Time">
        <p className="text-sm text-muted-foreground text-center py-8">Waiting for data…</p>
      </ChartCard>
    )
  }
  return (
    <ChartCard title="Requests Over Time">
      <ResponsiveContainer width="100%" height={220}>
        <LineChart data={history} margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
          <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
          <XAxis dataKey="time" tick={{ fontSize: 11 }} />
          <YAxis tick={{ fontSize: 11 }} />
          <Tooltip />
          <Legend />
          <Line
            type="monotone"
            dataKey="totalRequests"
            name="Requests"
            stroke="hsl(221.2 83.2% 53.3%)"
            strokeWidth={2}
            dot={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </ChartCard>
  )
}

export function ErrorRateChart({ history }) {
  if (!history?.length) {
    return (
      <ChartCard title="Error Rate (%)">
        <p className="text-sm text-muted-foreground text-center py-8">Waiting for data…</p>
      </ChartCard>
    )
  }
  return (
    <ChartCard title="Error Rate (%)">
      <ResponsiveContainer width="100%" height={220}>
        <AreaChart data={history} margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
          <defs>
            <linearGradient id="errorGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%"  stopColor="hsl(0 84.2% 60.2%)" stopOpacity={0.3} />
              <stop offset="95%" stopColor="hsl(0 84.2% 60.2%)" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
          <XAxis dataKey="time" tick={{ fontSize: 11 }} />
          <YAxis tick={{ fontSize: 11 }} unit="%" />
          <Tooltip formatter={(v) => `${v}%`} />
          <Legend />
          <Area
            type="monotone"
            dataKey="errorRate"
            name="Error Rate"
            stroke="hsl(0 84.2% 60.2%)"
            fill="url(#errorGrad)"
            strokeWidth={2}
            dot={false}
          />
        </AreaChart>
      </ResponsiveContainer>
    </ChartCard>
  )
}
