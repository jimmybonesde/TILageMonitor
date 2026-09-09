namespace TILageMonitor;

public sealed class AppStatus
{
    public string Outage { get; set; } = "none";
    public bool HasMaintenance { get; set; }
    public bool HasSubComponentMaintenance { get; set; }
    public List<AffectedFunction> AffectedFunctions { get; set; } = new();
}

public sealed class AffectedFunction
{
    public string Function { get; set; } = "";
    public int Critical { get; set; }
    public string ImpactDesc { get; set; } = "";
    public string Outage { get; set; } = "none";
    public bool HasMaintenance { get; set; }
}

public sealed class LageV2
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, AppStatus> AppStatus { get; set; } = new();
    public List<Cause> Cause { get; set; } = new();
}

public sealed class Cause
{
    public string Ci { get; set; } = "";
    public string Component { get; set; } = "";
    public string Service { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Function { get; set; } = "";
}

public sealed class IncidentResponse
{
    public bool Success { get; set; }
    public List<Incident> Data { get; set; } = new();
}

public sealed class Incident
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int Status { get; set; }
    public List<string> App { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<IncidentStep> Steps { get; set; } = new();
}

public sealed class IncidentStep
{
    public int Id { get; set; }
    public string Message { get; set; } = "";
    public int Status { get; set; }
    public DateTime Timestamp { get; set; }
    public bool HasMaintenance { get; set; }
}

public sealed class OutageResponse
{
    public bool Success { get; set; }
    public List<Outage> Data { get; set; } = new();
}

public sealed class Outage
{
    public string Ci { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Service { get; set; } = "";
    public List<OutageSlot> Slots { get; set; } = new();
}

public sealed class OutageSlot
{
    public string Function { get; set; } = "";
    public DateTime StartTimestamp { get; set; }
    public DateTime? EndTimestamp { get; set; }
}
