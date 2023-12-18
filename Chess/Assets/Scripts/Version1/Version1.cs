using System;
using System.Collections.Generic;
using System.Text;
using V1;
using UnityEngine;
using Unity.VisualScripting;

sealed class Version1 : ChessAbstract
{
    public override string Name { get; set; }
    public override string Version { get; set; }

    public override void Initialize(bool side)    
    {
        Name = "Alpha-Beta w/o Quiessence";
        Version = "1";
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
}