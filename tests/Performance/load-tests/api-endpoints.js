import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration
export const options = {
  stages: [
    { duration: '30s', target: 5 },   // Ramp up to 5 users
    { duration: '1m', target: 10 },   // Stay at 10 users
    { duration: '30s', target: 20 },  // Ramp up to 20 users
    { duration: '1m', target: 20 },   // Stay at 20 users
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% of requests should be below 2s
    http_req_failed: ['rate<0.1'],      // Error rate should be below 10%
    errors: ['rate<0.1'],
  },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:5000';

// Test data
const sampleBoqYaml = `
project:
  name: "Жилищна сграда"
  location: "София"
  date: "2024-01-15"

stages:
  - stage: "Груб строеж"
    items:
      - description: "Бетон C16/20"
        unit: "м3"
        quantity: 50.0
      - description: "Изкопни работи"
        unit: "м3"
        quantity: 100.0
`;

const samplePriceBaseYaml = `
entries:
  - code: "PB-001"
    description: "Бетон C16/20 вибриран"
    unit: "м3"
    unit_price: 120.0
  - code: "PB-002"
    description: "Изкопни работи механизирани"
    unit: "м3"
    unit_price: 15.0
`;

export default function () {
  // Health check
  group('health', () => {
    const res = http.get(`${BASE_URL}/healthz`);
    const success = check(res, {
      'health check status is 200': (r) => r.status === 200,
      'health check is fast': (r) => r.timings.duration < 100,
    });
    errorRate.add(!success);
    sleep(0.1);
  });

  // Parse endpoint
  group('parse', () => {
    const payload = JSON.stringify({ content: sampleBoqYaml });
    const params = {
      headers: { 'Content-Type': 'application/json' },
    };
    
    const res = http.post(`${BASE_URL}/parse?type=boq`, payload, params);
    const success = check(res, {
      'parse status is 200': (r) => r.status === 200,
      'parse response has data': (r) => r.json('project') !== undefined,
      'parse is within SLA': (r) => r.timings.duration < 500,
    });
    errorRate.add(!success);
    sleep(1);
  });

  // Extract endpoint (simulated - would need actual image)
  group('extract', () => {
    // In real scenario, this would be a multipart/form-data with image file
    // For load testing, we test with error response to measure endpoint speed
    const res = http.post(`${BASE_URL}/extract?type=boq`);
    check(res, {
      'extract endpoint responds': (r) => r.status !== 0,
    });
    sleep(2);
  });

  // Match endpoint
  group('match', () => {
    const matchPayload = {
      boqItems: [
        { description: "Бетон C16/20", unit: "м3", quantity: 50.0 },
        { description: "Изкопни работи", unit: "м3", quantity: 100.0 }
      ],
      priceBase: [
        { code: "PB-001", description: "Бетон C16/20 вибриран", unit: "м3", unitPrice: 120.0 },
        { code: "PB-002", description: "Изкопни работи механизирани", unit: "м3", unitPrice: 15.0 }
      ]
    };

    const params = {
      headers: { 'Content-Type': 'application/json' },
    };

    const res = http.post(`${BASE_URL}/match`, JSON.stringify(matchPayload), params);
    const success = check(res, {
      'match status is 200': (r) => r.status === 200,
      'match response has matches': (r) => Array.isArray(r.json()),
      'match is fast': (r) => r.timings.duration < 200,
    });
    errorRate.add(!success);
    sleep(1);
  });

  // Optimize endpoint
  group('optimize', () => {
    const optimizePayload = {
      boqItems: [
        { description: "Бетон C16/20", unit: "м3", quantity: 50.0 },
        { description: "Изкопни работи", unit: "м3", quantity: 100.0 }
      ],
      priceBase: [
        { code: "PB-001", description: "Бетон C16/20 вибриран", unit: "м3", unitPrice: 120.0 },
        { code: "PB-002", description: "Изкопни работи механизирани", unit: "м3", unitPrice: 15.0 }
      ],
      matchedItems: [
        {
          boqItem: { description: "Бетон C16/20", unit: "м3", quantity: 50.0 },
          candidates: [
            { code: "PB-001", description: "Бетон C16/20 вибриран", unit: "м3", unitPrice: 120.0, score: 0.95 }
          ]
        }
      ]
    };

    const params = {
      headers: { 'Content-Type': 'application/json' },
    };

    const res = http.post(`${BASE_URL}/optimize`, JSON.stringify(optimizePayload), params);
    const success = check(res, {
      'optimize status is 200': (r) => r.status === 200,
      'optimize has result': (r) => r.json('totalCost') !== undefined,
      'optimize is within SLA': (r) => r.timings.duration < 1000,
    });
    errorRate.add(!success);
    sleep(1);
  });

  // Observations metrics
  group('metrics', () => {
    const res = http.get(`${BASE_URL}/observations/metrics`);
    check(res, {
      'metrics status is 200': (r) => r.status === 200,
      'metrics response is JSON': (r) => r.headers['Content-Type'].includes('application/json'),
    });
    sleep(0.5);
  });
}

export function handleSummary(data) {
  return {
    'reports/summary.html': htmlReport(data),
    'reports/summary.json': JSON.stringify(data),
  };
}

function htmlReport(data) {
  const metrics = data.metrics;
  
  return `
<!DOCTYPE html>
<html>
<head>
  <title>FirstMake Load Test Report</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 20px; }
    h1 { color: #333; }
    table { border-collapse: collapse; width: 100%; margin-top: 20px; }
    th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
    th { background-color: #4CAF50; color: white; }
    tr:nth-child(even) { background-color: #f2f2f2; }
    .pass { color: green; font-weight: bold; }
    .fail { color: red; font-weight: bold; }
  </style>
</head>
<body>
  <h1>FirstMake API Load Test Report</h1>
  <p>Generated: ${new Date().toISOString()}</p>
  
  <h2>Summary</h2>
  <table>
    <tr>
      <th>Metric</th>
      <th>Value</th>
    </tr>
    <tr>
      <td>Total Requests</td>
      <td>${metrics.http_reqs ? metrics.http_reqs.values.count : 'N/A'}</td>
    </tr>
    <tr>
      <td>Failed Requests</td>
      <td>${metrics.http_req_failed ? (metrics.http_req_failed.values.rate * 100).toFixed(2) + '%' : 'N/A'}</td>
    </tr>
    <tr>
      <td>Avg Duration</td>
      <td>${metrics.http_req_duration ? metrics.http_req_duration.values.avg.toFixed(2) + 'ms' : 'N/A'}</td>
    </tr>
    <tr>
      <td>P95 Duration</td>
      <td>${metrics.http_req_duration ? metrics.http_req_duration.values['p(95)'].toFixed(2) + 'ms' : 'N/A'}</td>
    </tr>
  </table>
  
  <h2>Detailed Metrics</h2>
  <pre>${JSON.stringify(metrics, null, 2)}</pre>
</body>
</html>
  `;
}
