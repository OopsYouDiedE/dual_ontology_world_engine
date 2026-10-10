using System.Collections.Immutable;
using System.Text;

namespace DualOntologyWorldEngine.Core;

public readonly record struct EntityId(string Value);
public readonly record struct FacetId(string Value);
public readonly record struct AuthorTermId(string Value);
public readonly record struct SubjectProjectionId(Guid SubjectWorld, Guid Local);
public readonly record struct KnowledgeRevision(long Value);
public readonly record struct VocabularyRevision(long Value);

public enum ContractErrorCode
{
    InvalidIdentity, DuplicateIdentity, InvalidReference, InvalidRevision, StaleRevision, InvalidBatch,
    Unsupported, MalformedData, LimitExceeded, RevisionOverflow, NotificationFault, Unrecognized, Ambiguous,
    UnsupportedSavePoint
}
public sealed record ContractError(ContractErrorCode Code, string Path, string Message,
    string? UnsupportedKind = null, KnowledgeRevision? ExpectedRevision = null,
    KnowledgeRevision? ActualRevision = null, ImmutableArray<SubjectProjectionId> CandidateIds = default);
public abstract record ContractResult<T>
{
    public sealed record Success(T Value) : ContractResult<T>;
    public sealed record Failure(ContractError Error) : ContractResult<T>;
}

public sealed class CognitionContractException(ContractError error) : InvalidOperationException(error.Message)
{
    public ContractError Error { get; } = error;
}

internal static class Contract
{
    internal static void Require(bool valid, ContractErrorCode code, string path, string message)
    {
        if (!valid) throw new CognitionContractException(new(code, path, message, CandidateIds: []));
    }
    internal static T Value<T>(ContractResult<T> result) => result switch
    {
        ContractResult<T>.Success s => s.Value,
        ContractResult<T>.Failure f => throw new CognitionContractException(f.Error),
        _ => throw new InvalidOperationException()
    };
    internal static ContractResult<T> Try<T>(Func<T> body)
    {
        try { return new ContractResult<T>.Success(body()); }
        catch (CognitionContractException e) { return new ContractResult<T>.Failure(e.Error); }
    }
    internal static void Identity(string? value, string path)
        => Require(!string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.IsNormalized(NormalizationForm.FormC),
            ContractErrorCode.InvalidIdentity, path, "持久身份必须为非空 NFC，且不含首尾空白。");
    internal static void Id(SubjectProjectionId id, Guid world, string path)
    {
        Require(id.Local != Guid.Empty && id.SubjectWorld != Guid.Empty, ContractErrorCode.InvalidIdentity, path, "主体句柄不能为空。");
        Require(id.SubjectWorld == world, ContractErrorCode.InvalidReference, path, "主体句柄属于不同世界。");
    }
    internal static void Enum<T>(T value, string path) where T : struct, Enum
        => Require(System.Enum.IsDefined(value), ContractErrorCode.Unsupported, path, "不支持的枚举值。");
    internal static void Array<T>(ImmutableArray<T> value, string path)
        => Require(!value.IsDefault, ContractErrorCode.MalformedData, path, "数组不能为 default。");
    internal static void Text(string? value, string path)
        => Require(value is not null, ContractErrorCode.MalformedData, path, "必要文本不能为空值。");
    internal static void Number(double value, string path, bool nonnegative = false)
        => Require(double.IsFinite(value) && (!nonnegative || value >= 0), ContractErrorCode.MalformedData, path, "数值必须有限且符合范围。");
    internal static string Opaque() => "opaque:" + Guid.NewGuid().ToString("D");
}
