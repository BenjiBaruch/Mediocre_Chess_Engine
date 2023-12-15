using System;

abstract class ChessAbstract
{
    protected abstract string Name { get; }
    protected abstract string Version { get; }
    protected bool Side;
    public abstract Move GetMove(BoardStruct b, double timeLimit);
}
