using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TaggJack.Services.Inkjet.Linx;

public sealed class RciClient : IDisposable
{
	public const byte Nul = 0;

	public const byte Soh = 1;

	public const byte Stx = 2;

	public const byte Etx = 3;

	public const byte Ack = 6;

	public const byte Xon = 17;

	public const byte Xoff = 19;

	public const byte Nak = 21;

	public const byte Esc = 27;

	public const int DefaultPort = 29043;

	private readonly string _host;

	private readonly int _port;

	private readonly int _timeoutMs;

	private readonly bool _useChecksum;

	private readonly Action<string>? _log;

	private TcpClient? _tcp;

	private NetworkStream? _stream;

	private bool _disposed;

	public bool IsConnected => _tcp?.Connected ?? false;

	public RciClient(string host, int port = 29043, int timeoutMs = 8000, bool useChecksum = true, Action<string>? log = null)
	{
		_host = host;
		_port = port;
		_timeoutMs = timeoutMs;
		_useChecksum = useChecksum;
		_log = log;
	}

	public void Connect()
	{
		EnsureNotDisposed();
		Disconnect();
		TcpClient tcpClient = (_tcp = new TcpClient
		{
			NoDelay = true
		});
		try
		{
			if (!tcpClient.ConnectAsync(_host, _port).Wait(_timeoutMs))
			{
				tcpClient.Dispose();
				_tcp = null;
				throw new TimeoutException($"Connect to {_host}:{_port} timed out after {_timeoutMs} ms.");
			}
		}
		catch (AggregateException ex)
		{
			tcpClient.Dispose();
			_tcp = null;
			throw new IOException($"Could not connect to {_host}:{_port}: {ex.InnerException?.Message ?? ex.Message}", ex);
		}
		_stream = tcpClient.GetStream();
		_stream.ReadTimeout = _timeoutMs;
		_stream.WriteTimeout = _timeoutMs;
		Log($"TCP connected to {_host}:{_port} (Linx RCI)");
	}

	public void Disconnect()
	{
		if (_tcp != null)
		{
			Log("TCP disconnect");
		}
		_stream?.Dispose();
		_stream = null;
		_tcp?.Dispose();
		_tcp = null;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			Disconnect();
			_disposed = true;
		}
	}

	public void Session(Action<RciClient> action)
	{
		Connect();
		try
		{
			action(this);
		}
		finally
		{
			Disconnect();
		}
	}

	public RciReply Send(byte commandId, ReadOnlySpan<byte> data = default(ReadOnlySpan<byte>), string? label = null, bool extendedStatus = false)
	{
		NetworkStream networkStream = RequireStream();
		byte[] array = BuildFrame(commandId, data, _useChecksum, extendedStatus);
		string text = label ?? $"cmd {commandId:D} ({commandId:X2}h)";
		Log($">> TX {text}: {FormatHex(array)} ({array.Length} bytes)");
		networkStream.Write(array);
		RciReply rciReply = ReadReply();
		Log("<< RX " + text + ": " + DescribeReply(rciReply));
		if (rciReply.Raw.Length != 0)
		{
			Log("<< RX " + text + " raw: " + FormatHex(rciReply.Raw));
		}
		return rciReply;
	}

	public static byte[] BuildFrame(byte commandId, ReadOnlySpan<byte> data, bool useChecksum = true, bool extendedStatus = false)
	{
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.WriteByte(27);
		memoryStream.WriteByte((byte)(extendedStatus ? 1 : 2));
		List<byte> list = new List<byte> { (byte)(extendedStatus ? 1 : 2) };
		WriteEscaped(memoryStream, list, commandId);
		ReadOnlySpan<byte> readOnlySpan = data;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			byte value = readOnlySpan[i];
			WriteEscaped(memoryStream, list, value);
		}
		memoryStream.WriteByte(27);
		memoryStream.WriteByte(3);
		list.Add(3);
		if (useChecksum)
		{
			WriteEscaped(memoryStream, null, ComputeChecksum(list));
		}
		return memoryStream.ToArray();
	}

	public static byte ComputeChecksum(IReadOnlyList<byte> unescapedStxThroughEtx)
	{
		int num = 0;
		foreach (byte item in unescapedStxThroughEtx)
		{
			num += item;
		}
		return (byte)((256 - (num & 0xFF)) & 0xFF);
	}

	public static string FormatHex(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length == 0)
		{
			return "(empty)";
		}
		string[] array = new string[bytes.Length];
		for (int i = 0; i < bytes.Length; i++)
		{
			array[i] = bytes[i].ToString("X2");
		}
		return string.Join(' ', array);
	}

	public static string DescribeReply(RciReply reply)
	{
		string value = (reply.Acknowledged ? "ACK" : "NAK");
		return $"{value} P-Status={reply.PrinterStatus} ({DescribePrinterStatus(reply.PrinterStatus)}) C-Status={reply.CommandStatus} ({DescribeCommandStatus(reply.CommandStatus)}) cmd={reply.CommandId:D}/{reply.CommandId:X2}h data={reply.Data.Length} bytes";
	}

	public static string DescribeCommandStatus(byte status)
	{
		return status switch
		{
			0 => "OK", 
			8 => "Invalid checksum", 
			17 => "Invalid command", 
			18 => "Jet not running", 
			19 => "Jet not idle", 
			20 => "Print not idle", 
			23 => "Parameter rejected", 
			24 => "Minimum string length", 
			25 => "Maximum string length", 
			28 => "Memory full", 
			34 => "Unknown data set", 
			35 => "No messages", 
			36 => "Unknown message", 
			41 => "Trigger print: photocell mode", 
			42 => "Trigger print: print idle", 
			46 => "Print command: no message", 
			47 => "Jet command: ink low", 
			59 => "No print message loaded", 
			62 => "Invalid buffer divisor", 
			63 => "No remote fields in message", 
			64 => "Number of remote characters", 
			65 => "Remote data too large", 
			66 => "Remote buffer now full", 
			67 => "Remote buffer still full", 
			81 => "Printer busy", 
			82 => "Unknown raster / message type", 
			_ => $"code {status}", 
		};
	}

	public static string DescribePrinterStatus(byte status)
	{
		if (status != 0)
		{
			return $"fault {status}";
		}
		return "no fault";
	}

	public static string DescribeJetState(byte state)
	{
		return state switch
		{
			0 => "Jet running", 
			1 => "Jet startup", 
			2 => "Jet shutdown", 
			3 => "Jet stopped", 
			4 => "Jet fault", 
			_ => $"unknown jet state {state}", 
		};
	}

	public static string DescribePrintState(byte state)
	{
		return state switch
		{
			0 => "Printing", 
			1 => "Undefined", 
			2 => "Idle", 
			3 => "Generating pixels", 
			4 => "Waiting for trigger", 
			5 => "Last print after stop", 
			6 => "Printing / generating pixels", 
			_ => $"unknown print state {state}", 
		};
	}

	public static string ReadCString(ReadOnlySpan<byte> bytes)
	{
		int num = bytes.IndexOf<byte>(0);
		if (num < 0)
		{
			num = bytes.Length;
		}
		return Encoding.Latin1.GetString(bytes.Slice(0, num)).TrimEnd('\0', ' ');
	}

	public static byte[] PadName(string name, int length = 16)
	{
		if (name.Length > length - 1)
		{
			throw new ArgumentException($"Name '{name}' exceeds {length - 1} characters.");
		}
		byte[] array = new byte[length];
		Encoding.Latin1.GetBytes(name, array);
		return array;
	}

	private RciReply ReadReply()
	{
		List<byte> list = new List<byte>();
		byte b2;
		while (true)
		{
			byte b = ReadByte();
			if (b != 27)
			{
				Log($"<< RX skipped non-ESC 0x{b:X2} while waiting for reply");
				continue;
			}
			b2 = ReadByte();
			if ((b2 != 17 && b2 != 19) || 1 == 0)
			{
				if ((b2 == 6 || b2 == 21) ? true : false)
				{
					break;
				}
				Log($"<< RX unexpected ESC 0x{b2:X2} while waiting for ACK/NAK");
			}
		}
		list.Add(27);
		list.Add(b2);
		byte b3 = b2;
		List<byte> list2 = new List<byte>();
		while (true)
		{
			byte b4 = ReadByte();
			if (b4 != 27)
			{
				list2.Add(b4);
				list.Add(b4);
				continue;
			}
			byte b5 = ReadByte();
			list.Add(27);
			list.Add(b5);
			if ((b5 == 17 || b5 == 19) ? true : false)
			{
				continue;
			}
			switch (b5)
			{
			case 27:
				list2.Add(27);
				break;
			default:
				throw new InvalidOperationException($"Unexpected ESC 0x{b5:X2} inside RCI reply.");
			case 3:
			{
				byte? b6 = null;
				if (_useChecksum)
				{
					byte b7 = ReadByte();
					list.Add(b7);
					if (b7 == 27)
					{
						byte b8 = ReadByte();
						list.Add(b8);
						if (b8 != 27)
						{
							throw new InvalidOperationException($"Expected escaped checksum ESC ESC, got ESC 0x{b8:X2}.");
						}
						b6 = 27;
					}
					else
					{
						b6 = b7;
					}
					byte b9 = ComputeChecksum(BuildChecksumInput(b3, list2));
					if (b6 != b9)
					{
						throw new InvalidOperationException($"RCI checksum mismatch: got {b6.Value:X2}h, expected {b9:X2}h.");
					}
				}
				if (list2.Count < 3)
				{
					throw new InvalidOperationException("Short RCI reply: " + FormatHex(list.ToArray()));
				}
				return new RciReply(b3 == 6, list2[0], list2[1], list2[2], (list2.Count > 3) ? list2.Skip(3).ToArray() : Array.Empty<byte>(), b6, list.ToArray());
			}
			}
		}
	}

	private static List<byte> BuildChecksumInput(byte delimiter, List<byte> payload)
	{
		List<byte> list = new List<byte>(payload.Count + 2);
		list.Add(delimiter);
		list.AddRange(payload);
		list.Add(3);
		return list;
	}

	private static void WriteEscaped(MemoryStream ms, List<byte>? checksumSink, byte value)
	{
		checksumSink?.Add(value);
		if (value == 27)
		{
			ms.WriteByte(27);
			ms.WriteByte(27);
		}
		else
		{
			ms.WriteByte(value);
		}
	}

	private byte ReadByte()
	{
		NetworkStream networkStream = RequireStream();
		byte[] array = new byte[1];
		int num;
		try
		{
			num = networkStream.Read(array, 0, 1);
		}
		catch (IOException innerException)
		{
			throw new TimeoutException("Timed out reading from Linx printer.", innerException);
		}
		if (num == 0)
		{
			throw new EndOfStreamException("Connection closed by Linx printer while reading.");
		}
		return array[0];
	}

	private NetworkStream RequireStream()
	{
		EnsureNotDisposed();
		if (_stream == null || _tcp == null || !_tcp.Connected)
		{
			throw new InvalidOperationException("Not connected. Call Connect() first.");
		}
		return _stream;
	}

	private void EnsureNotDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	private void Log(string message)
	{
		_log?.Invoke(message);
	}
}
