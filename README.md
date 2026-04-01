# TicketFlow

  

**Asynchronous ticket order processing system with high concurrency control**
  
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?style=flat-square&logo=docker&logoColor=white)

---

  

## 📌 Overview

  

TicketFlow is a backend system designed to prevent duplicate ticket sales under high concurrency.

  

When multiple users attempt to purchase the same ticket simultaneously, the system ensures that only one order is successfully confirmed, while all others are safely rejected.

  

---

  

## 🎯 Problem

  

In high-demand scenarios, multiple users may attempt to reserve the same resource at the same time, causing:

  

- Duplicate sales  

- Data inconsistency  

- Race conditions  

  

---

  

## 💡 Solution

  

The system uses:

  

- Asynchronous processing (RabbitMQ)

- Optimistic concurrency control

- Idempotency

- Retry with exponential backoff

- Dead-letter queue (DLQ)

  

---

  

## 🏗️ Architecture

  

- API → receives requests  

- Worker → processes orders  

- RabbitMQ → messaging  

- PostgreSQL → persistence  

  

---

  

# 🚀 Installation and Setup (FULL)

  

## 1. Prerequisites

  

Make sure you have installed:

  

- .NET 8 SDK  

- Docker Desktop  

- Git  

  

Optional:

- k6  

  

---

  

## 2. Clone the repository

  

```bash

git clone https://github.com/rafaelviktor29/ticket-flow.git

cd ticket-flow

```

  

---

  

# 🔧 RUNNING THE PROJECT

  

## 🧪 OPTION 1 — Development Mode

  

```bash

docker compose up -d postgres rabbitmq

dotnet run --project src/TicketFlow.API

dotnet run --project src/TicketFlow.Worker

```

  

API:

http://localhost:54049

  

---

  

## 🐳 OPTION 2 — Full Docker Mode

  

```bash

docker compose up --build

docker compose up --build --scale worker=4

```

  

---

  

# 🧪 TESTS

  

```bash

dotnet test

```

  

---

  

# ⚡ LOAD TEST

  

```bash

k6 run -e TICKET_ID=YOUR_TICKET_ID k6/load-test.js

```

  

---

  

# 🎓 AUTHOR

  

- Rafael Viktor Soares Pereira