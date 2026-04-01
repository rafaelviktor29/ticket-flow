import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";

// Metrics
const ordersCreated = new Counter("orders_created");
const ordersConfirmed = new Counter("orders_confirmed");
const ordersFailed = new Counter("orders_failed");
const errorRate = new Rate("error_rate");
const responseTime = new Trend("response_time_ms", true);

// Backend status enum (string values to match API JSON)
const STATUS = {
  CONFIRMED: "Confirmed",
  FAILED: "Failed",
};

// Test configuration
export const options = {
  scenarios: {
    high_concurrency: {
      executor: "ramping-vus",
      startVUs: 0,
      stages: [
        { duration: "10s", target: 100 },
        { duration: "30s", target: 100 },
        { duration: "10s", target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_duration: ["p(95)<2000"],
    error_rate: ["rate<0.1"],
  },
};

const BASE_URL = "http://localhost:54049";
const TICKET_ID = __ENV.TICKET_ID;

// Generate UUID to simulate distinct users
function uuidv4() {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
    var r = (Math.random() * 16) | 0;
    var v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// Main execution
export default function () {
  if (!TICKET_ID) {
    throw new Error("TICKET_ID not provided. Use -e TICKET_ID=...");
  }

  var payload = JSON.stringify({
    ticketId: TICKET_ID,
    userId: uuidv4(),
    idempotencyKey: uuidv4(),
  });

  var params = {
    headers: { "Content-Type": "application/json" },
    timeout: "10s",
  };

  // Send order creation request
  var start = Date.now();
  var res = http.post(BASE_URL + "/api/orders", payload, params);
  responseTime.add(Date.now() - start);

  var isCreated = check(res, {
    "order accepted (202)": function (r) {
      return r.status === 202;
    },
  });

  if (!isCreated) {
    errorRate.add(1);
    return;
  }

  errorRate.add(0);
  ordersCreated.add(1);

  var order = res.json();
  if (!order || !order.id) return;

  // Poll to verify asynchronous processing
  for (var i = 0; i < 15; i++) {
    sleep(2);

    var statusRes = http.get(BASE_URL + "/api/orders/" + order.id, params);

    if (statusRes.status !== 200) continue;

    var statusOrder;

    try {
      statusOrder = statusRes.json();
    } catch (e) {
      continue;
    }

    // Robust response validation
    if (
      !statusOrder ||
      typeof statusOrder !== "object" ||
      statusOrder.status === undefined ||
      statusOrder.status === null
    ) {
      continue;
    }

    var status = String(statusOrder.status);

    if (status === STATUS.CONFIRMED) {
      ordersConfirmed.add(1);
      return;
    }

    if (status === STATUS.FAILED) {
      ordersFailed.add(1);
      return;
    }
  }
}

// Helper functions
function getMetric(data, name) {
  if (!data.metrics[name] || !data.metrics[name].values) return 0;
  return data.metrics[name].values.count || 0;
}

function getMetricAvg(data, name) {
  if (!data.metrics[name] || !data.metrics[name].values) return 0;
  return data.metrics[name].values.avg || 0;
}

function getMetricP(data, name, p) {
  if (!data.metrics[name] || !data.metrics[name].values) return 0;
  return data.metrics[name].values[p] || 0;
}

// Final test summary
export function handleSummary(data) {
  console.log("\n=== LOAD TEST SUMMARY ===");
  console.log("Orders created:      " + getMetric(data, "orders_created"));
  console.log("Orders confirmed:    " + getMetric(data, "orders_confirmed"));
  console.log("Orders failed:       " + getMetric(data, "orders_failed"));
  console.log("Average time (ms):   " + getMetricAvg(data, "response_time_ms").toFixed(2));
  console.log("p95 (ms):            " + getMetricP(data, "response_time_ms", "p(95)").toFixed(2));
  console.log("p99 (ms):            " + getMetricP(data, "response_time_ms", "p(99)").toFixed(2));
  console.log("================================\n");

  return {
    "k6/result.json": JSON.stringify(data, null, 2),
  };
}