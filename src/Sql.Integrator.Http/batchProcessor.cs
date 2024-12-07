using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class BatchProcessor<T>
{
    private readonly int _batchSize;
    private readonly Func<IEnumerable<T>, Task<IEnumerable<TResult>>> _processBatchFunction;

    public BatchProcessor(int batchSize, Func<IEnumerable<T>, Task<IEnumerable<TResult>>> processBatchFunction)
    {
        _batchSize = batchSize;
        _processBatchFunction = processBatchFunction;
    }

    public async Task<IEnumerable<TResult>> ProcessInBatchesAsync(IEnumerable<T> items)
    {
        var batches = items
            .Select((item, index) => new { item, index })
            .GroupBy(x => x.index / _batchSize)
            .Select(g => g.Select(x => x.item));

        var tasks = batches.Select(batch => _processBatchFunction(batch));
        var results = await Task.WhenAll(tasks);

        return results.SelectMany(r => r);
    }
}
