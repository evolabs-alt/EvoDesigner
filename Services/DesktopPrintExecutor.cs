using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services;

public class DesktopPrintExecutor : IPrintExecutor
{
	public async Task PrintAllRecordsAsync(LabelTemplate template, string printerName, DataTable dataTable, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		License.PortalLicenseService.Instance.RequireLicensed();
		TaskCompletionSource<object?> completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				new LabelPrintService().PrintAllRecords(template, printerName, dataTable);
				completionSource.TrySetResult(null);
			}
			catch (OperationCanceledException)
			{
				completionSource.TrySetCanceled(cancellationToken);
			}
			catch (Exception exception)
			{
				completionSource.TrySetException(exception);
			}
		});
		thread.IsBackground = true;
		thread.Name = "DesktopPrintStaWorker";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		using (cancellationToken.Register(delegate
		{
			completionSource.TrySetCanceled(cancellationToken);
		}))
		{
			await completionSource.Task;
		}
	}
}
