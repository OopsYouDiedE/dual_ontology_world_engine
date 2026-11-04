using DuelView.Compiled;

namespace DuelView.Compiled
{
    public static partial class R
    {
        // The complete installed catalog is the owner, including dependencies
        // outside the queried template. Never freeze a mutable/custom content
        // scope here: that could inspect unused invalid branches too early.
        internal static object? ImmutableContentIdentity =>
            ContentContext.Value is FrozenList definitions ? definitions : null;
    }
}

namespace DuelView.Core
{
    /// <summary>Content-only classifications with immutable scalar inputs.</summary>
    public static class ContentTemplateQueries
    {
        // Fixed tags select separate existing 512-entry FIFO partitions. The
        // full template and argument remain in each key; hash collisions only
        // share capacity, never results. At most 8192 scalars per content owner.
        private static readonly string[] PartitionTags =
        {
            "content-boolean-template-v1:0", "content-boolean-template-v1:1",
            "content-boolean-template-v1:2", "content-boolean-template-v1:3",
            "content-boolean-template-v1:4", "content-boolean-template-v1:5",
            "content-boolean-template-v1:6", "content-boolean-template-v1:7",
            "content-boolean-template-v1:8", "content-boolean-template-v1:9",
            "content-boolean-template-v1:a", "content-boolean-template-v1:b",
            "content-boolean-template-v1:c", "content-boolean-template-v1:d",
            "content-boolean-template-v1:e", "content-boolean-template-v1:f"
        };

        private static string PartitionTag(string id, string argument)
        {
            // FNV-1a over ordinal UTF-16 units is stable across processes,
            // cultures and platforms. No per-call partition string is built.
            uint hash = 2166136261;
            unchecked
            {
                foreach (char unit in id) hash = (hash ^ unit) * 16777619;
                hash *= 16777619; // Separate the template from its argument.
                foreach (char unit in argument) hash = (hash ^ unit) * 16777619;
            }
            return PartitionTags[(int)(hash & 15)];
        }

        // Only explicit C# entry calls use this cache. Nested Evaluation nodes
        // retain their original Spend, depth and character accounting.
        public static object InstantiateBoolean(string id, object argument)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            if (argument is not string text || !R.Bool(R.memo_group_enabled("declaration")))
                return DvValueTemplate.Instantiate(id, argument);

            var owner = R.ImmutableContentIdentity;
            if (owner is null) return DvValueTemplate.Instantiate(id, argument);

            // Reuse the existing session-local, bounded memo partitions. A
            // different full catalog, template or input cannot share a result.
            var key = new List<object>(3) { PartitionTag(id, text), id, text };
            var cached = R.memo_read(owner, key);
            if (R.Bool(R.at(cached, 0d)))
            {
                var value = R.at(cached, 1d);
                if (value is bool) return value;
            }

            // Exceptions and non-boolean results are never retained. Preserve
            // the original return value and the caller's normal type check.
            var result = DvValueTemplate.Instantiate(id, argument);
            if (result is bool) R.memo_put(owner, key, result);
            return result;
        }
    }
}
