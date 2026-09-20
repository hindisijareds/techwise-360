using System;
using System.Collections;

/// <summary>Registers an immutable server context before the gameplay timer starts.</summary>
public sealed class TechWiseAssessmentSessionService
{
    public TechWiseServerAttempt Attempt { get; private set; }
    public string PortalOrigin { get; private set; }
    readonly string preparedId = Guid.NewGuid().ToString("N");

    public IEnumerator Register(Action<bool, string> callback)
    {
        var student = TechWiseSessionStore.GetProfile()?.id;
        PortalOrigin = TechWisePortalClient.PortalBaseUrl;
        var request = new TechWiseStartRequest
        {
            attempt_id = preparedId,
            simulation_type = TechWiseSimulationModeManager.SimulationType,
            competition_id = TechWiseSimulationModeManager.CompetitionId
        };
        yield return TechWisePortalClient.EnsureInstance().StartAssessmentCoroutine(request, (ok, message, attempt) =>
        {
            if (ok && (student != TechWiseSessionStore.GetProfile()?.id || PortalOrigin != TechWisePortalClient.PortalBaseUrl))
            { callback(false, "Account changed. Return to the menu and start again."); return; }
            if (ok) Attempt = attempt;
            callback(ok, message);
        });
    }
}
