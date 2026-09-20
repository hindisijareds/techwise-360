using System;

/// <summary>Attempt identity and monotonic elapsed time, independent of frames and timeScale.</summary>
public sealed class TechWiseAssessmentClock
{
    public string AttemptId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public bool Started { get; private set; }
    public bool Submitted { get; private set; }
    double startedAt, elapsed;

    public bool Start(double monotonicSeconds, DateTime utc, string registeredId = null)
    {
        if (Started || double.IsNaN(monotonicSeconds) || double.IsInfinity(monotonicSeconds)) return false;
        AttemptId = string.IsNullOrEmpty(registeredId) ? Guid.NewGuid().ToString("N") : Guid.Parse(registeredId).ToString("N");
        StartedAtUtc = utc;
        startedAt = monotonicSeconds;
        Started = true;
        return true;
    }

    public double Elapsed(double monotonicSeconds)
    {
        if (Started && !Submitted && !double.IsNaN(monotonicSeconds) && !double.IsInfinity(monotonicSeconds))
            elapsed = Math.Max(elapsed, Math.Max(0, monotonicSeconds - startedAt));
        return elapsed;
    }

    public bool Submit(double monotonicSeconds)
    {
        if (!Started || Submitted) return false;
        Elapsed(monotonicSeconds);
        Submitted = true;
        return true;
    }
}
