using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Http;

internal static class PerformanceRegressionChecks
{
    private static void Require(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS " + text); }
    public static async Task RunAsync()
    {
        await PerformanceJpegChecks.GoldenAsync();
        await PerformanceMediaChecks.ReadExcel(PerformanceMediaChecks.Workbook(3, true), default);
        Require(true, "Valid workbook with three distinct worksheets");
        try { await PerformanceMediaChecks.ReadExcel(PerformanceMediaChecks.Workbook(500), default); throw new Exception("Amplified workbook accepted"); }
        catch (DirectionPlanException e) when (e.Code == "TASK_EXCEL_DUPLICATE_SHEETS" && e.Status == 422) { Require(true, "Repeated worksheet rejected as invalid"); }
        var bytes = PerformanceMediaChecks.Workbook(1, true, 50000);
        using var cancellation = new CancellationTokenSource();
        var observedNodes = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == "Sgol.Performance" && instrument.Name == "sgol.excel.parsed_nodes") l.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => { observedNodes += value; cancellation.Cancel(); });
        listener.Start();
        try { await PerformanceMediaChecks.ReadExcel(bytes, cancellation.Token); throw new Exception("Parser ignored cancellation"); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { Require(observedNodes >= 256, "Cancellation interrupts actual worksheet traversal after 256 nodes"); }
        using var gate = new WorkGate("regression", 1, 1);
        var held = await gate.EnterAsync(default);
        using var queuedCancellation = new CancellationTokenSource();
        var queued = gate.EnterAsync(queuedCancellation.Token).AsTask();
        try { using var excess = await gate.EnterAsync(default); throw new Exception("Queue overflow admitted"); }
        catch (DirectionPlanException e) when (e.Code == "OPERATION_BUSY" && e.Status == 503) { Require(true, "Full bounded queue rejected before work"); }
        queuedCancellation.Cancel();
        try { await queued; throw new Exception("Queue ignored cancellation"); } catch (OperationCanceledException) { }
        held.Dispose(); using var recovered = await gate.EnterAsync(default);
        Require(true, "Cancelled queued work frees admission; service recovers");
        var diagnostic = Sgol.Web.Infrastructure.Persistence.Planning.OperationalAgendaSearch.Diagnostic(new DirectionPlanException("SCHEDULING_SEARCH_INCONCLUSIVE",422) {Data={ ["Reason"]="OCCURRENCE_SEARCH_SIZE_LIMIT",["Occurrences"]=2686,["MaximumSearchOccurrences"]=1000 }});
        Require(diagnostic.Contains("2686") && diagnostic.Contains("1000") && diagnostic.Contains("Reduce"), "Search diagnostic identifies size limit and actionable scope reduction");
        var original = new DefaultHttpContext(); original.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.2");
        using var ticket = new InternalLoginOrigin(original);
        var incoming = new DefaultHttpContext(); incoming.Connection.RemoteIpAddress = IPAddress.Loopback;
        incoming.Request.Headers[InternalLoginOrigin.Header] = ticket.Token;
        Require(InternalLoginOrigin.Resolve(incoming) == "127.0.0.2", "Internal login ticket preserves original quota partition");
        Require(InternalLoginOrigin.Resolve(incoming) == "127.0.0.1", "Internal ticket is single use");
        incoming.Request.Headers[InternalLoginOrigin.Header] = new string('a', 64);
        incoming.Request.Headers["X-Forwarded-For"] = "203.0.113.2";
        Require(InternalLoginOrigin.Resolve(incoming) == "127.0.0.1", "Forged address and forged ticket do not alter quota");
    }
}
