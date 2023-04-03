using System.Collections.Concurrent;
using Remora.Results;

namespace RemoraHTTPInteractions.Services;

public class InMemoryDataStore<TKey, TValue> where TKey : notnull
{
    public static InMemoryDataStore<TKey, TValue> Instance { get; } = new();
    
    private readonly ConcurrentDictionary<TKey, (SemaphoreSlim Lock, TValue Value)> _data = new();
    
    public bool TryAddValue(TKey key, TValue value) => _data.TryAdd(key, (new(1,1), value));
    
    public async Task<Result<DataLease<TKey, TValue>>> TryGetLeaseAsync(TKey key, CancellationToken ct = default)
    {
        if (_data.TryGetValue(key, out var value))
        {
            await value.Lock.WaitAsync(ct);
            return new DataLease<TKey, TValue>(this, value.Lock, key, value.Value);
        }
        
        return new NotFoundError();
    }
    
    public async ValueTask<bool> DeleteAsync(TKey key)
    {
        if (!_data.TryRemove(key, out var value))
        {
            return false;
        }
        
        if (value.Value is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (value.Value is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        value.Lock.Dispose();
        
        return true;
    }

    /// <summary>
    /// Updates the value of the given key, if it exists.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <param name="value">The value to update it with.</param>
    /// <exception cref="KeyNotFoundException">The key didn't exist in the store.</exception>
    /// <exception cref="InvalidOperationException">The lock isn't held by the caller. This is indicative of a bug.</exception>
    internal void Update(TKey key, TValue value)
    {
        if (!_data.TryGetValue(key, out var existing))
        {
            throw new KeyNotFoundException("The presumably existent key proved non-existent. This is indicative of a concurrency issue.");
        }
        
        if (existing.Lock.CurrentCount != 0)
        {
            throw new InvalidOperationException("The lock is not held by the caller lease. This is indicative of a concurrency issue.");
        }
        
        _data[key] = (existing.Lock, value);
    }

    private InMemoryDataStore() {}
}

/// <summary>
/// Represents a smart pointer to a value in the data store. "Leasing" data automatically returns it to the store when the lease is disposed.
/// </summary>
/// <typeparam name="TKey">The key.</typeparam>
/// <typeparam name="TValue">The associated value.</typeparam>
public class DataLease<TKey, TValue> : IAsyncDisposable where TKey : notnull
{
    private readonly TKey _key;
    private          TValue _value;
    private readonly SemaphoreSlim _waitHandle;   
    private readonly InMemoryDataStore<TKey, TValue> _store;

    private bool _isExpired;
    private bool _isMarkedForDeletion;

    /// <summary>
    /// Gets or sets the data associated with this lease. Setting this value will update the value in the data store when the lease is disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The lease expired, and can no longer be used.</exception>
    public TValue Data
    {
        get => _isExpired ? throw new ObjectDisposedException(nameof(DataLease<TKey, TValue>)) : _value;
        set
        {
            if (_isExpired)
                throw new ObjectDisposedException(nameof(DataLease<TKey, TValue>));
            
            ArgumentNullException.ThrowIfNull(value);
            _value = value;
        }
    }
    
    internal DataLease(InMemoryDataStore<TKey, TValue> store, SemaphoreSlim handle, TKey key, TValue value)
    {
        _store = store;
        _waitHandle = handle;
        _key = key;
        _value = value;
    }
    
    public void MarkForDeletion()
    {
        _isMarkedForDeletion = true;
    }

    public async ValueTask DisposeAsync()
    {
        _isExpired = true;
        
        if (!_isMarkedForDeletion)
        {
            _store.Update(_key, _value);
            _waitHandle.Release();
        }
        else
        {
            if (await _store.DeleteAsync(_key))
            {
                _waitHandle.Dispose();
                return;
            }

            if (_value is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (_value is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _waitHandle.Dispose();
        }
    }
}