namespace V81TestChn;

internal static class SpeechVoiceGate
{
    // Only the local pipeline is suppressed, and an existing mute stays muted.
    internal static bool Mute(bool original, object instance, object? localPipeline, bool active, bool suppress)
        => original || (active && suppress && ReferenceEquals(instance, localPipeline));
}
