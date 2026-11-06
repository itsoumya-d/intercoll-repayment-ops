namespace Intercoll.RepaymentOps.Api;

public enum PlanFrequency { Weekly, Fortnightly, Monthly }
public enum HardshipStatus { None, BreathingSpace, FinancialHardship, Vulnerability }
public enum VisitOutcome { AgreedPlan, UnableToPay, WrongPerson, Disputed, NoAnswer, Callback }

public sealed record DebtAccount(
    string Id,
    string CustomerName,
    decimal Balance,
    decimal Arrears,
    decimal WeeklyIncome,
    decimal EssentialSpend,
    HardshipStatus Hardship,
    bool Disputed = false);

public sealed record RepaymentPlan(
    string AccountId,
    decimal ScheduledPayment,
    PlanFrequency Frequency,
    int Instalments,
    decimal TotalRepayable,
    DateOnly FirstPayment,
    DateOnly FinalPayment,
    string Rationale);

public sealed record PlanRequest(
    decimal Offer,
    PlanFrequency Frequency,
    DateOnly StartDate,
    int? MaxInstalments = null);

public sealed record FieldVisit(
    string AccountId,
    DateOnly VisitDate,
    VisitOutcome Outcome,
    string Notes,
    decimal? PaymentAgreed = null,
    DateOnly? FollowUp = null);

public sealed record AccountSummary(
    string AccountId,
    decimal Balance,
    decimal Arrears,
    HardshipStatus Hardship,
    bool HasActivePlan,
    int DaysSinceContact,
    string NextAction);

public sealed record AuditEvent(string AccountId, string Action, string Detail, DateTimeOffset At);
