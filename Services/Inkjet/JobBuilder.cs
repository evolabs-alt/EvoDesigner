using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace TaggJack.Services.Inkjet;

public static class JobBuilder
{
	public const ushort DefaultFont = 286;

	public static byte[] BuildLibraryJob(string name, string text, ushort jobNumber = 1, string summary = "", ushort fontNumber = 286, ushort yPosition = 1)
	{
		byte[] b = BuildParametersAndLines(text, fontNumber, yPosition);
		byte[] array = AppendEndAndPad(Concat(BuildHeader(name, jobNumber, summary, 0u), b));
		BinaryPrimitives.WriteUInt32BigEndian(array.AsSpan(0, 4), (uint)array.Length);
		return array;
	}

	public static byte[] BuildCompletePrintJob(string name, string text, ushort jobNumber = 1, string summary = "", ushort fontNumber = 286, ushort yPosition = 1)
	{
		return BuildLibraryJob(name, text, jobNumber, summary, fontNumber, yPosition);
	}

	public static byte[] BuildPrintBufferJob(string text, ushort fontNumber = 286, ushort yPosition = 1)
	{
		return AppendEndAndPad(BuildParametersAndLines(text, fontNumber, yPosition));
	}

	private static byte[] BuildHeader(string name, ushort jobNumber, string summary, uint totalLength)
	{
		if ((jobNumber < 1 || jobNumber > 999) ? true : false)
		{
			throw new ArgumentOutOfRangeException("jobNumber", "Job number must be 1..999.");
		}
		byte[] array = new byte[64];
		BinaryPrimitives.WriteUInt32BigEndian(array.AsSpan(0, 4), totalLength);
		array[8] = 17;
		array[9] = 1;
		WriteFixedAscii(array.AsSpan(10, 20), name);
		BinaryPrimitives.WriteUInt16BigEndian(array.AsSpan(30, 2), jobNumber);
		WriteFixedAscii(array.AsSpan(32, 32), summary);
		return array;
	}

	private static byte[] BuildParametersAndLines(string text, ushort fontNumber, ushort yPosition)
	{
		ArgumentException.ThrowIfNullOrEmpty(text, "text");
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		if (bytes.Length == 0)
		{
			throw new ArgumentException("Text encoded to empty UTF-8.", "text");
		}
		using MemoryStream memoryStream = new MemoryStream();
		WriteU16(memoryStream, 2);
		WriteU16(memoryStream, 2048);
		WriteU16(memoryStream, 6);
		WriteU16(memoryStream, yPosition);
		WriteU16(memoryStream, 2305);
		WriteU16(memoryStream, 4);
		memoryStream.WriteByte(10);
		WriteBlockParams(memoryStream, fontNumber, yPosition);
		memoryStream.Write(bytes);
		WriteBlockParams(memoryStream, fontNumber, yPosition);
		return memoryStream.ToArray();
	}

	private static void WriteBlockParams(MemoryStream ms, ushort fontNumber, ushort yReference)
	{
		ms.WriteByte(16);
		WriteU16(ms, 18);
		WriteU16(ms, fontNumber);
		WriteU16(ms, 0);
		WriteU16(ms, yReference);
		ms.WriteByte(0);
		ms.WriteByte(1);
		WriteU16(ms, 0);
		WriteU16(ms, 18);
		ms.WriteByte(16);
	}

	private static byte[] AppendEndAndPad(byte[] job)
	{
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(job);
		memoryStream.WriteByte(13);
		while (memoryStream.Length % 4 != 0L)
		{
			memoryStream.WriteByte(0);
		}
		return memoryStream.ToArray();
	}

	private static byte[] Concat(byte[] a, byte[] b)
	{
		byte[] array = new byte[a.Length + b.Length];
		Buffer.BlockCopy(a, 0, array, 0, a.Length);
		Buffer.BlockCopy(b, 0, array, a.Length, b.Length);
		return array;
	}

	private static void WriteFixedAscii(Span<byte> dest, string value)
	{
		dest.Clear();
		if (!string.IsNullOrEmpty(value))
		{
			byte[] bytes = Encoding.ASCII.GetBytes(value);
			int length = Math.Min(bytes.Length, dest.Length);
			bytes.AsSpan(0, length).CopyTo(dest);
		}
	}

	private static void WriteU16(MemoryStream ms, ushort value)
	{
		ms.WriteByte((byte)((value >> 8) & 0xFF));
		ms.WriteByte((byte)(value & 0xFF));
	}

	public static byte[] PatchFixedTextToExternalVariable(byte[] job, string fixedText = "XXXXXXXXXX", byte variableNumber = 1)
	{
		ArgumentNullException.ThrowIfNull(job, "job");
		ArgumentException.ThrowIfNullOrEmpty(fixedText, "fixedText");
		if ((variableNumber < 1 || variableNumber > 10) ? true : false)
		{
			throw new ArgumentOutOfRangeException("variableNumber");
		}
		byte[] bytes = Encoding.ASCII.GetBytes(fixedText);
		int num = IndexOf(job, bytes);
		if (num < 0)
		{
			throw new InvalidOperationException("Fixed text '" + fixedText + "' not found in job blob.");
		}
		int num2 = 8 + bytes.Length;
		byte[] array = new byte[num2];
		array[0] = 18;
		array[1] = (byte)((num2 >> 8) & 0xFF);
		array[2] = (byte)(num2 & 0xFF);
		array[3] = variableNumber;
		bytes.CopyTo(array.AsSpan(4));
		array[4 + bytes.Length] = variableNumber;
		array[5 + bytes.Length] = (byte)((num2 >> 8) & 0xFF);
		array[6 + bytes.Length] = (byte)(num2 & 0xFF);
		array[7 + bytes.Length] = 18;
		int num3 = array.Length - bytes.Length;
		byte[] array2 = new byte[job.Length + num3];
		Buffer.BlockCopy(job, 0, array2, 0, num);
		Buffer.BlockCopy(array, 0, array2, num, array.Length);
		Buffer.BlockCopy(job, num + bytes.Length, array2, num + array.Length, job.Length - num - bytes.Length);
		if (array2.Length >= 4)
		{
			BinaryPrimitives.WriteUInt32BigEndian(array2.AsSpan(0, 4), (uint)array2.Length);
		}
		if (array2.Length >= 8)
		{
			array2[4] = (array2[5] = (array2[6] = (array2[7] = 0)));
		}
		return array2;
	}

	private static int IndexOf(byte[] haystack, byte[] needle)
	{
		for (int i = 0; i + needle.Length <= haystack.Length; i++)
		{
			bool flag = true;
			for (int j = 0; j < needle.Length; j++)
			{
				if (haystack[i + j] != needle[j])
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
