using System;

abstract class ChessAbstract
{
    protected abstract string Name { get; }
    protected abstract string Version { get; }
    protected bool Side;
    public abstract int GetMove(int[] board, double timeLimit);
}
