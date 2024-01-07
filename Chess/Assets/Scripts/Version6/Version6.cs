using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Search = V6.Search;
using Debug = UnityEngine.Debug;
using V6;

sealed class Version6 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }
    Search search;
    public override void Initialize(bool side)
    {
        Side = side;
        search = new();
        Name = "Iterative Deepeing, PV, Optimized Movegen";
        Version = "6";
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return search.BestMoveToDepth(b, depth);
    }

    public override Move GetMoveTimed(BoardStruct b, long timeLimit)
    {
        Move best = search.BestMove(b, timeLimit);
        Debug.Log("Best move: " + best.ToString);
        return best;
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
        return new MoveGen(b).GrabBitBoard(name);
    }

    public override ulong GrabAttackBoard(int square, BoardStruct b)
    {
        return new MoveGen(b).GrabAttackBoard(square);
    }
}