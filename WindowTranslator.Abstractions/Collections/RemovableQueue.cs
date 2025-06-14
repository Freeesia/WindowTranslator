using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace WindowTranslator.Collections;

/// <summary>
/// 削除可能なキュー
/// </summary>
/// <typeparam name="T">要素の型</typeparam>
#pragma warning disable CA1711 // ちゃんとdotnetのQueueとして動くのでOK
public class RemovableQueue<T> : ICollection<T>, IReadOnlyCollection<T>
#pragma warning restore CA1711
    where T : notnull
{
    private readonly LinkedList<T> inner;

    /// <inheritdoc/>
    public int Count => inner.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <summary>
    /// 初期化します。
    /// </summary>
    public RemovableQueue()
        => inner = new();

    /// <summary>
    /// 初期化します。
    /// </summary>
    /// <param name="list">初期キュー</param>
    public RemovableQueue(IEnumerable<T> list)
        => inner = new(list);

    /// <summary>
    /// 要素を取り出します。
    /// 取り出した要素はキューから削除されます。
    /// </summary>
    /// <param name="item">一番古い要素</param>
    /// <returns>取り出せたかどうか</returns>
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

    /// <summary>
    /// 要素を取り出します。
    /// </summary>
    /// <returns>一番古い要素</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public T Dequeue()
        => TryDequeue(out var item) ? item : throw new InvalidOperationException();

    /// <inheritdoc/>
    public bool Remove(T item)
        => inner.Remove(item);

    /// <inheritdoc/>
    public void Add(T item)
        => inner.AddLast(item);

    /// <inheritdoc/>
    public void Clear()
        => inner.Clear();

    /// <inheritdoc/>
    public bool Contains(T item)
        => inner.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
        => inner.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
        => inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => inner.GetEnumerator();
}
