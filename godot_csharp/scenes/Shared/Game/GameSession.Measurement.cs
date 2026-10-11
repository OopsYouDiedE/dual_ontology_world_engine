using System.Text.Json;
using DualOntologyWorldEngine.Awakening;

namespace DualOntologyWorldEngine.Gameplay;

public sealed partial class GameSession
{
    public string ReadMilestoneProgressJson()
        => _session is Chapter1Session chapter
            ? JsonSerializer.Serialize(chapter.ReadMilestoneProgress())
            : JsonSerializer.Serialize(new { chapter = _session?.ChapterId, available = false });
}
