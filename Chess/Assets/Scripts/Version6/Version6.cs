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
        Name = "Iterative Deepeing and PV";
        Version = "6";
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return search.BestMoveToDepth(b, depth);
    }

    public override Move GetMoveTimed(BoardStruct b, long timeLimit)
    {
        return search.BestMove(b, timeLimit);
    }

    public override double TimeFunc(string fName)
    {
        throw new NotImplementedException();
    }

    public override Dictionary<string, int> GetPartCounts()
    {
        throw new NotImplementedException();
    }
}