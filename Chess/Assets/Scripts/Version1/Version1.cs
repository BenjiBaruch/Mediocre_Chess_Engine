using System;
using System.Collections.Generic;
using System.Text;
using V1;
using UnityEngine;

sealed class Version1 : ChessAbstract
{
    public override string Name { get; }
    public override string Version { get; }
    public Version1()
    {
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