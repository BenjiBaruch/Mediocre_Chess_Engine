using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Search = V4.Search;
using Debug = UnityEngine.Debug;

sealed class Version4 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }
    Search search;
    public override void Initialize(bool side)
    {
        Stopwatch watch = Stopwatch.StartNew();
        Side = side;
        search = new();
        Name = "Basic Transposition Table";
        Version = "4";
        watch.Stop();
        Debug.Log("v4 initialization time: " + watch.ElapsedMilliseconds);
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return search.BestMove(b, depth);
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
}