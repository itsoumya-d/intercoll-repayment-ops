# intercoll-repayment-ops

Reference data and operations service for a New Zealand consumer-debt purchaser. It models fair repayment plans, affordability caps, breathing space, field visits, disputes, hardship referrals, write-off checks and audit events.

## Stack

- .NET 10 minimal API
- C# domain service with xUnit tests
- Static operations dashboard in `wwwroot`
- GitHub Actions CI

## Run

```bash
dotnet run --project src/Intercoll.RepaymentOps.Api
dotnet test
```

## API

- `GET /api/accounts`
- `GET /api/accounts/{id}`
- `POST /api/accounts/{id}/plan`
- `POST /api/accounts/{id}/breathing-space`
- `POST /api/visits`
- `GET /api/visits`
- `GET /api/audit`
- `GET /health`
