using System;
using System.Collections.Generic;
using System.Text;
using Search = V3.Search;
using UnityEngine;

sealed class Version3 : ChessAbstract
{
    public override string Name { get; }
    public override string Version { get; }
    public Version3()
    {
        Name = "Move Sorting";
        Version = "3";
    }
    public override Move GetMove(BoardStruct b, double timeLimit)
    {
        return new Search(b).BestMove(3);
    }

    static void Main(string[] args) {
    }
}