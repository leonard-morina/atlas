# Local infrastructure

Database, log sink and message broker. Run from the **repository root**:

```
docker compose up -d
```

| | Address | Credentials |
|---|---|---|
| SQL Server | `localhost,1433` | `sa` / `Local_Dev_Password_1` |
| Seq | `http://localhost:5341` | none |
| RabbitMQ | `localhost:5672` · management UI `http://localhost:15672` | `atlas` / `atlas_local_dev` |

All ports and passwords live in the root `.env`, the single source for both Docker Compose and the .NET
services. If a port is already taken (1433 often is on Windows), change it there once.

Running `docker compose up -d` from this folder also works: the `${VAR:-default}` fallbacks in
`docker-compose.yml` then apply, and they match the committed `.env`.

This is infrastructure only. The .NET services run on the host with `dotnet run`, not in containers.
