namespace TaggJack.Services.Inkjet.Linx;

public readonly record struct PrintModeSettings(byte Mode, byte PrintGoNoData, byte PrintGoPixelRam, byte ClearPrintBuffer, byte RemoteBufferDivisor)
{
	public string Describe()
	{
		return $"mode={((Mode == 1) ? "single" : ((Mode == 0) ? "continuous" : Mode.ToString()))} buffer-divisor={RemoteBufferDivisor} clear-buffer={ClearPrintBuffer != 0}";
	}
}
