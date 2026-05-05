namespace MeloongCore;

/// <summary>
/// 线程安全的 <see cref="List{T}"/> 实现。
/// 当调用枚举器时返回一个临时副本，以避免列表在枚举过程中被修改导致异常。
/// </summary>
public class ConcurrentList<T> : IList<T>, IList, IEnumerable, IEnumerable<T> {

    private readonly List<T> _items = [];
    public readonly object syncLock = new();

    // 构造函数
    public ConcurrentList() { }
    public ConcurrentList(IEnumerable<T> data) => _items = new List<T>(data);
    public static implicit operator ConcurrentList<T>(List<T> data) => new(data);
    public static implicit operator List<T>(ConcurrentList<T> data) { lock (data.syncLock) { return new List<T>(data._items); } }

    // 枚举器
    public IEnumerator<T> GetEnumerator() {
        lock (syncLock) {
            return _items.ToList().GetEnumerator();
        }
    }
    IEnumerator IEnumerable.GetEnumerator() {
        lock (syncLock) {
            return _items.ToList().GetEnumerator();
        }
    }

    // 成员
    public int Count { get { lock (syncLock) { return _items.Count; } } }
    public bool IsReadOnly => false;
    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => true;
    object ICollection.SyncRoot => syncLock;

    public T this[int index] {
        get { lock (syncLock) { return _items[index]; } }
        set { lock (syncLock) { _items[index] = value; } }
    }
    object? IList.this[int index] {
        get => this[index];
        set { lock (syncLock) { _items[index] = (T) value!; } }
    }

    public void Add(T item) { lock (syncLock) { _items.Add(item); } }
    int IList.Add(object? value) { lock (syncLock) { _items.Add((T) value!); return _items.Count - 1; } }

    public void Insert(int index, T item) { lock (syncLock) { _items.Insert(index, item); } }
    void IList.Insert(int index, object? value) { lock (syncLock) { _items.Insert(index, (T) value!); } }

    public bool Remove(T item) { lock (syncLock) { return _items.Remove(item); } }
    void IList.Remove(object? value) { lock (syncLock) { if (value is T t) _items.Remove(t); } }

    public void RemoveAt(int index) { lock (syncLock) { _items.RemoveAt(index); } }
    public void Clear() { lock (syncLock) { _items.Clear(); } }

    public bool Contains(T item) { lock (syncLock) { return _items.Contains(item); } }
    bool IList.Contains(object? value) { lock (syncLock) { return value is T t && _items.Contains(t); } }

    public int IndexOf(T item) { lock (syncLock) { return _items.IndexOf(item); } }
    int IList.IndexOf(object? value) { lock (syncLock) { return value is T t ? _items.IndexOf(t) : -1; } }

    public void CopyTo(T[] array, int arrayIndex) { lock (syncLock) { _items.CopyTo(array, arrayIndex); } }
    void ICollection.CopyTo(Array array, int index) { lock (syncLock) { ((ICollection) _items).CopyTo(array, index); } }

}