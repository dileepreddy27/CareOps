import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate } from 'k6/metrics'

const baseUrl = __ENV.BASE_URL || 'http://host.docker.internal:5080'
const latency = new Trend('careops_read_latency', true)
const dashboardLatency = new Trend('careops_dashboard_latency', true)
const queueLatency = new Trend('careops_queue_latency', true)
const failures = new Rate('careops_read_failures')

export const options = {
  scenarios: {
    operations_reads: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 20),
      duration: __ENV.DURATION || '30s',
    },
  },
  thresholds: {
    careops_read_latency: ['p(95)<300'],
    careops_read_failures: ['rate<0.01'],
    http_req_failed: ['rate<0.01'],
  },
}

export function setup() {
  const response = http.post(`${baseUrl}/api/auth/login`, JSON.stringify({
    email: 'specialist@careops.local',
    password: 'CareOps-Demo-2026!',
  }), { headers: { 'Content-Type': 'application/json' } })
  check(response, { 'demo login succeeds': result => result.status === 200 })
  return { token: response.json('accessToken') }
}

export default function (data) {
  const params = { headers: { Authorization: `Bearer ${data.token}` } }
  const responses = http.batch([
    ['GET', `${baseUrl}/api/dashboard`, null, params],
    ['GET', `${baseUrl}/api/providers?page=1&pageSize=25`, null, params],
  ])
  for (const [index, response] of responses.entries()) {
    latency.add(response.timings.duration)
    if (index === 0) dashboardLatency.add(response.timings.duration)
    else queueLatency.add(response.timings.duration)
    failures.add(response.status !== 200)
    check(response, { 'read succeeds': result => result.status === 200 })
  }
  sleep(1)
}
