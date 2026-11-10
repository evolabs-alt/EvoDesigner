using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TaggJack.Services.Inkjet.Linx;

public static class RciCommands
{
	public const byte StartJet = 15;

	public const byte StopJet = 16;

	public const byte StartPrint = 17;

	public const byte StopPrint = 18;

	public const byte TriggerPrint = 19;

	public const byte PrinterStatus = 20;

	public const byte MessageTypeRequest = 24;

	public const byte DownloadMessage = 25;

	public const byte UploadMessage = 26;

	public const byte DeleteMessage = 27;

	public const byte DownloadRemoteField = 29;

	public const byte LoadPrintMessage = 30;

	public const byte RequestPrintMessage = 31;

	public const byte SetPrintMode = 32;

	public const byte RequestPrintMode = 33;

	public const byte RequestDataDirectory = 97;

	public const byte RequestRemoteFields = 157;

	public const byte SendRemoteByName = 158;

	public const byte SendRemoteByStream = 159;

	public const byte ClearError = 84;

	public static RciReply Status(RciClient client)
	{
		return client.Send(20, default(ReadOnlySpan<byte>), "status (20)");
	}

	public static PrinterLiveStatus ParseStatus(RciReply reply)
	{
		reply.EnsureOk("Status");
		if (reply.Data.Length < 6)
		{
			throw new InvalidOperationException("Short status reply: " + RciClient.FormatHex(reply.Data));
		}
		uint errorMask = (uint)(reply.Data[2] | (reply.Data[3] << 8) | (reply.Data[4] << 16) | (reply.Data[5] << 24));
		return new PrinterLiveStatus(reply.Data[0], reply.Data[1], errorMask);
	}

	public static RciReply Jet(RciClient client, bool start)
	{
		int commandId = (start ? 15 : 16);
		string label = (start ? "start jet (15)" : "stop jet (16)");
		return client.Send((byte)commandId, default(ReadOnlySpan<byte>), label);
	}

	public static RciReply PrintControl(RciClient client, bool start)
	{
		int commandId = (start ? 17 : 18);
		string label = (start ? "start print (17)" : "stop print (18)");
		return client.Send((byte)commandId, default(ReadOnlySpan<byte>), label);
	}

	public static RciReply Trigger(RciClient client)
	{
		return client.Send(19, default(ReadOnlySpan<byte>), "trigger print (19)");
	}

	public static RciReply LoadMessage(RciClient client, string name, ushort printCount = 0)
	{
		byte[] array = new byte[18];
		RciClient.PadName(name).CopyTo(array, 0);
		BinaryPrimitives.WriteUInt16LittleEndian(array.AsSpan(16, 2), printCount);
		return client.Send(30, array, $"load message (30) '{name}' count={printCount}");
	}

	public static RciReply RequestCurrentMessage(RciClient client)
	{
		return client.Send(31, default(ReadOnlySpan<byte>), "request print message (31)");
	}

	public static (string Name, ushort Remaining) ParseCurrentMessage(RciReply reply)
	{
		reply.EnsureOk("Request print message");
		if (reply.Data.Length < 16)
		{
			return (Name: "(none)", Remaining: 0);
		}
		string item = RciClient.ReadCString(reply.Data.AsSpan(0, Math.Min(16, reply.Data.Length)));
		ushort item2 = 0;
		if (reply.Data.Length >= 18)
		{
			item2 = BinaryPrimitives.ReadUInt16LittleEndian(reply.Data.AsSpan(16, 2));
		}
		return (Name: item, Remaining: item2);
	}

	public static RciReply DownloadRemoteFieldData(RciClient client, string text)
	{
		return DownloadRemoteFieldData(client, Encoding.UTF8.GetBytes(text), text);
	}

	public static RciReply DownloadRemoteFieldData(RciClient client, ReadOnlySpan<byte> textBytes, string? preview = null)
	{
		byte[] array = new byte[2 + textBytes.Length];
		BinaryPrimitives.WriteUInt16LittleEndian(array.AsSpan(0, 2), (ushort)textBytes.Length);
		textBytes.CopyTo(array.AsSpan(2));
		string text = preview ?? ((textBytes.Length <= 40) ? Encoding.Latin1.GetString(textBytes) : $"{textBytes.Length} bytes");
		return client.Send(29, array, "remote buffer (29) '" + text + "'");
	}

	public static byte[] BuildRemoteBufferItem(IReadOnlyList<RemoteFieldInfo> layout, IReadOnlyDictionary<string, string> values)
	{
		using MemoryStream memoryStream = new MemoryStream();
		foreach (RemoteFieldInfo item in layout)
		{
			values.TryGetValue(item.Name, out string value);
			byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
			byte[] array = new byte[item.MaxCharacters];
			Array.Fill(array, (byte)32);
			Buffer.BlockCopy(bytes, 0, array, 0, Math.Min(bytes.Length, array.Length));
			memoryStream.Write(array);
		}
		return memoryStream.ToArray();
	}

	public static RciReply GetPrintMode(RciClient client)
	{
		return client.Send(33, default(ReadOnlySpan<byte>), "request print mode (33)");
	}

	public static PrintModeSettings ParsePrintMode(RciReply reply)
	{
		reply.EnsureOk("Request print mode");
		if (reply.Data.Length < 5)
		{
			throw new InvalidOperationException("Short print-mode reply: " + RciClient.FormatHex(reply.Data));
		}
		return new PrintModeSettings(reply.Data[0], reply.Data[1], reply.Data[2], reply.Data[3], reply.Data[4]);
	}

	public static RciReply ConfigurePrintMode(RciClient client, byte mode, byte remoteBufferDivisor, bool clearBuffer = true)
	{
		byte[] array = new byte[9]
		{
			mode,
			0,
			0,
			clearBuffer ? ((byte)1) : ((byte)0),
			remoteBufferDivisor,
			0,
			0,
			0,
			0
		};
		return client.Send(32, array, $"set print mode (32) mode={mode} divisor={remoteBufferDivisor} clear={clearBuffer}");
	}

	public static RciReply ClearRemoteFieldData(RciClient client)
	{
		return client.Send(29, new byte[2] { 0, 0 }, "clear remote fields (29)");
	}

	public static RciReply SendRemoteFieldByName(RciClient client, string fieldName, string value)
	{
		return SendRemoteFieldsByName(client, new(string, string)[1] { (fieldName, value) });
	}

	public static RciReply SendRemoteFieldsByName(RciClient client, IReadOnlyList<(string Name, string Value)> fields)
	{
		if (fields.Count == 0)
		{
			throw new ArgumentException("At least one named remote field is required.");
		}
		if (fields.Count > 255)
		{
			throw new ArgumentException("Too many remote fields (max 255).");
		}
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte((byte)fields.Count);
		foreach (var field in fields)
		{
			string item = field.Name;
			string item2 = field.Value;
			byte[] bytes = Encoding.Latin1.GetBytes(item);
			int num = bytes.Length;
			if ((num > 31 || num == 0) ? true : false)
			{
				throw new ArgumentException("Remote field name '" + item + "' must be 1..31 characters.");
			}
			memoryStream.Write(bytes);
			memoryStream.WriteByte(0);
			memoryStream.Write(Encoding.UTF8.GetBytes(item2));
			memoryStream.WriteByte(0);
		}
		string text = string.Join(", ", fields.Select<(string, string), string>(((string Name, string Value) f) => f.Name + "='" + f.Value + "'"));
		return client.Send(158, memoryStream.ToArray(), "remote by name (158) " + text);
	}

	public static RciReply RequestRemoteFieldList(RciClient client)
	{
		return client.Send(157, default(ReadOnlySpan<byte>), "request remote fields (157)");
	}

	public static IReadOnlyList<RemoteFieldInfo> ParseRemoteFieldList(RciReply reply)
	{
		reply.EnsureOk("Request remote fields");
		if (reply.Data.Length == 0 || (reply.Data.Length == 1 && reply.Data[0] == 0))
		{
			return Array.Empty<RemoteFieldInfo>();
		}
		byte b = reply.Data[0];
		List<RemoteFieldInfo> list = new List<RemoteFieldInfo>(b);
		int num = 1;
		for (int i = 0; i < b; i++)
		{
			if (num + 36 > reply.Data.Length)
			{
				break;
			}
			num++;
			string name = RciClient.ReadCString(reply.Data.AsSpan(num, 32));
			num += 32;
			byte maxCharacters = reply.Data[num++];
			byte b2 = reply.Data[num++];
			num += 2;
			list.Add(new RemoteFieldInfo(name, maxCharacters, b2 != 0));
		}
		return list;
	}

	public static RciReply ListDirectory(RciClient client, char dataType)
	{
		return client.Send(97, new byte[1] { (byte)dataType }, $"data directory (97) type={dataType}");
	}

	public static IReadOnlyList<string> ParseMessageDirectory(RciReply reply)
	{
		reply.EnsureOk("Message directory");
		if (reply.Data.Length < 3)
		{
			return Array.Empty<string>();
		}
		ushort num = BinaryPrimitives.ReadUInt16LittleEndian(reply.Data.AsSpan(1, 2));
		List<string> list = new List<string>(num);
		int num2 = 3;
		for (int i = 0; i < num; i++)
		{
			if (num2 + 41 > reply.Data.Length)
			{
				break;
			}
			list.Add(RciClient.ReadCString(reply.Data.AsSpan(num2 + 9, 16)));
			num2 += 41;
		}
		return list;
	}

	public static IReadOnlyList<string> ParseNamedDirectory(RciReply reply, int headerSize, int nameOffset)
	{
		reply.EnsureOk("Data directory");
		if (reply.Data.Length < 3)
		{
			return Array.Empty<string>();
		}
		ushort num = BinaryPrimitives.ReadUInt16LittleEndian(reply.Data.AsSpan(1, 2));
		List<string> list = new List<string>(num);
		int num2 = 3;
		for (int i = 0; i < num; i++)
		{
			if (num2 + headerSize > reply.Data.Length)
			{
				break;
			}
			list.Add(RciClient.ReadCString(reply.Data.AsSpan(num2 + nameOffset, 16)));
			num2 += headerSize;
		}
		return list;
	}

	public static RciReply UploadAllMessages(RciClient client)
	{
		return client.Send(26, new byte[1] { 0 }, "upload all messages (26)");
	}

	public static IReadOnlyList<string> ParseUploadedMessageNames(RciReply reply)
	{
		if (reply.Data.Length < 1)
		{
			return Array.Empty<string>();
		}
		byte b = reply.Data[0];
		List<string> list = new List<string>(b);
		int num = 1;
		for (int i = 0; i < b; i++)
		{
			if (num + 41 > reply.Data.Length)
			{
				break;
			}
			ushort val = BinaryPrimitives.ReadUInt16LittleEndian(reply.Data.AsSpan(num, 2));
			list.Add(RciClient.ReadCString(reply.Data.AsSpan(num + 9, 16)));
			num += Math.Max(val, (ushort)41);
		}
		return list;
	}

	public static RciReply DownloadTextMessage(RciClient client, byte[] messageBlob)
	{
		return client.Send(25, messageBlob, $"download message (25) {messageBlob.Length} bytes");
	}

	public static RciReply DeleteNamedMessage(RciClient client, string name)
	{
		byte[] array = new byte[17]
		{
			1, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0
		};
		RciClient.PadName(name).CopyTo(array, 1);
		return client.Send(27, array, "delete message (27) '" + name + "'");
	}

	public static RciReply MessageTypes(RciClient client)
	{
		return client.Send(24, default(ReadOnlySpan<byte>), "message types (24)");
	}
}
