using System;
using System.Collections.Generic;

[Serializable]
public sealed class TechWiseAssessmentScoringSettings
{
    public string version = "complete-build-v2";
    public int resetPreviousPenalty = 3;
    public int minorPenalty = 4;
    public int standardPenalty = 8;
    public int criticalPenalty = 16;
    public int maximumTimePenalty = 20;
    public int overtimeIntervalSeconds = 30;
    public int penaltyPerInterval = 2;
    public int assemblyTargetSeconds = 600;
    public int disassemblyTargetSeconds = 420;

    public TechWiseAssessmentScoringSettings Snapshot()
    {
        var copy = (TechWiseAssessmentScoringSettings)MemberwiseClone();
        copy.minorPenalty = Math.Max(0, Math.Min(100, minorPenalty));
        copy.standardPenalty = Math.Max(copy.minorPenalty, Math.Min(100, standardPenalty));
        copy.criticalPenalty = Math.Max(copy.standardPenalty, Math.Min(100, criticalPenalty));
        // Preserve accuracy's greater influence even with a malformed configuration.
        copy.maximumTimePenalty = Math.Max(0, Math.Min(20, maximumTimePenalty));
        copy.overtimeIntervalSeconds = Math.Max(1, overtimeIntervalSeconds);
        copy.penaltyPerInterval = Math.Max(0, Math.Min(20, penaltyPerInterval));
        copy.assemblyTargetSeconds = Math.Max(1, assemblyTargetSeconds);
        copy.disassemblyTargetSeconds = Math.Max(1, disassemblyTargetSeconds);
        copy.resetPreviousPenalty = Math.Max(0, Math.Min(100, resetPreviousPenalty));
        return copy;
    }
}

[Serializable]
public sealed class TechWiseAssessmentScore
{
    public int final_score, accuracy_percent, completion_percent, mistake_penalty, time_penalty;
}

public static class TechWiseAssessmentScoring
{
    public static string Category(string kind) => kind switch
    {
        "reset_previous" => "Reset to Previous",
        "wrong_screw_hole" => "Incorrect Screw Hole",
        "wrong_part" => "Incorrect Component",
        "invalid_target" => "Invalid Target",
        "incorrect_orientation" => "Incorrect Orientation",
        "wrong_order" => "Incorrect Sequence",
        "missing_component" => "Missing Component",
        "missing_connection" => "Missing Connection",
        "missing_dependency" => "Critical Assembly Error",
        "missing_fastening" => "Incomplete Fastening",
        "incomplete_removal" => "Incomplete Disassembly",
        _ => "Invalid Placement"
    };

    public static void Describe(TechWiseVrMistakeDetail detail, TechWiseAssessmentScoringSettings settings, int order, double elapsed)
    {
        detail.category = Category(detail.kind);
        detail.severity = detail.kind == "missing_dependency" ? "critical" :
            detail.kind == "incorrect_orientation" ? "minor" : "standard";
        detail.score_penalty = detail.severity == "critical" ? settings.criticalPenalty :
            detail.severity == "minor" ? settings.minorPenalty : settings.standardPenalty;
        if (detail.kind == "reset_previous") detail.score_penalty = settings.resetPreviousPenalty;
        detail.order = order;
        detail.elapsed_seconds = elapsed;
        detail.occurred_at_seconds = (int)Math.Min(int.MaxValue, Math.Floor(Math.Max(0, elapsed)));
    }

    public static TechWiseAssessmentScore Calculate(TechWiseAssessmentScoringSettings source,
        IEnumerable<TechWiseVrMistakeDetail> mistakes, int durationSeconds, bool disassembly, int completed, int total)
    {
        var settings = source.Snapshot();
        long penalty = 0;
        foreach (var mistake in mistakes)
            if (mistake != null) penalty = Math.Min(int.MaxValue, penalty + Math.Max(0, mistake.score_penalty));
        var target = disassembly ? settings.disassemblyTargetSeconds : settings.assemblyTargetSeconds;
        var overtime = Math.Max(0L, (long)durationSeconds - target);
        var timePenalty = (int)Math.Min(settings.maximumTimePenalty, overtime / settings.overtimeIntervalSeconds * settings.penaltyPerInterval);
        var completion = total <= 0 ? 0 : (int)Math.Round(100d * Math.Max(0, Math.Min(total, completed)) / total, MidpointRounding.AwayFromZero);
        if (completed < total) completion = Math.Min(99, completion);
        return new TechWiseAssessmentScore
        {
            final_score = Math.Min(completion, (int)Math.Max(0, 100 - penalty - timePenalty)),
            accuracy_percent = Math.Min(completion, (int)Math.Max(0, 100 - penalty)),
            completion_percent = completion, mistake_penalty = (int)penalty, time_penalty = timePenalty
        };
    }
}
