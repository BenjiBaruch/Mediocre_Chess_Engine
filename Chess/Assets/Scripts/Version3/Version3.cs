using System;
using System.Collections.Generic;
using System.Text;
using Search = V3.Search;
using UnityEngine;

sealed class Version3 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }
    public override void Initialize(bool side)
    {
        Name = "Move Sorting";
        Version = "3";
        Side = side;
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return new Search(b).BestMove(depth);
    }

    public override Move GetMoveTimed(BoardStruct b, long timeLimit)
    {
        throw new NotImplementedException();
    }

    public override double TimeFunc(string fName)
    {
        throw new NotImplementedException();
    }

    public override Dictionary<string, int> GetPartCounts()
    {
        throw new NotImplementedException();
    }

    public override ulong GrabBitBoard(string name, BoardStruct b)
    {
        throw new NotImplementedException();
    }

    public override ulong GrabAttackBoard(int square, BoardStruct b)
    {
        throw new NotImplementedException();
    }
}