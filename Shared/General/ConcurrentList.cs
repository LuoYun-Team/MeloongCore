namespace MeloongCore;

/// <summary>
/// 线程安全的 <see cref="List{T}"/> 实现。
/// 当调用枚举器时返回一个临时副本，以避免列表在枚举过程中被修改导致异常。
/// </summary>
public class ConcurrentList<T> : IList<T>, IList, IEnumerable, IEnumerable<T> {

    private readonly List<T> _items = [];
    private readonly object _lock = new();

    // 构造函数
    public ConcurrentList() { }
    public ConcurrentList(IEnumerable<T> data) { _items = new List<T>(data); }
    public static implicit operator ConcurrentList<T>(List<T> data) => new(data);
    public static implicit operator List<T>(ConcurrentList<T> data) { lock (data._lock) { return new List<T>(data._items); } }

    public int Count { get { lock (_lock) { return _items.Count; } } }
    public bool IsReadOnly => false;
    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => true;
    object ICollection.SyncRoot => _lock;

    public T this[int index] {
        get { lock (_lock) { return _items[index]; } }
        set { lock (_lock) { _items[index] = value; } }
    }
    object? IList.this[int index] {
        get => this[index];
        set { lock (_lock) { _items[index] = (T)value!; } }
    }

    public void Add(T item) { lock (_lock) { _items.Add(item); } }
    int IList.Add(object? value) { lock (_lock) { _items.Add((T)value!); return _items.Count - 1; } }

    public void Insert(int index, T item) { lock (_lock) { _items.Insert(index, item); } }
    void IList.Insert(int index, object? value) { lock (_lock) { _items.Insert(index, (T)value!); } }

    public bool Remove(T item) { lock (_lock) { return _items.Remove(item); } }
    void IList.Remove(object? value) { lock (_lock) { if (value is T t) _items.Remove(t); } }

    public void RemoveAt(int index) { lock (_lock) { _items.RemoveAt(index); } }
    public void Clear() { lock (_lock) { _items.Clear(); } }

    public bool Contains(T item) { lock (_lock) { return _items.Contains(item); } }
    bool IList.Contains(object? value) { lock (_lock) { return value is T t && _items.Contains(t); } }

    public int IndexOf(T item) { lock (_lock) { return _items.IndexOf(item); } }
    int IList.IndexOf(object? value) { lock (_lock) { return value is T t ? _items.IndexOf(t) : -1; } }

    public void CopyTo(T[] array, int arrayIndex) { lock (_lock) { _items.CopyTo(array, arrayIndex); } }
    void ICollection.CopyTo(Array array, int index) { lock (_lock) { ((ICollection)_items).CopyTo(array, index); } }

    // 对枚举器进行覆写，返回一个临时副本
    public IEnumerator<T> GetEnumerator() {
        lock (_lock) {
            return _items.ToList().GetEnumerator();
        }
    }
    IEnumerator IEnumerable.GetEnumerator() {
        lock (_lock) {
            return _items.ToList().GetEnumerator();
        }
    }

}