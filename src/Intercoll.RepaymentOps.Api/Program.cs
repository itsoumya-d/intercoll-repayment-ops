using Intercoll.RepaymentOps.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<RepaymentService>();
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var accounts = new Dictionary<string, DebtAccount>
{
    ["DM-1042"] = new("DM-1042", "A. Taylor", 4820m, 320m, 980m, 720m, HardshipStatus.None),
    ["DM-2087"] = new("DM-2087", "M. Singh", 12750m, 850m, 1150m, 1040m, HardshipStatus.FinancialHardship),
    ["DM-3311"] = new("DM-3311", "J. Walker", 2380m, 0m, 1500m, 1100m, HardshipStatus.BreathingSpace)
};
var plans = new Dictionary<string, RepaymentPlan>();
var visits = new List<FieldVisit>();
var audit = new List<AuditEvent>();

app.MapGet("/api/accounts", (RepaymentService svc) => accounts.Values.Select(a =>
    svc.Summarise(a, plans.ContainsKey(a.Id), a.Id == "DM-1042" ? 12 : 4)));

app.MapGet("/api/accounts/{id}", (string id) =>
    accounts.TryGetValue(id, out var account) ? Results.Ok(account) : Results.NotFound());

app.MapPost("/api/accounts/{id}/plan", (string id, PlanRequest request, RepaymentService svc) =>
{
    if (!accounts.TryGetValue(id, out var account)) return Results.NotFound();
    try
    {
        var plan = svc.BuildPlan(account, request);
        plans[id] = plan;
        audit.Add(new(id, "plan.created", $"{plan.ScheduledPayment:0.00} {plan.Frequency}", DateTimeOffset.UtcNow));
        return Results.Ok(plan);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/accounts/{id}/breathing-space", (string id, int? days, RepaymentService svc) =>
{
    if (!accounts.TryGetValue(id, out var account)) return Results.NotFound();
    var plan = svc.ApplyBreathingSpace(account, DateOnly.FromDateTime(DateTime.UtcNow), days ?? 30);
    accounts[id] = account with { Hardship = HardshipStatus.BreathingSpace };
    audit.Add(new(id, "breathing-space.started", plan.Rationale, DateTimeOffset.UtcNow));
    return Results.Ok(plan);
});

app.MapPost("/api/visits", (FieldVisit visit, RepaymentService svc) =>
{
    try
    {
        var saved = svc.RecordVisit(visit);
        visits.Add(saved);
        audit.Add(new(saved.AccountId, "visit.recorded", saved.Outcome.ToString(), DateTimeOffset.UtcNow));
        return Results.Created($"/api/visits/{visits.Count}", saved);
    }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
});

app.MapGet("/api/visits", () => visits);
app.MapGet("/api/audit", () => audit);
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
