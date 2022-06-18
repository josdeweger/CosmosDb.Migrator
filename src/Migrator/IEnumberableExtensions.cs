namespace CosmosDb.Migrator;

public static class IEnumberableExtensoins
{
    public static IEnumerable<IEnumerable<TSource>> Batch<TSource>(
        this IEnumerable<TSource> source, int size)
    {
        TSource[]? bucket = null;
        var count = 0;

        foreach (var item in source)
        {
            bucket ??= new TSource[size];
            bucket[count++] = item;
            
            if (count != size)
            {
                continue;
            }

            yield return bucket;

            bucket = null;
            count = 0;
        }

        if (bucket != null && count > 0)
        {
            yield return bucket.Take(count).ToArray();
        }
    }
    
    public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
        => self.Select((item, index) => (item, index)); 
}
