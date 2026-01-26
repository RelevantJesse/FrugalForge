namespace WowAhPlanner.Core.Domain.Planning;

using WowAhPlanner.Core.Domain;

public sealed record StepIntermediateActionLine(
    int ItemId,
    decimal RequiredQuantity,
    decimal OwnedUsedQuantity,
    decimal ToProduceQuantity,
    ProducerKind Kind,
    string ProducerName);

