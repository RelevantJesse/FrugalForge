namespace WowAhPlanner.Core.Domain.Planning;

public sealed record PlanStepBreakdown(
    int StepIndex,
    IReadOnlyList<StepIntermediateActionLine> Intermediates,
    IReadOnlyList<StepAcquireLine> Acquisitions);

