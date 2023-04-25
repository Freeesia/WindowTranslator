using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace WindowTranslator;

#pragma warning disable CA1711 // ちゃんとdotnetのQueueとして動くのでOK
public class RemovableQueue<T> : ICollection<T>, IReadOnlyCollection<T>
#pragma warning restore CA1711
    where T : notnull
{
    private readonly LinkedList<T> inner;

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public RemovableQueue()
    {
        inner = new();
    }

    public RemovableQueue(IEnumerable<T> list)
    {
        inner = new(list);
    }

    public bool TryDequeue([NotNullWhen(true)] out T? item)
    {
        if (inner.First is { } first)
        {
            item = first.Value;
            inner.RemoveFirst();
            return true;
        }
        else
        {
            item = default;
            return false;
        }
    }

    public T Dequeue()
        => TryDequeue(out var item) ? item : throw new InvalidOperationException();

    public bool Remove(T item)
        => inner.Remove(item);

    public void Add(T item)
        => inner.AddLast(item);

    public void Clear()
        => inner.Clear();

    public bool Contains(T item)
        => inner.Contains(item);

    public void CopyTo(T[] array, int arrayIndex)
        => inner.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator()
        => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => inner.GetEnumerator();
}
