namespace Sgol.Web.Infrastructure.Http;

// Form antiforgery can read the body without passing RequestAborted. Enforce the
// same budget at the stream boundary so slow readers cannot retain admission.
public sealed class RequestBodyDeadlineMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public static TimeSpan Timeout(IConfiguration configuration) => TimeSpan.FromSeconds(
        Math.Clamp(configuration.GetValue<int?>("RequestLimits:TimeoutSeconds") ?? 300, 2, 300));

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength is not > 0 && !context.Request.Headers.ContainsKey("Transfer-Encoding"))
        { await next(context); return; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        budget.CancelAfter(Timeout(configuration));
        var original = context.Request.Body;
        context.Request.Body = new DeadlineStream(original, budget.Token);
        try { await next(context); }
        finally { context.Request.Body = original; }
    }

    private sealed class DeadlineStream(Stream inner, CancellationToken budget) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        { budget.ThrowIfCancellationRequested(); return inner.Read(buffer, offset, count); }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!cancellationToken.CanBeCanceled) return await inner.ReadAsync(buffer, budget);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(budget, cancellationToken);
            return await inner.ReadAsync(buffer, linked.Token);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        // The server owns the original request stream.
        protected override void Dispose(bool disposing) { }
    }
}
