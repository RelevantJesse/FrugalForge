namespace WowAhPlanner.Core.Domain.Planning;

public sealed record StepAcquireLine(
    int ItemId,
    decimal RequiredQuantity,
    decimal OwnedUsedQuantity,
    decimal AcquireQuantity,
    AcquisitionSource Source);

