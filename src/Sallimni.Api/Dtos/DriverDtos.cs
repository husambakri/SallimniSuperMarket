namespace Sallimni.Api.Dtos;

public record DriverInfoDto(Guid Id, string Name, string? Phone);

public record DriverStopDto(Guid StopId, int Sequence, string Label, double Latitude, double Longitude,
    bool IsCompleted, decimal? CodAmount, int ItemCount, DateTimeOffset? EstimatedArrivalAt);

public record DriverTaskDto(Guid TaskId, int Type, int Status, Guid WaveId,
    DateTimeOffset CreatedAt, List<DriverStopDto> Stops);

public record DeliverRequest(decimal CollectedAmount);
