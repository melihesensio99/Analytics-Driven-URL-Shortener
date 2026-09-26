# 🚀 Analytics-Driven & Event-Driven Enterprise URL Shortener

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7.0-DC382D?logo=redis)](https://redis.io/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.13-FF6600?logo=rabbitmq)](https://www.rabbitmq.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)](https://www.docker.com/)
[![Clean Architecture](https://img.shields.io/badge/Architecture-Clean%20%26%20SOLID-00599C)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)

A high-throughput, enterprise-grade distributed URL Shortener API built with **.NET 10**, **Clean Architecture**, **SOLID Principles**, **Redis Read-Through Caching**, **Base62 Collision-Free Encoding**, **PostgreSQL MVCC Table Bloat Prevention**, and an **Event-Driven RabbitMQ Analytics Pipeline**.

---

## 🏛️ System Architecture

```mermaid
graph TD
    Client[🌐 User Browser / App] -->|HTTP Request| API[⚡ ASP.NET Core 10 Web API]

    subgraph Security Katmanı
        API -->|Check IP Limits| RL[🛡️ IP Rate Limiter - Fixed Window]
    end

    subgraph High-Speed Yönlendirme (Read Path < 2ms)
        API -->|1. Try Cache| Redis[(⚡ Redis 7 In-Memory Cache)]
        Redis -.->|Cache Miss| PG[(🐘 PostgreSQL 16 Main DB)]
    end

    subgraph Event-Driven Asenkron Analitik Hattı
        API -->|2. Fire-and-Forget Publish| MQ[🐇 RabbitMQ Message Queue]
        MQ -->|Consume Batch| Worker[⚙️ UrlClickConsumerWorker BackgroundService]
        Worker -->|3. Bulk Insert 1000s| Logs[(📊 PostgreSQL url_click_logs)]
    end
```

---

## ⚡ Interactive Sequence Flow

```mermaid
sequenceDiagram
    participant User as 🌐 User / Client
    participant Controller as ⚡ Controller (API)
    participant Redis as ⚡ Redis Cache
    participant MQ as 🐇 RabbitMQ Queue
    participant Worker as ⚙️ Consumer Worker
    participant DB as 🐘 PostgreSQL DB

    User->>Controller: GET /{shortCode} (e.g. GET /2d)
    Controller->>Redis: Check key "url:2d"
    alt Cache HIT (< 2ms)
        Redis-->>Controller: Return LongUrl
    else Cache MISS
        Controller->>DB: Query short_code = '2d'
        DB-->>Controller: Return LongUrl
        Controller->>Redis: Populate Cache (TTL 24h)
    end
    Controller-->>User: HTTP 302 Found (Instant Redirect)

    Note over Controller,MQ: Asynchronous Non-Blocking Event Publishing
    Controller-)MQ: Publish UrlClickedEvent { ShortCode, IP, UserAgent, Referer, Time }
    
    Note over MQ,Worker: Batch Processing Loop (Count >= 10 OR Timeout >= 3s)
    MQ-)Worker: Consume Messages
    Worker->>DB: Bulk Insert into url_click_logs
    Worker->>MQ: BasicAck (Clear Messages)
```

---

## 🧠 Solved System Design Problems & Architectural Highlights

| # | System Design Problem | Risk / Trade-off | Architectural Solution |
|---|---|---|---|
| **1** | **Database Read Bottleneck** | 99% of requests are reads. Direct DB queries cause Connection Pool Exhaustion. | **Redis Read-Through Cache** (< 2ms lookups, reduces DB load by >95%). |
| **2** | **Short Code Collisions** | Truncated MD5/SHA256 hashes cause collisions & costly DB retry loops. | **Auto-Increment ID + Base62 Encoding** (Mathematically guaranteed zero collisions). |
| **3** | **PostgreSQL MVCC Table Bloat** | `UPDATE click_count` creates Dead Tuples, bloating tables to GBs & fragmenting indexes. | **Immutable Main Table + Insert-Only Analytics Logs + Atomic Redis `INCR` Counters**. |
| **4** | **Redirect Latency & Telemetry** | Extracting IP, Geo, & User-Agent synchronously delays redirects from 2ms to 200ms. | **RabbitMQ Asynchronous Publishing + Background Batch Consumer Worker**. |
| **5** | **Bot & DDOS Abuse** | Bots flooding `POST /shorten` fill storage with spam URLs. | **IP-Based ASP.NET Core Rate Limiting** (`HTTP 429 Too Many Requests`). |
| **6** | **HTTP Redirect Code Choice** | 301 vs 302 mismatch causes lost metrics or server overload. | **HTTP 302 Found (`permanent: false`)** to capture telemetry on every click. |

---

## 🔢 Base62 Encoding Math vs Base64

Base62 uses `0-9`, `a-z`, `A-Z` (62 URL-safe characters), eliminating URL-breaking special characters (`+`, `/`, `=`) found in Base64.

$$\text{Capacity} = 62^\text{Length}$$

| Character Length | Unique Combination Capacity |
|---|---|
| 3 Characters | 238,328 |
| 5 Characters | 916,132,832 (~916 Million) |
| **6 Characters** | **56,800,235,584 (~56.8 Billion)** |
| **7 Characters** | **3,521,614,606,208 (~3.5 Trillion)** |

---

## 🛠️ Getting Started (Docker Compose)

### Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed.

### 1. Clone & Spin Up
```bash
git clone https://github.com/melihesensio99/Analytics-Driven-URL-Shortener.git
cd Analytics-Driven-URL-Shortener
docker compose up --build -d
```

### 2. Available Services
- 🌐 **REST API & Redirect Service:** `http://localhost:5000`
- 📑 **Interactive Swagger UI:** `http://localhost:5000/swagger`
- 🐇 **RabbitMQ Management Dashboard:** `http://localhost:15672` *(User: `guest`, Pass: `guest`)*
- 🐘 **PostgreSQL Database:** `localhost:5432`
- ⚡ **Redis Cache:** `localhost:6379`

---

## 📑 API Reference

### 1. Shorten URL
`POST /api/urlshortener/shorten`

**Request:**
```json
{
  "url": "https://github.com/donnemartin/system-design-primer"
}
```

**Response (HTTP 200 OK):**
```json
{
  "shortCode": "1",
  "shortUrl": "http://localhost:5000/1",
  "longUrl": "https://github.com/donnemartin/system-design-primer",
  "createdAt": "2026-09-26T20:00:00Z"
}
```

### 2. Redirect Short URL
`GET /{shortCode}` $\rightarrow$ **HTTP 302 Temporary Redirect**

### 3. Get URL Analytics
`GET /api/urlshortener/analytics/{shortCode}`

**Response (HTTP 200 OK):**
```json
{
  "shortCode": "1",
  "longUrl": "https://github.com/donnemartin/system-design-primer",
  "createdAt": "2026-09-26T20:00:00Z",
  "totalClicks": 125,
  "dbClicks": 100,
  "cachedClicks": 25
}
```

---

## 🧩 SOLID & Clean Architecture Mapping

- **Single Responsibility Principle (SRP):** `Base62Encoder` handles numeric encoding only; `RedisCacheService` manages Redis keys only; `RabbitMQPublisher` publishes events only.
- **Open/Closed Principle (OCP):** Interface abstractions (`ICacheService`, `IMessagePublisher`) allow replacing Redis or RabbitMQ without touching core domain or application logic.
- **Liskov Substitution Principle (LSP):** Concrete infrastructure implementations fulfill interface contracts seamlessly.
- **Interface Segregation Principle (ISP):** Small, focused interfaces (`IUrlRepository`, `ICacheService`, `IBase62Encoder`).
- **Dependency Inversion Principle (DIP):** High-level application service (`UrlShortenerService`) depends strictly on abstractions (`ICacheService`, `IUrlRepository`), not concrete low-level drivers.

---

## 📝 License
This project is open-source under the MIT License.
