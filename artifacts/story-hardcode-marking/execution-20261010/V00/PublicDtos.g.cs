// Generated from EXACT authorized production declarations by verify_v00.py.
// This is a declaration-only compile fixture, NOT the production engine.
#nullable enable
namespace DualOntologyWorldEngine.Story;

public abstract record WorldInput;
public sealed record SelectCandidate(string Token) : WorldInput;
public abstract record WorldOutput;
public sealed record AvailableCandidate(string Token, string Id, string Label);
public sealed record CandidatesAvailable(string Actor, IReadOnlyList<AvailableCandidate> Candidates) : WorldOutput;
public sealed record SemanticDialogue(string PresentationId, IReadOnlyList<InteractionLine> Lines) : WorldOutput;
public sealed record NoApplicableCandidate(string Actor) : WorldOutput;

public sealed record InteractionLine(string Speaker, string Mood, string Text, string VoiceClip, string LineId = "", string SourceId = "");
public sealed record InteractionChoice(string Id, string Label, bool IsAvailable, string LockHint, string LabelId = "");
public sealed record WorldCommand(string Name, IReadOnlyList<object?> Arguments);
public enum PresentationMode { Dialogue, EvidenceCard, Menu, World, Done }
public sealed record EvidenceCard(string Id, string Title, string Facts, string Source, string Picture, IReadOnlyList<string> Notes);
public sealed record InteractionPacket(IReadOnlyList<InteractionLine> Lines, IReadOnlyList<InteractionChoice> Choices,
    PresentationMode PresentationMode, IReadOnlyList<WorldCommand> WorldCommands, bool IsComplete = false, EvidenceCard? Card = null);
