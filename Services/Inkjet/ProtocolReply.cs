namespace TaggJack.Services.Inkjet;

public readonly record struct ProtocolReply(byte Control, byte CommandId, byte[] RequestFrame, byte[] ReplyFrame, bool ReplyMissing = false)
{
	public byte ReplyId
	{
		get
		{
			if (ReplyFrame.Length == 0)
			{
				return 0;
			}
			return ReplyFrame[0];
		}
	}

	public int ReplyDataLength
	{
		get
		{
			if (ReplyFrame.Length < 3)
			{
				return 0;
			}
			return (ReplyFrame[1] << 8) | ReplyFrame[2];
		}
	}

	public bool HasReportByte
	{
		get
		{
			if (!ReplyMissing && ReplyFrame.Length >= 5)
			{
				return ReplyDataLength >= 1;
			}
			return false;
		}
	}

	public byte ReportByte
	{
		get
		{
			if (!HasReportByte)
			{
				return 0;
			}
			return ReplyFrame[3];
		}
	}
}
