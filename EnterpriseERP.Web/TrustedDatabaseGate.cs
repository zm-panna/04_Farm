using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TrustedInfrastructure;
namespace EnterpriseERP.Web;
/// <summary>Hosted DI gate for EF commands and commits. Already-running calls require provider-side revocation.</summary>
public sealed class TrustedDatabaseCommandGate(ITrustedRuntimeGate gate) : DbCommandInterceptor
{
    private void Check(CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); gate.ThrowIfUnavailable(); }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result) { Check(); return result; }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData data, InterceptionResult<int> result) { Check(); return result; }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData data, InterceptionResult<object> result) { Check(); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Check(ct); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) { Check(ct); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default) { Check(ct); return ValueTask.FromResult(result); }
}
public sealed class TrustedDatabaseTransactionGate(ITrustedRuntimeGate gate) : DbTransactionInterceptor
{
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData data, InterceptionResult result) { gate.ThrowIfUnavailable(); return result; }
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
    { ct.ThrowIfCancellationRequested(); gate.ThrowIfUnavailable(); return ValueTask.FromResult(result); }
}
