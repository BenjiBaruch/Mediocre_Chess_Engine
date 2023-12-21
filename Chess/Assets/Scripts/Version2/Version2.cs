using System;
using System.Collections.Generic;
using System.Text;
using Search = V2.Search;
using UnityEngine;

sealed class Version2 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }
    public override void Initialize(bool side)
    {
        Name = "Quiessence";
        Version = "2";
        Side = side;
    }
    public override Move GetMoveDrafted(BoardStruct b, int depth)
    {
        return new Search(b).BestMove(depth);
    }

    public override Move GetMoveTimed(BoardStruct b, double timeLimit)
    {
        throw new NotImplementedException();
    }
    public override double TimeFunc(string fName)
    {
        throw new NotImplementedException();
    }
}