using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaggJack.Models;

namespace TaggJack.Services.Inkjet;

public sealed class InkjetPrintService
{
	private const int QueueTransferItemLimit = 10;

	public static string PadVar(string value, int width)
	{
		if (value == null)
		{
			value = string.Empty;
		}
		if (width <= 0)
		{
			return value;
		}
		if (value.Length >= width)
		{
			return value.Substring(0, width);
		}
		return value.PadRight(width);
	}

	public static List<(byte Index, string Value)> BuildVariablesFromData(DataTable dataTable, IReadOnlyList<InkjetPositionMapping> mappings, int varWidth)
	{
		if (mappings == null || mappings.Count == 0)
		{
			throw new ArgumentException("At least one position mapping is required.");
		}
		List<InkjetPositionMapping> list = mappings.OrderBy((InkjetPositionMapping m) => m.Position).ToList();
		List<(byte, string)> list2 = new List<(byte, string)>();
		foreach (DataRow row in dataTable.Rows)
		{
			foreach (InkjetPositionMapping item in list)
			{
				int position = item.Position;
				if ((position < 1 || position > 10) ? true : false)
				{
					throw new ArgumentOutOfRangeException("mappings", "Position must be 1..10.");
				}
				if (string.IsNullOrWhiteSpace(item.FieldName))
				{
					throw new ArgumentException($"Position {item.Position} has no field mapping.");
				}
				if (!dataTable.Columns.Contains(item.FieldName))
				{
					throw new ArgumentException("Column '" + item.FieldName + "' not found in data source result.");
				}
				string value = row[item.FieldName]?.ToString() ?? string.Empty;
				list2.Add(((byte)item.Position, PadVar(value, varWidth)));
			}
		}
		return list2;
	}

	public Task<InkjetOperationResult> QueueLoadAsync(InkjetConfig config, DataTable dataTable, CancellationToken cancellationToken = default(CancellationToken))
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
					throw new InvalidOperationException("Inkjet template has no position mappings.");
				}
				if (dataTable == null || dataTable.Rows.Count == 0)
				{
					throw new InvalidOperationException("No records to send to inkjet printer.");
				}
				byte varsPerItem = (byte)list.Count;
				List<(byte, string)> list2 = BuildVariablesFromData(dataTable, list, config.VarWidth);
				int value = list2.Count / varsPerItem;
				int size = 10 * varsPerItem;
				List<List<(byte, string)>> list3 = (from source in list2.Chunk(size)
					select source.ToList()).ToList();
				List<byte> list4 = (from i in list2.Select<(byte, string), byte>(((byte Index, string Value) v) => v.Index).Distinct()
					orderby i
					select i).ToList();
				bool needsVar = list4.Contains(1);
				string text = config.JobName.Trim();
				ushort jobId = (ushort)Math.Clamp((config.JobId <= 0) ? 1 : config.JobId, 1, 999);
				Append($"Queue load: '{text}' → add {value} item(s) in {list3.Count} batch(es) (max {10}/batch, vars-per-item={varsPerItem}, indexes: {string.Join(",", list4)})...");
				cancellationToken.ThrowIfCancellationRequested();
				int num = QueryQueueRemainingItems(config, Append);
				bool appendOnly = num > 0;
				if (appendOnly)
				{
					Append($"  Queue already has {num} item(s) — append only " + "(skip select/E8h seed/enable so the last queued serial is kept).");
				}
				else
				{
					PrimeJobAndEnableQueue(config, Append, jobId, text, needsVar, list2, varsPerItem, cancellationToken);
				}
				cancellationToken.ThrowIfCancellationRequested();
				int num2 = 0;
				bool nonDoubleEnabled = appendOnly;
				if (appendOnly)
				{
					Append("  E9h non-double: already enabled; unchanged for append.");
				}
				for (int batchIndex = 0; batchIndex < list3.Count; batchIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					List<(byte Index, string Value)> chunk = list3[batchIndex];
					int num3 = chunk.Count / varsPerItem;
					int value2 = num2 + 1;
					int value3 = num2 + num3;
					Append($"Batch {batchIndex + 1}/{list3.Count}: records {value2}-{value3} ({num3} item(s))...");
					ProtocolReply batchReply = default(ProtocolReply);
					WithSession(config, Append, delegate(A48601Client c)
					{
						if (batchIndex == 0 && !appendOnly)
						{
							nonDoubleEnabled = ApplyNonDoublePrinting(c, enabled: true, Append);
						}
						batchReply = Commands.EnqueueExternalVariables(c, chunk, varsPerItem);
						LogQueueResult(batchReply, Append, indent: true);
					});
					if (batchReply.ReplyMissing)
					{
						string text2 = $"Batch {batchIndex + 1}/{list3.Count} returned no reply; queued {num2} of {value} item(s).";
						Append("Error: " + text2);
						return new InkjetOperationResult
						{
							Success = false,
							ProtocolLog = log.ToString(),
							Message = text2
						};
					}
					DataQueueResult dataQueueResult = Commands.ParseDataQueueReply(batchReply);
					if (!dataQueueResult.Ok || dataQueueResult.RemainingItems == 0)
					{
						string text3 = $"Batch {batchIndex + 1}/{list3.Count} failed: report=0x{dataQueueResult.Report:X4}, remaining={dataQueueResult.RemainingItems}; queued {num2} of {value} item(s).";
						Append("Error: " + text3);
						return new InkjetOperationResult
						{
							Success = false,
							ProtocolLog = log.ToString(),
							Message = text3
						};
					}
					num2 += num3;
					Append($"Batch {batchIndex + 1}/{list3.Count} accepted: sent={num3}, total sent={num2}/{value}, printer remaining={dataQueueResult.RemainingItems}.");
				}
				if (nonDoubleEnabled)
				{
					Append("Non-double printing is on — extra objects after the last serial will not reprint it.");
				}
				else
				{
					Append("WARNING: non-double printing is not confirmed; the last serial may repeat.");
				}
				Append("Ready — each object/photocell print consumes the next queued item.");
				string message = (appendOnly ? $"Appended {value} item(s) in {list3.Count} batch(es) to existing queue on {config.PrinterHost}:{config.PrinterPort}" : $"Queued {value} item(s) in {list3.Count} batch(es) to {config.PrinterHost}:{config.PrinterPort}");
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = message
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
			void Append(string value4)
			{
				log.AppendLine(value4);
			}
		}, cancellationToken);
	}

	private static int QueryQueueRemainingItems(InkjetConfig config, Action<string> append)
	{
		ProtocolReply reply = default(ProtocolReply);
		WithSession(config, append, delegate(A48601Client c)
		{
			reply = Commands.QueryDataQueueStatus(c);
		});
		if (reply.ReplyMissing)
		{
			append("  Queue status: (none — treating as empty, will prime job + enable)");
			return 0;
		}
		LogQueueResult(reply, append, indent: true);
		try
		{
			return Commands.ParseDataQueueReply(reply).RemainingItems;
		}
		catch (Exception ex)
		{
			append("  Queue status parse failed (" + ex.Message + ") — treating as empty, will prime job + enable");
			return 0;
		}
	}

	private static void PrimeJobAndEnableQueue(InkjetConfig config, Action<string> append, ushort jobId, string name, bool needsVar1, List<(byte Index, string Value)> vars, byte varsPerItem, CancellationToken cancellationToken)
	{
		WithSession(config, append, delegate(A48601Client c)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ProtocolReply jobByNumber = Commands.GetJobByNumber(c, jobId);
			if (jobByNumber.ReplyMissing || jobByNumber.ReplyDataLength < 8)
			{
				throw new InvalidOperationException("Could not fetch job blob (93h).");
			}
			byte[] array = jobByNumber.ReplyFrame.AsSpan(3, jobByNumber.ReplyDataLength).ToArray();
			bool flag = IndexOfAscii(array, "XXXXXXXXXX") >= 0;
			if (needsVar1 && flag)
			{
				byte[] array2 = JobBuilder.PatchFixedTextToExternalVariable(array, "XXXXXXXXXX", 1);
				ProtocolAck protocolAck = Commands.SendCompleteJobForPrinting(c, array2);
				append("  patched XXXXXXXXXX→var#1 + EEh: " + A48601Client.DescribeControl(protocolAck.Control));
				append("  (do not re-select job on UI — library text field is still fixed)");
			}
			else
			{
				ProtocolReply protocolReply = Commands.SelectJob(c, name);
				append($"  select '{name}': present={protocolReply.HasReportByte} 0x{protocolReply.ReportByte:X2}");
				if (needsVar1 && flag)
				{
					append("  WARNING: var 1 requested but field looks fixed");
				}
			}
			List<(byte, string)> list = (from v in vars.Take(varsPerItem)
				select (Index: v.Index, CreateQueueSeedValue(v.Value.Length, v.Index))).ToList();
			ProtocolAck protocolAck2 = Commands.SendVariables(c, list);
			append($"  E8h placeholder seed ({list.Count} vars): {A48601Client.DescribeControl(protocolAck2.Control)}");
			ProtocolReply protocolReply2 = Commands.RefreshPrintUnit(c);
			append(protocolReply2.ReplyMissing ? "  refresh print unit (50h FFh): ACK only" : ("  refresh print unit (50h FFh): " + A48601Client.DescribeFrame(protocolReply2.ReplyFrame)));
			LogQueueResult(Commands.SetDataQueueEnabled(c, enabled: true), append, indent: true);
		}, 15000);
	}

	private static string CreateQueueSeedValue(int width, byte index)
	{
		string text = $"~Q{index}~";
		if (width <= 0)
		{
			return text;
		}
		if (text.Length >= width)
		{
			return text.Substring(0, width);
		}
		return text.PadRight(width);
	}

	private static bool ApplyNonDoublePrinting(A48601Client client, bool enabled, Action<string> append)
	{
		try
		{
			ProtocolAck protocolAck = Commands.SetNonDoublePrinting(client, enabled);
			append("  E9h non-double " + (enabled ? "enable" : "disable") + ": " + A48601Client.DescribeControl(protocolAck.Control));
			return protocolAck.Control == 6;
		}
		catch (Exception ex)
		{
			append("  E9h note: " + ex.Message);
			return false;
		}
	}

	public Task<InkjetOperationResult> PingAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, "ping", (A48601Client c) => "Status OK. Reply: " + A48601Client.DescribeFrame(Commands.RequestStatus(c).ReplyFrame));
	}

	public Task<InkjetOperationResult> SelectJobAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, "select", delegate(A48601Client c)
		{
			ProtocolReply reply = Commands.SelectJob(c, config.JobName);
			LogSelectResult(reply, delegate
			{
			});
			return DescribeSelect(reply);
		}, keepOpen: true);
	}

	public Task<InkjetOperationResult> ListJobsAsync(InkjetConfig config)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				Append("Querying library slots 1..20...");
				for (ushort pos = 1; pos <= 20; pos++)
				{
					ProtocolReply reply = default(ProtocolReply);
					WithSession(config, Append, delegate(A48601Client c)
					{
						reply = Commands.QueryLibrarySlot(c, pos);
					});
					if (reply.ReplyMissing || reply.ReplyFrame.Length < 4)
					{
						Append($"  [{pos}] (no reply)");
					}
					else
					{
						Span<byte> span = reply.ReplyFrame.AsSpan(3, reply.ReplyDataLength);
						if (span.Length < 2)
						{
							Append($"  [{pos}] empty/short");
						}
						else
						{
							int num = (span[0] << 8) | span[1];
							if (num == 0)
							{
								Append($"  [{pos}] empty");
							}
							else
							{
								ReadOnlySpan<byte> readOnlySpan;
								if (span.Length <= 2)
								{
									readOnlySpan = ReadOnlySpan<byte>.Empty;
								}
								else
								{
									readOnlySpan = span.Slice(2, span.Length - 2);
								}
								ReadOnlySpan<byte> readOnlySpan2 = readOnlySpan;
								int num2 = readOnlySpan2.IndexOf<byte>(0);
								if (num2 >= 0)
								{
									readOnlySpan2 = readOnlySpan2.Slice(0, num2);
								}
								string value = Encoding.ASCII.GetString(readOnlySpan2);
								Append($"  [{pos}] id={num} name='{value}'");
							}
						}
					}
				}
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = "List complete"
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

	public Task<InkjetOperationResult> SendVarsAsync(InkjetConfig config, IReadOnlyList<(byte Index, string Value)> variables)
	{
		return RunUtilityAsync(config, "vars", delegate(A48601Client c)
		{
			List<(byte, string)> list = variables.Select<(byte, string), (byte, string)>(((byte Index, string Value) v) => (Index: v.Index, PadVar(v.Value, config.VarWidth))).ToList();
			ProtocolAck protocolAck = Commands.SendVariables(c, list);
			foreach (var item in list)
			{
				_ = item;
			}
			return "Vars result: " + A48601Client.DescribeControl(protocolAck.Control);
		}, keepOpen: true);
	}

	public Task<InkjetOperationResult> UpdateVarsAsync(InkjetConfig config, IReadOnlyList<(byte Index, string Value)> variables)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config);
				List<(byte Index, string)> padded = variables.Select<(byte, string), (byte, string)>(((byte Index, string Value) v) => (Index: v.Index, PadVar(v.Value, config.VarWidth))).ToList();
				Append("Updating '" + config.JobName + "' (select + E8h vars)...");
				WithSession(config, Append, delegate(A48601Client c)
				{
					Commands.UpdateJobVariables(c, config.JobName, padded, refreshPrintUnit: false, use9030Vars: false, delegate(string msg)
					{
						Append("  " + msg);
					});
				});
				foreach (var (value, text) in padded)
				{
					Append($"  set var[{value}] = '{text}' ({text.Length} chars)");
				}
				Append("Ready — pass the object (photocell).");
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = "Update complete"
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

	public Task<InkjetOperationResult> DumpJobAsync(InkjetConfig config)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				ushort id = (ushort)Math.Clamp((config.JobId <= 0) ? 1 : config.JobId, 1, 999);
				Append($"Fetching job id={id} (93h)...");
				ProtocolReply reply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					reply = Commands.GetJobByNumber(c, id);
				}, 10000);
				if (reply.ReplyMissing)
				{
					Append("No reply frame (ACK only).");
					return new InkjetOperationResult
					{
						Success = false,
						ProtocolLog = log.ToString(),
						Message = "No reply"
					};
				}
				Append("Reply: " + A48601Client.DescribeFrame(reply.ReplyFrame));
				byte[] source = reply.ReplyFrame.AsSpan(3, reply.ReplyDataLength).ToArray();
				string text = Encoding.ASCII.GetString(source.Select((byte b) => (byte)((b < 32 || b > 126) ? 46 : b)).ToArray());
				Append("ASCII view: " + text);
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = "Dump complete"
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

	public Task<InkjetOperationResult> MakeExternalAsync(InkjetConfig config, string? text = null)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config);
				ushort id = (ushort)Math.Clamp((config.JobId <= 0) ? 1 : config.JobId, 1, 999);
				string value = PadVar(text ?? "SHADE", config.VarWidth);
				Append($"Fetching job id={id}, patching fixed XXXXXXXXXX → external var #1...");
				ProtocolReply get = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					get = Commands.GetJobByNumber(c, id);
				}, 10000);
				if (get.ReplyMissing || get.ReplyDataLength < 8)
				{
					throw new InvalidOperationException("Could not fetch job blob (93h).");
				}
				byte[] array = get.ReplyFrame.AsSpan(3, get.ReplyDataLength).ToArray();
				byte[] patched = JobBuilder.PatchFixedTextToExternalVariable(array, "XXXXXXXXXX", 1);
				Append($"  original {array.Length} bytes → patched {patched.Length} bytes");
				Append("Loading patched job as current print job (EEh)...");
				WithSession(config, Append, delegate(A48601Client c)
				{
					ProtocolAck protocolAck = Commands.SendCompleteJobForPrinting(c, patched);
					Append("  EEh: " + A48601Client.DescribeControl(protocolAck.Control));
					ProtocolAck protocolAck2 = Commands.SendVariables(c, new List<(byte, string)> { (1, value) });
					Append("  E8h var[1]='" + value + "': " + A48601Client.DescribeControl(protocolAck2.Control));
				});
				Append("Ready — pass the object (photocell).");
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = "Make-external complete"
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

	public Task<InkjetOperationResult> QueueEnableAsync(InkjetConfig config, bool enabled)
	{
		if (!enabled)
		{
			return QueueDisableAsync(config);
		}
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				ProtocolReply reply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					reply = Commands.SetDataQueueEnabled(c, enabled: true);
					ApplyNonDoublePrinting(c, enabled: true, Append);
				});
				bool flag = LogQueueResult(reply, Append);
				return new InkjetOperationResult
				{
					Success = flag,
					ProtocolLog = log.ToString(),
					Message = (flag ? "Queue enabled (non-double printing on)" : "Queue enable failed")
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

	public Task<InkjetOperationResult> QueueResetAsync(InkjetConfig config)
	{
		return RunQueueSimpleAsync(config, (A48601Client c) => Commands.ResetDataQueue(c), "reset");
	}

	public Task<InkjetOperationResult> QueueStatusAsync(InkjetConfig config)
	{
		return RunQueueSimpleAsync(config, (A48601Client c) => Commands.QueryDataQueueStatus(c), "status");
	}

	public Task<InkjetOperationResult> QueueDisableAsync(InkjetConfig config)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				ProtocolReply reply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					reply = Commands.SetDataQueueEnabled(c, enabled: false);
					ApplyNonDoublePrinting(c, enabled: false, Append);
				});
				bool flag = LogQueueResult(reply, Append);
				return new InkjetOperationResult
				{
					Success = flag,
					ProtocolLog = log.ToString(),
					Message = (flag ? "Queue disabled (non-double printing off)" : "Queue disable failed")
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

	public Task<InkjetOperationResult> JetAsync(InkjetConfig config, bool start)
	{
		return RunUtilityAsync(config, start ? "jet start" : "jet stop", delegate(A48601Client c)
		{
			ProtocolAck protocolAck = (start ? Commands.StartJet(c) : Commands.StopJet(c));
			return "Jet " + (start ? "start" : "stop") + " result: " + A48601Client.DescribeControl(protocolAck.Control);
		}, keepOpen: true, requireJobName: false);
	}

	public Task<InkjetOperationResult> TriggerAsync(InkjetConfig config)
	{
		return RunUtilityAsync(config, "trigger", (A48601Client c) => "Print trigger result: " + A48601Client.DescribeControl(Commands.TriggerPrint(c).Control), keepOpen: true, requireJobName: false);
	}

	public Task<InkjetOperationResult> AckFaultsAsync(InkjetConfig config)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				Append("Sending acknowledge-faults (3Ch)...");
				WithSession(config, Append, delegate(A48601Client c)
				{
					c.Send(60, default(ReadOnlySpan<byte>), "ack faults (3Ch)");
				});
				Append("Fault ack sent (ACK).");
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = "Faults acknowledged"
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

	public Task<InkjetOperationResult> PushLibraryAsync(InkjetConfig config, string text, bool forceLibrary)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				if (!forceLibrary)
				{
					Append("'push' is blocked: library write (9Bh) causes printer fault 5140 on this device.");
					Append("Use an existing UI template with queue load / update instead.");
					Append("To force 9Bh anyway, confirm Force Library.");
					return new InkjetOperationResult
					{
						Success = false,
						ProtocolLog = log.ToString(),
						Message = "Library write blocked (use Force Library to override)"
					};
				}
				ValidateConfig(config);
				if (string.IsNullOrEmpty(text))
				{
					throw new ArgumentException("Text is required for library push.");
				}
				string name = config.JobName.Trim();
				ushort num = (ushort)Math.Clamp((config.JobId <= 0) ? 1 : config.JobId, 1, 999);
				byte[] libraryJob = JobBuilder.BuildLibraryJob(name, text, num, text, 286, 1);
				Append($"Pushing library job '{name}' id={num} ({libraryJob.Length} bytes)...");
				Append("WARNING: may raise fault 5140 on the printer.");
				ProtocolReply pushReply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					pushReply = Commands.PushToLibrary(c, libraryJob, (byte)1);
				});
				LogLibraryResult(pushReply, Append);
				if (!IsLibraryWriteOk(pushReply, Append))
				{
					try
					{
						WithSession(config, Append, delegate(A48601Client c)
						{
							c.Send(60, default(ReadOnlySpan<byte>), "ack faults (3Ch)");
						});
					}
					catch (Exception ex)
					{
						Append("Fault ack failed: " + ex.Message);
					}
					return new InkjetOperationResult
					{
						Success = false,
						ProtocolLog = log.ToString(),
						Message = "Library write failed"
					};
				}
				ProtocolReply selectReply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					selectReply = Commands.SelectJob(c, name);
				});
				Append(DescribeSelect(selectReply));
				return new InkjetOperationResult
				{
					Success = IsSelectOk(selectReply),
					ProtocolLog = log.ToString(),
					Message = "Library push complete"
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
			}
		});
	}

	private Task<InkjetOperationResult> RunQueueSimpleAsync(InkjetConfig config, Func<A48601Client, ProtocolReply> send, string action)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName: false);
				ProtocolReply reply = default(ProtocolReply);
				WithSession(config, Append, delegate(A48601Client c)
				{
					reply = send(c);
				});
				bool flag = LogQueueResult(reply, Append);
				return new InkjetOperationResult
				{
					Success = flag,
					ProtocolLog = log.ToString(),
					Message = (flag ? ("Queue " + action + " OK") : ("Queue " + action + " failed"))
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

	private Task<InkjetOperationResult> RunUtilityAsync(InkjetConfig config, string label, Func<A48601Client, string> action, bool keepOpen = false, bool requireJobName = true)
	{
		return Task.Run(delegate
		{
			StringBuilder log = new StringBuilder();
			try
			{
				ValidateConfig(config, requireJobName);
				string message;
				if (keepOpen)
				{
					using A48601Client a48601Client = CreateClient(config, Append);
					Append($"Connecting to {config.PrinterHost}:{config.PrinterPort}...");
					a48601Client.Connect();
					message = action(a48601Client);
					Append(message);
				}
				else
				{
					string localSummary = string.Empty;
					WithSession(config, Append, delegate(A48601Client c)
					{
						localSummary = action(c);
					});
					message = localSummary;
					Append(message);
				}
				return new InkjetOperationResult
				{
					Success = true,
					ProtocolLog = log.ToString(),
					Message = message
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
			void Append(string value)
			{
				log.AppendLine(value);
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
			throw new ArgumentException("Job name is required.");
		}
	}

	private static A48601Client CreateClient(InkjetConfig config, Action<string> append, int timeoutMs = 5000)
	{
		return new A48601Client(config.PrinterHost.Trim(), config.PrinterPort, timeoutMs, useHandshake: true, delegate(string msg)
		{
			append("  [proto] " + msg);
		});
	}

	private static void WithSession(InkjetConfig config, Action<string> append, Action<A48601Client> action, int timeoutMs = 5000)
	{
		using A48601Client a48601Client = CreateClient(config, append, timeoutMs);
		append($"Connecting to {config.PrinterHost}:{config.PrinterPort}...");
		a48601Client.Session(action);
	}

	private static bool LogQueueResult(ProtocolReply reply, Action<string> append, bool indent = false)
	{
		string text = (indent ? "  " : "");
		append(text + "Queue control: " + A48601Client.DescribeControl(reply.Control));
		if (reply.ReplyMissing)
		{
			append(text + "Queue reply:   (none — feature may be unsupported)");
			return false;
		}
		append(text + "Queue reply:   " + A48601Client.DescribeFrame(reply.ReplyFrame));
		try
		{
			DataQueueResult dataQueueResult = Commands.ParseDataQueueReply(reply);
			append($"{text}Queue report:  0x{dataQueueResult.Report:X4} — {Commands.DescribeDataQueueReport(dataQueueResult.Report)}");
			append($"{text}Queue status:  0x{dataQueueResult.QueueStatus:X2} — {Commands.DescribeDataQueueStatus(dataQueueResult.QueueStatus)}");
			append($"{text}Printer bits:  0x{dataQueueResult.PrinterStatus:X2}");
			append($"{text}Remaining:     {dataQueueResult.RemainingItems} item(s), {dataQueueResult.RemainingMemoryBytes} bytes free");
			if (dataQueueResult.QueueStatus == 1)
			{
				append(text + "NOTE: printer says job has no external variable — fix the job first.");
			}
			return dataQueueResult.Ok;
		}
		catch (Exception ex)
		{
			append(text + "Queue parse:   " + ex.Message);
			return false;
		}
	}

	private static void LogSelectResult(ProtocolReply reply, Action<string> append)
	{
		append(DescribeSelect(reply));
	}

	private static string DescribeSelect(ProtocolReply reply)
	{
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder2);
		handler.AppendLiteral("Select control: ");
		handler.AppendFormatted(A48601Client.DescribeControl(reply.Control));
		stringBuilder3.AppendLine(ref handler);
		if (reply.ReplyMissing)
		{
			stringBuilder.Append("Select reply:   (none)");
			return stringBuilder.ToString().TrimEnd();
		}
		stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder4 = stringBuilder2;
		handler = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder2);
		handler.AppendLiteral("Select reply:   ");
		handler.AppendFormatted(A48601Client.DescribeFrame(reply.ReplyFrame));
		stringBuilder4.AppendLine(ref handler);
		if (!reply.HasReportByte)
		{
			stringBuilder.Append("Select report:  (empty length — treating as OK)");
		}
		else
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder5 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(21, 2, stringBuilder2);
			handler.AppendLiteral("Select report:  0x");
			handler.AppendFormatted(reply.ReportByte, "X2");
			handler.AppendLiteral(" — ");
			handler.AppendFormatted(Commands.DescribeSelectReport(reply.ReportByte));
			stringBuilder5.Append(ref handler);
		}
		return stringBuilder.ToString().TrimEnd();
	}

	private static bool IsSelectOk(ProtocolReply reply)
	{
		if (!reply.ReplyMissing)
		{
			if (reply.HasReportByte)
			{
				return reply.ReportByte == 0;
			}
			return true;
		}
		return false;
	}

	private static void LogLibraryResult(ProtocolReply reply, Action<string> append)
	{
		append("Library control: " + A48601Client.DescribeControl(reply.Control));
		if (reply.ReplyMissing)
		{
			append("Library reply:   (no C5h report — printer ACK'd write; check UI job list)");
			return;
		}
		append("Library reply:   " + A48601Client.DescribeFrame(reply.ReplyFrame));
		append($"Library report:  0x{reply.ReportByte:X2} — {Commands.DescribeLibraryReport(reply.ReportByte)}");
	}

	private static bool IsLibraryWriteOk(ProtocolReply reply, Action<string> append)
	{
		if (reply.ReplyMissing)
		{
			append("Library write did NOT complete: got ACK but no C5h report.");
			return false;
		}
		byte reportByte = reply.ReportByte;
		if ((uint)reportByte <= 1u)
		{
			return true;
		}
		return false;
	}

	private static int IndexOfAscii(byte[] haystack, string text)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(text);
		for (int i = 0; i + bytes.Length <= haystack.Length; i++)
		{
			bool flag = true;
			for (int j = 0; j < bytes.Length; j++)
			{
				if (haystack[i + j] != bytes[j])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return i;
			}
		}
		return -1;
	}
}
