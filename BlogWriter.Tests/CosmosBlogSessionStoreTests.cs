using System.Collections;
using System.Net;
using BlogWriter;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;

namespace BlogWriter.Tests;

public sealed class CosmosBlogSessionStoreTests
{
    [Theory]
    [InlineData(null, null, 1000, 2000)]
    [InlineData(700, null, 700, 2000)]
    [InlineData(null, 1500, 1000, 1500)]
    [InlineData(700, 700, 700, 700)]
    [InlineData(0, 100, 1000, 2000)]
    [InlineData(2000, 1000, 1000, 2000)]
    public async Task ListAsync_ProjectsSavedDetailsWithOwnerScopedPagedQuery(
        int? min, int? max, int expectedMin, int expectedMax)
    {
        string draft = string.Join("\t", Enumerable.Range(1, 51).Select(number => $"word{number}"));
        var row = new Dictionary<string, object?>
        {
            ["Id"] = "first", ["MainTask"] = "topic", ["Draft"] = draft,
            ["CreatedAt"] = "2026-10-01T12:00:00Z", ["UpdatedAt"] = "2026-10-02T12:00:00Z",
        };
        if (min.HasValue) row["MinWords"] = min.Value;
        if (max.HasValue) row["MaxWords"] = max.Value;
        var container = new QueryContainer(
            JsonConvert.SerializeObject(new[] { row }),
            "[{\"Id\":\"second\",\"MainTask\":\"empty\",\"Draft\":null}]");
        var logger = new RecordingLogger();
        using var cancellation = new CancellationTokenSource();
        var store = new CosmosBlogSessionStore(container, new OwnerProvider(), logger);

        IReadOnlyList<BlogSessionSummary> summaries = await store.ListAsync(cancellation.Token);

        Assert.Equal(2, summaries.Count);
        Assert.Equal("first", summaries[0].Id);
        Assert.Equal((expectedMin, expectedMax), (summaries[0].MinWords, summaries[0].MaxWords));
        Assert.Equal(string.Join(" ", Enumerable.Range(1, 50).Select(number => $"word{number}")), summaries[0].DraftPreview);
        Assert.True(summaries[0].IsDraftTruncated);
        Assert.Empty(summaries[1].DraftPreview);
        Assert.False(summaries[1].IsDraftTruncated);
        Assert.Contains("SELECT TOP 20", container.Query!.QueryText);
        Assert.Contains("c.State.MinWords AS MinWords", container.Query.QueryText);
        Assert.Contains("c.State.MaxWords AS MaxWords", container.Query.QueryText);
        Assert.Contains("c.State.Draft AS Draft", container.Query.QueryText);
        Assert.Contains("WHERE c.OwnerId = @ownerId ORDER BY c.UpdatedAt DESC", container.Query.QueryText);
        Assert.Equal("owner", Assert.Single(container.Query.GetQueryParameters()).Value);
        Assert.Equal(new PartitionKey("owner"), container.Options!.PartitionKey);
        Assert.Equal(2, container.ReadCalls);
        Assert.Equal(cancellation.Token, container.LastCancellationToken);
        Assert.True(container.IteratorDisposed);
        Assert.Single(logger.Messages);
        Assert.DoesNotContain(draft, logger.Messages[0]);
    }

    [Fact]
    public async Task ListAsync_PreservesCancellationAndQueryErrors()
    {
        var container = new QueryContainer("[]");
        var store = new CosmosBlogSessionStore(container, new OwnerProvider(), new RecordingLogger());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ListAsync(cancellation.Token));
        Assert.True(container.IteratorDisposed);

        container.Failure = new InvalidOperationException("query failed");
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ListAsync());
        Assert.Same(container.Failure, error);
    }

    private sealed class OwnerProvider : ISessionOwnerProvider
    {
        public Task<string> GetOwnerIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("owner");
    }

    private sealed class RecordingLogger : ILogger<CosmosBlogSessionStore>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class QueryIterator<T>(QueryContainer container, string[] pages) : FeedIterator<T>
    {
        private int _index;
        public override bool HasMoreResults => _index < pages.Length;
        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            container.LastCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (container.Failure is not null) throw container.Failure;
            container.ReadCalls++;
            T[] items = JsonConvert.DeserializeObject<T[]>(pages[_index++])!;
            return Task.FromResult<FeedResponse<T>>(new QueryPage<T>(items));
        }
        protected override void Dispose(bool disposing) => container.IteratorDisposed = true;
    }

    private sealed class QueryPage<T>(T[] items) : FeedResponse<T>
    {
        public override Headers Headers => new();
        public override double RequestCharge => 1;
        public override string ActivityId => "test";
        public override string ContinuationToken => "";
        public override string IndexMetrics => "";
        public override int Count => items.Length;
        public override IEnumerable<T> Resource => items;
        public override HttpStatusCode StatusCode => HttpStatusCode.OK;
        public override CosmosDiagnostics Diagnostics => null!;
        public override IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
    }

    private sealed class QueryContainer(params string[] pages) : Container
    {
        public QueryDefinition? Query { get; private set; }
        public QueryRequestOptions? Options { get; private set; }
        public CancellationToken LastCancellationToken { get; set; }
        public int ReadCalls { get; set; }
        public bool IteratorDisposed { get; set; }
        public Exception? Failure { get; set; }
        public override FeedIterator<T> GetItemQueryIterator<T>(QueryDefinition queryDefinition, string? continuationToken = null, QueryRequestOptions? requestOptions = null)
        {
            Query = queryDefinition;
            Options = requestOptions;
            return new QueryIterator<T>(this, pages);
        }
        public override string Id => throw new NotSupportedException();
        public override Database Database => throw new NotSupportedException();
        public override Conflicts Conflicts => throw new NotSupportedException();
        public override Scripts Scripts => throw new NotSupportedException();
        public override Task<ContainerResponse> ReadContainerAsync(ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> ReadContainerStreamAsync(ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ContainerResponse> ReplaceContainerAsync(ContainerProperties properties, ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> ReplaceContainerStreamAsync(ContainerProperties properties, ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ContainerResponse> DeleteContainerAsync(ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> DeleteContainerStreamAsync(ContainerRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ThroughputResponse> ReadThroughputAsync(RequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ThroughputResponse> ReplaceThroughputAsync(int throughput, RequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties properties, RequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> CreateItemStreamAsync(Stream stream, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> CreateItemAsync<T>(T item, PartitionKey? partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> ReadItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> ReadItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> UpsertItemStreamAsync(Stream stream, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> UpsertItemAsync<T>(T item, PartitionKey? partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> ReplaceItemStreamAsync(Stream stream, string id, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(T item, string id, PartitionKey? partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> ReadManyItemsStreamAsync(IReadOnlyList<(string, PartitionKey)> items, ReadManyRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(IReadOnlyList<(string, PartitionKey)> items, ReadManyRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> PatchItemAsync<T>(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> operations, PatchItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> PatchItemStreamAsync(string id, PartitionKey partitionKey, IReadOnlyList<PatchOperation> operations, PatchItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ResponseMessage> DeleteItemStreamAsync(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<ItemResponse<T>> DeleteItemAsync<T>(string id, PartitionKey partitionKey, ItemRequestOptions requestOptions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override FeedIterator GetItemQueryStreamIterator(QueryDefinition queryDefinition, string continuationToken, QueryRequestOptions requestOptions) => throw new NotSupportedException();
        public override FeedIterator GetItemQueryStreamIterator(string queryText, string continuationToken, QueryRequestOptions requestOptions) => throw new NotSupportedException();
        public override FeedIterator<T> GetItemQueryIterator<T>(string queryText, string continuationToken, QueryRequestOptions requestOptions) => throw new NotSupportedException();
        public override FeedIterator GetItemQueryStreamIterator(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken, QueryRequestOptions requestOptions) => throw new NotSupportedException();
        public override FeedIterator<T> GetItemQueryIterator<T>(FeedRange feedRange, QueryDefinition queryDefinition, string continuationToken, QueryRequestOptions requestOptions) => throw new NotSupportedException();
        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution, string continuationToken, QueryRequestOptions requestOptions, CosmosLinqSerializerOptions linqSerializerOptions) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangesHandler<T> onChangesDelegate) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName, ChangesEstimationHandler estimationDelegate, TimeSpan? estimationPeriod) => throw new NotSupportedException();
        public override ChangeFeedEstimator GetChangeFeedEstimator(string processorName, Container leaseContainer) => throw new NotSupportedException();
        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey) => throw new NotSupportedException();
        public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public override FeedIterator GetChangeFeedStreamIterator(ChangeFeedStartFrom startFrom, ChangeFeedMode mode, ChangeFeedRequestOptions requestOptions) => throw new NotSupportedException();
        public override FeedIterator<T> GetChangeFeedIterator<T>(ChangeFeedStartFrom startFrom, ChangeFeedMode mode, ChangeFeedRequestOptions requestOptions) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(string processorName, ChangeFeedHandler<T> onChangesDelegate) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(string processorName, ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(string processorName, ChangeFeedStreamHandler onChangesDelegate) => throw new NotSupportedException();
        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(string processorName, ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate) => throw new NotSupportedException();
    }
}
