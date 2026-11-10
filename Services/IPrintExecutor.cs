using System.Data;
using System.Threading;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services;

public interface IPrintExecutor
{
	Task PrintAllRecordsAsync(LabelTemplate template, string printerName, DataTable dataTable, CancellationToken cancellationToken);
}
