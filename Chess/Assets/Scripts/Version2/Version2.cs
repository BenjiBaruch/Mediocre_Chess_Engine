using System;
using System.Collections.Generic;
using System.Text;
using Search = V2.Search;

sealed class Version2 : ChessAbstract
{
    protected override string Name { get; }
    protected override string Version { get; }
    public Version2(bool side)
    {
        Side = side;
        Name = "Quiessence";
        Version = "2";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(3);
    }

    static void Main(string[] args) {
    }
}