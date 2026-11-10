using System;

namespace TaggJack.Services.Inkjet.Linx;

public readonly record struct RciReply(bool Acknowledged, byte PrinterStatus, byte CommandStatus, byte CommandId, byte[] Data, byte? Checksum, byte[] Raw)
{
	public bool Ok
	{
		get
		{
			bool flag = Acknowledged;
			if (flag)
			{
				byte commandStatus = CommandStatus;
				bool flag2 = ((commandStatus == 0 || commandStatus == 66) ? true : false);
				flag = flag2;
			}
			return flag;
		}
	}

	public void EnsureOk(string context)
	{
		if (Ok)
		{
			return;
		}
		string value = (Acknowledged ? "ACK" : "NAK");
		throw new InvalidOperationException($"{context} failed: {value} C-Status={CommandStatus} ({RciClient.DescribeCommandStatus(CommandStatus)}) P-Status={PrinterStatus} ({RciClient.DescribePrinterStatus(PrinterStatus)}).");
	}
}
