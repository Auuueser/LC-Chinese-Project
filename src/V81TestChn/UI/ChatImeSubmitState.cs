namespace V81TestChn;

// Shared by the Input System submit callback and TMP's later UI event pass.
// No text is stored: the IME owns committing candidates (including raw pinyin).
internal sealed class ChatImeSubmitState
{
    private bool _composing;
    private bool _blockedUntilRelease;
    private int _compositionEndedFrame = -1;
    private int _blockedFrame = -1;

    internal void Reset()
    {
        _composing = false;
        _blockedUntilRelease = false;
        _compositionEndedFrame = _blockedFrame = -1;
    }

    internal void Observe(int frame, bool composing, bool enterHeld, bool enterReleased)
    {
        if (_composing && !composing) _compositionEndedFrame = frame;
        _composing = composing;
        if ((!enterHeld || enterReleased) && frame != _blockedFrame) _blockedUntilRelease = false;
    }

    internal void Open(int frame, bool enterHeld)
    {
        Reset();
        // The Enter that opened chat must not also finish the newly focused field.
        if (enterHeld) Block(frame, true);
    }

    internal bool ShouldBlock(int frame, bool enterHeld)
    {
        if (!_composing && _compositionEndedFrame != frame &&
            _blockedFrame != frame && !_blockedUntilRelease) return false;
        Block(frame, enterHeld);
        return true;
    }

    private void Block(int frame, bool enterHeld)
    {
        _blockedFrame = frame;
        _blockedUntilRelease |= enterHeld;
    }
}
