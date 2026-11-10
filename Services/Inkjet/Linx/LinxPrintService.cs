using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services.Inkjet.Linx;

public sealed class LinxPrintService
{
	private const int DefaultPollMs = 1000;

	private const int DefaultTimeoutMs = 8000;

	public Task<InkjetOperationResult> QueueLoadAsync(InkjetConfig config, DataTable dataTable, CancellationToken cancellationToken = default(CancellationToken), IProgress<string>? progress = null)
	{
		return Task.Run(delegate
		{
			cancellationToken.ThrowIfCancellationRequested();
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config);
				List<InkjetPositionMapping> list = config.Mappings ?? new List<InkjetPositionMapping>();
				if (list.Count == 0)
				{
					throw new InvalidOperationException("Linx template has no position mappings.");
				}
				foreach (InkjetPositionMapping item2 in list.OrderBy((InkjetPositionMapping m) => m.Position))
				{
					if (string.IsNullOrWhiteSpace(item2.FieldName))
					{
						throw new InvalidOperationException($"Position {item2.Position} has no data field mapping.");
					}
					if (string.IsNullOrWhiteSpace(item2.PrinterFieldName))
					{
						throw new InvalidOperationException($"Position {item2.Position} has no printer field name.");
					}
					if (!dataTable.Columns.Contains(item2.FieldName))
					{
						throw new InvalidOperationException("Column '" + item2.FieldName + "' not found in data source result.");
					}
				}
				if (dataTable == null || dataTable.Rows.Count == 0)
				{
					throw new InvalidOperationException("No records to send to Linx printer.");
				}
				string name = config.JobName.Trim();
				if (name.Length > 15)
				{
					throw new ArgumentException("Linx message name must be at most 15 characters.");
				}
				List<InkjetPositionMapping> orderedMappings = list.OrderBy((InkjetPositionMapping m) => m.Position).ToList();
				List<Dictionary<string, string>> items = new List<Dictionary<string, string>>(dataTable.Rows.Count);
				foreach (DataRow row in dataTable.Rows)
				{
					cancellationToken.ThrowIfCancellationRequested();
					Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					foreach (InkjetPositionMapping item3 in orderedMappings)
					{
						string value = InkjetPrintService.PadVar(row[item3.FieldName]?.ToString() ?? string.Empty, config.VarWidth);
						dictionary[item3.PrinterFieldName.Trim()] = value;
					}
					items.Add(dictionary);
				}
				Append($"Queue load: '{name}' → {items.Count} item(s) (fields: {string.Join(", ", orderedMappings.Select((InkjetPositionMapping m) => m.PrinterFieldName.Trim()))})...");
				int queued = 0;
				WithSession(config, Append, delegate(RciClient c)
				{
					cancellationToken.ThrowIfCancellationRequested();
					string item = RciCommands.ParseCurrentMessage(RciCommands.RequestCurrentMessage(c)).Name;
					if (!string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
					{
						TryStopPrint(c, Append);
						RciCommands.LoadMessage(c, name, 0).EnsureOk("Load '" + name + "'");
						Append("  Loaded '" + name + "'.");
					}
					else
					{
						Append("  Current message is already '" + item + "' — appending without re-select.");
					}
					IReadOnlyList<RemoteFieldInfo> readOnlyList = RciCommands.ParseRemoteFieldList(RciCommands.RequestRemoteFieldList(c));
					if (readOnlyList.Count == 0)
					{
						throw new InvalidOperationException("Printer reported no remote fields. Select a remote-field message first.");
					}
					Append("  Remote layout: " + string.Join(", ", readOnlyList.Select((RemoteFieldInfo f) => $"{f.Name}[{f.MaxCharacters}]")));
					foreach (InkjetPositionMapping item4 in orderedMappings)
					{
						string fieldName = item4.PrinterFieldName.Trim();
						if (!readOnlyList.Any((RemoteFieldInfo f) => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
						{
							throw new InvalidOperationException($"Printer message has no remote field named '{fieldName}'. Available: {string.Join(", ", readOnlyList.Select((RemoteFieldInfo f) => f.Name))}.");
						}
					}
					PrintModeSettings? printModeSettings = null;
					try
					{
						printModeSettings = RciCommands.ParsePrintMode(RciCommands.GetPrintMode(c));
						Append("  current " + printModeSettings.Value.Describe());
					}
					catch (Exception ex3)
					{
						Append("  print mode read: " + ex3.Message);
					}
					byte b = printModeSettings?.RemoteBufferDivisor ?? ((byte)Math.Clamp(items.Count, 1, 255));
					if (!printModeSettings.HasValue || printModeSettings.Value.Mode != 1)
					{
						RciReply reply = RciCommands.ConfigurePrintMode(c, 1, b, clearBuffer: false);
						if (!reply.Ok)
						{
							Append("  print mode: " + RciClient.DescribeReply(reply) + " (continuing)");
						}
						else
						{
							Append($"  print mode: single, buffer-divisor={b}");
						}
					}
					Append("  appending to existing remote buffer (not clearing).");
					Append($"Queuing {items.Count} job(s). Buffer holds a few at a time; the rest wait for prints.");
					TryStartPrint(c, Append);
					DateTime dateTime = DateTime.MinValue;
					int num = 0;
					while (num < items.Count)
					{
						cancellationToken.ThrowIfCancellationRequested();
						byte[] array = RciCommands.BuildRemoteBufferItem(readOnlyList, items[num]);
						string text2 = FormatQueueItem(items[num]);
						RciReply reply2 = RciCommands.DownloadRemoteFieldData(c, array, text2);
						if (reply2.CommandStatus == 67)
						{
							if ((DateTime.UtcNow - dateTime).TotalSeconds >= 5.0)
							{
								Append($"  buffer full ({queued}/{items.Count} queued). Waiting for a carton to free a slot...");
								dateTime = DateTime.UtcNow;
							}
							TryStartPrint(c, Append);
							cancellationToken.WaitHandle.WaitOne(1000);
							cancellationToken.ThrowIfCancellationRequested();
						}
						else
						{
							if (!reply2.Ok)
							{
								throw new InvalidOperationException($"Queue item {num + 1} failed: {RciClient.DescribeReply(reply2)}");
							}
							queued++;
							num++;
							Append($"  ACCEPTED {queued}/{items.Count}: {text2}");
							if (reply2.CommandStatus == 66)
							{
								Append("  remote buffer now full (this item was accepted).");
								TryStartPrint(c, Append);
							}
						}
					}
					TryStartPrint(c, Append);
					PrinterLiveStatus printerLiveStatus = RciCommands.ParseStatus(RciCommands.Status(c));
					Append($"Appended {queued} of {items.Count} item(s). {printerLiveStatus.Describe()}");
				});
				string text = $"Appended {queued} item(s) to Linx queue on {config.PrinterHost}:{config.PrinterPort}";
				return new InkjetOperationResult
				{
					Success = (queued == items.Count),
					ProtocolLog = log.ToString(),
					Message = ((queued == items.Count) ? text : $"Only {queued} of {items.Count} item(s) queued on {config.PrinterHost}:{config.PrinterPort}")
				};
			}
			catch (OperationCanceledException)
			{
				Append("Cancelled.");
				return new InkjetOperationResult
				{
					Success = false,
					ProtocolLog = log.ToString(),
					Message = "Linx queue load cancelled"
				};
			}
			catch (Exception ex2)
			{
				Append("Error: " + ex2.Message);
				return new InkjetOperationResult
				{
					Success = false,
					ProtocolLog = log.ToString(),
					Message = ex2.Message
				};
			}
			void Append(string message)
			{
				log.AppendLine(message);
				progress?.Report(message);
			}
		}, cancellationToken);
	}

	public Task<InkjetOperationResult> StatusAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			RciReply reply = RciCommands.Status(c);
			return (Ok: true, Summary: "Status OK. " + RciCommands.ParseStatus(reply).Describe() + "\nReply: " + RciClient.DescribeReply(reply));
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> SelectMessageAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			RciCommands.LoadMessage(c, config.JobName.Trim(), 0).EnsureOk("Select '" + config.JobName.Trim() + "'");
			return (Ok: true, Summary: "Loaded '" + config.JobName.Trim() + "'.");
		});
	}

	public Task<InkjetOperationResult> ListMessagesAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			IReadOnlyList<string> readOnlyList = RciCommands.ParseMessageDirectory(RciCommands.ListDirectory(c, 'M'));
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder2);
			handler.AppendLiteral("Messages (");
			handler.AppendFormatted(readOnlyList.Count);
			handler.AppendLiteral("):");
			stringBuilder3.AppendLine(ref handler);
			if (readOnlyList.Count == 0)
			{
				stringBuilder.AppendLine("  (none — printer may be printing, or store is empty)");
			}
			else
			{
				foreach (string item in readOnlyList)
				{
					stringBuilder2 = stringBuilder;
					StringBuilder stringBuilder4 = stringBuilder2;
					handler = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder2);
					handler.AppendLiteral("  ");
					handler.AppendFormatted(item);
					stringBuilder4.AppendLine(ref handler);
				}
			}
			return (Ok: true, Summary: stringBuilder.ToString().TrimEnd());
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> RemoteFieldsAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			IReadOnlyList<RemoteFieldInfo> readOnlyList = RciCommands.ParseRemoteFieldList(RciCommands.RequestRemoteFieldList(c));
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder3 = stringBuilder2;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder2);
			handler.AppendLiteral("Remote fields (");
			handler.AppendFormatted(readOnlyList.Count);
			handler.AppendLiteral("):");
			stringBuilder3.AppendLine(ref handler);
			foreach (RemoteFieldInfo item in readOnlyList)
			{
				stringBuilder2 = stringBuilder;
				StringBuilder stringBuilder4 = stringBuilder2;
				handler = new StringBuilder.AppendInterpolatedStringHandler(19, 3, stringBuilder2);
				handler.AppendLiteral("  ");
				handler.AppendFormatted(item.Name);
				handler.AppendLiteral("  max=");
				handler.AppendFormatted(item.MaxCharacters);
				handler.AppendLiteral("  autoload=");
				handler.AppendFormatted(item.AutoLoad);
				stringBuilder4.AppendLine(ref handler);
			}
			return (Ok: true, Summary: stringBuilder.ToString().TrimEnd());
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> QueueClearAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			TryStopPrint(c, delegate
			{
			});
			RciCommands.ClearRemoteFieldData(c).EnsureOk("Clear remote buffer");
			return (Ok: true, Summary: "Remote print buffer cleared.");
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> QueueModeAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, (RciClient c) => (Ok: true, Summary: "Print mode: " + RciCommands.ParsePrintMode(RciCommands.GetPrintMode(c)).Describe()), requireJobName: false);
	}

	public Task<InkjetOperationResult> JetAsync(InkjetConfig config, bool start)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			RciReply reply = RciCommands.Jet(c, start);
			reply.EnsureOk(start ? "Start jet" : "Stop jet");
			return (Ok: true, Summary: RciClient.DescribeReply(reply));
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> PrintControlAsync(InkjetConfig config, bool start)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			RciReply reply = RciCommands.PrintControl(c, start);
			reply.EnsureOk(start ? "Start print" : "Stop print");
			return (Ok: true, Summary: RciClient.DescribeReply(reply));
		}, requireJobName: false);
	}

	public Task<InkjetOperationResult> TriggerAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, delegate(RciClient c)
		{
			RciReply reply = RciCommands.Trigger(c);
			reply.EnsureOk("Trigger print");
			return (Ok: true, Summary: RciClient.DescribeReply(reply));
		}, requireJobName: false);
	}

	private Task<InkjetOperationResult> RunUtilityAsync(InkjetConfig config, Func<RciClient, (bool Ok, string Summary)> action, bool requireJobName = true)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName);
				(bool ok, string summary) result = (ok: false, summary: "");
				WithSession(config, Append, delegate(RciClient c)
				{
					result = action(c);
				});
				Append(result.summary);
				return new InkjetOperationResult
				{
					Success = result.ok,
					ProtocolLog = log.ToString(),
					Message = result.summary
				};
			}
			catch (Exception ex)
			{
				Append("Error: " + ex.Message);
				return new InkjetOperationResult
				{
					Success = false,
					ProtocolLog = log.ToString(),
					Message = ex.Message
				};
			}
			void Append(string message)
			{
				log.AppendLine(message);
			}
		});
	}

	private static void ValidateConfig(InkjetConfig config, bool requireJobName = true)
	{
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		if (string.IsNullOrWhiteSpace(config.PrinterHost))
		{
			throw new ArgumentException("Printer IP/host is required.");
		}
		int printerPort = config.PrinterPort;
		if ((printerPort < 1 || printerPort > 65535) ? true : false)
		{
			throw new ArgumentException("Printer port must be 1..65535.");
		}
		if (requireJobName && string.IsNullOrWhiteSpace(config.JobName))
		{
			throw new ArgumentException("Message name is required.");
		}
	}

	private static RciClient CreateClient(InkjetConfig config, Action<string> append, int timeoutMs = 8000)
	{
		return new RciClient(config.PrinterHost.Trim(), config.PrinterPort, timeoutMs, useChecksum: true, delegate(string msg)
		{
			append("  [rci] " + msg);
		});
	}

	private static void WithSession(InkjetConfig config, Action<string> append, Action<RciClient> action, int timeoutMs = 8000)
	{
		using RciClient rciClient = CreateClient(config, append, timeoutMs);
		append($"Connecting to {config.PrinterHost}:{config.PrinterPort}...");
		rciClient.Session(action);
	}

	private static void TryStartPrint(RciClient client, Action<string> append)
	{
		try
		{
			RciReply reply = RciCommands.PrintControl(client, start: true);
			if (reply.CommandStatus != 20)
			{
				if (reply.Acknowledged)
				{
					append("  start print sent.");
				}
				else
				{
					append("  start print: " + RciClient.DescribeReply(reply));
				}
			}
		}
		catch (Exception ex)
		{
			append("  start print: " + ex.Message);
		}
	}

	private static void TryStopPrint(RciClient client, Action<string> append)
	{
		try
		{
			RciReply reply = RciCommands.PrintControl(client, start: false);
			append("  stop print: " + RciClient.DescribeReply(reply));
		}
		catch (Exception ex)
		{
			append("  stop print: " + ex.Message);
		}
	}

	private static string FormatQueueItem(IReadOnlyDictionary<string, string> item)
	{
		return string.Join(" | ", item.Select<KeyValuePair<string, string>, string>((KeyValuePair<string, string> kv) => kv.Key + "=" + kv.Value));
	}
}
