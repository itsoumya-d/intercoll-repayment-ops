using Xunit;
using Intercoll.RepaymentOps.Api;

namespace Intercoll.RepaymentOps.Tests;

public class RepaymentServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Start = Today.AddDays(1);
    private readonly RepaymentService _svc = new();
    private static DebtAccount Account(decimal balance = 4800m, decimal arrears = 300m,
        HardshipStatus hardship = HardshipStatus.None, bool disputed = false) =>
        new("DM-1", "Test", balance, arrears, 1000m, 700m, hardship, disputed);

    [Fact]
    public void AffordableAmount_IsHalfSurplus_ForStandardAccount() =>
        Assert.Equal(150m, _svc.AffordableAmount(Account()));

    [Fact]
    public void AffordableAmount_UsesQuarterSurplus_ForHardship() =>
        Assert.Equal(75m, _svc.AffordableAmount(Account(hardship: HardshipStatus.FinancialHardship)));

    [Fact]
    public void AffordableAmount_IsZero_WhenEssentialsExceedIncome() =>
        Assert.Equal(0m, _svc.AffordableAmount(new("x", "x", 100m, 0m, 500m, 600m, HardshipStatus.None)));

    [Fact]
    public void BuildPlan_CapsOfferAtAffordability()
    {
        var plan = _svc.BuildPlan(Account(), new(500m, PlanFrequency.Weekly, Start));
        Assert.Equal(150m, plan.ScheduledPayment);
    }

    [Fact]
    public void BuildPlan_UsesLowerOffer()
    {
        var plan = _svc.BuildPlan(Account(), new(50m, PlanFrequency.Weekly, Start));
        Assert.Equal(50m, plan.ScheduledPayment);
    }

    [Fact]
    public void BuildPlan_ComputesInstalments()
    {
        var plan = _svc.BuildPlan(Account(4800m), new(150m, PlanFrequency.Weekly, Start));
        Assert.Equal(32, plan.Instalments);
        Assert.Equal(4800m, plan.TotalRepayable);
    }

    [Fact]
    public void BuildPlan_FinalDateUsesFrequency()
    {
        var plan = _svc.BuildPlan(Account(600m), new(150m, PlanFrequency.Weekly, Start));
        Assert.Equal(Start.AddDays(21), plan.FinalPayment);
    }

    [Fact]
    public void BuildPlan_FortnightlyCadence()
    {
        var plan = _svc.BuildPlan(Account(900m), new(150m, PlanFrequency.Fortnightly, Start));
        Assert.Equal(Start.AddDays(70), plan.FinalPayment);
    }

    [Fact]
    public void BuildPlan_MonthlyCadence()
    {
        var plan = _svc.BuildPlan(Account(900m), new(150m, PlanFrequency.Monthly, Start));
        Assert.Equal(Start.AddDays(150), plan.FinalPayment);
    }

    [Fact]
    public void BuildPlan_RejectsDisputedDebt()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _svc.BuildPlan(Account(disputed: true), new(150m, PlanFrequency.Weekly, Start)));
    }

    [Fact]
    public void BuildPlan_RejectsPastStart()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.BuildPlan(Account(), new(150m, PlanFrequency.Weekly, Today.AddDays(-1))));
    }

    [Fact]
    public void BuildPlan_RejectsNonPositiveOffer()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.BuildPlan(Account(), new(0m, PlanFrequency.Weekly, Start)));
    }

    [Fact]
    public void BuildPlan_RejectsZeroBalance()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.BuildPlan(Account(0m), new(10m, PlanFrequency.Weekly, Start)));
    }

    [Fact]
    public void BuildPlan_RejectsBreathingSpaceAccount()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _svc.BuildPlan(Account(hardship: HardshipStatus.BreathingSpace), new(150m, PlanFrequency.Weekly, Start)));
    }

    [Fact]
    public void BuildPlan_RejectsNoAffordability()
    {
        var account = new DebtAccount("x", "x", 1000m, 0m, 500m, 600m, HardshipStatus.None);
        Assert.Throws<InvalidOperationException>(() =>
            _svc.BuildPlan(account, new(150m, PlanFrequency.Weekly, Start)));
    }

    [Fact]
    public void BuildPlan_EnforcesMaxInstalments()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _svc.BuildPlan(Account(4800m), new(150m, PlanFrequency.Weekly, Start, 10)));
    }

    [Fact]
    public void BuildPlan_ExplainsNoFees()
    {
        var plan = _svc.BuildPlan(Account(), new(150m, PlanFrequency.Weekly, Start));
        Assert.Contains("no fees or interest", plan.Rationale);
    }

    [Fact]
    public void BreathingSpace_ReturnsZeroPlan()
    {
        var plan = _svc.ApplyBreathingSpace(Account(), Today, 30);
        Assert.Equal(0m, plan.ScheduledPayment);
        Assert.Equal(Today.AddDays(30), plan.FirstPayment);
    }

    [Fact]
    public void BreathingSpace_RejectsShortWindow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _svc.ApplyBreathingSpace(Account(), Today, 5));

    [Fact]
    public void BreathingSpace_RejectsLongWindow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _svc.ApplyBreathingSpace(Account(), Today, 91));

    [Fact]
    public void Visit_RequiresNotes() =>
        Assert.Throws<ArgumentException>(() =>
            _svc.RecordVisit(new("DM-1", Today, VisitOutcome.NoAnswer, "  ")));

    [Fact]
    public void Visit_AgreedPlanRequiresPayment() =>
        Assert.Throws<ArgumentException>(() =>
            _svc.RecordVisit(new("DM-1", Today, VisitOutcome.AgreedPlan, "Agreed", null)));

    [Fact]
    public void Visit_TrimsNotes()
    {
        var v = _svc.RecordVisit(new("DM-1", Today, VisitOutcome.AgreedPlan, "  agreed  ", 40m));
        Assert.Equal("agreed", v.Notes);
    }

    [Fact]
    public void Visit_RejectsNegativePayment() =>
        Assert.Throws<ArgumentException>(() =>
            _svc.RecordVisit(new("DM-1", Today, VisitOutcome.UnableToPay, "note", -1m)));

    [Fact]
    public void Summary_FlagsDispute()
    {
        var s = _svc.Summarise(Account(disputed: true), false, 2);
        Assert.Contains("investigate dispute", s.NextAction);
    }

    [Fact]
    public void Summary_FlagsHardship()
    {
        var s = _svc.Summarise(Account(hardship: HardshipStatus.FinancialHardship), false, 2);
        Assert.Contains("wellbeing", s.NextAction);
    }

    [Fact]
    public void Summary_FlagsBreathingSpace()
    {
        var s = _svc.Summarise(Account(hardship: HardshipStatus.BreathingSpace), false, 2);
        Assert.Contains("breathing space", s.NextAction);
    }

    [Fact]
    public void Summary_FlagsArrears()
    {
        var s = _svc.Summarise(Account(arrears: 10m), false, 2);
        Assert.Contains("affordability", s.NextAction);
    }

    [Fact]
    public void Summary_MonitorsPlan()
    {
        var s = _svc.Summarise(Account(arrears: 0m), true, 2);
        Assert.Contains("Monitor", s.NextAction);
    }

    [Fact]
    public void WriteOff_NeedsCommitteeAndHardshipAndCap()
    {
        Assert.True(_svc.IsWriteOffEligible(Account(4000m), 6, true));
        Assert.False(_svc.IsWriteOffEligible(Account(4000m), 6, false));
        Assert.False(_svc.IsWriteOffEligible(Account(6000m), 12, true));
        Assert.False(_svc.IsWriteOffEligible(Account(4000m, arrears: 0m), 12, true));
    }
}
