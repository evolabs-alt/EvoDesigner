namespace TaggJack.Services.Inkjet;

public readonly record struct ProtocolAck(byte Control, byte CommandId, byte[] RequestFrame);
