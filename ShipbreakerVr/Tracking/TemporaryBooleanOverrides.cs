using System;
using System.Collections.Generic;

namespace ShipbreakerVr.Tracking;

// Frame-scoped overrides restore the value requested by the owner before that frame.
internal sealed class TemporaryBooleanOverrides<T> where T : class
{
    private readonly Func<T, bool> read;
    private readonly Action<T, bool> write;
    private readonly HashSet<T> changed = new HashSet<T>();

    public TemporaryBooleanOverrides(Func<T, bool> read, Action<T, bool> write)
    {
        this.read = read;
        this.write = write;
    }

    public bool Suppress(T item)
    {
        if (changed.Contains(item) || !read(item)) return false;
        changed.Add(item);
        write(item, false);
        return true;
    }

    public void Restore()
    {
        foreach (var item in changed) write(item, true);
        changed.Clear();
    }
}
