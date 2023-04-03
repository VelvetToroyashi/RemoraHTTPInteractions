using RemoraHTTPInteractions.Services;

namespace RemoraHTTPInteractions.Tests;

public class InMemoryDataStoreTests
{
    [Test]
    public void CanInsertData()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        
        var insert = dataStore.TryAddValue("key", "value");
        
        Assert.IsTrue(insert);
    }
    
    [Test]
    public async Task CanRetrieveData()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        
        dataStore.TryAddValue("test_key", "value");
        
        var value = await dataStore.TryGetLeaseAsync("test_key");
        
        Assert.IsTrue(value.IsSuccess);
        Assert.AreEqual("value", value.Entity.Data);
    }
    
    [Test]
    public async Task FailsForNonexistentData()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        
        var value = await dataStore.TryGetLeaseAsync("bogus");
        
        Assert.IsFalse(value.IsSuccess);
    }

    [Test]
    public async Task CannotAcquireLeaseOnRentedData()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        
        dataStore.TryAddValue("key2", "value");
        
        var value = await dataStore.TryGetLeaseAsync("key2");

        Assert.IsTrue(value.IsSuccess);
        
        var value2 = dataStore.TryGetLeaseAsync("key2");
        
        Assert.IsFalse(value2.IsCompleted); // Grabbing a lease on a key that is already leased waits on it to be released
    }

    [Test]
    public async Task DisposingLeaseReleasesLock()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        dataStore.TryAddValue("test_key_3", "value");
        
        var value = await dataStore.TryGetLeaseAsync("test_key_3");
        await value.Entity.DisposeAsync();
        
        var value2 = await dataStore.TryGetLeaseAsync("test_key_3", new CancellationTokenSource(100).Token);
        
        Assert.IsTrue(value2.IsSuccess);
    }

    [Test]
    public async Task DeleteRemovesKeyOnDisposal()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        dataStore.TryAddValue("test_key_4", "value");
        
        var data = await dataStore.TryGetLeaseAsync("test_key_4");
        data.Entity.MarkForDeletion();
        
        await data.Entity.DisposeAsync();
        
        var result = await dataStore.TryGetLeaseAsync("test_key_4");
        
        Assert.IsFalse(result.IsSuccess);
    }

    [Test]
    public async Task UpdateReflectsOnDisposal()
    {
        var dataStore = InMemoryDataStore<string, string>.Instance;
        dataStore.TryAddValue("test_key_5", "value");
        
        var data = await dataStore.TryGetLeaseAsync("test_key_5");
        
        data.Entity.Data = "new_value";
        await data.Entity.DisposeAsync();
        
        var result = await dataStore.TryGetLeaseAsync("test_key_5");
        
        Assert.IsTrue(result.IsDefined(out var newData));
        Assert.AreEqual("new_value", newData.Data);
    }
}
