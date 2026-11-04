using Daoris.Contracts.Parts;

namespace Daoris.Knowledge;

/// <summary>
/// The quest log's kinds of operation, each registered by its module (<see cref="QuestOperationAttribute"/>), ordered and built once per
/// process: what the store writes and reads a row's payload through, what its rebase and a remote's push ask of a kind, and the module
/// whose verbs a store's door reaches by type.
/// </summary>
internal sealed class QuestOperations
{
    private static readonly Lazy<QuestOperations> Built =
        new(() => Of(Parts.Declared<QuestOperationAttribute>(typeof(QuestOperations).Assembly)));

    private readonly PartSet<IQuestOperation> _parts;
    private readonly Dictionary<QuestOperationKind, IQuestOperation> _byKind;

    private QuestOperations(PartSet<IQuestOperation> parts, Dictionary<QuestOperationKind, IQuestOperation> byKind)
    {
        _parts = parts;
        _byKind = byKind;
    }

    /// <summary>This assembly's registered operations. The store's open asks for them first, so a registration refused fails the open.</summary>
    /// <exception cref="PartOrderException">The marks cannot be ordered, or a module cannot be built or names another kind.</exception>
    internal static QuestOperations Registered => Built.Value;

    /// <summary>The declarations, in the kit's order.</summary>
    internal IReadOnlyList<PartDeclaration> Order => _parts.Order;

    /// <summary>
    /// <paramref name="order"/>'s modules built, each keyed by its kind. A module whose mark names another kind than its own is refused,
    /// so a kind declared twice is a name declared twice, which the kit refuses as it orders.
    /// </summary>
    /// <exception cref="PartOrderException">A module cannot be built, or its mark names another kind.</exception>
    internal static QuestOperations Of(IReadOnlyList<PartDeclaration> order)
    {
        var parts = Parts.Build(order, type => (IQuestOperation)Activator.CreateInstance(type)!);
        var byKind = new Dictionary<QuestOperationKind, IQuestOperation>();
        foreach (var (declared, operation) in parts.Order.Zip(parts.In(QuestOperationPhase.Log)))
        {
            var kind = QuestStore.KindText(operation.Kind);
            if (!string.Equals(kind, declared.Name, StringComparison.Ordinal))
            {
                throw new PartOrderException(
                    $"the part {declared.Name} ({declared.Type.Name}) is the operation of `{kind}`, so its mark names another kind");
            }

            byKind.Add(operation.Kind, operation);
        }

        return new QuestOperations(parts, byKind);
    }

    /// <summary>The module registered for <paramref name="kind"/>; null for a kind whose payload the store still writes itself.</summary>
    internal IQuestOperation? For(QuestOperationKind kind) => _byKind.GetValueOrDefault(kind);

    /// <summary>The one module of type <typeparamref name="T"/>: what a store's door calls the verbs of.</summary>
    internal T Of<T>()
        where T : IQuestOperation => _parts.Of<T>();
}
