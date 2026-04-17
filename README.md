# dotnet-forge

A reusable ASP.NET Core 8 API template for startups and manufacturing clients,
built in three phases: core API scaffold, manufacturing device hooks, and a
client alert/notification kit.

---

## Phase 1 — Core API

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose v2)
- `.env` file — copy from the example and fill in secrets:

```bash
cp .env.example .env
```

### Start the stack

```bash
docker compose up --build
```

| Service | URL |
|---------|-----|
| API | `http://localhost:5000` |
| Swagger UI | `http://localhost:5000/swagger` |
| Health check | `http://localhost:5000/health` |
| Postgres | `localhost:5432` |

Add `--profile dev` to also start pgAdmin at `http://localhost:5050`.

### Phase 1 endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/auth/register` | No | Create a user account |
| `POST` | `/api/auth/login` | No | Exchange credentials for a JWT |

---

## Phase 2 — Manufacturing Hooks

Phase 2 adds Modbus and OPC-UA device adapters, a background polling service,
SignalR real-time streaming, and sensor-reading persistence.

### Running the device simulators

To test end-to-end without physical hardware, start the simulators included
in the `dev` Compose profile:

```bash
docker compose --profile dev up
```

This starts the following containers alongside the API and Postgres:

| Container | Protocol | Default port |
|-----------|----------|-------------|
| `modbus-sim` | Modbus TCP | 502 |
| `opcua-sim` | OPC-UA | 50000 |
| `pgadmin` | — | 5050 |

The simulator ports are configurable via `MODBUS_SIM_PORT` and
`OPCUA_SIM_PORT` in your `.env` file (see `.env.example`).

### Tag address format

| Protocol | Format | Example | Meaning |
|----------|--------|---------|---------|
| Modbus | `"<type>:<address>:<count>"` | `"HR:0:1"` | Holding register 0, read 1 word |
| OPC-UA | OPC-UA NodeId string | `"ns=2;s=Temperature"` | Namespace 2, node `Temperature` |

### Phase 2 endpoints

All Phase 2 endpoints require a valid JWT (`Authorization: Bearer <token>`).

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/adapters` | Register a new adapter and connect to the device |
| `GET` | `/api/adapters` | List all registered adapters and their connection status |
| `DELETE` | `/api/adapters/{adapterId}` | Disconnect and remove an adapter |
| `GET` | `/api/sensorreadings?adapterId=&tagAddress=&from=&to=&page=&pageSize=` | Query historical sensor readings |
| `GET` | `/api/sensorreadings/latest?adapterId=` | Latest reading for each tag on an adapter |
| WebSocket | `/hubs/device-data` | SignalR hub for real-time tag updates |

### Registering a Modbus adapter

Use the `modbus-sim` hostname when the API runs inside Docker Compose, or
`localhost` when the API runs on the host (`dotnet watch run`):

```bash
curl -X POST http://localhost:5000/api/adapters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-jwt>" \
  -d '{
    "host": "modbus-sim",
    "port": 502,
    "protocol": "modbus",
    "pollIntervalSeconds": 1,
    "tags": ["HR:0:1", "HR:1:1"]
  }'
```

### Registering an OPC-UA adapter

```bash
curl -X POST http://localhost:5000/api/adapters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-jwt>" \
  -d '{
    "host": "opcua-sim",
    "port": 50000,
    "protocol": "opcua",
    "pollIntervalSeconds": 2,
    "tags": ["ns=2;s=Temperature", "ns=2;s=SlowUInt1"]
  }'
```

### Subscribing to live tag updates via SignalR

The `/hubs/device-data` hub requires JWT authentication. After connecting,
call `SubscribeToTag` with the adapter ID returned by `POST /api/adapters`
and the tag address you want to watch. The server pushes `TagUpdate` messages
to the group as each poll completes.

```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5000/hubs/device-data", {
    accessTokenFactory: () => "<your-jwt>",
  })
  .withAutomaticReconnect()
  .build();

// Receive live tag values
connection.on("TagUpdate", (adapterId, tagAddress, value, unit, recordedAt) => {
  console.log(`[${recordedAt}] ${adapterId}/${tagAddress} = ${value} ${unit}`);
});

await connection.start();

// Subscribe to a specific tag
await connection.invoke("SubscribeToTag", "<adapterId>", "HR:0:1");

// Unsubscribe when done
// await connection.invoke("UnsubscribeFromTag", "<adapterId>", "HR:0:1");
```

Install the SignalR client package with:

```bash
npm install @microsoft/signalr
```
