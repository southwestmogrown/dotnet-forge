# dotnet-forge

## Phase 2 — Device Simulator Setup

Phase 2 adds Modbus and OPC-UA device adapters. To test end-to-end without
physical hardware, start the simulators included in the `dev` profile:

```bash
docker compose --profile dev up
```

This starts the Modbus TCP simulator (`modbus-sim` on port 502) and the
OPC-UA PLC simulator (`opcua-sim` on port 50000) alongside pgAdmin, the
API, and Postgres.

### Pointing the API at the Modbus simulator

Register a Modbus adapter by calling `POST /api/adapters` with the
`modbus-sim` container hostname (when the API runs inside Docker Compose)
or `localhost` (when the API runs on the host):

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

> If the API is running on the host (e.g. via `dotnet watch run`), replace
> `"host": "modbus-sim"` with `"host": "localhost"`.

### Pointing the API at the OPC-UA simulator

```bash
curl -X POST http://localhost:5000/api/adapters \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-jwt>" \
  -d '{
    "host": "opcua-sim",
    "port": 50000,
    "protocol": "opcua",
    "pollIntervalSeconds": 2,
    "tags": ["ns=2;s=SlowUInt1"]
  }'
```

The simulator ports are configurable via `MODBUS_SIM_PORT` and
`OPCUA_SIM_PORT` in your `.env` file (see `.env.example`).
