import http from "k6/http";
import { check, sleep } from "k6";
import { Counter, Rate, Trend } from "k6/metrics";

// Métricas
const pedidosCriados = new Counter("pedidos_criados");
const pedidosConfirmados = new Counter("pedidos_confirmados");
const pedidosFalhos = new Counter("pedidos_falhos");
const taxaErro = new Rate("taxa_erro");
const tempoResposta = new Trend("tempo_resposta_ms", true);

// Enum do backend
const STATUS = {
  CONFIRMED: 2,
  FAILED: 3,
};

// Configuração do teste
export const options = {
  scenarios: {
    alta_concorrencia: {
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
    taxa_erro: ["rate<0.1"],
  },
};

const BASE_URL = "http://localhost:54049";
const TICKET_ID = __ENV.TICKET_ID;

// Geração de UUID para simular usuários distintos
function uuidv4() {
  return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
    var r = (Math.random() * 16) | 0;
    var v = c === "x" ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// Execução principal
export default function () {
  if (!TICKET_ID) {
    throw new Error("TICKET_ID não informado. Use -e TICKET_ID=...");
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

  // Envio da requisição de criação de pedido
  var start = Date.now();
  var res = http.post(BASE_URL + "/api/orders", payload, params);
  tempoResposta.add(Date.now() - start);

  var criado = check(res, {
    "pedido aceito (202)": function (r) {
      return r.status === 202;
    },
  });

  if (!criado) {
    taxaErro.add(1);
    return;
  }

  taxaErro.add(0);
  pedidosCriados.add(1);

  var order = res.json();
  if (!order || !order.id) return;

  // Polling para verificar processamento assíncrono
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

    // Validação robusta da resposta
    if (
      !statusOrder ||
      typeof statusOrder !== "object" ||
      statusOrder.status === undefined ||
      statusOrder.status === null
    ) {
      continue;
    }

    var status = Number(statusOrder.status);

    if (status === STATUS.CONFIRMED) {
      pedidosConfirmados.add(1);
      return;
    }

    if (status === STATUS.FAILED) {
      pedidosFalhos.add(1);
      return;
    }
  }
}

// Funções auxiliares
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

// Resumo final do teste
export function handleSummary(data) {
  console.log("\n=== RESUMO DO TESTE DE CARGA ===");
  console.log("Pedidos criados:     " + getMetric(data, "pedidos_criados"));
  console.log("Pedidos confirmados: " + getMetric(data, "pedidos_confirmados"));
  console.log("Pedidos falhos:      " + getMetric(data, "pedidos_falhos"));
  console.log("Tempo médio (ms):    " + getMetricAvg(data, "tempo_resposta_ms").toFixed(2));
  console.log("p95 (ms):            " + getMetricP(data, "tempo_resposta_ms", "p(95)").toFixed(2));
  console.log("p99 (ms):            " + getMetricP(data, "tempo_resposta_ms", "p(99)").toFixed(2));
  console.log("================================\n");

  return {
    "k6/resultado.json": JSON.stringify(data, null, 2),
  };
}