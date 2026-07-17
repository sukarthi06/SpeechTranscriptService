namespace SpeechTranscriptService.Domain.ValueObjects;

public record ConsumedMessage(ChunkWavConvertCompletedMessage Payload, ulong DeliveryTag);
