namespace TaggJack.Services.Inkjet.Linx;

public readonly record struct PrinterLiveStatus(byte JetState, byte PrintState, uint ErrorMask)
{
	public string Describe()
	{
		return $"{RciClient.DescribeJetState(JetState)}; {RciClient.DescribePrintState(PrintState)}; error-mask=0x{ErrorMask:X8}";
	}
}
