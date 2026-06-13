namespace Sallimni.DriverApp.Models;

public class DriverInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public override string ToString() => Name;
}

public class DriverStopDto
{
    public Guid StopId { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsCompleted { get; set; }
    public decimal? CodAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTimeOffset? EstimatedArrivalAt { get; set; }

    public bool IsPending => !IsCompleted;
    public bool HasEta => EstimatedArrivalAt.HasValue;
    public string EtaText => EstimatedArrivalAt is { } e ? e.ToLocalTime().ToString("HH:mm") : "";
}

public class DriverTaskDto
{
    public Guid TaskId { get; set; }
    public int Type { get; set; }   // 0=Collection, 1=Distribution
    public int Status { get; set; }
    public Guid WaveId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<DriverStopDto> Stops { get; set; } = new();

    public bool IsCollection => Type == 0;
    public bool IsDistribution => Type == 1;
    public int StopCount => Stops.Count;
}
