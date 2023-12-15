using System;
using System.Collections.Generic;
using System.Text;
using V1;

sealed class Version1 : ChessAbstract
{
    protected override string Name { get; }
    protected override string Version { get; }
    public Version1(bool side)
    {
        Side = side;
        Name = "Alpha-Beta w/o Quiessence";
        Version = "1";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(2);
    }

    static void Main(string[] args) {
    }
}