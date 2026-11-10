using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaggJack.Services.Inkjet;

public static class Commands
{
	public const byte Status = 35;

	public const byte AcknowledgeFaults = 60;

	public const byte SendJobToLibrary = 155;

	public const byte LibraryWriteReport = 197;

	public const byte SelectJobByName = 160;

	public const byte StartPrintingDtop = 148;

	public const byte JetOrPrinter = 198;

	public const byte SendExternalVariables = 232;

	public const byte NonDoublePrinting = 233;

	public const byte SendJobForPrinting = 239;

	public const byte DataQueue = 109;

	public static ProtocolReply RequestStatus(A48601Client client)
	{
		return client.SendWithReply(35, default(ReadOnlySpan<byte>), "status (23h)");
	}

	public static ProtocolReply PushToLibrary(A48601Client client, ReadOnlySpan<byte> libraryJob, byte? writeType = 1)
	{
		byte[] array;
		string text;
		if (!writeType.HasValue)
		{
			array = libraryJob.ToArray();
			text = "omit";
		}
		else
		{
			array = new byte[libraryJob.Length + 1];
			libraryJob.CopyTo(array);
			array[^1] = writeType.Value;
			text = $"{writeType.Value:X2}h";
		}
		ProtocolReply result = client.SendWithReply(155, array, "push library (9Bh) writeType=" + text, allowMissingReply: true);
		if (!result.ReplyMissing)
		{
			ValidateReportId(result.ReplyFrame, 197, "library write");
		}
		return result;
	}

	public static ProtocolReply PushToLibraryCustomRank(A48601Client client, ReadOnlySpan<byte> libraryJob, ushort position = 1, bool replace = true)
	{
		byte[] array = new byte[3 + libraryJob.Length];
		array[0] = (replace ? ((byte)1) : ((byte)0));
		array[1] = (byte)((position >> 8) & 0xFF);
		array[2] = (byte)(position & 0xFF);
		libraryJob.CopyTo(array.AsSpan(3));
		ProtocolReply result = client.SendWithReply(157, array, $"push library custom (9Dh) pos={position} replace={replace}", allowMissingReply: true);
		if (!result.ReplyMissing)
		{
			ValidateReportId(result.ReplyFrame, 197, "library custom write");
		}
		return result;
	}

	public static ProtocolAck SendToPrintBuffer(A48601Client client, ReadOnlySpan<byte> printBufferJob)
	{
		return client.Send(239, printBufferJob, "print buffer (EFh)");
	}

	public static ProtocolAck SendCompleteJobForPrinting(A48601Client client, ReadOnlySpan<byte> completeJob)
	{
		return client.Send(238, completeJob, "complete job for printing (EEh)");
	}

	public static ProtocolReply SelectJob(A48601Client client, string jobName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobName, "jobName");
		byte[] bytes = Encoding.ASCII.GetBytes(jobName.Trim());
		int num = bytes.Length;
		if ((num < 1 || num > 20) ? true : false)
		{
			throw new ArgumentException("Job name must be 1..20 ASCII characters.", "jobName");
		}
		ProtocolReply result = client.SendWithReply(160, bytes, "select job (A0h) name='" + jobName.Trim() + "'", allowMissingReply: true);
		if (!result.ReplyMissing && result.ReplyFrame.Length >= 5)
		{
			ValidateReportId(result.ReplyFrame, 160, "select job");
		}
		return result;
	}

	public static ProtocolAck StartJet(A48601Client client)
	{
		return client.Send(198, new byte[1] { 1 }, "start jet (C6h 01h)");
	}

	public static ProtocolAck StopJet(A48601Client client)
	{
		return client.Send(198, new byte[1] { 0 }, "stop jet (C6h 00h)");
	}

	public static ProtocolAck TriggerPrint(A48601Client client)
	{
		return client.Send(148, default(ReadOnlySpan<byte>), "trigger print / DTOP (94h)");
	}

	public static ProtocolReply RefreshPrintUnit(A48601Client client)
	{
		return client.SendWithReply(80, new byte[1] { 255 }, "refresh print unit (50h FFh)", allowMissingReply: true);
	}

	public static void UpdateJobVariables(A48601Client client, string jobName, IReadOnlyList<(byte Index, string Value)> variables, bool refreshPrintUnit, bool use9030Vars = false, Action<string>? log = null)
	{
		ProtocolReply protocolReply = SelectJob(client, jobName);
		log?.Invoke($"select report present={protocolReply.HasReportByte} value=0x{protocolReply.ReportByte:X2}");
		if (protocolReply.HasReportByte && protocolReply.ReportByte != 0)
		{
			throw new InvalidOperationException("Select failed: " + DescribeSelectReport(protocolReply.ReportByte));
		}
		ProtocolAck protocolAck = (use9030Vars ? SendVariables9030(client, variables) : SendVariables(client, variables));
		log?.Invoke($"vars: {A48601Client.DescribeControl(protocolAck.Control)} ({(use9030Vars ? "99h" : "E8h")})");
		if (refreshPrintUnit)
		{
			ProtocolReply protocolReply2 = RefreshPrintUnit(client);
			if (protocolReply2.ReplyMissing)
			{
				log?.Invoke("refresh print unit: ACK only (no report frame)");
			}
			else
			{
				log?.Invoke("refresh print unit: " + A48601Client.DescribeFrame(protocolReply2.ReplyFrame));
			}
		}
	}

	public static ProtocolReply QueryLibrarySlot(A48601Client client, ushort position)
	{
		byte[] array = new byte[2]
		{
			(byte)((position >> 8) & 0xFF),
			(byte)(position & 0xFF)
		};
		return client.SendWithReply(71, array, $"library query (47h) pos={position}", allowMissingReply: true);
	}

	public static ProtocolReply GetJobByNumber(A48601Client client, ushort jobNumber)
	{
		byte[] array = new byte[2]
		{
			(byte)((jobNumber >> 8) & 0xFF),
			(byte)(jobNumber & 0xFF)
		};
		return client.SendWithReply(147, array, $"get job (93h) id={jobNumber}", allowMissingReply: true);
	}

	public static ProtocolAck SendVariables(A48601Client client, IReadOnlyList<(byte Index, string Value)> variables)
	{
		if (variables.Count == 0)
		{
			throw new ArgumentException("At least one variable is required.", "variables");
		}
		using MemoryStream memoryStream = new MemoryStream();
		foreach (var (b, s) in variables)
		{
			if ((b < 1 || b > 10) ? true : false)
			{
				throw new ArgumentOutOfRangeException("variables", "Variable index must be 1..10.");
			}
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			memoryStream.WriteByte(b);
			memoryStream.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
			memoryStream.WriteByte((byte)(bytes.Length & 0xFF));
			memoryStream.Write(bytes);
		}
		return client.Send(232, memoryStream.ToArray(), $"external vars (E8h) count={variables.Count}");
	}

	public static ProtocolAck SendVariables9030(A48601Client client, IReadOnlyList<(byte Index, string Value)> variables)
	{
		if (variables.Count == 0)
		{
			throw new ArgumentException("At least one variable is required.", "variables");
		}
		string[] array = new string[10];
		foreach (var (b, text) in variables)
		{
			if ((b < 1 || b > 10) ? true : false)
			{
				throw new ArgumentOutOfRangeException("variables", "Variable index must be 1..10.");
			}
			array[b - 1] = text;
		}
		byte b2 = variables.Max<(byte, string), byte>(((byte Index, string Value) v) => v.Index);
		using MemoryStream memoryStream = new MemoryStream();
		for (byte b3 = 1; b3 <= b2; b3++)
		{
			memoryStream.WriteByte(18);
			string text2 = array[b3 - 1];
			if (!string.IsNullOrEmpty(text2))
			{
				memoryStream.Write(Encoding.ASCII.GetBytes(text2));
			}
			memoryStream.WriteByte(18);
		}
		return client.Send(153, memoryStream.ToArray(), $"external vars 9030 (99h) slots=1..{b2}");
	}

	public static ProtocolAck SetNonDoublePrinting(A48601Client client, bool enabled)
	{
		return client.Send(233, new byte[1] { enabled ? ((byte)1) : ((byte)0) }, (enabled ? "enable" : "disable") + " non-double print (E9h)");
	}

	public static ProtocolReply SetDataQueueEnabled(A48601Client client, bool enabled)
	{
		byte[] array = new byte[3]
		{
			0,
			1,
			enabled ? ((byte)1) : ((byte)0)
		};
		return client.SendWithReply(109, array, "data queue " + (enabled ? "enable" : "disable") + " (6Dh)", allowMissingReply: true, acknowledgeReply: false);
	}

	public static ProtocolReply ResetDataQueue(A48601Client client)
	{
		byte[] array = new byte[2] { 0, 2 };
		return client.SendWithReply(109, array, "data queue reset (6Dh)", allowMissingReply: true, acknowledgeReply: false);
	}

	public static ProtocolReply QueryDataQueueStatus(A48601Client client)
	{
		byte[] array = new byte[2] { 0, 3 };
		return client.SendWithReply(109, array, "data queue status (6Dh)", allowMissingReply: true, acknowledgeReply: false);
	}

	public static ProtocolReply EnqueueExternalVariables(A48601Client client, IReadOnlyList<(byte Index, string Value)> variables, byte variablesPerItem = 1)
	{
		if (variables.Count == 0)
		{
			throw new ArgumentException("At least one variable value is required.", "variables");
		}
		if ((variablesPerItem < 1 || variablesPerItem > 10) ? true : false)
		{
			throw new ArgumentOutOfRangeException("variablesPerItem", "Must be 1..10.");
		}
		if (variables.Count % variablesPerItem != 0)
		{
			throw new ArgumentException($"Variable count ({variables.Count}) must be a multiple of --vars-per-item ({variablesPerItem}).");
		}
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte(0);
		memoryStream.WriteByte(10);
		memoryStream.WriteByte((byte)((variables.Count >> 8) & 0xFF));
		memoryStream.WriteByte((byte)(variables.Count & 0xFF));
		memoryStream.WriteByte(variablesPerItem);
		foreach (var (b, s) in variables)
		{
			if ((b < 1 || b > 10) ? true : false)
			{
				throw new ArgumentOutOfRangeException("variables", "Variable index must be 1..10.");
			}
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			memoryStream.WriteByte(b);
			memoryStream.WriteByte((byte)((bytes.Length >> 8) & 0xFF));
			memoryStream.WriteByte((byte)(bytes.Length & 0xFF));
			memoryStream.Write(bytes);
		}
		int value = variables.Count / variablesPerItem;
		return client.SendWithReply(109, memoryStream.ToArray(), $"data queue add (6Dh) items={value} vars/item={variablesPerItem}", allowMissingReply: true, acknowledgeReply: false);
	}

	public static DataQueueResult ParseDataQueueReply(ProtocolReply reply)
	{
		if (reply.ReplyMissing || reply.ReplyFrame.Length < 4)
		{
			return DataQueueResult.Missing;
		}
		byte[] replyFrame = reply.ReplyFrame;
		if (replyFrame[0] != 109)
		{
			throw new InvalidOperationException($"Expected 6Dh reply, got {replyFrame[0]:X2}h.");
		}
		int num = (replyFrame[1] << 8) | replyFrame[2];
		if (num < 10 || replyFrame.Length < 3 + num + 1)
		{
			throw new InvalidOperationException("Short 6Dh reply: " + A48601Client.FormatHex(replyFrame));
		}
		ushort report = (ushort)((replyFrame[3] << 8) | replyFrame[4]);
		byte queueStatus = replyFrame[5];
		byte printerStatus = replyFrame[6];
		ushort remainingItems = (ushort)((replyFrame[7] << 8) | replyFrame[8]);
		uint remainingMemoryBytes = (uint)((replyFrame[9] << 24) | (replyFrame[10] << 16) | (replyFrame[11] << 8) | replyFrame[12]);
		return new DataQueueResult(report, queueStatus, printerStatus, remainingItems, remainingMemoryBytes);
	}

	public static string DescribeDataQueueReport(ushort report)
	{
		return report switch
		{
			0 => "Unknown command", 
			1 => "Command processed", 
			2 => "State already set (enable/disable)", 
			3 => "Data processing function nonexistent", 
			4 => "Not validated data queue", 
			255 => "Data queue full (not processed)", 
			_ => $"Unknown report 0x{report:X4}", 
		};
	}

	public static string DescribeDataQueueStatus(byte status)
	{
		return status switch
		{
			0 => "Data queue in use", 
			1 => "Job in place without external variable", 
			2 => "Queue empty, no data to print", 
			_ => $"Unknown queue status 0x{status:X2}", 
		};
	}

	public static string DescribeLibraryReport(byte report)
	{
		return report switch
		{
			0 => "Write successful, job replaced", 
			1 => "Write successful, job created", 
			2 => "Write failed (library full)", 
			5 => "Write failed (ID already exists in another message)", 
			8 => "Undefined error", 
			9 => "Existing job in store (for creation)", 
			11 => "Maximum capacity of store reached", 
			12 => "Message ID reserved for production", 
			13 => "Message used by a production", 
			14 => "No library available", 
			15 => "Unable to remove active message", 
			17 => "Library mode not suitable for this command (Auto vs Custom rank mismatch)", 
			19 => "Font and/or algorithms missing in the store", 
			_ => $"Unknown report 0x{report:X2}", 
		};
	}

	public static string DescribeSelectReport(byte report)
	{
		return report switch
		{
			0 => "Message selection successful", 
			1 => "Message not present in the store", 
			2 => "No active production", 
			3 => "Message not usable due to faulty content", 
			4 => "Inconsistent production (distance A/B/C too small)", 
			5 => "Algorithm used by the message not present", 
			6 => "Font used by the message not present", 
			_ => $"Unknown report 0x{report:X2}", 
		};
	}

	private static void ValidateReportId(byte[] replyFrame, byte expectedId, string context)
	{
		if (replyFrame.Length < 5)
		{
			throw new InvalidOperationException("Short reply for " + context + ": " + A48601Client.FormatHex(replyFrame));
		}
		if (replyFrame[0] != expectedId)
		{
			throw new InvalidOperationException($"Unexpected reply ID for {context}: got {replyFrame[0]:X2}, expected {expectedId:X2}.");
		}
		int num = (replyFrame[1] << 8) | replyFrame[2];
		if (num < 1 || replyFrame.Length < 3 + num + 1)
		{
			throw new InvalidOperationException("Invalid reply length for " + context + ".");
		}
	}
}
