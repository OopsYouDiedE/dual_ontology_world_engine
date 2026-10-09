using DualOntologyWorldEngine.Story;

namespace V00.PublicPlayer;

// Trusted host call site. ControllerProcess receives only serialized visible data.
// The host owns lifecycle/perception/presentation; none are controller commands.
public sealed class PublicPlayerDriver(CandidateBoundary boundary, ControllerProcess controller)
{
    public async Task<IReadOnlyList<WorldOutput>> TurnAsync(IReadOnlyList<WorldOutput> outputs,
        InteractionPacket? presentation = null)
    {
        var observation = boundary.Observe(outputs, presentation);
        try
        {
            return boundary.Accept(await controller.DecideAsync(observation));
        }
        catch (Exception error) when (error is BoundaryRejected or IOException or System.Text.DecoderFallbackException)
        {
            // A failed child cannot be restarted against still-valid offers.
            // An explicit new game/session must create a fresh boundary.
            boundary.TransportFailure(error is BoundaryRejected rejected ? rejected.Code : "controller_transport_error");
            throw;
        }
    }
}
