import http from 'k6/http';
import { check, sleep } from 'k6';

// Stress test configuration - push system to limits
export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '2m', target: 100 },  // Ramp up to 100 users
    { duration: '3m', target: 100 },  // Stay at 100 users
    { duration: '1m', target: 200 },  // Spike to 200 users
    { duration: '2m', target: 200 },  // Stay at 200 users
    { duration: '2m', target: 0 },    // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(99)<5000'], // 99% of requests should be below 5s under stress
    http_req_failed: ['rate<0.2'],      // Error rate should be below 20% under stress
  },
};

const BASE_URL = __ENV.API_URL || 'http://localhost:5000';

export default function () {
  // Heavy load on match endpoint
  const matchPayload = {
    boqItems: generateBoqItems(50),
    priceBase: generatePriceBase(500)
  };

  const params = {
    headers: { 'Content-Type': 'application/json' },
  };

  const res = http.post(`${BASE_URL}/match`, JSON.stringify(matchPayload), params);
  
  check(res, {
    'status is 200 or 503': (r) => r.status === 200 || r.status === 503,
    'response time acceptable': (r) => r.timings.duration < 10000,
  });

  sleep(1);
}

function generateBoqItems(count) {
  const items = [];
  const descriptions = [
    "Бетон C16/20",
    "Изкопни работи",
    "Мазилка",
    "Боядисване",
    "Теракот"
  ];

  for (let i = 0; i < count; i++) {
    items.push({
      description: descriptions[i % descriptions.length] + ` - вариант ${i}`,
      unit: "м3",
      quantity: 10.0 + i
    });
  }

  return items;
}

function generatePriceBase(count) {
  const entries = [];
  const descriptions = [
    "Бетон C16/20 вибриран",
    "Изкопни работи механизирани",
    "Мазилка вътрешна",
    "Боядисване с латекс",
    "Теракотни плочки"
  ];

  for (let i = 0; i < count; i++) {
    entries.push({
      code: `PB-${String(i).padStart(4, '0')}`,
      description: descriptions[i % descriptions.length] + ` - тип ${Math.floor(i / descriptions.length)}`,
      unit: "м3",
      unitPrice: 100.0 + i
    });
  }

  return entries;
}
