using System;
using System.IO;
using System.Net.Sockets;

namespace TaggJack.Services.Inkjet;

public sealed class A48601Client : IDisposable
{
	public const byte Enq = 5;

	public const byte Ack = 6;

	public const byte Nack = 21;

	private readonly string _host;

	private readonly int _port;

	private readonly int _timeoutMs;

	private readonly bool _useHandshake;

	private readonly Action<string>? _log;

	private TcpClient? _tcp;

	private NetworkStream? _stream;

	private bool _disposed;

	public int TimeoutMs => _timeoutMs;

	public bool IsConnected => _tcp?.Connected ?? false;

	public A48601Client(string host, int port, int timeoutMs = 5000, bool useHandshake = true, Action<string>? log = null)
	{
		_host = host;
		_port = port;
		_timeoutMs = timeoutMs;
		_useHandshake = useHandshake;
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
		Log($"TCP connected to {_host}:{_port}");
		if (_useHandshake)
		{
			Handshake();
		}
		else
		{
			Log("Handshake skipped (--no-handshake)");
		}
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

	public void Session(Action<A48601Client> action)
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

	public ProtocolAck Send(byte commandId, ReadOnlySpan<byte> data = default(ReadOnlySpan<byte>), string? label = null)
	{
		NetworkStream networkStream = RequireStream();
		byte[] array = BuildFrame(commandId, data);
		string text = label ?? $"cmd {commandId:X2}h";
		Log($">> TX {text}: {FormatHex(array)} ({array.Length} bytes, dataLen={data.Length})");
		networkStream.Write(array);
		byte b = ExpectAck(text);
		Log("<< RX " + text + ": " + DescribeControl(b));
		return new ProtocolAck(b, commandId, array);
	}

	public ProtocolReply SendWithReply(byte commandId, ReadOnlySpan<byte> data = default(ReadOnlySpan<byte>), string? label = null, bool allowMissingReply = false, bool acknowledgeReply = true)
	{
		NetworkStream networkStream = RequireStream();
		byte[] array = BuildFrame(commandId, data);
		string text = label ?? $"cmd {commandId:X2}h";
		Log($">> TX {text}: {FormatHex(array)} ({array.Length} bytes, dataLen={data.Length})");
		networkStream.Write(array);
		byte b = ExpectAck(text);
		Log("<< RX " + text + " control: " + DescribeControl(b));
		byte[] array2;
		try
		{
			array2 = ReadFrame();
		}
		catch (Exception ex) when (((Func<bool>)delegate
		{
			// Could not convert BlockContainer to single expression
			bool flag = allowMissingReply;
			if (flag)
			{
				flag = ((ex is TimeoutException || ex is EndOfStreamException || ex is IOException) ? true : false);
			}
			return flag;
		}).Invoke())
		{
			Log($"<< RX {text} reply: (none — {ex.GetType().Name}: {ex.Message})");
			return new ProtocolReply(b, commandId, array, Array.Empty<byte>(), ReplyMissing: true);
		}
		Log("<< RX " + text + " frame: " + DescribeFrame(array2));
		Log("<< RX " + text + " raw:   " + FormatHex(array2));
		if (!acknowledgeReply)
		{
			Log(">> TX reply-ACK: skipped (command reply does not accept acknowledgement)");
		}
		else
		{
			try
			{
				networkStream.WriteByte(6);
				Log(">> TX reply-ACK: " + DescribeControl(6));
			}
			catch (IOException)
			{
				Log(">> TX reply-ACK skipped (peer closed)");
			}
		}
		return new ProtocolReply(b, commandId, array, array2);
	}

	public static byte[] BuildFrame(byte commandId, ReadOnlySpan<byte> data)
	{
		if (data.Length > 4092)
		{
			throw new ArgumentOutOfRangeException("data", "Payload exceeds A48601 max length.");
		}
		byte[] array = new byte[3 + data.Length + 1];
		array[0] = commandId;
		array[1] = (byte)((data.Length >> 8) & 0xFF);
		array[2] = (byte)(data.Length & 0xFF);
		data.CopyTo(array.AsSpan(3));
		array[^1] = XorChecksum(array.AsSpan(0, array.Length - 1));
		return array;
	}

	public static byte XorChecksum(ReadOnlySpan<byte> bytes)
	{
		byte b = 0;
		ReadOnlySpan<byte> readOnlySpan = bytes;
		for (int i = 0; i < readOnlySpan.Length; i++)
		{
			byte b2 = readOnlySpan[i];
			b ^= b2;
		}
		return b;
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

	public static string DescribeControl(byte value)
	{
		return value switch
		{
			5 => "ENQ (05h) — printer has data / reverse direction", 
			6 => "ACK (06h) — accepted", 
			21 => "NACK (15h) — rejected", 
			_ => $"0x{value:X2} (unexpected control byte)", 
		};
	}

	public static string DescribeFrame(ReadOnlySpan<byte> frame)
	{
		if (frame.Length >= 4)
		{
			byte value = frame[0];
			int value2 = (frame[1] << 8) | frame[2];
			ReadOnlySpan<byte> bytes = frame.Slice(3, frame.Length - 1 - 3);
			byte value3 = frame[frame.Length - 1];
			return $"ID={value:X2}h length={value2} data=[{FormatHex(bytes)}] checksum={value3:X2}h";
		}
		return $"short frame ({frame.Length} bytes): {FormatHex(frame)}";
	}

	private void Handshake()
	{
		NetworkStream networkStream = RequireStream();
		Log(">> TX handshake: ENQ (05h)");
		networkStream.WriteByte(5);
		byte[] array = ReadExact(1);
		Log("<< RX handshake: " + DescribeControl(array[0]));
		if (array[0] != 6)
		{
			throw new InvalidOperationException($"Handshake failed: expected ACK (06), got {array[0]:X2}. " + "Is the printer in Standard protocol mode, and is another client (e.g. CoLOS) connected?");
		}
	}

	private byte ExpectAck(string context)
	{
		byte[] array = ReadExact(1);
		if (array[0] == 5)
		{
			Log($"<< RX {context}: {DescribeControl(5)} — draining pending frame");
			RequireStream().WriteByte(6);
			Log(">> TX drain-ACK: ACK (06h)");
			byte[] array2 = ReadFrame();
			Log("<< RX pending frame: " + DescribeFrame(array2));
			Log("<< RX pending raw:   " + FormatHex(array2));
			RequireStream().WriteByte(6);
			Log(">> TX pending-ACK: ACK (06h)");
			array = ReadExact(1);
		}
		if (array[0] == 6)
		{
			return array[0];
		}
		if (array[0] == 21)
		{
			Log("<< RX " + context + ": " + DescribeControl(21));
			throw new InvalidOperationException("NACK received after " + context + " (bad checksum, unknown ID, or rejected).");
		}
		Log("<< RX " + context + ": " + DescribeControl(array[0]));
		throw new InvalidOperationException($"Unexpected response after {context}: {array[0]:X2} (expected ACK).");
	}

	private byte[] ReadFrame()
	{
		byte[] array = ReadExact(3);
		byte value = array[0];
		int num = (array[1] << 8) | array[2];
		byte[] array2 = ReadExact(num + 1);
		byte[] array3 = new byte[3 + array2.Length];
		array.CopyTo(array3, 0);
		array2.CopyTo(array3, 3);
		byte b = XorChecksum(array3.AsSpan(0, array3.Length - 1));
		if (array3[^1] != b)
		{
			throw new InvalidOperationException($"Reply checksum mismatch for ID {value:X2}: got {array3[^1]:X2}, expected {b:X2}.");
		}
		return array3;
	}

	private byte[] ReadExact(int count)
	{
		NetworkStream networkStream = RequireStream();
		byte[] array = new byte[count];
		int num;
		for (int i = 0; i < count; i += num)
		{
			try
			{
				num = networkStream.Read(array, i, count - i);
			}
			catch (IOException innerException)
			{
				throw new TimeoutException($"Timed out reading {count} bytes from printer.", innerException);
			}
			if (num == 0)
			{
				throw new EndOfStreamException("Connection closed by printer while reading.");
			}
		}
		return array;
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
