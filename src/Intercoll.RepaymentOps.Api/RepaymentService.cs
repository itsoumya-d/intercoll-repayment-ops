namespace Intercoll.RepaymentOps.Api;

public sealed class RepaymentService
{
    private static readonly Dictionary<PlanFrequency, int> DaysPerInstalment = new()
    {
        [PlanFrequency.Weekly] = 7,
        [PlanFrequency.Fortnightly] = 14,
        [PlanFrequency.Monthly] = 30
    };

    public RepaymentPlan BuildPlan(DebtAccount account, PlanRequest request)
    {
        if (account.Balance <= 0) throw new ArgumentException("Balance must be positive.", nameof(account));
        if (account.Disputed) throw new InvalidOperationException("Cannot plan while the debt is disputed.");
        if (request.Offer <= 0) throw new ArgumentException("Offer must be positive.", nameof(request));
        if (request.StartDate < DateOnly.FromDateTime(DateTime.UtcNow)) throw new ArgumentException("Start date cannot be in the past.", nameof(request));

        var affordable = AffordableAmount(account);
        var payment = Math.Min(request.Offer, affordable);
        if (payment <= 0) throw new InvalidOperationException("No affordable repayment available.");

        if (account.Hardship == HardshipStatus.BreathingSpace)
            throw new InvalidOperationException("Breathing space is active; no payment is due.");

        var instalments = (int)Math.Ceiling(account.Balance / payment);
        if (request.MaxInstalments is > 0 && instalments > request.MaxInstalments)
            throw new InvalidOperationException("The plan exceeds the maximum term.");

        var final = request.StartDate.AddDays(DaysPerInstalment[request.Frequency] * (instalments - 1));
        var rationale = $"Affordability cap {affordable:0.00}; offer {request.Offer:0.00}; no fees or interest added.";
        return new RepaymentPlan(account.Id, payment, request.Frequency, instalments, payment * instalments,
            request.StartDate, final, rationale);
    }

    public decimal AffordableAmount(DebtAccount account)
    {
        if (account.Hardship is HardshipStatus.FinancialHardship or HardshipStatus.Vulnerability)
            return Math.Max(0, Math.Round((account.WeeklyIncome - account.EssentialSpend) * 0.25m, 2));
        var surplus = Math.Max(0, account.WeeklyIncome - account.EssentialSpend);
        return Math.Round(surplus * 0.5m, 2);
    }

    public RepaymentPlan ApplyBreathingSpace(DebtAccount account, DateOnly today, int days = 30)
    {
        if (days is < 7 or > 90) throw new ArgumentOutOfRangeException(nameof(days));
        var updated = account with { Hardship = HardshipStatus.BreathingSpace };
        var restart = today.AddDays(days);
        return new RepaymentPlan(updated.Id, 0, PlanFrequency.Weekly, 0, account.Balance,
            restart, restart, $"Breathing space for {days} days; no payments due.");
    }

    public FieldVisit RecordVisit(FieldVisit visit)
    {
        if (string.IsNullOrWhiteSpace(visit.Notes)) throw new ArgumentException("Visit notes are required.");
        if (visit.PaymentAgreed is < 0) throw new ArgumentException("Payment cannot be negative.");
        if (visit.Outcome == VisitOutcome.AgreedPlan && visit.PaymentAgreed is null or 0)
            throw new ArgumentException("An agreed plan needs a payment amount.");
        return visit with { Notes = visit.Notes.Trim() };
    }

    public AccountSummary Summarise(DebtAccount account, bool activePlan, int daysSinceContact)
    {
        var next = account.Disputed ? "Pause collection; investigate dispute"
            : account.Hardship == HardshipStatus.BreathingSpace ? "No contact until breathing space ends"
            : account.Hardship is HardshipStatus.FinancialHardship or HardshipStatus.Vulnerability ? "Refer to wellbeing team and financial mentor"
            : account.Arrears > 0 ? "Contact customer and review affordability"
            : activePlan ? "Monitor plan"
            : "No plan; invite a repayment conversation";
        return new AccountSummary(account.Id, account.Balance, account.Arrears, account.Hardship, activePlan, daysSinceContact, next);
    }

    public bool IsWriteOffEligible(DebtAccount account, int monthsInHardship, bool committeeApproved) =>
        committeeApproved && monthsInHardship >= 6 && account.Balance <= 5000 && account.Arrears > 0;
}
