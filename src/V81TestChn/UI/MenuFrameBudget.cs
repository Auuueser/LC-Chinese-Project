namespace V81TestChn;

// All panel coroutines share one frame budget, including their first MoveNext.
internal sealed class MenuFrameBudget
{
    private int _frame = -1;
    private int _components;
    private int _nodes;
    private long _startedAt = -1;

    private void EnterFrame(int frame)
    {
        if (_frame == frame) return;
        _frame = frame;
        _components = 0;
        _nodes = 0;
        _startedAt = -1;
    }

    // Independent from item counts: a single component can contain a large list.
    // The current item is atomic; this bounds scheduling, not its worst-case cost.
    internal bool HasTimeRemaining(int frame, long timestamp, long tickLimit)
    {
        EnterFrame(frame);
        if (_startedAt < 0) { _startedAt = timestamp; return true; }
        return timestamp - _startedAt < tickLimit;
    }

    internal bool TrySpendNode(int frame, int limit)
    {
        EnterFrame(frame);
        if (_nodes >= limit * 4 || _components >= limit) return false;
        _nodes++;
        return true;
    }

    internal bool TrySpendComponent(int frame, int limit)
    {
        EnterFrame(frame);
        if (_components >= limit) return false;
        _components++;
        return true;
    }
}
